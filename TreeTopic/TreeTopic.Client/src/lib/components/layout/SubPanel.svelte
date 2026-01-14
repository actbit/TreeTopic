<script lang="ts">
  import { subpanelCollapsed, responsiveLayout } from '$lib/stores/ui';

  interface Props {
    title?: string;
    children?: any;
  }

  let { title, children }: Props = $props();
</script>

<aside
  class="app-subpanel flex flex-col bg-surface {$responsiveLayout ? 'subpanel-mobile' : 'subpanel-desktop'} {$subpanelCollapsed ? 'subpanel-collapsed' : 'subpanel-expanded'}"
  aria-hidden={$responsiveLayout && $subpanelCollapsed}
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
    transition: width var(--transition-normal), transform var(--transition-normal);
  }

  .subpanel-desktop.subpanel-collapsed {
    width: 0;
  }

  .subpanel-desktop.subpanel-expanded {
    width: 320px;
  }

  .subpanel-mobile {
    position: fixed;
    top: 60px;
    right: 0;
    bottom: 0;
    width: 80vw;
    max-width: 360px;
    transform: translateX(100%);
    z-index: 30;
    display: flex;
    box-shadow: var(--shadow-2xl);
    border-left: 1px solid var(--color-border);
  }

  .subpanel-mobile.subpanel-expanded {
    transform: translateX(0);
  }

  .subpanel-mobile.subpanel-collapsed {
    transform: translateX(100%);
  }
</style>
