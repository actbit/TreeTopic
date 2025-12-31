<script lang="ts">
  import { sidebarCollapsed } from '$lib/stores/ui';

  interface Props {
    children?: any;
  }

  let { children }: Props = $props();
</script>

<aside
  class="app-sidebar overflow-y-auto flex flex-col bg-surface {$sidebarCollapsed ? 'sidebar-collapsed' : 'sidebar-expanded'}"
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
    transition: width var(--transition-normal);
  }

  .sidebar-collapsed {
    width: 0;
  }

  .sidebar-expanded {
    width: 256px;
  }

  .sidebar-nav {
    flex: 1;
    padding: var(--spacing-md);
    display: flex;
    flex-direction: column;
    gap: var(--spacing-xs);
  }
</style>
