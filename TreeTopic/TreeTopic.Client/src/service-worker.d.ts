/* eslint-disable */
/// <reference types="@sveltejs/kit" />
/// <reference types="svelte" />
/// <reference no-default-lib="true"/>
/// <reference lib="esnext"/>
/// <reference lib="webworker"/>
/// <reference lib="scripthost"/>

declare const self: ServiceWorkerGlobalScope;

declare module '*.svelte' {
  export default SvelteComponent;
}
declare module 'svelte/store' {
  export { get, writable, derived, readable } from 'svelte/store';
}

interface ImportMetaEnv {
  readonly VITE_PWA_SW_SCOPE: string;
  readonly VITE_PWA_SW_REDIRECT: 'true' | 'false' | 'local' | 'fallback';
  readonly VITE_PWA_SW_VIRTUAL: 'true' | 'false';
  readonly VITE_PWA_DISABLE: 'true' | 'false';
}
