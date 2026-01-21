import { writable, type Writable } from 'svelte/store';
import { browser } from '$app/environment';

interface PushSubscription {
  endpoint: string;
  keys: {
    p256dh: string;
    auth: string;
  };
}

interface PushState {
  supported: boolean;
  permission: NotificationPermission;
  subscribed: boolean;
  subscription: PushSubscription | null;
}

function createPushStore() {
  const { subscribe, set, update } = writable<PushState>({
    supported: false,
    permission: 'default',
    subscribed: false,
    subscription: null
  });

  return {
    subscribe,

    async init() {
      if (!browser) return;

      // サポートチェック
      if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        set({
          supported: false,
          permission: 'default',
          subscribed: false,
          subscription: null
        });
        return;
      }

      // パーミッションチェック
      const permission = Notification.permission;

      // 購読状態チェック
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();

      set({
        supported: true,
        permission,
        subscribed: subscription !== null,
        subscription: subscription ? {
          endpoint: subscription.endpoint,
          keys: {
            p256dh: subscription.toJSON().keys?.p256dh ?? '',
            auth: subscription.toJSON().keys?.auth ?? ''
          }
        } : null
      });
    },

    async requestPermission(): Promise<boolean> {
      if (!browser) return false;

      const permission = await Notification.requestPermission();

      update(s => ({ ...s, permission }));

      return permission === 'granted';
    },

    async subscribe(vapidPublicKey: string): Promise<boolean> {
      if (!browser) throw new Error('Not in browser');

      const registration = await navigator.serviceWorker.ready;

      try {
        // 既存の購読を解除
        const existingSubscription = await registration.pushManager.getSubscription();
        if (existingSubscription) {
          await existingSubscription.unsubscribe();
        }

        // 新しい購読を作成
        const subscription = await registration.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: urlBase64ToUint8Array(vapidPublicKey)
        });

        const subscriptionData = {
          endpoint: subscription.endpoint,
          keys: {
            p256dh: subscription.toJSON().keys?.p256dh ?? '',
            auth: subscription.toJSON().keys?.auth ?? ''
          }
        };

        update(s => ({
          ...s,
          subscribed: true,
          subscription: subscriptionData
        }));

        // サーバーに購読情報を送信
        await sendSubscriptionToServer(subscriptionData);

        return true;
      } catch (error) {
        console.error('Failed to subscribe to push notifications:', error);
        return false;
      }
    },

    async unsubscribe(): Promise<boolean> {
      if (!browser) return false;

      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();

      if (subscription) {
        await subscription.unsubscribe();

        update(s => ({
          ...s,
          subscribed: false,
          subscription: null
        }));

        // サーバーから購読情報を削除
        await deleteSubscriptionFromServer();

        return true;
      }

      return false;
    }
  };
}

function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - base64String.length % 4) % 4);
  const base64 = (base64String + padding)
    .replace(/-/g, '+')
    .replace(/_/g, '/');

  const rawData = window.atob(base64);
  const outputArray = new Uint8Array(rawData.length);

  for (let i = 0; i < rawData.length; ++i) {
    outputArray[i] = rawData.charCodeAt(i);
  }

  return outputArray;
}

async function sendSubscriptionToServer(subscription: PushSubscription): Promise<void> {
  const response = await fetch('/api/push/subscribe', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(subscription)
  });

  if (!response.ok) {
    throw new Error('Failed to send subscription to server');
  }
}

async function deleteSubscriptionFromServer(): Promise<void> {
  const response = await fetch('/api/push/unsubscribe', {
    method: 'POST'
  });

  if (!response.ok) {
    throw new Error('Failed to delete subscription from server');
  }
}

export const push = createPushStore();
