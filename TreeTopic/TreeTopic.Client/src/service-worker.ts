/// <reference types="@sveltejs/kit" />
import { build, files, version } from '$service-worker';

if (typeof self !== 'undefined') {
  // Push通知受信時の処理
  self.addEventListener('push', (event: PushEvent) => {
    if (!event.data) {
      return;
    }

    const data = event.data.json();

    const options: NotificationOptions = {
      body: data.body || '',
      icon: data.icon || '/pwa-192x192.png',
      badge: data.badge || '/pwa-192x192.png',
      vibrate: [200, 100, 200],
      data: data.data,
      requireInteraction: false
    };

    event.waitUntil(
      self.registration.showNotification(data.title || 'TreeTopic', options)
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
    event.respondWith(
      caches.match(event.request).then((response) => {
        return response || fetch(event.request);
      })
    );
  });
}
