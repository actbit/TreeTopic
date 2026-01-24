/// <reference types="@sveltejs/kit" />
import { build, files, version } from '$service-worker';

if (typeof self !== 'undefined') {
  // Push通知受信時の処理
  self.addEventListener('push', (event: PushEvent) => {
    if (!event.data) {
      return;
    }

    const data = event.data.json();

    // dataプロパティからテナント情報を取得
    let tenantInfo = '';
    let tenantId = '';
    try {
      const parsedData = typeof data.data === 'string' ? JSON.parse(data.data) : data.data;
      if (parsedData?.tenant) {
        tenantId = parsedData.tenant;
        tenantInfo = `[${parsedData.tenant}] `;
      }
    } catch (e) {
      // パースエラーは無視
    }

    const options: NotificationOptions = {
      body: data.body || '',
      icon: data.icon || '/pwa-192x192.png',
      badge: data.badge || '/pwa-192x192.png',
      vibrate: [200, 100, 200],
      data: {
        ...data.data,
        tenant: tenantId,
        // クリック時に開くURL（テナント情報を含む）
        url: tenantId ? `/${tenantId}/room` : '/'
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
