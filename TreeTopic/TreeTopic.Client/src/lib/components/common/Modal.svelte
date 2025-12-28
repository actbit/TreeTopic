<script lang="ts">
  import { onMount } from 'svelte';

  interface Props {
    isOpen?: boolean;
    title?: string;
    onClose?: () => void;
    size?: 'small' | 'medium' | 'large';
    closeButton?: boolean;
    children?: any;
  }

  let { isOpen = false, title, onClose, size = 'medium', closeButton = true, children }: Props = $props();

  let dialogElement: HTMLDialogElement | undefined = $state();

  const sizeClasses = {
    small: 'max-w-sm',
    medium: 'max-w-md',
    large: 'max-w-2xl',
  };

  onMount(() => {
    if (dialogElement) {
      if (isOpen) {
        dialogElement.showModal();
      } else {
        dialogElement.close();
      }
    }
  });

  $effect(() => {
    if (dialogElement) {
      if (isOpen) {
        dialogElement.showModal();
      } else {
        dialogElement.close();
      }
    }
  });

  function handleClose() {
    if (onClose) {
      onClose();
    }
  }

  function handleBackdropClick(event: MouseEvent) {
    if (event.target === dialogElement) {
      handleClose();
    }
  }
</script>

<dialog
  bind:this={dialogElement}
  class="modal modal-backdrop rounded-lg shadow-xl backdrop:bg-black backdrop:opacity-50 p-0"
  on:close={handleClose}
  on:click={handleBackdropClick}
>
  <div class="modal-content bg-white rounded-lg max-w-full {sizeClasses[size]} w-full">
    {#if title || closeButton}
      <div class="flex items-center justify-between px-6 py-4 border-b border-border">
        {#if title}
          <h2 class="text-xl font-bold text-text">{title}</h2>
        {/if}

        {#if closeButton}
          <button
            on:click={handleClose}
            class="ml-auto p-1 text-text-light hover:text-text hover:bg-surface rounded transition-colors"
            aria-label="Close modal"
          >
            ✕
          </button>
        {/if}
      </div>
    {/if}

    <div class="px-6 py-4 max-h-[calc(100vh-200px)] overflow-y-auto">
      {#if children}
        {@render children()}
      {/if}
    </div>
  </div>
</dialog>

<style>
  :global(dialog.modal) {
    border: none;
    border-radius: 0;
    padding: 0;
    max-width: none;
    margin: auto;
    background: transparent;
  }

  :global(dialog.modal::backdrop) {
    background-color: rgba(0, 0, 0, 0.5);
  }

  :global(dialog.modal[open]) {
    display: flex;
    animation: modalSlideIn 0.3s ease-out;
  }

  @keyframes modalSlideIn {
    from {
      transform: scale(0.95);
      opacity: 0;
    }
    to {
      transform: scale(1);
      opacity: 1;
    }
  }
</style>
