<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import * as pdfjsLib from 'pdfjs-dist';
  import * as fabric from 'fabric';
  import { PDFDocument } from 'pdf-lib';
  import type { PDFDocumentProxy, PDFPageProxy } from 'pdfjs-dist/types/src/display/api';
  import { api } from '$lib/api/client';
  import { files } from '$lib/stores/files';
  import { currentRoom } from '$lib/stores/rooms';
  import { page } from '$app/stores';

  pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
    'pdfjs-dist/build/pdf.worker.min.mjs',
    import.meta.url
  ).toString();

  const modalId = 'pdf-editor';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let fileUrl = $derived.by(() => (modal?.data?.fileUrl ?? null) as string | null);
  let fileName = $derived.by(() => (modal?.data?.fileName ?? 'Document.pdf') as string);

  let pdfCanvasElement: HTMLCanvasElement | null = $state(null);
  let fabricCanvasElement: HTMLCanvasElement | null = $state(null);
  let fabricCanvas: fabric.Canvas | null = $state(null);

  let pdfDocument: PDFDocumentProxy | null = $state(null);
  let sourcePdfBytes: Uint8Array | null = $state(null);
  let currentPage = $state(1);
  let totalPages = $state(0);
  let scale = $state(1.0);
  let isLoading = $state(false);
  let isSaving = $state(false);
  let error = $state<string | null>(null);
  let pageInput = $state('1');

  let selectedTool = $state<'select' | 'pen' | 'text' | 'rectangle' | 'circle'>('select');
  let selectedColor = $state('#E94B3C');
  let brushSize = $state(3);
  let annotationsByPage = new Map<number, string>();
  let renderRequestId = 0;
  let isApplyingAnnotationState = false;

  const colors = [
    { name: 'Red', value: '#E94B3C' },
    { name: 'Blue', value: '#4A90E2' },
    { name: 'Green', value: '#50C878' },
    { name: 'Yellow', value: '#FFD700' },
    { name: 'Black', value: '#000000' },
  ];

  $effect(() => {
    if (isOpen && fileUrl) {
      resetEditorState();
      void loadPdf(fileUrl);
    }
  });

  $effect(() => {
    totalPages = pdfDocument?.numPages ?? 0;
  });

  $effect(() => {
    if (isOpen && fabricCanvasElement && !fabricCanvas) {
      initializeFabricCanvas();
    }
  });

  $effect(() => {
    if (pdfDocument && pdfCanvasElement && fabricCanvas) {
      void renderPage(currentPage);
    }
  });

  function resetEditorState() {
    currentPage = 1;
    pageInput = '1';
    scale = 1.0;
    annotationsByPage.clear();
    error = null;
  }

  function initializeFabricCanvas() {
    if (!fabricCanvasElement) return;

    fabricCanvas = new fabric.Canvas(fabricCanvasElement, {
      isDrawingMode: false,
      selection: true,
      backgroundColor: undefined,
    });

    fabricCanvas.freeDrawingBrush = new fabric.PencilBrush(fabricCanvas);
    fabricCanvas.freeDrawingBrush.color = selectedColor;
    fabricCanvas.freeDrawingBrush.width = brushSize;

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
      default:
        fabricCanvas.isDrawingMode = false;
        fabricCanvas.selection = true;
        break;
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
      width: 120,
      height: 90,
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
    if (activeObjects.length === 0) return;

    activeObjects.forEach((obj) => {
      fabricCanvas?.remove(obj);
    });
    fabricCanvas.discardActiveObject();
    persistCurrentPageAnnotations();
  }

  function clearAnnotations() {
    if (!fabricCanvas) return;
    if (!confirm('Clear all annotations on this page?')) return;

    fabricCanvas.clear();
    persistCurrentPageAnnotations();
  }

  async function loadPdf(url: string) {
    try {
      isLoading = true;
      error = null;

      const response = await fetch(url);
      if (!response.ok) {
        throw new Error('Failed to download PDF');
      }

      const arrayBuffer = await response.arrayBuffer();
      sourcePdfBytes = new Uint8Array(arrayBuffer);

      const loadingTask = pdfjsLib.getDocument({ data: arrayBuffer });
      pdfDocument = await loadingTask.promise;
      currentPage = 1;
      pageInput = '1';
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load PDF';
    } finally {
      isLoading = false;
    }
  }

  function loadFromJsonAsync(canvas: fabric.Canvas, json: string): Promise<void> {
    return new Promise((resolve) => {
      canvas.loadFromJSON(json, () => {
        canvas.renderAll();
        resolve();
      });
    });
  }

  function persistCurrentPageAnnotations() {
    if (!fabricCanvas) return;
    const json = JSON.stringify(fabricCanvas.toJSON());
    annotationsByPage.set(currentPage, json);
  }

  async function applyAnnotationsForCurrentPage() {
    if (!fabricCanvas) return;

    const snapshot = annotationsByPage.get(currentPage);
    isApplyingAnnotationState = true;

    if (!snapshot) {
      fabricCanvas.clear();
      fabricCanvas.renderAll();
      isApplyingAnnotationState = false;
      return;
    }

    await loadFromJsonAsync(fabricCanvas, snapshot);
    isApplyingAnnotationState = false;
  }

  async function renderPage(pageNum: number) {
    if (!pdfDocument || !pdfCanvasElement || !fabricCanvas) return;

    const requestId = ++renderRequestId;

    try {
      isLoading = true;
      error = null;

      const page: PDFPageProxy = await pdfDocument.getPage(pageNum);
      const viewport = page.getViewport({ scale });

      if (requestId !== renderRequestId) return;

      pdfCanvasElement.width = viewport.width;
      pdfCanvasElement.height = viewport.height;

      const context = pdfCanvasElement.getContext('2d');
      if (!context) {
        throw new Error('Failed to get canvas context');
      }

      await page.render({ canvasContext: context, viewport, canvas: pdfCanvasElement }).promise;

      if (requestId !== renderRequestId) return;

      fabricCanvas.setDimensions({
        width: viewport.width,
        height: viewport.height,
      });
      await applyAnnotationsForCurrentPage();
    } catch (err) {
      if (requestId !== renderRequestId) return;
      error = err instanceof Error ? err.message : 'Failed to render page';
    } finally {
      if (requestId === renderRequestId) {
        isLoading = false;
      }
    }
  }

  function previousPage() {
    if (currentPage <= 1) return;
    persistCurrentPageAnnotations();
    currentPage--;
    pageInput = currentPage.toString();
  }

  function nextPage() {
    if (currentPage >= totalPages) return;
    persistCurrentPageAnnotations();
    currentPage++;
    pageInput = currentPage.toString();
  }

  function goToPage() {
    const pageNum = Number.parseInt(pageInput, 10);
    if (Number.isNaN(pageNum) || pageNum < 1 || pageNum > totalPages) {
      pageInput = currentPage.toString();
      return;
    }

    if (pageNum === currentPage) {
      pageInput = currentPage.toString();
      return;
    }

    persistCurrentPageAnnotations();
    currentPage = pageNum;
  }

  function zoomIn() {
    persistCurrentPageAnnotations();
    scale = Math.min(scale + 0.2, 3.0);
  }

  function zoomOut() {
    persistCurrentPageAnnotations();
    scale = Math.max(scale - 0.2, 0.5);
  }

  function resetZoom() {
    persistCurrentPageAnnotations();
    scale = 1.0;
  }

  function downloadPdf() {
    if (!fileUrl) return;

    const link = document.createElement('a');
    link.href = fileUrl;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  async function buildAnnotationPngDataUrl(snapshot: string, width: number, height: number): Promise<string | null> {
    const tempCanvasElement = document.createElement('canvas');
    const tempCanvas = new fabric.StaticCanvas(tempCanvasElement, {
      width,
      height,
      backgroundColor: undefined,
      selection: false,
    });

    await loadFromJsonAsync(tempCanvas as unknown as fabric.Canvas, snapshot);

    if (tempCanvas.getObjects().length === 0) {
      tempCanvas.dispose();
      return null;
    }

    const dataUrl = tempCanvas.toDataURL({ format: 'png', multiplier: 1 });
    tempCanvas.dispose();
    return dataUrl;
  }

  async function savePdf() {
    if (!pdfDocument || !sourcePdfBytes || !fabricCanvas) return;

    if (!$currentRoom) {
      error = 'No room selected';
      return;
    }

    try {
      isSaving = true;
      error = null;
      persistCurrentPageAnnotations();

      const outputPdf = await PDFDocument.load(sourcePdfBytes);

      for (let i = 0; i < outputPdf.getPageCount(); i++) {
        const pageIndex = i + 1;
        const annotationJson = annotationsByPage.get(pageIndex);
        if (!annotationJson) continue;

        const sourcePage = await pdfDocument.getPage(pageIndex);
        const viewport = sourcePage.getViewport({ scale });
        const pageCanvasWidth = viewport.width;
        const pageCanvasHeight = viewport.height;

        const overlayDataUrl = await buildAnnotationPngDataUrl(annotationJson, pageCanvasWidth, pageCanvasHeight);
        if (!overlayDataUrl) continue;

        const embeddedPng = await outputPdf.embedPng(overlayDataUrl);
        const targetPage = outputPdf.getPage(i);
        const { width, height } = targetPage.getSize();

        targetPage.drawImage(embeddedPng, {
          x: 0,
          y: 0,
          width,
          height,
        });
      }

      const finalPdfBytes = await outputPdf.save();
      const baseName = fileName.replace(/\.pdf$/i, '');
      const finalFileName = `${baseName}_edited.pdf`;
      const file = new File([finalPdfBytes as unknown as BlobPart], finalFileName, {
        type: 'application/pdf',
      });

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
    persistCurrentPageAnnotations();
    ui.closeModal(modalId);
    pdfDocument = null;
    sourcePdfBytes = null;
    annotationsByPage.clear();
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

  $effect(() => {
    if (!fabricCanvas) return;

    if (fabricCanvas.freeDrawingBrush) {
      fabricCanvas.freeDrawingBrush.color = selectedColor;
      fabricCanvas.freeDrawingBrush.width = brushSize;
    }
    updateToolMode();
  });

  $effect(() => {
    if (!fabricCanvas || isApplyingAnnotationState) return;
    const save = () => persistCurrentPageAnnotations();

    fabricCanvas.on('object:added', save);
    fabricCanvas.on('object:modified', save);
    fabricCanvas.on('object:removed', save);
    fabricCanvas.on('path:created', save);

    return () => {
      fabricCanvas?.off('object:added', save);
      fabricCanvas?.off('object:modified', save);
      fabricCanvas?.off('object:removed', save);
      fabricCanvas?.off('path:created', save);
    };
  });
</script>

<svelte:window on:keydown={handleKeyDown} />

<Modal {isOpen} title={`${fileName} (Edit Mode)`} onClose={handleClose} size="xlarge" closeButton={!isLoading && !isSaving}>
  <div class="flex flex-col h-full bg-white">
    <div class="border-b border-border p-3 bg-surface flex items-center justify-between gap-3 flex-wrap">
      <div class="flex items-center gap-2">
        <Button variant="secondary" size="small" onclick={previousPage} disabled={isLoading || currentPage <= 1}>
          Prev
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

        <Button variant="secondary" size="small" onclick={nextPage} disabled={isLoading || currentPage >= totalPages}>
          Next
        </Button>
      </div>

      <div class="flex items-center gap-1">
        <Button variant={selectedTool === 'select' ? 'primary' : 'secondary'} size="small" onclick={() => selectTool('select')} disabled={isLoading}>
          Select
        </Button>
        <Button variant={selectedTool === 'pen' ? 'primary' : 'secondary'} size="small" onclick={() => selectTool('pen')} disabled={isLoading}>
          Pen
        </Button>
        <Button variant="secondary" size="small" onclick={addText} disabled={isLoading}>
          Text
        </Button>
        <Button variant="secondary" size="small" onclick={addRectangle} disabled={isLoading}>
          Rect
        </Button>
        <Button variant="secondary" size="small" onclick={addCircle} disabled={isLoading}>
          Circle
        </Button>
      </div>

      <div class="flex items-center gap-2">
        <select
          bind:value={selectedColor}
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
            disabled={isLoading}
          />
          <span class="w-6 text-center">{brushSize}</span>
        </label>
      </div>

      <div class="flex items-center gap-1">
        <Button variant="secondary" size="small" onclick={deleteSelected} disabled={isLoading}>
          Delete
        </Button>
        <Button variant="secondary" size="small" onclick={clearAnnotations} disabled={isLoading}>
          Clear Page
        </Button>
      </div>

      <div class="flex items-center gap-2">
        <Button variant="secondary" size="small" onclick={zoomOut} disabled={isLoading || scale <= 0.5}>
          -
        </Button>

        <span class="text-sm text-text-light w-12 text-center">{Math.round(scale * 100)}%</span>

        <Button variant="secondary" size="small" onclick={zoomIn} disabled={isLoading || scale >= 3.0}>
          +
        </Button>

        <Button variant="secondary" size="small" onclick={resetZoom} disabled={isLoading}>
          Reset
        </Button>
      </div>

      <div class="flex items-center gap-2">
        <Button variant="secondary" size="small" onclick={downloadPdf} disabled={isLoading || !fileUrl}>
          Download
        </Button>
        <Button variant="primary" size="small" onclick={savePdf} disabled={isLoading || isSaving || !fileUrl} loading={isSaving}>
          Save PDF
        </Button>
      </div>
    </div>

    {#if error}
      <div class="p-4 bg-error bg-opacity-10 border-b border-error text-error text-sm">
        {error}
      </div>
    {/if}

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
          <canvas bind:this={pdfCanvasElement} class="absolute top-0 left-0"></canvas>
          <canvas bind:this={fabricCanvasElement} class="absolute top-0 left-0"></canvas>
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
