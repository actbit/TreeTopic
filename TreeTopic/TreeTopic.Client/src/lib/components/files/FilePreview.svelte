<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import type { File as FileDto } from '$lib/types/ui';

  interface Props {
    file?: FileDto | null;
  }

  let { file }: Props = $props();
  let isOpen = $derived(file !== null && $activeModals.some((m) => m.id === 'file-preview'));

  function handleClose() {
    ui.closeModal('file-preview');
  }

  function isPDF(fileType: string): boolean {
    return fileType === 'pdf';
  }

  function isImage(fileType: string): boolean {
    return fileType === 'image';
  }

  function isPDFUrl(url: string): boolean {
    return url.toLowerCase().endsWith('.pdf');
  }
</script>

{#if file}
  <Modal {isOpen} title={file.fileName} onClose={handleClose} size="large">
    <div class="w-full h-96 bg-surface rounded flex items-center justify-center">
      {#if isImage(file.fileType)}
        <img
          src={file.url}
          alt={file.fileName}
          class="max-w-full max-h-full object-contain rounded"
          loading="lazy"
        />
      {:else if isPDF(file.fileType) || isPDFUrl(file.url)}
        <div class="text-center text-text-light">
          <p class="text-lg mb-4 font-semibold">PDF Document</p>
          <p class="text-sm mb-4">{file.fileName}</p>
          <a
            href={file.url}
            target="_blank"
            rel="noreferrer"
            class="inline-block px-4 py-2 bg-primary text-white rounded hover:bg-primary-hover transition-colors"
          >
            Open in New Tab
          </a>
        </div>
      {:else}
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">{file.fileName}</p>
          <p class="text-sm mb-4">File type: {file.fileType}</p>
          <a
            href={file.url}
            download
            class="inline-block px-4 py-2 bg-primary text-white rounded hover:bg-primary-hover transition-colors"
          >
            Download
          </a>
        </div>
      {/if}
    </div>

    <div class="mt-4 p-4 bg-surface rounded border border-border">
      <div class="grid grid-cols-2 gap-4 text-sm">
        <div>
          <p class="text-text-light">File Type</p>
          <p class="font-semibold text-text">{file.fileType.toUpperCase()}</p>
        </div>
        <div>
          <p class="text-text-light">Size</p>
          <p class="font-semibold text-text">{(file.size / 1024).toFixed(2)} KB</p>
        </div>
        <div>
          <p class="text-text-light">Uploaded By</p>
          <p class="font-semibold text-text">{file.uploadedByName}</p>
        </div>
        <div>
          <p class="text-text-light">Uploaded At</p>
          <p class="font-semibold text-text">
            {new Date(file.uploadedAt).toLocaleDateString()}
          </p>
        </div>
      </div>

      {#if file.description}
        <div class="mt-4 pt-4 border-t border-border">
          <p class="text-text-light text-sm">Description</p>
          <p class="text-text">{file.description}</p>
        </div>
      {/if}
    </div>
  </Modal>
{/if}
