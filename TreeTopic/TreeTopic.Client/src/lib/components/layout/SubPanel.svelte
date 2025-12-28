<script lang="ts">
  import { subpanelCollapsed } from '$lib/stores/ui';

  interface Props {
    title?: string;
    children?: any;
  }

  let { title, children }: Props = $props();
</script>

<aside
  class="app-subpanel transition-all duration-300 flex flex-col bg-surface
    {$subpanelCollapsed ? 'w-0' : 'w-80'}"
>
  {#if title}
    <div class="px-4 py-3 border-b border-border">
      <h3 class="font-semibold text-text">{title}</h3>
    </div>
  {/if}

  <div class="flex-1 overflow-y-auto p-4">
    {#if children}
      {@render children()}
    {/if}
  </div>
</aside>

<style>
  :global(.app-subpanel) {
    border-left: 1px solid var(--color-border);
  }
</style>
