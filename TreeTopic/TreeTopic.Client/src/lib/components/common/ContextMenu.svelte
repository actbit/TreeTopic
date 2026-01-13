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
  let adjustedX = $state(0);
  let adjustedY = $state(0);

  $effect(() => {
    adjustedX = x;
    adjustedY = y;
  });

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
  class="context-menu"
  style="left: {adjustedX}px; top: {adjustedY}px;"
>
  {#each items as item (item.id)}
    {#if item.divider}
      <div class="context-menu-divider"></div>
    {:else}
      <button
        type="button"
        onclick={() => handleAction(item.action)}
        disabled={item.isDisabled}
        class="context-menu-item {item.isDangerous ? 'context-menu-item-danger' : ''}"
      >
        {#if item.icon}
          <span class="context-menu-item-icon">{item.icon}</span>
        {/if}
        <span>{item.label}</span>
      </button>
    {/if}
  {/each}
</div>

<style>
  .context-menu {
    position: fixed;
    background-color: var(--color-background);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-md);
    box-shadow: var(--shadow-lg);
    z-index: 50;
    min-width: 192px;
    padding: var(--spacing-xs) 0;
    animation: slideInUp 0.2s ease-out;
  }

  .context-menu-divider {
    height: 1px;
    background-color: var(--color-border);
    margin: var(--spacing-xs) 0;
  }

  .context-menu-item {
    width: 100%;
    padding: var(--spacing-sm) var(--spacing-md);
    text-align: left;
    font-size: var(--font-size-sm);
    display: flex;
    align-items: center;
    gap: var(--spacing-md);
    color: var(--color-text);
    background-color: transparent;
    border: none;
    cursor: pointer;
    transition: background-color 0.2s ease;
  }

  .context-menu-item:hover:not(:disabled) {
    background-color: var(--color-surface);
  }

  .context-menu-item:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .context-menu-item-danger {
    color: var(--color-error);
  }

  .context-menu-item-danger:hover:not(:disabled) {
    background-color: color-mix(in srgb, var(--color-error) 5%, transparent);
  }

  .context-menu-item-icon {
    font-size: var(--font-size-base);
    flex-shrink: 0;
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
