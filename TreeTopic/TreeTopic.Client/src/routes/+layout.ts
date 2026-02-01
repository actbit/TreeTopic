// SPA mode: disable SSR to avoid __data.json fetches on deep links.
export const ssr = false;

import { browser } from '$app/environment';

// サービスワーカーを登録（遅延実行・重複登録防止）
if (browser && 'serviceWorker' in navigator) {
  // ページ読み込み完了後にアイドル時間を使って登録
  async function registerServiceWorker() {
    // 既に登録されている場合はスキップ
    const registrations = await navigator.serviceWorker.getRegistrations();
    const existingRegistration = registrations.find(
      (reg) => reg.active?.scriptURL.endsWith('/service-worker.js')
    );
    if (existingRegistration) {
      console.log('Service Worker already registered:', existingRegistration);
      return;
    }

    // 新規登録
    try {
      const registration = await navigator.serviceWorker.register('/service-worker.js', { type: 'module' });
      console.log('Service Worker registered:', registration);
    } catch (error) {
      console.error('Service Worker registration failed:', error);
    }
  }

  // ページ読み込み完了後にアイドル時間で実行
  if ('requestIdleCallback' in window) {
    (window as any).requestIdleCallback(() => registerServiceWorker());
  } else {
    // requestIdleCallback がサポートされていない場合は setTimeout で遅延
    setTimeout(() => registerServiceWorker(), 1000);
  }
}

