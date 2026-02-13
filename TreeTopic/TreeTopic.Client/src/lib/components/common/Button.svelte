<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import type { Snippet } from 'svelte';

  type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'success' | 'danger';
  type ButtonSize = 'small' | 'base' | 'large';

  interface Props {
    variant?: ButtonVariant;
    size?: ButtonSize;
    disabled?: boolean;
    loading?: boolean;
    fullWidth?: boolean;
    icon?: string;
    type?: 'button' | 'submit' | 'reset';
    onclick?: (e: MouseEvent) => void;
    children?: Snippet;
    ariaLabel?: string;
    title?: string;
  }

  let {
    variant = 'primary',
    size = 'base',
    disabled = false,
    loading = false,
    fullWidth = false,
    icon,
    type = 'button',
    onclick,
    children,
    ariaLabel,
    title,
  }: Props = $props();

  const dispatch = createEventDispatcher<{ click: MouseEvent }>();

  function handleClick(e: MouseEvent) {
    onclick?.(e);
    dispatch('click', e);
  }
</script>

<button
  {type}
  {disabled}
  onclick={handleClick}
  class="btn btn-{variant} btn-{size} {fullWidth ? 'btn-full-width' : ''}"
  aria-label={ariaLabel}
  title={title}
  aria-busy={loading}
  aria-disabled={disabled}
>
  {#if loading}
    <span class="btn-spinner">...</span>
  {:else if icon}
    <span class="btn-icon">{icon}</span>
  {/if}
  {#if children}
    {@render children()}
  {/if}
</button>

<style>
  .btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    font-family: var(--font-family-base);
    font-weight: 600;
    border: none;
    cursor: pointer;
    transition: all 0.2s ease;
    outline-offset: 2px;
  }

  .btn:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .btn:focus-visible {
    outline: 2px solid var(--color-primary);
    outline-offset: 2px;
  }

  .btn:active:not(:disabled) {
    transform: scale(0.98);
  }

  .btn-primary {
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
  }

  .btn-primary:hover:not(:disabled) {
    background-color: var(--color-primary-hover);
  }

  .btn-primary:active:not(:disabled) {
    background-color: var(--color-primary-active);
  }

  .btn-secondary {
    background-color: var(--color-surface);
    color: var(--color-text);
    border: 1px solid var(--color-border);
  }

  .btn-secondary:hover:not(:disabled) {
    background-color: var(--color-surface-hover);
  }

  .btn-ghost {
    background-color: transparent;
    color: var(--color-primary);
  }

  .btn-ghost:hover:not(:disabled) {
    background-color: var(--color-surface);
  }

  .btn-ghost:active:not(:disabled) {
    background-color: var(--color-surface-hover);
  }

  .btn-success {
    background-color: var(--color-success);
    color: var(--color-text-inverse);
  }

  .btn-success:hover:not(:disabled) {
    opacity: 0.9;
  }

  .btn-success:active:not(:disabled) {
    opacity: 0.8;
  }

  .btn-danger {
    background-color: var(--color-error);
    color: var(--color-text-inverse);
  }

  .btn-danger:hover:not(:disabled) {
    opacity: 0.9;
  }

  .btn-danger:active:not(:disabled) {
    opacity: 0.8;
  }

  .btn-small {
    padding: 4px 12px;
    font-size: var(--font-size-xs);
    border-radius: var(--border-radius-sm);
  }

  .btn-base {
    padding: 8px 16px;
    font-size: var(--font-size-base);
    border-radius: var(--border-radius-sm);
  }

  .btn-large {
    padding: 12px 24px;
    font-size: var(--font-size-lg);
    border-radius: var(--border-radius-md);
  }

  .btn-full-width {
    width: 100%;
  }

  .btn-spinner {
    display: inline-block;
    animation: spin 1s linear infinite;
  }

  @keyframes spin {
    from {
      transform: rotate(0deg);
    }
    to {
      transform: rotate(360deg);
    }
  }
</style>
