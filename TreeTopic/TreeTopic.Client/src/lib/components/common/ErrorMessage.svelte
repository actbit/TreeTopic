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

  const icons = {
    error: '⚠',
    warning: '⚡',
    info: 'ℹ',
  };
</script>

<div
  class="error-message error-message-{type} {fullWidth ? 'error-message-full-width' : ''}"
  role="alert"
>
  <span class="error-message-icon">{icons[type]}</span>

  <div class="error-message-content">
    <p>{message}</p>
  </div>

  {#if dismissable && onDismiss}
    <button
      type="button"
      onclick={onDismiss}
      class="error-message-dismiss"
      aria-label="Dismiss message"
    >
      ✕
    </button>
  {/if}
</div>

<style>
  .error-message {
    display: flex;
    align-items: flex-start;
    gap: 12px;
    padding: 16px;
    border-left: 4px solid;
    border-radius: var(--border-radius-sm);
    animation: slideInDown 0.3s ease-out;
  }

  .error-message-error {
    background-color: #fef2f2;
    border-color: var(--color-error);
    color: var(--color-error);
  }

  .error-message-warning {
    background-color: #fefce8;
    border-color: var(--color-warning);
    color: var(--color-warning);
  }

  .error-message-info {
    background-color: #eff6ff;
    border-color: var(--color-info);
    color: var(--color-info);
  }

  .error-message-full-width {
    width: 100%;
  }

  .error-message-icon {
    font-size: var(--font-size-xl);
    flex-shrink: 0;
  }

  .error-message-content {
    flex: 1;
  }

  .error-message-content p {
    font-size: var(--font-size-sm);
    font-weight: 500;
  }

  .error-message-dismiss {
    flex-shrink: 0;
    padding: 4px;
    background: transparent;
    border: none;
    cursor: pointer;
    transition: opacity 0.2s ease;
  }

  .error-message-dismiss:hover {
    opacity: 0.7;
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
