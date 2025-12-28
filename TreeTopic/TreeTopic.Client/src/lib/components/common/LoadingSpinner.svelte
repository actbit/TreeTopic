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
  <div class="fixed inset-0 flex items-center justify-center bg-black bg-opacity-50 z-50">
    <div class="bg-white rounded-lg shadow-xl p-8 flex flex-col items-center gap-4">
      <div
        class="animate-spin"
        style="width: {sizeMap[size]}; height: {sizeMap[size]};"
      >
        <svg
          viewBox="0 0 50 50"
          xmlns="http://www.w3.org/2000/svg"
          class="w-full h-full"
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
        <p class="text-text-secondary">{message}</p>
      {/if}
    </div>
  </div>
{:else}
  <div class="flex flex-col items-center gap-3">
    <div
      class="animate-spin"
      style="width: {sizeMap[size]}; height: {sizeMap[size]};"
    >
      <svg
        viewBox="0 0 50 50"
        xmlns="http://www.w3.org/2000/svg"
        class="w-full h-full"
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
      <p class="text-sm text-text-secondary">{message}</p>
    {/if}
  </div>
{/if}

<style>
  :global(.animate-spin) {
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
