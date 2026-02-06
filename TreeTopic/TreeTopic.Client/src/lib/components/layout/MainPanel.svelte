<script lang="ts">
  import type { Snippet } from 'svelte';

  interface Props {
    children?: Snippet;
  }

  let { children }: Props = $props();
</script>

<main class="app-main flex overflow-hidden flex-col bg-surface">
  {#if children}
    {@render children()}
  {/if}
</main>

<style>
  .app-main {
    flex: 1;
    min-height: 0;
    min-width: 0;
    height: 100%;
  }
</style>
