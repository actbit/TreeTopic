<script lang="ts">
  import { onMount } from 'svelte';

  export interface ContextMenuItem {
    id: string;
    label: string;
    icon?: string;
    action: () => void;
    isDangerous?: boolean;
    isDisabled?: boolean;
    divider?: boolean;
  }

  interface Props {
    items: ContextMenuItem[];
    x: number;
    y: number;
    onClose?: () => void;
  }

  let { items, x, y, onClose }: Props = $props();

  let menuElement: HTMLDivElement | undefined = $state();
  let adjustedX = $state(x);
  let adjustedY = $state(y);

  function handleAction(action: () => void) {
    action();
    if (onClose) {
      onClose();
    }
  }

  function handleClickOutside(event: MouseEvent) {
    if (menuElement && !menuElement.contains(event.target as Node)) {
      if (onClose) {
        onClose();
      }
    }
  }

  onMount(() => {
    if (menuElement) {
      // メニューが画面外に出ないように位置を調整
      const rect = menuElement.getBoundingClientRect();
      const windowHeight = window.innerHeight;
      const windowWidth = window.innerWidth;

      if (rect.bottom > windowHeight) {
        adjustedY = Math.max(0, y - rect.height);
      }

      if (rect.right > windowWidth) {
        adjustedX = Math.max(0, x - rect.width);
      }
    }

    document.addEventListener('click', handleClickOutside);
    document.addEventListener('contextmenu', handleClickOutside);

    return () => {
      document.removeEventListener('click', handleClickOutside);
      document.removeEventListener('contextmenu', handleClickOutside);
    };
  });
</script>

<div
  bind:this={menuElement}
  class="fixed bg-white border border-border rounded-md shadow-lg z-50 min-w-48 py-1 slide-in-up"
  style="left: {adjustedX}px; top: {adjustedY}px;"
>
  {#each items as item (item.id)}
    {#if item.divider}
      <div class="h-px bg-border my-1"></div>
    {:else}
      <button
        type="button"
        on:click={() => handleAction(item.action)}
        disabled={item.isDisabled}
        class="w-full px-4 py-2 text-left text-sm flex items-center gap-3 text-text transition-colors
          hover:bg-surface disabled:opacity-50 disabled:cursor-not-allowed
          {item.isDangerous ? 'text-error hover:bg-red-50' : ''}"
      >
        {#if item.icon}
          <span class="text-base flex-shrink-0">{item.icon}</span>
        {/if}
        <span>{item.label}</span>
      </button>
    {/if}
  {/each}
</div>

<style>
  :global(.slide-in-up) {
    animation: slideInUp 0.2s ease-out;
  }

  @keyframes slideInUp {
    from {
      opacity: 0;
      transform: translateY(10px);
    }
    to {
      opacity: 1;
      transform: translateY(0);
    }
  }
</style>
