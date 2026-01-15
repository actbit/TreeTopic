<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import * as pdfjsLib from 'pdfjs-dist';
  import type { PDFDocumentProxy, PDFPageProxy } from 'pdfjs-dist/types/src/display/api';
  import { api } from '$lib/api/client';
  import { files } from '$lib/stores/files';
  import { currentRoom } from '$lib/stores/rooms';
  import { page } from '$app/stores';

  // Initialize PDF.js worker
  pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
    'pdfjs-dist/build/pdf.worker.min.mjs',
    import.meta.url
  ).toString();

  const modalId = 'pdf-viewer';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let fileUrl = $derived.by(() => modal?.data?.fileUrl ?? null);
  let fileName = $derived.by(() => modal?.data?.fileName ?? 'Document');

  let canvasElement: HTMLCanvasElement | null = $state(null);
  let pdfDocument: PDFDocumentProxy | null = $state(null);
  let currentPage = $state(1);
  let totalPages = $state(0);
  let scale = $state(1.0);
  let isLoading = $state(false);
  let isSaving = $state(false);
  let error = $state<string | null>(null);
  let pageInput = $state('1');

  $effect(() => {
    if (isOpen && fileUrl) {
      loadPdf(fileUrl);
      pageInput = '1';
      scale = 1.0;
    }
  });

  $effect(() => {
    totalPages = pdfDocument?.numPages ?? 0;
  });

  $effect(() => {
    if (pdfDocument && canvasElement) {
      renderPage(currentPage);
    }
  });

  async function loadPdf(url: string) {
    try {
      isLoading = true;
      error = null;
      const pdf = await pdfjsLib.getDocument(url).promise;
      pdfDocument = pdf;
      currentPage = 1;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load PDF';
    } finally {
      isLoading = false;
    }
  }

  async function renderPage(pageNum: number) {
    if (!pdfDocument || !canvasElement) return;

    try {
      isLoading = true;
      const page: PDFPageProxy = await pdfDocument.getPage(pageNum);

      const viewport = page.getViewport({ scale });
      const canvas = canvasElement;
      canvas.width = viewport.width;
      canvas.height = viewport.height;

      const context = canvas.getContext('2d');
      if (!context) {
        throw new Error('Failed to get canvas context');
      }

      const renderParams = {
        canvasContext: context,
        viewport,
        canvas,
      };
      await page.render(renderParams).promise;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to render page';
    } finally {
      isLoading = false;
    }
  }

  function previousPage() {
    if (currentPage > 1) {
      currentPage--;
      pageInput = currentPage.toString();
    }
  }

  function nextPage() {
    if (currentPage < totalPages) {
      currentPage++;
      pageInput = currentPage.toString();
    }
  }

  function goToPage() {
    const pageNum = parseInt(pageInput, 10);
    if (pageNum >= 1 && pageNum <= totalPages) {
      currentPage = pageNum;
    } else {
      pageInput = currentPage.toString();
    }
  }

  function zoomIn() {
    scale = Math.min(scale + 0.2, 3.0);
  }

  function zoomOut() {
    scale = Math.max(scale - 0.2, 0.5);
  }

  function resetZoom() {
    scale = 1.0;
  }

  function downloadPdf() {
    if (fileUrl) {
      const link = document.createElement('a');
      link.href = fileUrl;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    }
  }

  async function savePdf() {
    if (!fileUrl) return;

    if (!$currentRoom) {
      error = 'No room selected';
      return;
    }

    try {
      isSaving = true;
      error = null;

      const response = await fetch(fileUrl);
      if (!response.ok) {
        throw new Error('Failed to download PDF for saving');
      }

      const blob = await response.blob();
      const file = new File([blob], `saved_${fileName}`, { type: 'application/pdf' });

      const tenant = api.getCurrentTenant() || $page.params.tenant || 'default';
      const roomId = $currentRoom.id;

      const formData = new FormData();
      formData.append('roomId', roomId);
      formData.append('file', file);

      const uploadResponse = await api.post<any>(
        `/${tenant}/api/File/room/${roomId}`,
        formData
      );

      if (uploadResponse) {
        files.addFile({
          id: uploadResponse.id ?? uploadResponse.Id ?? '',
          roomId: uploadResponse.roomId ?? uploadResponse.RoomId ?? roomId,
          messageId: uploadResponse.messageId ?? uploadResponse.MessageId ?? undefined,
          fileName: uploadResponse.fileName ?? uploadResponse.FileName ?? file.name,
          originalFileName:
            uploadResponse.originalFileName ??
            uploadResponse.OriginalFileName ??
            uploadResponse.fileName ??
            uploadResponse.FileName ??
            file.name,
          mimeType: uploadResponse.mimeType ?? uploadResponse.MimeType ?? 'application/pdf',
          size: uploadResponse.size ?? uploadResponse.Size ?? file.size,
          url: uploadResponse.url ?? uploadResponse.Url ?? '',
          fileType: uploadResponse.fileType ?? uploadResponse.FileType ?? 'pdf',
          uploadedAt:
            uploadResponse.uploadedAt ??
            uploadResponse.UploadedAt ??
            new Date().toISOString(),
          uploadedBy: uploadResponse.uploadedBy ?? uploadResponse.UploadedBy ?? '',
          uploadedByName:
            uploadResponse.uploadedByName ?? uploadResponse.UploadedByName ?? '',
          versions: [],
          isArchived: uploadResponse.isArchived ?? uploadResponse.IsArchived ?? false,
          tags: uploadResponse.tags ?? uploadResponse.Tags ?? [],
          description: uploadResponse.description ?? uploadResponse.Description ?? '',
        });
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to save PDF';
    } finally {
      isSaving = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
    pdfDocument = null;
  }

  function handleKeyDown(e: KeyboardEvent) {
    if (!isOpen) return;

    switch (e.key) {
      case 'ArrowLeft':
        previousPage();
        break;
      case 'ArrowRight':
        nextPage();
        break;
      case '+':
      case '=':
        zoomIn();
        break;
      case '-':
        zoomOut();
        break;
    }
  }
</script>

<svelte:window on:keydown={handleKeyDown} />

<Modal {isOpen} title={fileName} onClose={handleClose} size="xlarge" closeButton={!isLoading}>
  <div class="flex flex-col h-full bg-white">
    <!-- Toolbar -->
    <div class="border-b border-border p-3 bg-surface flex items-center justify-between gap-3 flex-wrap">
      <!-- Navigation controls -->
      <div class="flex items-center gap-2">
        <Button
          variant="secondary"
          size="small"
          onclick={previousPage}
          disabled={isLoading || currentPage <= 1}
        >
          ← Previous
        </Button>

        <div class="flex items-center gap-1">
          <input
            type="number"
            bind:value={pageInput}
            onchange={goToPage}
            min="1"
            max={totalPages}
            disabled={isLoading || totalPages === 0}
            class="w-12 px-2 py-1 border border-border rounded text-center text-sm"
          />
          <span class="text-sm text-text-light">/ {totalPages}</span>
        </div>

        <Button
          variant="secondary"
          size="small"
          onclick={nextPage}
          disabled={isLoading || currentPage >= totalPages}
        >
          Next →
        </Button>
      </div>

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

      <!-- Download button -->
      <Button
        variant="secondary"
        size="small"
        onclick={downloadPdf}
        disabled={isLoading || !fileUrl}
      >
        Download
      </Button>
      <Button
        variant="primary"
        size="small"
        onclick={savePdf}
        disabled={isLoading || isSaving || !fileUrl}
        loading={isSaving}
      >
        Save
      </Button>
    </div>

    <!-- Error message -->
    {#if error}
      <div class="p-4 bg-error bg-opacity-10 border-b border-error text-error text-sm">
        {error}
      </div>
    {/if}

    <!-- Canvas area -->
    <div class="flex-1 overflow-auto flex items-center justify-center bg-gray-100">
      {#if isLoading}
        <div class="text-center">
          <div class="inline-block">
            <div class="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          </div>
          <p class="mt-2 text-sm text-text-light">Loading page...</p>
        </div>
      {:else if pdfDocument}
        <canvas
          bind:this={canvasElement}
          class="max-w-full max-h-full shadow-lg"
        ></canvas>
      {:else}
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">No PDF loaded</p>
          <p class="text-sm">Upload a PDF file to view it here</p>
        </div>
      {/if}
    </div>
  </div>
</Modal>

<style>
  input[type='number']::-webkit-outer-spin-button,
  input[type='number']::-webkit-inner-spin-button {
    -webkit-appearance: none;
    margin: 0;
  }

  input[type='number'] {
    appearance: textfield;
    -moz-appearance: textfield;
  }

  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }

  :global(.animate-spin) {
    animation: spin 1s linear infinite;
  }
</style>
