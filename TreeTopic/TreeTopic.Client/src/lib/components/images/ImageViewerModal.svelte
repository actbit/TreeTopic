<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import { ui, activeModals } from '$lib/stores/ui';

  const modalId = 'image-viewer';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let fileUrl = $derived.by(() => (modal?.data?.fileUrl ?? null) as string | null);
  let fileName = $derived.by(() => (modal?.data?.fileName ?? 'Image') as string);

  let scale = $state(1.0);
  let isLoading = $state(false);
  let error = $state<string | null>(null);

  function zoomIn() {
    scale = Math.min(scale + 0.2, 3.0);
  }

  function zoomOut() {
    scale = Math.max(scale - 0.2, 0.5);
  }

  function resetZoom() {
    scale = 1.0;
  }

  function downloadImage() {
    if (fileUrl) {
      const link = document.createElement('a');
      link.href = fileUrl;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
    scale = 1.0;
    error = null;
  }

  function handleImageError() {
    error = 'Failed to load image';
    isLoading = false;
  }

  function handleImageLoad() {
    isLoading = false;
  }
</script>

<Modal {isOpen} title={fileName} onClose={handleClose} size="xlarge" closeButton={!isLoading}>
  <div class="flex flex-col h-full bg-white">
    <!-- Toolbar -->
    <div class="border-b border-border p-3 bg-surface flex items-center justify-between gap-3 flex-wrap">
      <!-- Zoom controls -->
      <div class="flex items-center gap-2">
        <Button
          variant="secondary"
          size="small"
          onclick={zoomOut}
          disabled={isLoading || scale <= 0.5}
        >
          −
        </Button>

        <span class="text-sm text-text-light w-12 text-center">{Math.round(scale * 100)}%</span>

        <Button
          variant="secondary"
          size="small"
          onclick={zoomIn}
          disabled={isLoading || scale >= 3.0}
        >
          +
        </Button>

        <Button
          variant="secondary"
          size="small"
          onclick={resetZoom}
          disabled={isLoading}
        >
          Reset
        </Button>
      </div>

      <!-- Action buttons -->
      <div class="flex items-center gap-2">
        <Button
          variant="secondary"
          size="small"
          onclick={downloadImage}
          disabled={isLoading || !fileUrl}
        >
          Download
        </Button>
      </div>
    </div>

    <!-- Error message -->
    {#if error}
      <div class="p-4 bg-error bg-opacity-10 border-b border-error text-error text-sm">
        {error}
      </div>
    {/if}

    <!-- Image area -->
    <div class="flex-1 overflow-auto flex items-center justify-center bg-gray-100 p-4">
      {#if isLoading && !error}
        <div class="text-center">
          <div class="inline-block">
            <div class="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          </div>
          <p class="mt-2 text-sm text-text-light">Loading image...</p>
        </div>
      {/if}
      
      {#if fileUrl && !error}
        <!-- svelte-ignore a11y_click_events_have_key_events -->
        <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
        <img
          src={fileUrl}
          alt={fileName}
          class="max-w-full max-h-full shadow-lg transition-transform"
          style="transform: scale({scale});"
          onload={handleImageLoad}
          onerror={handleImageError}
        />
      {:else if !fileUrl}
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">No image loaded</p>
          <p class="text-sm">Upload an image file to view it here</p>
        </div>
      {/if}
    </div>
  </div>
</Modal>

<style>
  img {
    transform-origin: center center;
  }
</style>
