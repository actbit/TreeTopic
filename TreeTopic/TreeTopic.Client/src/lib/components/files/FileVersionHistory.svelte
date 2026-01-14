<script lang="ts">
  import { formatDate, formatTime } from '$lib/utils/date';
  import { formatFileSize } from '$lib/utils/validation';
  import type { File as FileDto } from '$lib/types/ui';

  interface Props {
    file: FileDto;
    versions?: FileDto[];
  }

  let { file, versions = [] }: Props = $props();

  function handleDownload(fileUrl: string, fileName: string) {
    const link = document.createElement('a');
    link.href = fileUrl;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  function isCurrent(versionFile: FileDto): boolean {
    return versionFile.id === file.id;
  }
</script>

<div class="space-y-4">
  <h3 class="font-semibold text-text">Version History</h3>

  {#if versions.length === 0}
    <div class="text-center py-8 text-text-light">
      <p>No version history available</p>
    </div>
  {:else}
    <div class="space-y-2">
      {#each versions as version (version.id)}
        <div
          class="flex items-center gap-3 p-3 bg-surface rounded border {isCurrent(version)
            ? 'border-primary bg-primary bg-opacity-5'
            : 'border-border'} hover:bg-white transition-colors group"
        >
          <div class="flex-1">
            <div class="flex items-baseline gap-2">
              <p class="font-medium text-text">{formatDate(version.uploadedAt)}</p>
              <p class="text-xs text-text-light">{formatTime(version.uploadedAt)}</p>
              {#if isCurrent(version)}
                <span class="px-2 py-0.5 text-xs font-semibold bg-primary text-white rounded">
                  Current
                </span>
              {/if}
            </div>
            <p class="text-sm text-text-light">
              {formatFileSize(version.size)} • Uploaded by {version.uploadedByName}
            </p>
          </div>

          <div class="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
            <a
              href={version.url}
              target="_blank"
              rel="noreferrer"
              class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
              title="View"
            >
              View
            </a>
            <button
              type="button"
              onclick={() => handleDownload(version.url, version.fileName)}
              class="px-3 py-2 text-sm text-text-light hover:text-primary rounded hover:bg-white transition-colors font-medium"
              title="Download"
            >
              Download
            </button>
          </div>
        </div>
      {/each}
    </div>
  {/if}
</div>
