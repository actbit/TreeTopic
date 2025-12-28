<script lang="ts">
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
    children?: any;
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
  }: Props = $props();

  const variantClass = {
    primary: 'bg-primary text-white hover:bg-primary-hover active:bg-primary-active',
    secondary:
      'bg-surface text-text border border-border hover:bg-surface-hover active:bg-surface',
    ghost: 'text-primary hover:bg-surface active:bg-surface-hover',
    success: 'bg-success text-white hover:opacity-90 active:opacity-80',
    danger: 'bg-error text-white hover:opacity-90 active:opacity-80',
  };

  const sizeClass = {
    small: 'px-3 py-1 text-xs rounded',
    base: 'px-4 py-2 text-base rounded-sm',
    large: 'px-6 py-3 text-lg rounded-md',
  };
</script>

<button
  {type}
  {disabled}
  {onclick}
  class="inline-flex items-center justify-center gap-2 font-semibold transition-all duration-200 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed {variantClass[
    variant
  ]} {sizeClass[size]} {fullWidth ? 'w-full' : ''}"
>
  {#if loading}
    <span class="animate-spin">⏳</span>
  {:else if icon}
    <span>{icon}</span>
  {/if}
  {#if children}
    {@render children()}
  {/if}
</button>

<style>
  button {
    font-family: var(--font-family-base);
    font-weight: 600;
    border: none;
    transition: all 0.2s ease;
  }

  button:active:not(:disabled) {
    transform: scale(0.98);
  }
</style>
