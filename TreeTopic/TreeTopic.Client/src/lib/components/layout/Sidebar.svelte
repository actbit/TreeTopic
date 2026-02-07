<script lang="ts">
  import { sidebarCollapsed, responsiveLayout } from '$lib/stores/ui';
  import type { Snippet } from 'svelte';

  interface Props {
    children?: Snippet;
  }

  let { children }: Props = $props();
</script>

<aside
  class={`app-sidebar overflow-y-auto flex flex-col bg-surface ${$responsiveLayout ? 'sidebar-mobile' : 'sidebar-desktop'} ${$sidebarCollapsed ? 'sidebar-collapsed' : 'sidebar-expanded'} ${$responsiveLayout && !$sidebarCollapsed ? 'mobile-visible' : ''}`}
  aria-hidden={$responsiveLayout && $sidebarCollapsed}
>
  <nav class="sidebar-nav">
    {#if children}
      {@render children()}
    {/if}
  </nav>
</aside>

<style>
  .app-sidebar {
    border-right: 1px solid var(--color-border);
    transition: width var(--transition-normal), transform var(--transition-normal);
    width: 256px;
    background-color: var(--color-surface);
    min-height: 100%;
    position: relative;
    z-index: 10;
  }

  .sidebar-desktop.sidebar-collapsed {
    width: 0;
  }

  .sidebar-desktop.sidebar-expanded {
    width: 256px;
  }

  .sidebar-mobile {
    position: fixed;
    top: 0;
    left: 0;
    height: 100vh;
    width: 280px;
    transform: translateX(-100%);
    padding-top: 60px;
    border-right: 1px solid var(--color-border);
    box-shadow: var(--shadow-2xl);
    z-index: 30;
  }

  .sidebar-mobile.sidebar-expanded {
    transform: translateX(0);
  }

  .sidebar-mobile.sidebar-collapsed {
    transform: translateX(-100%);
  }

  .sidebar-nav {
    flex: 1;
    padding: var(--spacing-md);
    display: flex;
    flex-direction: column;
    gap: var(--spacing-xs);
  }
</style>
