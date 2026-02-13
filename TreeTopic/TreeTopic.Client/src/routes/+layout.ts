export const ssr = false;

import { browser } from '$app/environment';

if (browser && 'serviceWorker' in navigator) {
  async function registerServiceWorker() {
    const registrations = await navigator.serviceWorker.getRegistrations();
    const existingRegistration = registrations.find(
      (reg) => reg.active?.scriptURL.endsWith('/service-worker.js')
    );
    if (existingRegistration) {
      return;
    }

    try {
      await navigator.serviceWorker.register('/service-worker.js', { type: 'module' });
    } catch (error) {
    }
  }

  if ('requestIdleCallback' in window) {
    (window as any).requestIdleCallback(() => registerServiceWorker());
  } else {
    setTimeout(() => registerServiceWorker(), 1000);
  }
}

