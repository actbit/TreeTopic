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
  class="modal"
  on:close={handleClose}
  on:click={handleBackdropClick}
>
  <div class="modal-content modal-{size}">
    {#if title || closeButton}
      <div class="modal-header">
        {#if title}
          <h2 class="modal-title">{title}</h2>
        {/if}

        {#if closeButton}
          <button
            on:click={handleClose}
            class="modal-close"
            aria-label="Close modal"
          >
            ✕
          </button>
        {/if}
      </div>
    {/if}

    <div class="modal-body">
      {#if children}
        {@render children()}
      {/if}
    </div>
  </div>
</dialog>

<style>
  .modal {
    border: none;
    border-radius: 0;
    padding: 0;
    max-width: none;
    margin: auto;
    background: transparent;
  }

  .modal::backdrop {
    background-color: rgba(0, 0, 0, 0.5);
  }

  .modal[open] {
    display: flex;
    animation: modalSlideIn 0.3s ease-out;
  }

  .modal-content {
    background-color: var(--color-background);
    color: var(--color-text);
    border-radius: var(--border-radius-lg);
    box-shadow: var(--shadow-xl);
    width: 100%;
  }

  .modal-small {
    max-width: 384px;
  }

  .modal-medium {
    max-width: 448px;
  }

  .modal-large {
    max-width: 672px;
  }

  .modal-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px 24px;
    border-bottom: 1px solid var(--color-border);
  }

  .modal-title {
    font-size: var(--font-size-xl);
    font-weight: 700;
    color: var(--color-text);
  }

  .modal-close {
    margin-left: auto;
    padding: 4px;
    background: transparent;
    border: none;
    color: var(--color-text-light);
    font-size: var(--font-size-lg);
    cursor: pointer;
    border-radius: var(--border-radius-sm);
    transition: all 0.2s ease;
  }

  .modal-close:hover {
    color: var(--color-text);
    background-color: var(--color-surface);
  }

  .modal-body {
    padding: 16px 24px;
    max-height: calc(100vh - 200px);
    overflow-y: auto;
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
