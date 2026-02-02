<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import * as pdfjsLib from 'pdfjs-dist';
  import * as fabric from 'fabric';
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

  const modalId = 'pdf-editor';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let fileUrl = $derived.by(() => modal?.data?.fileUrl ?? null);
  let fileName = $derived.by(() => modal?.data?.fileName ?? 'Document');

  // PDF.js canvas (for rendering PDF)
  let pdfCanvasElement: HTMLCanvasElement | null = $state(null);
  // Fabric.js canvas (for annotations)
  let fabricCanvasElement: HTMLCanvasElement | null = $state(null);
  let fabricCanvas: fabric.Canvas | null = $state(null);
  
  let pdfDocument: PDFDocumentProxy | null = $state(null);
  let currentPage = $state(1);
  let totalPages = $state(0);
  let scale = $state(1.0);
  let isLoading = $state(false);
  let isSaving = $state(false);
  let error = $state<string | null>(null);
  let pageInput = $state('1');

  // Annotation tools
  let selectedTool = $state<'select' | 'pen' | 'text' | 'rectangle' | 'circle'>('select');
  let selectedColor = $state('#E94B3C');
  let brushSize = $state(3);
  
  const colors = [
    { name: 'Red', value: '#E94B3C' },
    { name: 'Blue', value: '#4A90E2' },
    { name: 'Green', value: '#50C878' },
    { name: 'Yellow', value: '#FFD700' },
    { name: 'Black', value: '#000000' },
  ];

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
    if (pdfDocument && pdfCanvasElement && fabricCanvas) {
      renderPage(currentPage);
    }
  });

  $effect(() => {
    if (isOpen && fabricCanvasElement && !fabricCanvas) {
      initializeFabricCanvas();
    }
  });

  function initializeFabricCanvas() {
    if (!fabricCanvasElement) return;

    fabricCanvas = new fabric.Canvas(fabricCanvasElement, {
      isDrawingMode: false,
      selection: true,
    });

    // Configure brush
    fabricCanvas.freeDrawingBrush = new fabric.PencilBrush(fabricCanvas);
    fabricCanvas.freeDrawingBrush.color = selectedColor;
    fabricCanvas.freeDrawingBrush.width = brushSize;

    // Update drawing mode based on selected tool
    updateToolMode();
  }

  function updateToolMode() {
    if (!fabricCanvas) return;

    switch (selectedTool) {
      case 'pen':
        fabricCanvas.isDrawingMode = true;
        fabricCanvas.selection = false;
        if (fabricCanvas.freeDrawingBrush) {
          fabricCanvas.freeDrawingBrush.color = selectedColor;
          fabricCanvas.freeDrawingBrush.width = brushSize;
        }
        break;
      case 'select':
        fabricCanvas.isDrawingMode = false;
        fabricCanvas.selection = true;
        break;
      default:
        fabricCanvas.isDrawingMode = false;
        fabricCanvas.selection = true;
    }
  }

  function selectTool(tool: 'select' | 'pen' | 'text' | 'rectangle' | 'circle') {
    selectedTool = tool;
    updateToolMode();
  }

  function addText() {
    if (!fabricCanvas) return;

    const text = new fabric.IText('Enter text', {
      left: 100,
      top: 100,
      fill: selectedColor,
      fontSize: 20,
    });

    fabricCanvas.add(text);
    fabricCanvas.setActiveObject(text);
    text.enterEditing();
    selectTool('select');
  }

  function addRectangle() {
    if (!fabricCanvas) return;

    const rect = new fabric.Rect({
      left: 100,
      top: 100,
      width: 100,
      height: 100,
      fill: 'transparent',
      stroke: selectedColor,
      strokeWidth: brushSize,
    });

    fabricCanvas.add(rect);
    fabricCanvas.setActiveObject(rect);
    selectTool('select');
  }

  function addCircle() {
    if (!fabricCanvas) return;

    const circle = new fabric.Circle({
      left: 100,
      top: 100,
      radius: 50,
      fill: 'transparent',
      stroke: selectedColor,
      strokeWidth: brushSize,
    });

    fabricCanvas.add(circle);
    fabricCanvas.setActiveObject(circle);
    selectTool('select');
  }

  function deleteSelected() {
    if (!fabricCanvas) return;

    const activeObjects = fabricCanvas.getActiveObjects();
    if (activeObjects.length > 0) {
      activeObjects.forEach((obj) => {
        fabricCanvas?.remove(obj);
      });
      fabricCanvas.discardActiveObject();
    }
  }

  function clearAnnotations() {
    if (!fabricCanvas) return;
    
    if (confirm('Clear all annotations?')) {
      fabricCanvas.clear();
    }
  }

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
    if (!pdfDocument || !pdfCanvasElement || !fabricCanvas) return;

    try {
      isLoading = true;
      const page: PDFPageProxy = await pdfDocument.getPage(pageNum);

      const viewport = page.getViewport({ scale });
      
      // Set PDF canvas size
      pdfCanvasElement.width = viewport.width;
      pdfCanvasElement.height = viewport.height;

      const context = pdfCanvasElement.getContext('2d');
      if (!context) {
        throw new Error('Failed to get canvas context');
      }

      // Render PDF page
      const renderParams = {
        canvasContext: context,
        viewport,
        canvas: pdfCanvasElement,
      };
      await page.render(renderParams).promise;

      // Set fabric canvas size to match (fabric v7 uses properties instead of methods)
      fabricCanvas.setDimensions({
        width: viewport.width,
        height: viewport.height
      });

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
    if (!fileUrl || !fabricCanvas || !pdfCanvasElement) return;

    if (!$currentRoom) {
      error = 'No room selected';
      return;
    }

    try {
      isSaving = true;
      error = null;

      // Create a temporary canvas to merge PDF and annotations
      const mergeCanvas = document.createElement('canvas');
      const mergeContext = mergeCanvas.getContext('2d');
      if (!mergeContext) {
        throw new Error('Failed to create merge canvas');
      }

      // Set canvas size to match PDF
      mergeCanvas.width = pdfCanvasElement.width;
      mergeCanvas.height = pdfCanvasElement.height;

      // Draw PDF background
      mergeContext.drawImage(pdfCanvasElement, 0, 0);

      // Draw annotations (fabric canvas)
      const fabricDataUrl = fabricCanvas.toDataURL({
        format: 'png',
        multiplier: 1,
      });
      
      await new Promise<void>((resolve, reject) => {
        const img = new Image();
        img.onload = () => {
          mergeContext.drawImage(img, 0, 0);
          resolve();
        };
        img.onerror = reject;
        img.src = fabricDataUrl;
      });

      // Convert to blob
      const blob = await new Promise<Blob>((resolve) => {
        mergeCanvas.toBlob((b) => {
          resolve(b ?? new Blob());
        }, 'image/png');
      });

      // Create file - save as PNG image
      const editedFileName = `edited_${fileName.replace(/\.pdf$/i, '')}.png`;
      const file = new File([blob], editedFileName, { type: 'image/png' });

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
          mimeType: uploadResponse.mimeType ?? uploadResponse.MimeType ?? 'image/png',
          size: uploadResponse.size ?? uploadResponse.Size ?? file.size,
          url: uploadResponse.url ?? uploadResponse.Url ?? '',
          fileType: uploadResponse.fileType ?? uploadResponse.FileType ?? 'image',
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

      ui.closeModal(modalId);
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to save PDF';
    } finally {
      isSaving = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
    pdfDocument = null;
    if (fabricCanvas) {
      fabricCanvas.dispose();
      fabricCanvas = null;
    }
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

<Modal {isOpen} title={`${fileName} (Edit Mode)`} onClose={handleClose} size="xlarge" closeButton={!isLoading && !isSaving}>
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
          ← Prev
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

      <!-- Annotation Tools -->
      <div class="flex items-center gap-1">
        <Button
          variant={selectedTool === 'select' ? 'primary' : 'secondary'}
          size="small"
          onclick={() => selectTool('select')}
          disabled={isLoading}
        >
          🖱️ Select
        </Button>
        <Button
          variant={selectedTool === 'pen' ? 'primary' : 'secondary'}
          size="small"
          onclick={() => selectTool('pen')}
          disabled={isLoading}
        >
          ✏️ Pen
        </Button>
        <Button
          variant="secondary"
          size="small"
          onclick={addText}
          disabled={isLoading}
        >
          📝 Text
        </Button>
        <Button
          variant="secondary"
          size="small"
          onclick={addRectangle}
          disabled={isLoading}
        >
          ⬜ Rect
        </Button>
        <Button
          variant="secondary"
          size="small"
          onclick={addCircle}
          disabled={isLoading}
        >
          ⭕ Circle
        </Button>
      </div>

      <!-- Color & Size -->
      <div class="flex items-center gap-2">
        <select 
          bind:value={selectedColor} 
          onchange={() => {
            if (fabricCanvas?.freeDrawingBrush) {
              fabricCanvas.freeDrawingBrush.color = selectedColor;
            }
          }}
          class="px-2 py-1 border border-border rounded text-sm"
          disabled={isLoading}
        >
          {#each colors as color}
            <option value={color.value}>{color.name}</option>
          {/each}
        </select>
        
        <label class="flex items-center gap-1 text-sm">
          Size:
          <input 
            type="range" 
            bind:value={brushSize} 
            min="1" 
            max="20" 
            class="w-16"
            onchange={() => {
              if (fabricCanvas?.freeDrawingBrush) {
                fabricCanvas.freeDrawingBrush.width = brushSize;
              }
            }}
            disabled={isLoading}
          />
          <span class="w-6 text-center">{brushSize}</span>
        </label>
      </div>

      <!-- Edit Actions -->
      <div class="flex items-center gap-1">
        <Button
          variant="secondary"
          size="small"
          onclick={deleteSelected}
          disabled={isLoading}
        >
          ❌ Delete
        </Button>
        <Button
          variant="secondary"
          size="small"
          onclick={clearAnnotations}
          disabled={isLoading}
        >
          🗑️ Clear All
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
      </div>

      <!-- Action buttons -->
      <div class="flex items-center gap-2">
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
          💾 Save
        </Button>
      </div>
    </div>

    <!-- Error message -->
    {#if error}
      <div class="p-4 bg-error bg-opacity-10 border-b border-error text-error text-sm">
        {error}
      </div>
    {/if}

    <!-- Canvas area -->
    <div class="flex-1 overflow-auto flex items-center justify-center bg-gray-100 p-4">
      {#if isLoading}
        <div class="text-center">
          <div class="inline-block">
            <div class="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          </div>
          <p class="mt-2 text-sm text-text-light">Loading page...</p>
        </div>
      {:else if pdfDocument}
        <div class="relative shadow-lg" style="width: {pdfCanvasElement?.width}px; height: {pdfCanvasElement?.height}px;">
          <!-- PDF Canvas (bottom layer) -->
          <canvas
            bind:this={pdfCanvasElement}
            class="absolute top-0 left-0"
          ></canvas>
          <!-- Fabric Canvas (top layer for annotations) -->
          <canvas
            bind:this={fabricCanvasElement}
            class="absolute top-0 left-0"
          ></canvas>
        </div>
      {:else}
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">No PDF loaded</p>
          <p class="text-sm">Upload a PDF file to edit it here</p>
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
