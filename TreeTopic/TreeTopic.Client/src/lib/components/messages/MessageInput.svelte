<script lang="ts">
  import Button from '../common/Button.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { selectedTopic } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import { addMessage, cancelReply, replyTarget } from '$lib/stores/messages';
  import { ui } from '$lib/stores/ui';
  import { isRequired } from '$lib/utils/validation';
  import { getMessageAnchorId } from '$lib/utils/messageAnchor';
  import { api } from '$lib/api/client';

  let subject = $state('');
  let content = $state('');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let fileInput: HTMLInputElement | undefined = $state();
  let selectedFiles = $state<File[]>([]);

  function fileKey(file: File): string {
    return `${file.name}|${file.size}|${file.lastModified}`;
  }

  function normalizeMessage(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    function getAttachmentKind(fileName: string, mimeType: string): 'image' | 'pdf' | 'document' | 'other' {
      if (mimeType?.startsWith('image/')) return 'image';
      const ext = (fileName?.split('.').pop() ?? '').toLowerCase();
      if (ext === 'pdf') return 'pdf';
      const docExts = new Set(['doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx', 'txt', 'md', 'rtf', 'csv']);
      if (docExts.has(ext)) return 'document';
      return 'other';
    }

    const rawFiles = raw?.files ?? raw?.Files ?? [];
    const attachments =
      Array.isArray(rawFiles)
        ? rawFiles.map((f: any) => {
            const fid = f?.id ?? f?.Id ?? '';
            const fileName = f?.fileName ?? f?.FileName ?? '';
            const mimeType = f?.fileType ?? f?.FileType ?? 'application/octet-stream';
            const size = f?.size ?? f?.Size ?? 0;
            const url = f?.url ?? f?.Url ?? '';
            const uploadedAt = f?.createdAt ?? f?.CreatedAt ?? null;
            return {
              id: fid,
              fileName,
              mimeType,
              size,
              url,
              fileType: getAttachmentKind(fileName, mimeType),
              uploadedAt: uploadedAt ? new Date(uploadedAt) : new Date(),
              uploadedBy: f?.uploadedBy ?? f?.UploadedBy ?? '',
            };
          })
        : [];

    return {
      id,
      topicId: raw?.topicId ?? raw?.TopicId ?? '',
      userId: raw?.applicationUserId ?? raw?.ApplicationUserId ?? raw?.userId ?? raw?.UserId ?? '',
      userName: raw?.userName ?? raw?.UserName ?? '',
      userDisplayName: raw?.userDisplayName ?? raw?.UserDisplayName ?? raw?.userName ?? raw?.UserName ?? '',
      userAvatar: raw?.userAvatar ?? raw?.UserAvatar ?? undefined,
      subject: raw?.subject ?? raw?.Subject ?? raw?.header ?? raw?.Header ?? '',
      content: raw?.content ?? raw?.Content ?? raw?.body ?? raw?.Body ?? '',
      replyToId: raw?.replyToId ?? raw?.ReplyToId ?? raw?.replyId ?? raw?.ReplyId ?? undefined,
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : undefined,
      attachments,
      isOwner: false,
      canEdit: false,
      canDelete: false,
      reactions: [],
      readBy: [],
      sortOrder: raw?.sortOrder ?? raw?.SortOrder ?? undefined,
    };
  }

  async function handleSubmit(e: Event) {
    e.preventDefault();

    error = null;

    if (!$currentRoom || !$selectedTopic) {
      error = 'Please select a room and topic first';
      return;
    }

    if (!isRequired(content)) {
      error = 'Message content cannot be empty';
      return;
    }

    isLoading = true;

    try {
      const tenant = api.getCurrentTenant();

      const trimmedContent = content.trim();
      const trimmedSubject = subject.trim();
      const header = trimmedSubject || trimmedContent.split('\n')[0]?.slice(0, 500) || 'Message';

      const form = new FormData();
      form.append('TopicId', $selectedTopic.id);
      form.append('Header', header);
      form.append('Body', trimmedContent);
      if ($replyTarget) {
        form.append('ReplyId', $replyTarget.id);
      }

      for (const file of selectedFiles) {
        form.append('Files', file);
      }

      const response = await api.post(`/${tenant}/api/Message`, form);
      addMessage({
        ...normalizeMessage(response),
        subject: trimmedSubject,
        content: trimmedContent,
      });
      subject = '';
      content = '';
      cancelReply();
      selectedFiles = [];
      if (fileInput) fileInput.value = '';
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to send message';
    } finally {
      isLoading = false;
    }
  }

  function handleFileSelect(e: Event) {
    const input = e.target as HTMLInputElement;
    const newlySelected = input.files ? Array.from(input.files) : [];

    const merged = new Map<string, File>();
    for (const f of selectedFiles) merged.set(fileKey(f), f);
    for (const f of newlySelected) merged.set(fileKey(f), f);

    selectedFiles = Array.from(merged.values());

    // Allow picking the same file again by clearing the input value.
    input.value = '';
  }

  function removeSelectedFile(file: File) {
    const key = fileKey(file);
    selectedFiles = selectedFiles.filter((f) => fileKey(f) !== key);
  }

  async function copyReplyUrl() {
    if (typeof window === 'undefined') return;
    if (!$replyTarget) return;

    const url = new URL(window.location.href);
    url.hash = getMessageAnchorId($replyTarget.id);
    const link = url.toString();

    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(link);
      } else {
        const textarea = document.createElement('textarea');
        textarea.value = link;
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        document.body.appendChild(textarea);
        textarea.select();
        document.execCommand('copy');
        document.body.removeChild(textarea);
      }

      window.history.replaceState(null, '', link);
      ui.addNotification({ type: 'success', message: 'Reply message URL copied' });
    } catch {
      ui.addNotification({ type: 'error', message: 'Failed to copy URL' });
    }
  }
</script>

<div class="panel-footer">
  <form on:submit={handleSubmit} class="spacing-sm flex flex-col w-full">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    {#if $replyTarget}
      <div class="replying-to">
        <div class="replying-to__bar"></div>
        <div class="replying-to__content">
            <div class="flex items-center gap-2">
              <span class="text-small text-light">Replying to</span>
              <span class="text-small text-bold">{$replyTarget.userDisplayName || $replyTarget.userName}</span>
              <button type="button" class="replying-to__copy" on:click={copyReplyUrl} title="Copy reply URL">URL</button>
              <button type="button" class="replying-to__cancel" on:click={() => cancelReply()} title="Cancel reply">×</button>
            </div>
          <div class="text-small text-light replying-to__text">
            {$replyTarget.subject || $replyTarget.content}
          </div>
        </div>
      </div>
    {/if}

    <input
      type="text"
      bind:value={subject}
      placeholder="Message subject (optional)"
      disabled={isLoading}
      class="form-input w-full text-small"
    />

    <textarea
      bind:value={content}
      placeholder="Type your message here..."
      disabled={isLoading}
      class="form-input w-full text-small"
      rows="3"
      style="resize: none;"
    />

    <div class="flex items-center gap-2">
      <input
        type="file"
        bind:this={fileInput}
        on:change={handleFileSelect}
        multiple
        disabled={isLoading}
        class="hidden"
      />

      <button
        type="button"
        on:click={() => fileInput?.click()}
        disabled={isLoading}
        class="button button-secondary button-small"
        title="Attach file"
      >
        Attach{selectedFiles.length > 0 ? ` (${selectedFiles.length})` : ''}
      </button>

      <div class="flex-1"></div>

      <Button
        type="submit"
        variant="primary"
        size="small"
        loading={isLoading}
        disabled={isLoading || !$selectedTopic}
      >
        Send
      </Button>
    </div>

    {#if !$selectedTopic}
      <p class="text-small text-light text-center">
        Select a topic to send messages
      </p>
    {/if}

    {#if selectedFiles.length > 0}
      <div class="selected-files">
        {#each selectedFiles as file (fileKey(file))}
          <div class="selected-file">
            <span class="selected-file__name">{file.name}</span>
            <button
              type="button"
              class="selected-file__remove"
              title="Remove file"
              on:click={() => removeSelectedFile(file)}
              disabled={isLoading}
            >
              ×
            </button>
          </div>
        {/each}
      </div>
    {/if}
  </form>
</div>

<style>
  textarea {
    font-family: var(--font-family-base);
  }

  .selected-files {
    display: flex;
    flex-wrap: wrap;
    gap: var(--spacing-xs);
    padding-top: var(--spacing-xs);
  }

  .selected-file {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 8px;
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    background-color: var(--color-surface);
    max-width: 100%;
  }

  .selected-file__name {
    font-size: var(--font-size-xs);
    color: var(--color-text);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 260px;
  }

  .selected-file__remove {
    border: none;
    background: transparent;
    color: var(--color-text-light);
    cursor: pointer;
    font-size: var(--font-size-base);
    line-height: 1;
    padding: 0 2px;
  }

  .selected-file__remove:hover:not(:disabled) {
    color: var(--color-error);
  }

  .replying-to {
    display: flex;
    gap: var(--spacing-sm);
    padding: var(--spacing-xs) var(--spacing-sm);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    background-color: var(--color-surface);
  }

  .replying-to__bar {
    width: 3px;
    border-radius: 2px;
    background-color: var(--color-primary);
    flex-shrink: 0;
  }

  .replying-to__content {
    min-width: 0;
    flex: 1;
  }

  .replying-to__text {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .replying-to__cancel {
    margin-left: auto;
    border: none;
    background: transparent;
    color: var(--color-text-light);
    cursor: pointer;
    font-size: var(--font-size-lg);
    line-height: 1;
  }

  .replying-to__copy {
    border: none;
    background: transparent;
    color: var(--color-text-light);
    cursor: pointer;
    font-size: var(--font-size-xs);
    padding: 2px 6px;
    border-radius: var(--border-radius-sm);
  }

  .replying-to__copy:hover {
    color: var(--color-primary);
    background-color: color-mix(in srgb, var(--color-primary) 10%, transparent);
  }

  .replying-to__cancel:hover {
    color: var(--color-primary);
  }
</style>
