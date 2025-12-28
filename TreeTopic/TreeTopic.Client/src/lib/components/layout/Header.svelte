<script lang="ts">
  import { ui } from '$lib/stores';
  import { currentUser } from '$lib/stores/auth';

  interface Props {
    onMenuToggle?: () => void;
    children?: any;
  }

  let { onMenuToggle, children }: Props = $props();
</script>

<header class="app-header border-b border-border bg-white sticky top-0 z-40 flex items-center justify-between px-6">
  <div class="flex items-center gap-4">
    {#if onMenuToggle}
      <button
        on:click={onMenuToggle}
        class="p-2 hover:bg-surface rounded transition-colors"
        aria-label="Toggle menu"
        title="Toggle sidebar"
      >
        ☰
      </button>
    {/if}

    <div class="flex items-center gap-2">
      <h1 class="text-xl font-bold text-primary">🌳 TreeTopic</h1>
    </div>
  </div>

  <div class="flex items-center gap-6">
    {#if children}
      <div class="flex items-center gap-4">
        {@render children()}
      </div>
    {/if}

    {#if $currentUser}
      <div class="flex items-center gap-3 pl-6 border-l border-border">
        {#if $currentUser.avatar}
          <img
            src={$currentUser.avatar}
            alt={$currentUser.displayName}
            class="w-8 h-8 rounded-full bg-surface"
          />
        {:else}
          <div class="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center text-sm font-bold">
            {$currentUser.displayName?.charAt(0) ?? 'U'}
          </div>
        {/if}
        <span class="text-sm font-medium text-text">{$currentUser.displayName}</span>
      </div>
    {/if}
  </div>
</header>

<style>
  :global(.app-header) {
    height: 60px;
  }
</style>
