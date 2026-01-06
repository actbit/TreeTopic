<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { files, addFile } from '$lib/stores/files';
  import { currentRoom } from '$lib/stores/rooms';
  import { selectedTopic } from '$lib/stores/topics';
  import { api } from '$lib/api/client';

  const modalId = 'file-upload';
  let isOpen = $derived.by(() => $activeModals.some((m) => m.id === modalId));

  let fileInput: HTMLInputElement | undefined = $state();
  let selectedFiles: File[] = $state([]);
  let isLoading = $state(false);
  let uploadProgress = $state<Record<string, number>>({});
  let error = $state<string | null>(null);

  function handleFileSelect(e: Event) {
    const input = e.target as HTMLInputElement;
    if (input.files) {
      selectedFiles = Array.from(input.files);
      error = null;
    }
  }

  function removeFile(index: number) {
    selectedFiles = selectedFiles.filter((_, i) => i !== index);
  }

  async function handleUpload() {
    if (selectedFiles.length === 0) {
      error = 'Please select at least one file';
      return;
    }

    if (!$currentRoom) {
      error = 'Please select a room first';
      return;
    }

    isLoading = true;
    error = null;

    try {
      const tenant = api.getCurrentTenant();
      for (const file of selectedFiles) {
        try {
          const response = await api.uploadFile(
            `/${tenant}/api/File/room/${$currentRoom.id}`,
            file,
            (progress) => {
              uploadProgress[file.name] = progress;
              uploadProgress = uploadProgress;
            }
          );

          addFile(response);
          delete uploadProgress[file.name];
        } catch (err: any) {
          error = `Failed to upload ${file.name}: ${err.message}`;
        }
      }

      selectedFiles = [];
      ui.closeModal(modalId);
    } catch (err: any) {
      error = err.message || 'Failed to upload files';
    } finally {
      isLoading = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
    selectedFiles = [];
    uploadProgress = {};
    error = null;
  }
</script>

<Modal {isOpen} title="Upload Files" onClose={handleClose} size="medium">
  <form on:submit|preventDefault={handleUpload} class="space-y-6">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <div
      class="border-2 border-dashed border-border rounded-lg p-10 text-center bg-surface hover:border-primary hover:bg-opacity-50 transition-all cursor-pointer"
      on:click={() => fileInput?.click()}
      on:keydown={(e) => e.key === 'Enter' && fileInput?.click()}
      role="button"
      tabindex="0"
    >
      <input
        type="file"
        bind:this={fileInput}
        on:change={handleFileSelect}
        multiple
        disabled={isLoading}
        class="hidden"
      />

      <div class="text-text-light">
        <p class="text-lg font-semibold mb-2">Drop files here or click to browse</p>
        <p class="text-sm">Supported: PDF, Images, Documents (Max 10MB per file)</p>
      </div>
    </div>

    {#if selectedFiles.length > 0}
      <div class="space-y-4">
        <h3 class="font-semibold text-text text-base">Selected Files ({selectedFiles.length})</h3>
        <div class="max-h-56 overflow-y-auto space-y-3">
          {#each selectedFiles as file, index (file.name)}
            <div class="flex items-center gap-4 p-4 bg-surface rounded-lg border border-border hover:border-primary transition-colors">
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium text-text truncate">{file.name}</p>
                <p class="text-xs text-text-light">
                  {(file.size / 1024).toFixed(2)} KB
                </p>
              </div>

              {#if uploadProgress[file.name] !== undefined && uploadProgress[file.name] < 100}
                <div class="flex-shrink-0 w-20">
                  <div class="w-full h-2 bg-border rounded-full overflow-hidden">
                    <div
                      class="h-full bg-primary transition-all duration-300"
                      style="width: {uploadProgress[file.name]}%"
                    ></div>
                  </div>
                  <p class="text-xs text-text-light mt-1 text-right">
                    {Math.round(uploadProgress[file.name])}%
                  </p>
                </div>
              {:else if uploadProgress[file.name] === 100}
                <div class="text-xs text-primary font-semibold">Complete</div>
              {/if}

              {#if !isLoading}
                <button
                  type="button"
                  on:click={() => removeFile(index)}
                  class="flex-shrink-0 p-1 text-text-light hover:text-danger rounded transition-colors"
                  title="Remove file"
                >
                  [x]
                </button>
              {/if}
            </div>
          {/each}
        </div>
      </div>
    {/if}

    <div class="flex gap-4 pt-8">
      <Button
        type="submit"
        variant="primary"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading || selectedFiles.length === 0}
      >
        {#if isLoading}
          Uploading...
        {:else}
          Upload Files
        {/if}
      </Button>
      <Button
        type="button"
        variant="secondary"
        size="base"
        fullWidth
        disabled={isLoading}
        on:click={handleClose}
      >
        Cancel
      </Button>
    </div>
  </form>
</Modal>

<style>
  [role='button'] {
    user-select: none;
  }
</style>
