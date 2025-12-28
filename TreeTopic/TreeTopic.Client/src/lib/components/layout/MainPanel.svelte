<script lang="ts">
  interface Props {
    children?: any;
  }

  let { children }: Props = $props();
</script>

<main class="app-main flex-1 bg-white overflow-hidden flex flex-col">
  {#if children}
    {@render children()}
  {/if}
</main>

<style>
  :global(.app-main) {
    display: flex;
    flex-direction: column;
    background-color: var(--color-background);
  }
</style>
