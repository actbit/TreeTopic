<script lang="ts">
  import { fileList, deleteFile as deleteFileApi, type Material } from '$lib/stores/files';
  import { currentRoom } from '$lib/stores/rooms';
  import { formatFileSize } from '$lib/utils/validation';
  import { formatDate } from '$lib/utils/date';
  import { ui } from '$lib/stores/ui';

  interface Props {
    compact?: boolean;
  }

  let { compact = false }: Props = $props();

  let deletingFiles = new Set<string>();

  async function handleDeleteFile(fileId: string, fileName: string) {
    if (!confirm(`Are you sure you want to delete "${fileName}"?`)) {
      return;
    }

    const tenant = api.getCurrentTenant();
    if (!tenant) {
      alert('Unable to determine tenant');
      return;
    }

    deletingFiles.add(fileId);

    try {
      await deleteFileApi(fileId, tenant);
    } catch (error) {
      console.error('Failed to delete file:', error);
      alert('Failed to delete file. Please try again.');
    } finally {
      deletingFiles.delete(fileId);
    }
  }

  let filteredFiles = $derived.by(() => {
    if (!$currentRoom) return [];
    return $fileList.filter((f) => f.roomId === $currentRoom?.id);
  });

  let groupedByType = $derived.by(() => {
    const groups: Record<string, typeof $fileList> = {
      documents: [],
      images: [],
      other: [],
    };

    filteredFiles.forEach((file) => {
      if (file.fileType === 'pdf' || file.fileType === 'document') {
        groups.documents.push(file);
      } else if (file.fileType === 'image') {
        groups.images.push(file);
      } else {
        groups.other.push(file);
      }
    });

    return groups;
  });

  function openUploadModal() {
    ui.openModal({
      id: 'file-upload',
      title: 'Upload Files',
      type: 'custom',
    });
  }

  function openPdfViewer(file: Material) {
    ui.openModal({
      id: 'pdf-viewer',
      title: file.fileName,
      type: 'custom',
      data: { fileUrl: file.url, fileName: file.fileName },
    });
  }

  function openPdfEditor(file: Material) {
    ui.openModal({
      id: 'pdf-editor',
      title: `Edit ${file.fileName}`,
      type: 'custom',
      data: { fileUrl: file.url, fileName: file.fileName, fileId: file.id },
    });
  }

  function openImageEditor(file: Material) {
    ui.openModal({
      id: 'image-editor',
      title: `Edit ${file.fileName}`,
      type: 'custom',
      data: { fileUrl: file.url, fileName: file.fileName, fileId: file.id },
    });
  }

  function getTypeLabel(fileType: string): string {
    switch (fileType) {
      case 'pdf':
        return 'PDF';
      case 'document':
        return 'Document';
      case 'image':
        return 'Image';
      case 'brainstorm':
        return 'Brainstorm';
      default:
        return 'File';
    }
  }
</script>

<div class="space-y-4">
  {#if filteredFiles.length === 0}
    <div class="text-center py-8 text-text-light">
      <p class="text-lg mb-3">No materials yet</p>
      <button
        onclick={openUploadModal}
        class="text-sm text-primary hover:text-primary-hover transition-colors"
      >
        Upload your first file
      </button>
    </div>
  {:else}
    <div class="flex justify-between items-center mb-4">
      <h3 class="font-semibold text-text">{filteredFiles.length} Materials</h3>
      <button
        onclick={openUploadModal}
        class="px-3 py-1 text-sm bg-primary text-white rounded hover:bg-primary-hover transition-colors"
      >
        + Upload
      </button>
    </div>

    {#if groupedByType.documents.length > 0}
      <div class="space-y-3">
        <h4 class="text-sm font-semibold text-text-light uppercase tracking-wide">Documents</h4>
        <div class="space-y-2">
          {#each groupedByType.documents as file (file.id)}
            <div
              class="flex items-center gap-4 p-4 bg-surface rounded hover:bg-white border border-border transition-colors group"
            >
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium text-text truncate">{file.fileName}</p>
                <p class="text-xs text-text-light mt-1">
                  {getTypeLabel(file.fileType)} • {formatFileSize(file.size)} • {formatDate(file.uploadedAt)}
                </p>
              </div>
              <div class="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                {#if file.fileType === 'pdf'}
                  <button
                    type="button"
                    onclick={() => openPdfViewer(file)}
                    class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
                    title="Open PDF"
                  >
                    Open PDF
                  </button>
                  <button
                    type="button"
                    onclick={() => openPdfEditor(file)}
                    class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
                    title="Edit PDF"
                  >
                    Edit PDF
                  </button>
                {:else}
                  <a
                    href={file.url}
                    target="_blank"
                    rel="noreferrer"
                    class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
                    title="View"
                  >
                    View
                  </a>
                {/if}
                <a
                  href={file.url}
                  download={file.fileName}
                  class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
                  title="Download"
                >
                  Download
                </a>
                <button
                  type="button"
                  onclick={() => handleDeleteFile(file.id, file.fileName)}
                  disabled={deletingFiles.has(file.id)}
                  class="px-3 py-2 text-sm text-danger hover:text-danger-hover rounded hover:bg-danger-light transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                  title="Delete"
                >
                  {deletingFiles.has(file.id) ? 'Deleting...' : 'Delete'}
                </button>
              </div>
            </div>
          {/each}
        </div>
      </div>
    {/if}

    {#if groupedByType.images.length > 0}
      <div class="space-y-3">
        <h4 class="text-sm font-semibold text-text-light uppercase tracking-wide">Images</h4>
        {#if !compact}
          <div class="grid grid-cols-3 gap-3">
            {#each groupedByType.images as file (file.id)}
              <div
                class="aspect-square bg-surface rounded border border-border overflow-hidden hover:shadow-md transition-shadow group cursor-pointer relative"
              >
                <img
                  src={file.url}
                  alt={file.fileName}
                  class="w-full h-full object-cover group-hover:scale-110 transition-transform"
                  loading="lazy"
                />
                <div
                  class="absolute inset-0 bg-black bg-opacity-0 group-hover:bg-opacity-50 transition-all flex items-center justify-center gap-2 opacity-0 group-hover:opacity-100"
                >
                  <button
                    type="button"
                    onclick={(e) => {
                      e.preventDefault();
                      openImageEditor(file);
                    }}
                    class="px-3 py-1 bg-white rounded hover:bg-primary transition-colors text-xs font-medium"
                    title="Edit"
                  >
                    Edit
                  </button>
                  <a
                    href={file.url}
                    target="_blank"
                    rel="noreferrer"
                    class="px-3 py-1 bg-white rounded hover:bg-primary transition-colors text-xs font-medium"
                    title="View"
                  >
                    View
                  </a>
                  <a
                    href={file.url}
                    download={file.fileName}
                    onclick={(e) => e.preventDefault()}
                    class="px-3 py-1 bg-white rounded hover:bg-primary transition-colors text-xs font-medium"
                    title="Download"
                  >
                    Download
                  </a>
                  <button
                    type="button"
                    onclick={(e) => {
                      e.preventDefault();
                      handleDeleteFile(file.id, file.fileName);
                    }}
                    disabled={deletingFiles.has(file.id)}
                    class="px-3 py-1 bg-danger text-white rounded hover:bg-danger-hover transition-colors text-xs font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                    title="Delete"
                  >
                    {deletingFiles.has(file.id) ? '...' : 'Delete'}
                  </button>
                </div>
              </div>
            {/each}
          </div>
        {:else}
          <div class="space-y-2">
            {#each groupedByType.images as file (file.id)}
              <div
                class="flex items-center gap-4 p-4 bg-surface rounded hover:bg-white border border-border transition-colors group"
              >
                <div class="flex-1 min-w-0">
                  <p class="text-sm font-medium text-text truncate">{file.fileName}</p>
                  <p class="text-xs text-text-light mt-1">
                    {getTypeLabel(file.fileType)} • {formatFileSize(file.size)} • {formatDate(file.uploadedAt)}
                  </p>
                </div>
                <div class="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                  <button
                    type="button"
                    onclick={() => openImageEditor(file)}
                    class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
                    title="Edit"
                  >
                    Edit
                  </button>
                  <a
                    href={file.url}
                    target="_blank"
                    rel="noreferrer"
                    class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
                    title="View"
                  >
                    View
                  </a>
                  <a
                    href={file.url}
                    download={file.fileName}
                    class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
                    title="Download"
                  >
                    Download
                  </a>
                  <button
                    type="button"
                    onclick={() => handleDeleteFile(file.id, file.fileName)}
                    disabled={deletingFiles.has(file.id)}
                    class="px-3 py-2 text-sm text-danger hover:text-danger-hover rounded hover:bg-danger-light transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                    title="Delete"
                  >
                    {deletingFiles.has(file.id) ? 'Deleting...' : 'Delete'}
                  </button>
                </div>
              </div>
            {/each}
          </div>
        {/if}
      </div>
    {/if}

    {#if groupedByType.other.length > 0}
      <div class="space-y-3">
        <h4 class="text-sm font-semibold text-text-light uppercase tracking-wide">Other Files</h4>
        <div class="space-y-2">
          {#each groupedByType.other as file (file.id)}
            <div
              class="flex items-center gap-4 p-4 bg-surface rounded hover:bg-white border border-border transition-colors group"
            >
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium text-text truncate">{file.fileName}</p>
                <p class="text-xs text-text-light mt-1">
                  {getTypeLabel(file.fileType)} • {formatFileSize(file.size)} • {formatDate(file.uploadedAt)}
                </p>
              </div>
              <div class="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                <a
                  href={file.url}
                  target="_blank"
                  rel="noreferrer"
                  class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
                  title="View"
                >
                  View
                </a>
                <a
                  href={file.url}
                  download={file.fileName}
                  class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
                  title="Download"
                >
                  Download
                </a>
                <button
                  type="button"
                  onclick={() => handleDeleteFile(file.id, file.fileName)}
                  disabled={deletingFiles.has(file.id)}
                  class="px-3 py-2 text-sm text-danger hover:text-danger-hover rounded hover:bg-danger-light transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                  title="Delete"
                >
                  {deletingFiles.has(file.id) ? 'Deleting...' : 'Delete'}
                </button>
              </div>
            </div>
          {/each}
        </div>
      </div>
    {/if}
  {/if}
</div>

<style>
  .group {
    position: relative;
  }
</style>
