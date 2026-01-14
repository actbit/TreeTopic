<script lang="ts">
  import Header from './Header.svelte';
  import Sidebar from './Sidebar.svelte';
  import MainPanel from './MainPanel.svelte';
  import SubPanel from './SubPanel.svelte';
  import { ui, sidebarCollapsed, subpanelCollapsed, responsiveLayout } from '$lib/stores/ui';
  import type { Snippet } from 'svelte';

  interface Props {
    subPanelTitle?: string;
    headerContent?: Snippet;
    sidebarContent?: Snippet;
    mainContent?: Snippet;
    subPanelContent?: Snippet;
  }

  let {
    subPanelTitle,
    headerContent,
    sidebarContent,
    mainContent,
    subPanelContent,
  }: Props = $props();

  function toggleSidebar() {
    ui.toggleSidebar();
  }

  function toggleSubpanel() {
    ui.toggleSubpanel();
  }

  function closeMobilePanels() {
    if (!$sidebarCollapsed) {
      ui.setSidebarCollapsed(true);
    }
    if (!$subpanelCollapsed) {
      ui.setSubpanelCollapsed(true);
    }
  }
</script>

<div class="app-container min-h-screen flex flex-col">
  <Header onMenuToggle={toggleSidebar}>
    {#if subPanelTitle}
      <button
        type="button"
        class="subpanel-toggle-button"
        onclick={toggleSubpanel}
        aria-expanded={!$subpanelCollapsed}
        title={`Toggle ${subPanelTitle}`}
      >
        {subPanelTitle}
      </button>
    {/if}
    {@render headerContent?.()}
  </Header>

  {#if $responsiveLayout}
    <div
      class="sidebar-backdrop"
      class:visible={!$sidebarCollapsed || !$subpanelCollapsed}
      onclick={closeMobilePanels}
      aria-hidden="true"
    ></div>
  {/if}

  <div class="layout-body" class:stacked={$responsiveLayout}>
    <Sidebar>
      {@render sidebarContent?.()}
    </Sidebar>

    <MainPanel>
      {@render mainContent?.()}
    </MainPanel>

    <SubPanel title={subPanelTitle}>
      {@render subPanelContent?.()}
    </SubPanel>
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
    overflow: hidden;
  }

  .layout-body.stacked {
    display: flex;
    flex-direction: column;
    grid-template-columns: 1fr;
    overflow: visible;
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

  .subpanel-toggle-button {
    padding: 6px 12px;
    border-radius: 999px;
    border: 1px solid var(--color-border);
    background: var(--color-surface);
    color: var(--color-text);
    font-size: var(--font-size-sm);
    font-weight: 600;
    cursor: pointer;
  }

  .subpanel-toggle-button:hover {
    background-color: var(--color-surface-hover);
  }

  @media (max-width: 1024px) {
    .layout-body {
      grid-template-columns: 1fr;
    }
  }
</style>
