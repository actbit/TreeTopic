<script lang="ts">
  import Header from './Header.svelte';
  import Sidebar from './Sidebar.svelte';
  import MainPanel from './MainPanel.svelte';
  import SubPanel from './SubPanel.svelte';
  import { ui, sidebarCollapsed, responsiveLayout } from '$lib/stores/ui';

  interface Props {
    subPanelTitle?: string;
  }

  let { subPanelTitle }: Props = $props();

  function toggleSidebar() {
    ui.toggleSidebar();
  }

  function toggleSubpanel() {
    ui.toggleSubpanel();
  }

  function closeMobileSidebar() {
    if (!$sidebarCollapsed) {
      ui.setSidebarCollapsed(true);
    }
  }
</script>

<div class="app-container min-h-screen flex flex-col">
  <Header onMenuToggle={toggleSidebar}>
    <slot name="headerContent" />
  </Header>

  {#if $responsiveLayout}
    <div
      class="sidebar-backdrop"
      class:visible={!$sidebarCollapsed}
      on:click={closeMobileSidebar}
      aria-hidden="true"
    />
  {/if}

  <div class="overflow-hidden layout-body" class:stacked={$responsiveLayout}>
    <Sidebar>
      <slot name="sidebarContent" />
    </Sidebar>

    <MainPanel>
      <slot name="mainContent" />
    </MainPanel>

    {#if !$responsiveLayout}
      <SubPanel title={subPanelTitle}>
        <slot name="subPanelContent" />
      </SubPanel>
    {/if}
  </div>
</div>

<style>
  .app-container {
    display: grid;
    grid-template-columns: 1fr;
    grid-template-rows: auto 1fr;
    height: 100vh;
    background-color: var(--color-background);
  }

  .layout-body {
    flex: 1;
    display: grid;
    grid-template-columns: auto 1fr auto;
    gap: 0;
    min-height: calc(100vh - 60px);
    align-items: stretch;
    grid-template-rows: 1fr;
  }

  .layout-body.stacked {
    display: flex;
    flex-direction: column;
    grid-template-columns: 1fr;
  }

  .sidebar-backdrop {
    display: none;
  }

  .sidebar-backdrop.visible {
    display: block;
    position: fixed;
    inset: 0;
    background-color: rgba(0, 0, 0, 0.45);
    z-index: 20;
  }

  @media (max-width: 1024px) {
    .layout-body {
      grid-template-columns: 1fr;
    }
  }
</style>
