<script lang="ts">
  import * as fabric from 'fabric';
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { files } from '$lib/stores/files';
  import { currentRoom } from '$lib/stores/rooms';
  import { api } from '$lib/api/client';
  import { page } from '$app/stores';

  const modalId = 'image-editor';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let fileUrl = $derived.by(() => modal?.data?.fileUrl ?? null);
  let fileName = $derived.by(() => modal?.data?.fileName ?? 'Image');

  let canvasElement: HTMLCanvasElement | null = $state(null);
  let canvas: fabric.Canvas | null = $state(null);
  let selectedColor = $state('#4A90E2'); // Blue
  let markerSize = $state(50);
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let history = $state<any[]>([]);
  let historyIndex = $state(-1);

  const colors = [
    { name: 'Blue', value: '#4A90E2' },
    { name: 'Red', value: '#E94B3C' },
    { name: 'Green', value: '#50C878' },
    { name: 'Yellow', value: '#FFD700' },
    { name: 'Purple', value: '#9B59B6' },
  ];
  const imageLoadOptions = { crossOrigin: 'anonymous' as const };

  function resetHistory() {
    history = [];
    historyIndex = -1;
  }

  function getPointerPosition(event: MouseEvent) {
    const rect = canvasElement?.getBoundingClientRect();
    if (!rect) return null;
    return {
      x: event.clientX - rect.left,
      y: event.clientY - rect.top,
    };
  }

  $effect(() => {
    if (isOpen && canvasElement && !canvas) {
      initializeCanvas();
    }
  });

  async function initializeCanvas() {
    if (!canvasElement) return;

    try {
      isLoading = true;
      error = null;

      canvas = new fabric.Canvas(canvasElement, {
        width: 800,
        height: 600,
        backgroundColor: '#ffffff',
      });

      resetHistory();
      await loadBackgroundImage();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to initialize canvas';
    } finally {
      isLoading = false;
    }
  }

  async function loadBackgroundImage() {
    if (!canvas) return;

    if (!fileUrl) {
      canvas.renderAll();
      saveState();
      return;
    }

    try {
      const img = await fabric.Image.fromURL(fileUrl, imageLoadOptions);
      const canvasWidth = canvas.getWidth();
      const canvasHeight = canvas.getHeight();
      const width = img.width ?? img.getScaledWidth() ?? canvasWidth;
      const height = img.height ?? img.getScaledHeight() ?? canvasHeight;
      const safeWidth = Math.max(1, width);
      const safeHeight = Math.max(1, height);
      const scale = Math.min(canvasWidth / safeWidth, canvasHeight / safeHeight);
      img.scale(scale);
      const scaledWidth = img.getScaledWidth();
      const scaledHeight = img.getScaledHeight();
      img.set({
        left: (canvasWidth - scaledWidth) / 2,
        top: (canvasHeight - scaledHeight) / 2,
        selectable: false,
        evented: false,
      });
      canvas.sendObjectToBack(img);
      canvas.renderAll();
      saveState();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load image';
    }
  }

  function addMarker(e: MouseEvent) {
    if (!canvas) return;

    const pointer = getPointerPosition(e);
    if (!pointer) return;

    const circle = new fabric.Circle({
      left: pointer.x,
      top: pointer.y,
      radius: markerSize / 2,
      fill: 'transparent',
      stroke: selectedColor,
      strokeWidth: 3,
      selectable: true,
      evented: true,
    });

    canvas.add(circle);
    canvas.setActiveObject(circle);
    canvas.renderAll();
  }

  function deleteSelected() {
    if (!canvas) return;

    const activeObj = canvas.getActiveObject();
    if (activeObj) {
      canvas.remove(activeObj);
      canvas.renderAll();
      saveState();
    }
  }

  function saveState() {
    if (!canvas) return;

    historyIndex++;
    history = history.slice(0, historyIndex);
    history.push(canvas.toJSON());
  }

  function undo() {
    if (historyIndex > 0 && canvas) {
      historyIndex--;
      const currentCanvas = canvas;
      const snapshot = history[historyIndex];
      currentCanvas.loadFromJSON(snapshot, () => {
        currentCanvas.renderAll();
      });
    }
  }

  function redo() {
    if (historyIndex < history.length - 1 && canvas) {
      historyIndex++;
      const currentCanvas = canvas;
      const snapshot = history[historyIndex];
      currentCanvas.loadFromJSON(snapshot, () => {
        currentCanvas.renderAll();
      });
    }
  }

  async function clearAll() {
    if (!canvas) return;

    try {
      isLoading = true;
      error = null;
      canvas.clear();
      canvas.backgroundColor = '#ffffff';
      resetHistory();
      await loadBackgroundImage();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to clear canvas';
    } finally {
      isLoading = false;
    }
  }

  async function saveImage() {
    if (!canvas) return;

    try {
      isLoading = true;
      error = null;

      // Convert canvas to blob
      const dataUrl = canvas.toDataURL({
        format: 'png',
        quality: 1,
        multiplier: 1,
      });

      // Convert data URL to blob
      const response = await fetch(dataUrl);
      const blob = await response.blob();

      // Create file
      const file = new File([blob], `edited_${fileName}`, { type: 'image/png' });

      // Upload
      const tenant = api.getCurrentTenant() || $page.params.tenant || 'default';
      const roomId = $currentRoom?.id;

      if (!roomId) {
        error = 'No room selected';
        return;
      }

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
          fileName: uploadResponse.fileName ?? uploadResponse.FileName ?? '',
          originalFileName:
            uploadResponse.originalFileName ??
            uploadResponse.OriginalFileName ??
            uploadResponse.fileName ??
            uploadResponse.FileName ??
            file.name,
          mimeType: uploadResponse.mimeType ?? uploadResponse.MimeType ?? 'image/png',
          size: uploadResponse.size ?? uploadResponse.Size ?? 0,
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
      error = err instanceof Error ? err.message : 'Failed to save image';
    } finally {
      isLoading = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
    if (canvas) {
      canvas.dispose();
      canvas = null;
    }
    history = [];
    historyIndex = -1;
  }

  function handleCanvasClick(e: MouseEvent) {
    // Only add marker if clicking on empty area
    if (!canvas) return;
    const pointer = getPointerPosition(e);
    if (!pointer) return;

    const objects = canvas.getObjects();
    const pointerPoint = new fabric.Point(pointer.x, pointer.y);

    const clickedObject = objects.find((obj: fabric.Object) => {
      if (obj.containsPoint) {
        if (obj.containsPoint(pointerPoint)) {
          return true;
        }
      }

      if (!obj.aCoords) {
        return false;
      }

      return (
        pointer.x >= obj.aCoords.tl.x &&
        pointer.x <= obj.aCoords.br.x &&
        pointer.y >= obj.aCoords.tl.y &&
        pointer.y <= obj.aCoords.br.y
      );
    });

    if (!clickedObject) {
      addMarker(e);
      saveState();
    }
  }
</script>

<Modal {isOpen} title={fileName} onClose={handleClose} size="xlarge" closeButton={!isLoading}>
  <div class="flex flex-col h-full bg-white">
    <!-- Toolbar -->
    <div class="border-b border-border p-4 bg-surface space-y-3">
      <!-- Color palette -->
      <div class="flex items-center gap-3">
        <span class="text-sm font-medium text-text">Color:</span>
        <div class="flex gap-2">
          {#each colors as color}
            <button
              onclick={() => (selectedColor = color.value)}
              class="w-8 h-8 rounded-full border-2 transition-all {selectedColor === color.value
                ? 'border-text shadow-md scale-110'
                : 'border-border hover:border-text'}"
              style="background-color: {color.value};"
              title={color.name}
            ></button>
          {/each}
        </div>
      </div>

      <!-- Size slider -->
      <div class="flex items-center gap-3">
        <span class="text-sm font-medium text-text">Size:</span>
        <input
          type="range"
          min="1"
          max="300"
          bind:value={markerSize}
          class="flex-1"
          disabled={isLoading}
        />
        <span class="text-sm text-text-light w-12 text-right">{markerSize}px</span>
      </div>

      <!-- Action buttons -->
      <div class="flex items-center gap-2">
        <Button
          variant="secondary"
          size="small"
          onclick={undo}
          disabled={isLoading || historyIndex <= 0}
          title="Undo"
        >
          ↶ Undo
        </Button>
        <Button
          variant="secondary"
          size="small"
          onclick={redo}
          disabled={isLoading || historyIndex >= history.length - 1}
          title="Redo"
        >
          ↷ Redo
        </Button>
        <Button
          variant="secondary"
          size="small"
          onclick={deleteSelected}
          disabled={isLoading}
          title="Delete selected circle"
        >
          🗑 Delete
        </Button>
        <Button
          variant="secondary"
          size="small"
          onclick={clearAll}
          disabled={isLoading}
          title="Clear all marks"
        >
          Clear All
        </Button>
      </div>
    </div>

    <!-- Error message -->
    {#if error}
      <div class="p-3 bg-error bg-opacity-10 border-b border-error text-error text-sm">
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
          <p class="mt-2 text-sm text-text-light">Loading image...</p>
        </div>
      {:else}
        <canvas
          bind:this={canvasElement}
          onmousedown={handleCanvasClick}
          class="border-2 border-border rounded cursor-crosshair shadow-lg"
        ></canvas>
      {/if}
    </div>

    <!-- Footer buttons -->
    <div class="border-t border-border p-4 bg-surface flex items-center gap-2 justify-end">
      <Button
        variant="secondary"
        size="base"
        onclick={handleClose}
        disabled={isLoading}
      >
        Cancel
      </Button>
      <Button
        variant="primary"
        size="base"
        onclick={saveImage}
        loading={isLoading}
        disabled={isLoading}
      >
        Save Image
      </Button>
    </div>
  </div>
</Modal>

<style>
  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }

  :global(.animate-spin) {
    animation: spin 1s linear infinite;
  }
</style>
