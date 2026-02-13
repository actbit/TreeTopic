import { writable, type Writable } from 'svelte/store';
import { browser } from '$app/environment';
import { getCurrentTenant } from '$lib/api/client';
import { api } from '$lib/api/client';

export interface LocalPushSubscription {
  endpoint: string;
  keys: {
    p256dh: string;
    auth: string;
  };
}

export interface PushState {
  supported: boolean;
  permission: NotificationPermission;
  subscribed: boolean;
  subscription: LocalPushSubscription | null;
}

function createPushStore() {
  const { subscribe, set, update } = writable<PushState>({
    supported: false,
    permission: 'default',
    subscribed: false,
    subscription: null
  });

  // VAPIDキーのlocalStorageキー
  const VAPID_KEY_STORAGE_KEY = 'push_vapid_key';

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

      const tenant = getCurrentTenant();
      if (!tenant) {
        set({
          supported: true,
          permission,
          subscribed: false,
          subscription: null
        });
        return;
      }

      let vapidPublicKey: string | null = null;
      try {
        const vapidResponse = await api.get<{ publicKey: string }>(`/${tenant}/api/push/vapid-public-key`);
        vapidPublicKey = vapidResponse?.publicKey ?? null;
      } catch (error) {
      }

      const storedVapidKey = localStorage.getItem(VAPID_KEY_STORAGE_KEY);
      const vapidKeyChanged = vapidPublicKey && storedVapidKey !== vapidPublicKey;

      if (vapidKeyChanged) {
        try {
          const registration = await navigator.serviceWorker.ready;
          const existingSubscription = await registration.pushManager.getSubscription();
          if (existingSubscription) {
            await existingSubscription.unsubscribe();
          }
        } catch (error) {
        }
        if (vapidPublicKey) {
          localStorage.setItem(VAPID_KEY_STORAGE_KEY, vapidPublicKey);
        }
      } else if (!storedVapidKey && vapidPublicKey) {
        localStorage.setItem(VAPID_KEY_STORAGE_KEY, vapidPublicKey);
      }

      // 購読状態チェック
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();

      let localSubscription: LocalPushSubscription | null = subscription ? {
        endpoint: subscription.endpoint,
        keys: {
          p256dh: subscription.toJSON().keys?.p256dh ?? '',
          auth: subscription.toJSON().keys?.auth ?? ''
        }
      } : null;

      if (subscription) {
        try {
          const response = await fetch(`/${tenant}/api/push/subscription-status?endpoint=${encodeURIComponent(subscription.endpoint)}`);
          if (response.ok) {
            const data = await response.json() as { exists: boolean };
            if (!data.exists) {
              await sendSubscriptionToServer(localSubscription!);
            }
          }
        } catch (error) {
        }
      }

      set({
        supported: true,
        permission,
        subscribed: localSubscription !== null,
        subscription: localSubscription
      });
    },

    async requestPermission(): Promise<boolean> {
      if (!browser) return false;

      const permission = await Notification.requestPermission();

      update(s => ({ ...s, permission }));

      return permission === 'granted';
    },

    async subscribePush(): Promise<boolean> {
      if (!browser) throw new Error('Not in browser');

      const tenant = getCurrentTenant();
      if (!tenant) throw new Error('No tenant found');

      // VAPID公開鍵を取得
      const vapidResponse = await api.get<{ publicKey: string }>(`/${tenant}/api/push/vapid-public-key`);
      if (!vapidResponse?.publicKey) {
        throw new Error('Failed to get VAPID public key');
      }

      const registration = await navigator.serviceWorker.ready;

      try {
        const existingSubscription = await registration.pushManager.getSubscription();
        let subscription: PushSubscription;

        if (existingSubscription) {
          await existingSubscription.unsubscribe();
        }

        subscription = await registration.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: urlBase64ToUint8Array(vapidResponse.publicKey) as BufferSource
        });

        const subscriptionData: LocalPushSubscription = {
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

        await sendSubscriptionToServer(subscriptionData);

        return true;
      } catch (error) {
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

async function sendSubscriptionToServer(subscription: LocalPushSubscription): Promise<void> {
  const tenant = getCurrentTenant();
  if (!tenant) throw new Error('No tenant found');

  const response = await fetch(`/${tenant}/api/push/subscribe`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(subscription)
  });

  if (response.status === 409) {
    return;
  }

  if (!response.ok) {
    throw new Error('Failed to send subscription to server');
  }
}

async function deleteSubscriptionFromServer(): Promise<void> {
  const tenant = getCurrentTenant();
  if (!tenant) throw new Error('No tenant found');

  const response = await fetch(`/${tenant}/api/push/unsubscribe`, {
    method: 'POST'
  });

  if (!response.ok) {
    throw new Error('Failed to delete subscription from server');
  }
}

export const push = createPushStore();
