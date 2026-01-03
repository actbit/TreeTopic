<script lang="ts">
  import { subpanelCollapsed } from '$lib/stores/ui';

  interface Props {
    title?: string;
    children?: any;
  }

  let { title, children }: Props = $props();
</script>

<aside
  class="app-subpanel flex flex-col bg-surface {$subpanelCollapsed ? 'subpanel-collapsed' : 'subpanel-expanded'}"
>
  {#if title}
    <div class="panel-header">
      <h3 class="panel-title">{title}</h3>
    </div>
  {/if}

  <div class="overflow-y-auto panel-body">
    {#if children}
      {@render children()}
    {/if}
  </div>
</aside>

<style>
  .app-subpanel {
    border-left: 1px solid var(--color-border);
    transition: width var(--transition-normal);
  }

  .subpanel-collapsed {
    width: 0;
  }

  .subpanel-expanded {
    width: 320px;
  }
</style>
