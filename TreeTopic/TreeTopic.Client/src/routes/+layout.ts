// SPA mode: disable SSR to avoid __data.json fetches on deep links.
export const ssr = false;

import { onMount } from 'svelte';
import { browser } from '$app/environment';

// サービスワーカーを登録
if (browser && 'serviceWorker' in navigator) {
  navigator.serviceWorker.register('/service-worker.js', { type: 'module' })
    .then((registration) => {
      console.log('Service Worker registered:', registration);
    })
    .catch((error) => {
      console.error('Service Worker registration failed:', error);
    });
}

