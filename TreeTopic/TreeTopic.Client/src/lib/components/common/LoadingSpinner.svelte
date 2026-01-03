<script lang="ts">
  interface Props {
    size?: 'small' | 'medium' | 'large';
    message?: string;
    fullScreen?: boolean;
  }

  let { size = 'medium', message, fullScreen = false }: Props = $props();

  const sizeMap = {
    small: '24px',
    medium: '40px',
    large: '64px',
  };
</script>

{#if fullScreen}
  <div class="spinner-fullscreen">
    <div class="spinner-container">
      <div
        class="spinner"
        style="width: {sizeMap[size]}; height: {sizeMap[size]};"
      >
        <svg
          viewBox="0 0 50 50"
          xmlns="http://www.w3.org/2000/svg"
          class="spinner-svg"
        >
          <circle
            cx="25"
            cy="25"
            r="20"
            fill="none"
            stroke="var(--color-primary)"
            stroke-width="4"
            stroke-dasharray="31.4 94.2"
          />
        </svg>
      </div>

      {#if message}
        <p class="spinner-message">{message}</p>
      {/if}
    </div>
  </div>
{:else}
  <div class="spinner-inline">
    <div
      class="spinner"
      style="width: {sizeMap[size]}; height: {sizeMap[size]};"
    >
      <svg
        viewBox="0 0 50 50"
        xmlns="http://www.w3.org/2000/svg"
        class="spinner-svg"
      >
        <circle
          cx="25"
          cy="25"
          r="20"
          fill="none"
          stroke="var(--color-primary)"
          stroke-width="4"
          stroke-dasharray="31.4 94.2"
        />
      </svg>
    </div>

    {#if message}
      <p class="spinner-message-inline">{message}</p>
    {/if}
  </div>
{/if}

<style>
  .spinner-fullscreen {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background-color: rgba(0, 0, 0, 0.5);
    z-index: 50;
  }

  .spinner-container {
    background-color: var(--color-background);
    border-radius: var(--border-radius-lg);
    box-shadow: var(--shadow-xl);
    padding: 32px;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 16px;
  }

  .spinner-inline {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 12px;
  }

  .spinner {
    animation: spin 1s linear infinite;
  }

  .spinner-svg {
    width: 100%;
    height: 100%;
  }

  .spinner-message {
    color: var(--color-text-secondary);
    font-size: var(--font-size-base);
  }

  .spinner-message-inline {
    font-size: var(--font-size-sm);
    color: var(--color-text-secondary);
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
