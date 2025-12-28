<script lang="ts">
  import { files } from '$lib/stores/files';
  import { currentRoom } from '$lib/stores/rooms';
  import { formatFileSize } from '$lib/utils/validation';
  import { formatDate } from '$lib/utils/date';
  import { ui } from '$lib/stores/ui';

  interface Props {
    compact?: boolean;
  }

  let { compact = false }: Props = $props();

  let filteredFiles = $derived.by(() => {
    if (!$currentRoom) return [];
    return $files.filter((f) => f.roomId === $currentRoom?.id);
  });

  let groupedByType = $derived.by(() => {
    const groups: Record<string, typeof $files> = {
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

  function handleDownload(fileUrl: string, fileName: string) {
    const link = document.createElement('a');
    link.href = fileUrl;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  function openUploadModal() {
    ui.openModal({
      id: 'file-upload',
      title: 'Upload Files',
      type: 'custom',
    });
  }

  function getFileIcon(fileType: string): string {
    switch (fileType) {
      case 'pdf':
        return '📄';
      case 'document':
        return '📋';
      case 'image':
        return '🖼️';
      case 'brainstorm':
        return '💡';
      default:
        return '📎';
    }
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
        return 'Brainstorm Board';
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
        on:click={openUploadModal}
        class="text-sm text-primary hover:text-primary-hover transition-colors"
      >
        Upload your first file
      </button>
    </div>
  {:else}
    <div class="flex justify-between items-center mb-4">
      <h3 class="font-semibold text-text">{filteredFiles.length} Materials</h3>
      <button
        on:click={openUploadModal}
        class="px-3 py-1 text-sm bg-primary text-white rounded hover:bg-primary-hover transition-colors"
      >
        + Upload
      </button>
    </div>

    {#if groupedByType.documents.length > 0}
      <div class="space-y-2">
        <h4 class="text-sm font-semibold text-text-light uppercase tracking-wide">Documents</h4>
        <div class="space-y-1">
          {#each groupedByType.documents as file (file.id)}
            <div
              class="flex items-center gap-3 p-3 bg-surface rounded hover:bg-white border border-border transition-colors group"
            >
              <span class="text-lg">{getFileIcon(file.fileType)}</span>
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium text-text truncate">{file.fileName}</p>
                <p class="text-xs text-text-light">
                  {formatFileSize(file.size)} • {formatDate(file.uploadedAt)}
                </p>
              </div>
              <div class="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                <a
                  href={file.url}
                  target="_blank"
                  rel="noreferrer"
                  class="p-1 text-text-light hover:text-primary rounded hover:bg-white transition-colors"
                  title="View"
                >
                  👁️
                </a>
                <button
                  type="button"
                  on:click={() => handleDownload(file.url, file.fileName)}
                  class="p-1 text-text-light hover:text-primary rounded hover:bg-white transition-colors"
                  title="Download"
                >
                  ⬇️
                </button>
              </div>
            </div>
          {/each}
        </div>
      </div>
    {/if}

    {#if groupedByType.images.length > 0}
      <div class="space-y-2">
        <h4 class="text-sm font-semibold text-text-light uppercase tracking-wide">Images</h4>
        {#if !compact}
          <div class="grid grid-cols-3 gap-2">
            {#each groupedByType.images as file (file.id)}
              <div
                class="aspect-square bg-surface rounded border border-border overflow-hidden hover:shadow-md transition-shadow group cursor-pointer"
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
                  <a
                    href={file.url}
                    target="_blank"
                    rel="noreferrer"
                    class="p-2 bg-white rounded hover:bg-primary transition-colors"
                    title="View"
                  >
                    👁️
                  </a>
                  <button
                    type="button"
                    on:click={(e) => {
                      e.preventDefault();
                      handleDownload(file.url, file.fileName);
                    }}
                    class="p-2 bg-white rounded hover:bg-primary transition-colors"
                    title="Download"
                  >
                    ⬇️
                  </button>
                </div>
              </div>
            {/each}
          </div>
        {:else}
          <div class="space-y-1">
            {#each groupedByType.images as file (file.id)}
              <div
                class="flex items-center gap-3 p-3 bg-surface rounded hover:bg-white border border-border transition-colors group"
              >
                <span class="text-lg">{getFileIcon(file.fileType)}</span>
                <div class="flex-1 min-w-0">
                  <p class="text-sm font-medium text-text truncate">{file.fileName}</p>
                  <p class="text-xs text-text-light">
                    {formatFileSize(file.size)} • {formatDate(file.uploadedAt)}
                  </p>
                </div>
                <div class="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                  <a
                    href={file.url}
                    target="_blank"
                    rel="noreferrer"
                    class="p-1 text-text-light hover:text-primary rounded hover:bg-white transition-colors"
                    title="View"
                  >
                    👁️
                  </a>
                  <button
                    type="button"
                    on:click={() => handleDownload(file.url, file.fileName)}
                    class="p-1 text-text-light hover:text-primary rounded hover:bg-white transition-colors"
                    title="Download"
                  >
                    ⬇️
                  </button>
                </div>
              </div>
            {/each}
          </div>
        {/if}
      </div>
    {/if}

    {#if groupedByType.other.length > 0}
      <div class="space-y-2">
        <h4 class="text-sm font-semibold text-text-light uppercase tracking-wide">Other Files</h4>
        <div class="space-y-1">
          {#each groupedByType.other as file (file.id)}
            <div
              class="flex items-center gap-3 p-3 bg-surface rounded hover:bg-white border border-border transition-colors group"
            >
              <span class="text-lg">{getFileIcon(file.fileType)}</span>
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium text-text truncate">{file.fileName}</p>
                <p class="text-xs text-text-light">
                  {formatFileSize(file.size)} • {formatDate(file.uploadedAt)}
                </p>
              </div>
              <div class="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                <a
                  href={file.url}
                  target="_blank"
                  rel="noreferrer"
                  class="p-1 text-text-light hover:text-primary rounded hover:bg-white transition-colors"
                  title="View"
                >
                  👁️
                </a>
                <button
                  type="button"
                  on:click={() => handleDownload(file.url, file.fileName)}
                  class="p-1 text-text-light hover:text-primary rounded hover:bg-white transition-colors"
                  title="Download"
                >
                  ⬇️
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
