<script lang="ts">
  import { sidebarCollapsed } from '$lib/stores/ui';

  interface Props {
    children?: any;
  }

  let { children }: Props = $props();
</script>

<aside
  class="app-sidebar overflow-y-auto transition-all duration-300 flex flex-col bg-surface
    {$sidebarCollapsed ? 'w-0' : 'w-64'}"
>
  <nav class="flex-1 p-4 space-y-2">
    {#if children}
      {@render children()}
    {/if}
  </nav>
</aside>

<style>
  :global(.app-sidebar) {
    border-right: 1px solid var(--color-border);
  }
</style>
