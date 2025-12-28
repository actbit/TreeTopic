<script lang="ts">
  import Header from './Header.svelte';
  import Sidebar from './Sidebar.svelte';
  import MainPanel from './MainPanel.svelte';
  import SubPanel from './SubPanel.svelte';
  import { ui } from '$lib/stores/ui';

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
</script>

<div class="app-container min-h-screen bg-background flex flex-col">
  <Header onMenuToggle={toggleSidebar}>
    <slot name="headerContent" />
  </Header>

  <div class="flex flex-1 overflow-hidden gap-0">
    <Sidebar>
      <slot name="sidebarContent" />
    </Sidebar>

    <MainPanel>
      <slot name="mainContent" />
    </MainPanel>

    <SubPanel title={subPanelTitle}>
      <slot name="subPanelContent" />
    </SubPanel>
  </div>
</div>

<style>
  :global(.app-container) {
    display: grid;
    grid-template-columns: 1fr;
    grid-template-rows: auto 1fr;
    height: 100vh;
  }

  :global(.app-container > div:last-child) {
    display: grid;
    grid-template-columns: auto 1fr auto;
    gap: 0;
  }

  @media (max-width: 1024px) {
    :global(.app-container > div:last-child) {
      grid-template-columns: 1fr;
    }
  }
</style>
