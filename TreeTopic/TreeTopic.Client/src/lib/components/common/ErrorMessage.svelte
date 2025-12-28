<script lang="ts">
  interface Props {
    message: string;
    onDismiss?: () => void;
    dismissable?: boolean;
    type?: 'error' | 'warning' | 'info';
    fullWidth?: boolean;
  }

  let { message, onDismiss, dismissable = true, type = 'error', fullWidth = false }: Props =
    $props();

  const typeStyles = {
    error: 'bg-red-50 border-error text-error',
    warning: 'bg-yellow-50 border-warning text-warning',
    info: 'bg-blue-50 border-info text-info',
  };

  const icons = {
    error: '⚠',
    warning: '⚡',
    info: 'ℹ',
  };
</script>

<div
  class="flex items-start gap-3 p-4 border-l-4 rounded {typeStyles[type]} {fullWidth
    ? 'w-full'
    : ''} animate-slideInDown"
  role="alert"
>
  <span class="text-xl flex-shrink-0">{icons[type]}</span>

  <div class="flex-1">
    <p class="text-sm font-medium">{message}</p>
  </div>

  {#if dismissable && onDismiss}
    <button
      type="button"
      on:click={onDismiss}
      class="flex-shrink-0 p-1 hover:opacity-70 transition-opacity"
      aria-label="Dismiss message"
    >
      ✕
    </button>
  {/if}
</div>

<style>
  :global(.animate-slideInDown) {
    animation: slideInDown 0.3s ease-out;
  }

  @keyframes slideInDown {
    from {
      opacity: 0;
      transform: translateY(-10px);
    }
    to {
      opacity: 1;
      transform: translateY(0);
    }
  }
</style>
