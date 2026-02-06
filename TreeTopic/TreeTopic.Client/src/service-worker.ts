/// <reference types="@sveltejs/kit" />
import { build, files, version } from '$service-worker';

if (typeof self !== 'undefined') {
  // Push通知受信時の処理
  self.addEventListener('push', (event: PushEvent) => {
    if (!event.data) {
      return;
    }

    const data = event.data.json();

    // dataプロパティからテナント情報とトピック情報を取得
    let tenantInfo = '';
    let tenantId = '';
    let topicId = '';
    let roomId = '';
    try {
      const parsedData = typeof data.data === 'string' ? JSON.parse(data.data) : data.data;
      if (parsedData?.tenant) {
        tenantId = parsedData.tenant;
        tenantInfo = `[${parsedData.tenant}] `;
      }
      if (parsedData?.topicId) {
        topicId = parsedData.topicId;
      }
      if (parsedData?.roomId) {
        roomId = parsedData.roomId;
      }
    } catch (e) {
      // パースエラーは無視
    }

    // クリック時に開くURLを生成（テナント、ルーム、トピック情報を含む）
    let url = '/';
    if (tenantId) {
      if (topicId && roomId) {
        // トピック情報がある場合はトピックページへ
        url = `/${tenantId}/room/${roomId}/topic/${topicId}`;
      } else if (roomId) {
        // ルーム情報のみがある場合はルームページへ
        url = `/${tenantId}/room/${roomId}`;
      } else {
        // テナント情報のみの場合はルーム一覧へ
        url = `/${tenantId}/room`;
      }
    }

    const options: NotificationOptions = {
      body: data.body || '',
      icon: data.icon || '/pwa-192x192.png',
      badge: data.badge || '/pwa-192x192.png',
      vibrate: [200, 100, 200],
      data: {
        ...data.data,
        tenant: tenantId,
        topicId,
        roomId,
        url
      },
      requireInteraction: false
    };

    event.waitUntil(
      self.registration.showNotification(`${tenantInfo}${data.title || 'TreeTopic'}`, options)
    );
  });

  // 通知クリック時の処理
  self.addEventListener('notificationclick', (event: NotificationEvent) => {
    event.notification.close();

    // URLを開く（data.urlがある場合）
    const urlToOpen = event.notification.data?.url || '/';
    event.waitUntil(
      clients.openWindow(urlToOpen)
    );
  });

  // PWAキャッシュ処理
  const cacheName = `v${version}`;
  const assets = [...build, ...files];

  self.addEventListener('install', (event: ExtendableEvent) => {
    event.waitUntil(
      caches.open(cacheName).then((cache) => cache.addAll(assets))
    );
  });

  self.addEventListener('activate', (event: ExtendableEvent) => {
    event.waitUntil(
      caches.keys().then(async (keys) => {
        for (const key of keys) {
          if (key !== cacheName) {
            await caches.delete(key);
          }
        }
      })
    );
  });

  self.addEventListener('fetch', (event: FetchEvent) => {
    const url = new URL(event.request.url);

    // APIリクエスト、SignalR、認証関連、browserLinkはキャッシュしない
    const shouldBypassCache =
      url.pathname.startsWith('/api/') ||
      url.pathname.startsWith('/hubs/') ||
      url.pathname.startsWith('/login') ||
      url.pathname.startsWith('/signin-') ||
      url.pathname.startsWith('/signout-') ||
      url.pathname.startsWith('/.well-known/') ||
      url.search.includes('negotiate') ||
      url.pathname.includes('browserLink') ||
      url.pathname.includes('_framework');

    if (shouldBypassCache) {
      return;
    }

    // HTML、CSS、JSのみキャッシュ（画像、フォント等はキャッシュしない）
    const pathname = url.pathname.toLowerCase();
    const isCacheableAsset =
      pathname.endsWith('.html') ||
      pathname.endsWith('.css') ||
      pathname.endsWith('.js') ||
      pathname.endsWith('.mjs');

    if (!isCacheableAsset) {
      return;
    }

    // HTML、CSS、JSのみキャッシュ
    event.respondWith(
      caches.match(event.request).then((response) => {
        if (response) {
          return response;
        }
        return fetch(event.request).then((fetchResponse) => {
          if (fetchResponse.status === 200 || fetchResponse.status === 0) {
            return caches.open(cacheName).then((cache) => {
              cache.put(event.request, fetchResponse.clone());
              return fetchResponse;
            });
          }
          return fetchResponse;
        });
      })
    );
  });
}
