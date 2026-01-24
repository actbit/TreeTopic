<script lang="ts">
  import Button from '../common/Button.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import {
    addTopic,
    selectedTopic,
    topicList,
    updateTopic,
    type Topic,
  } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import {
    addMessage,
    cancelReply,
    messageList,
    replyTarget,
    type Message,
  } from '$lib/stores/messages';
  import Modal from '../common/Modal.svelte';
  import { ui } from '$lib/stores/ui';
  import { isRequired, minLength } from '$lib/utils/validation';
  import { getMessageAnchorId } from '$lib/utils/messageAnchor';
  import { api } from '$lib/api/client';

  let subject = $state('');
  let content = $state('');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let fileInput: HTMLInputElement | undefined = $state();
  let selectedFiles = $state<File[]>([]);
  let isCreatingChildTopic = $state(false);
  let isChildTopicModalOpen = $state(false);
  let childTopicTitle = $state('');
  let childTopicDescription = $state('');
  let childTopicTitleError = $state<string | null>(null);
  let selectedHistoryMessageIds = $state<Set<string>>(new Set());
  let recentHistoryMessages = $state<Message[]>([]);
  let lastHistoryTopicId = $state<string | null>(null);

  interface ChildTopicPayload {
    parentId: string;
    title: string;
    description: string;
    selectedMessageIds: string[];
  }

  function fileKey(file: File): string {
    return `${file.name}|${file.size}|${file.lastModified}`;
  }

  type MessageApiResponse = {
    childTopicId?: string;
    ChildTopicId?: string;
  } & Record<string, unknown>;

  function normalizeMessage(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';

    // IDが空文字列の場合はエラーとして扱う（デバッグ用）
    if (!id) {
      console.error('Message ID is empty:', raw);
      throw new Error('Invalid message ID: ID is empty');
    }
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
      userId:
        raw?.roomUserId ??
        raw?.RoomUserId ??
        raw?.applicationUserId ??
        raw?.ApplicationUserId ??
        raw?.userId ??
        raw?.UserId ??
        '',
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
      childTopicId: raw?.childTopicId ?? raw?.ChildTopicId ?? null,
      childTopicTitle: raw?.childTopicTitle ?? raw?.ChildTopicTitle ?? null,
    };
  }

  function getSuggestedChildTopicTitle() {
    const MAX_LENGTH = 120;
    const normalizedSubject = subject.trim();
    if (normalizedSubject) {
      return normalizedSubject.length <= MAX_LENGTH
        ? normalizedSubject
        : `${normalizedSubject.slice(0, MAX_LENGTH).trim()}…`;
    }

    const normalizedContent = content.replace(/\s+/g, ' ').trim();
    if (!normalizedContent) {
      return '';
    }

    const firstLine = normalizedContent.split('\n')[0] ?? normalizedContent;
    return firstLine.length <= MAX_LENGTH
      ? firstLine
      : `${firstLine.slice(0, MAX_LENGTH).trim()}…`;
  }

  function openChildTopicModal() {
    if (!isCreatingChildTopic) {
      isCreatingChildTopic = true;
      childTopicTitleError = null;
      if (!childTopicTitle.trim()) {
        childTopicTitle = getSuggestedChildTopicTitle();
      }
    }
    isChildTopicModalOpen = true;
  }

  function closeChildTopicModal() {
    isChildTopicModalOpen = false;
  }

  function applyChildTopicSelection() {
    // Just close the modal, keep the child topic state for message submission
    closeChildTopicModal();
  }

  let childTopicTitlePreview = $derived(childTopicTitle.trim());
  let childTopicDescriptionPreview = $derived(childTopicDescription.trim());
  let isChildTopicActive = $derived(
    childTopicTitlePreview.length > 0 ||
    childTopicDescriptionPreview.length > 0 ||
    selectedHistoryMessageIds.size > 0 ||
    isCreatingChildTopic
  );

  function resetChildTopicForm() {
    isCreatingChildTopic = false;
    isChildTopicModalOpen = false;
    childTopicTitle = '';
    childTopicDescription = '';
    childTopicTitleError = null;
    selectedHistoryMessageIds = new Set();
  }

  $effect(() => {
    const currentTopicId = $selectedTopic?.id ?? null;
    if (currentTopicId !== lastHistoryTopicId) {
      selectedHistoryMessageIds = new Set();
      lastHistoryTopicId = currentTopicId;
    }
  });

  $effect(() => {
    recentHistoryMessages = $selectedTopic
      ? ($messageList || [])
          .filter((m) => m?.id && m.topicId === $selectedTopic.id)
          .sort((a, b) => b.createdAt.getTime() - a.createdAt.getTime())
          .slice(0, 20)
      : [];
  });

  function toggleHistoryMessageSelection(messageId: string) {
    const next = new Set(selectedHistoryMessageIds);
    if (next.has(messageId)) {
      next.delete(messageId);
    } else {
      next.add(messageId);
    }
    selectedHistoryMessageIds = next;
  }

  function clearHistorySelection() {
    selectedHistoryMessageIds = new Set();
  }

  async function handleSubmit(e: Event) {
    e.preventDefault();

    error = null;
    childTopicTitleError = null;

    if (!$currentRoom || !$selectedTopic) {
      error = 'Please select a room and topic first';
      return;
    }

    if (!isRequired(content)) {
      error = 'Message content cannot be empty';
      return;
    }

    let childTopicPayload: ChildTopicPayload | null = null;
    const trimmedChildTopicTitle = childTopicTitle.trim();
    if (trimmedChildTopicTitle.length > 0) {
      if (!minLength(trimmedChildTopicTitle, 2)) {
        childTopicTitleError = 'Child topic title must be at least 2 characters';
        return;
      }

      childTopicPayload = {
        parentId: $selectedTopic.id,
        title: trimmedChildTopicTitle,
        description: childTopicDescription.trim(),
        selectedMessageIds: Array.from(selectedHistoryMessageIds),
      };
    }

    isLoading = true;

    try {
      const tenant = api.getCurrentTenant();

      const trimmedContent = content.trim();
      const trimmedSubject = subject.trim();
      const header = trimmedSubject || '';

      let response: MessageApiResponse;
      if (selectedFiles.length > 0) {
        // ファイルがある場合はFormDataで /upload エンドポイントに送信
        const form = new FormData();
        form.append('topicId', $selectedTopic.id);
        form.append('header', header);
        form.append('body', trimmedContent);
        if ($replyTarget) {
          form.append('replyId', $replyTarget.id);
        }
        if (childTopicPayload) {
          form.append('childTopic.ParentId', childTopicPayload.parentId);
          form.append('childTopic.Title', childTopicPayload.title);
          form.append('childTopic.Description', childTopicPayload.description);
          childTopicPayload.selectedMessageIds.forEach((id) =>
            form.append('childTopic.SelectedMessageIds', id)
          );
        }
        for (const file of selectedFiles) {
          form.append('files', file);
        }
        response = await api.post<MessageApiResponse>(`/${tenant}/api/Message/upload`, form);
      } else {
        // ファイルがない場合はJSONで送信
        interface MessagePayload {
          topicId: string;
          header: string;
          body: string;
          replyId?: string;
          childTopic?: ChildTopicPayload;
        }

        const payload: MessagePayload = {
          topicId: $selectedTopic.id,
          header,
          body: trimmedContent,
        };

        if ($replyTarget) {
          payload.replyId = $replyTarget.id;
        }

        if (childTopicPayload) {
          payload.childTopic = childTopicPayload;
        }

        response = await api.post<MessageApiResponse>(`/${tenant}/api/Message`, payload);
      }
      const childTopicIdFromResponse =
        response?.childTopicId ?? response?.ChildTopicId ?? null;
      const newMessage = {
        ...normalizeMessage(response),
        subject: trimmedSubject,
        content: trimmedContent,
      };

      // 重複チェックを行ってから追加（念の為に複数チェック）
      const exists = $messageList.some((m) => m.id === newMessage.id);
      const stillExistsAfterDelay = setTimeout(() => {
        const doubleCheck = $messageList.some((m) => m.id === newMessage.id);
        if (!doubleCheck) {
          console.warn(`Message ${newMessage.id} was not added after delay, retrying...`);
          addMessage(newMessage);
        }
      }, 100);

      if (!exists) {
        addMessage(newMessage);
        // 遅延チェックをクリーンアップ
      } else {
        clearTimeout(stillExistsAfterDelay);
      }
      subject = '';
      content = '';
      cancelReply();
      selectedFiles = [];
      if (fileInput) fileInput.value = '';
      resetChildTopicForm();
      if (childTopicIdFromResponse) {
        void syncChildTopicFromMessage(childTopicIdFromResponse);
      }
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to send message';
    } finally {
      isLoading = false;
    }
  }

  function normalizeTopicResponse(raw: any): Topic {
    const id = raw?.id ?? raw?.Id ?? '';
    const roomId = raw?.roomId ?? raw?.RoomId ?? '';
    const parentId = raw?.parentId ?? raw?.ParentId ?? null;
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    return {
      id,
      roomId,
      parentId,
      sourceMessageId: raw?.sourceMessageId ?? raw?.SourceMessageId ?? null,
      title: raw?.title ?? raw?.Title ?? '',
      description: raw?.description ?? raw?.Description ?? undefined,
      childIds: raw?.childIds ?? raw?.ChildIds ?? [],
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : new Date(),
      creatorId: raw?.creatorId ?? raw?.CreatorId ?? '',
      messageCount: raw?.messageCount ?? raw?.MessageCount ?? 0,
      unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
      userPermission: raw?.userPermission ?? raw?.UserPermission ?? 'admin',
      permissions: raw?.permissions ?? raw?.Permissions ?? [],
      isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
      tags: raw?.tags ?? raw?.Tags ?? [],
      hasChildren: raw?.hasChildren ?? raw?.HasChildren ?? false,
    };
  }

  async function syncChildTopicFromMessage(childTopicId: string | null) {
    if (!childTopicId) return;

    const tenant = api.getCurrentTenant();
    if (!tenant) return;

    try {
      const response = await api.get<any>(`/${tenant}/api/Topic/${childTopicId}`);
      const normalizedTopic = normalizeTopicResponse(response);
      if (!normalizedTopic.id) return;

      const exists = $topicList.some((topic) => topic.id === normalizedTopic.id);
      if (!exists) {
        addTopic(normalizedTopic);
      }

      if (normalizedTopic.parentId) {
        const parentId = normalizedTopic.parentId;
        const parent = $topicList.find((topic) => topic.id === parentId);
        if (parent && !parent.hasChildren) {
          updateTopic(parentId, { hasChildren: true });
        }
      }
    } catch (syncError) {
      console.error('Failed to sync created child topic:', syncError);
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
  <form onsubmit={handleSubmit} class="spacing-sm flex flex-col w-full">
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
              <button type="button" class="replying-to__copy" onclick={copyReplyUrl} title="Copy reply URL">URL</button>
              <button type="button" class="replying-to__cancel" onclick={() => cancelReply()} title="Cancel reply">×</button>
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
    ></textarea>

    <div class="child-topic-toggle">
      <button
        type="button"
        onclick={openChildTopicModal}
        class="button button-ghost button-small"
        disabled={isLoading}
      >
        Create child topic
      </button>
      <span class="text-small text-light">
        Open modal to select messages for the child topic
      </span>
    </div>

    {#if isChildTopicActive}
      <div class="child-topic-indicator">
        <span class="child-topic-indicator__label">
          Child topic creation in progress
          {#if childTopicTitlePreview}
            ："{childTopicTitlePreview}"
          {:else}
            ：(title not set)
          {/if}
        </span>
        <button
          type="button"
          class="child-topic-indicator__clear"
          onclick={resetChildTopicForm}
          disabled={isLoading}
        >
          Clear
        </button>
      </div>
    {/if}

    <div class="flex items-center gap-2">
      <input
        type="file"
        bind:this={fileInput}
        onchange={handleFileSelect}
        multiple
        disabled={isLoading}
        class="hidden"
      />

      <button
        type="button"
        onclick={() => fileInput?.click()}
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
        {#each selectedFiles.filter(f => f) as file (fileKey(file))}
          <div class="selected-file">
            <span class="selected-file__name">{file.name}</span>
            <button
              type="button"
              class="selected-file__remove"
              title="Remove file"
              onclick={() => removeSelectedFile(file)}
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

<Modal
  isOpen={isChildTopicModalOpen}
  title="Create child topic"
  size="large"
  onClose={() => {
    closeChildTopicModal();
  }}
>
  <div class="child-topic-form">
    <div>
      <label for="child-topic-title" class="form-label">Child topic title</label>
      <input
        id="child-topic-title"
        type="text"
        bind:value={childTopicTitle}
        placeholder="Enter child topic title"
        class="form-input"
        disabled={isLoading}
        minlength="2"
        oninput={() => (childTopicTitleError = null)}
      />
      {#if childTopicTitleError}
        <p class="text-error text-small">{childTopicTitleError}</p>
      {/if}
    </div>
    <div>
      <label for="child-topic-description" class="form-label">Description (optional)</label>
      <textarea
        id="child-topic-description"
        bind:value={childTopicDescription}
        placeholder="Provide a description for the child topic"
        class="form-input child-topic-description"
        rows="2"
        disabled={isLoading}
      ></textarea>
    </div>
    {#if recentHistoryMessages.length > 0}
      <div class="child-topic-history">
        <div class="child-topic-history__header">
          <span class="text-small text-bold">Select messages to move</span>
          <button
            type="button"
            class="child-topic-history__clear"
            onclick={clearHistorySelection}
            disabled={isLoading || selectedHistoryMessageIds.size === 0}
          >
            Clear
          </button>
        </div>
        <div class="child-topic-history__list">
          {#each recentHistoryMessages.filter(h => h?.id) as history (history.id)}
            <label class="child-topic-history__item">
              <input
                type="checkbox"
                checked={selectedHistoryMessageIds.has(history.id)}
                disabled={isLoading}
                onchange={() => toggleHistoryMessageSelection(history.id)}
              />
              <div class="child-topic-history__content">
                <div class="child-topic-history__meta">
                  <span class="text-small text-bold">
                    {history.userDisplayName || history.userName}
                  </span>
                  <span class="text-small text-light">
                    {history.createdAt.toLocaleString()}
                  </span>
                </div>
                <div class="text-small child-topic-history__snippet">
                  {history.subject || history.content || 'Empty message'}
                </div>
              </div>
            </label>
          {/each}
        </div>
      </div>
    {/if}
    <div class="flex spacing-md padding-top-md">
      <Button
        type="button"
        variant="primary"
        size="base"
        fullWidth
        disabled={isLoading}
        onclick={applyChildTopicSelection}
      >
        Done
      </Button>
      <Button
        type="button"
        variant="secondary"
        size="base"
        fullWidth
        disabled={isLoading}
        onclick={resetChildTopicForm}
      >
        Cancel
      </Button>
    </div>
  </div>
</Modal>

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

  .child-topic-indicator {
    margin-top: var(--spacing-xs);
    padding: var(--spacing-xs) var(--spacing-sm);
    border-radius: var(--border-radius-sm);
    background-color: color-mix(in srgb, var(--color-primary) 8%, var(--color-surface));
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--spacing-xs);
  }

  .child-topic-indicator__label {
    font-size: var(--font-size-sm);
    color: var(--color-text);
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .child-topic-indicator__clear {
    border: none;
    background: transparent;
    color: var(--color-error);
    font-size: var(--font-size-xs);
    cursor: pointer;
    text-decoration: underline;
  }

  .child-topic-indicator__clear:disabled {
    color: var(--color-text-light);
    cursor: not-allowed;
    text-decoration: none;
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

  .child-topic-toggle {
    display: flex;
    align-items: center;
    gap: var(--spacing-sm);
    margin-top: var(--spacing-sm);
  }

  .child-topic-form {
    margin-top: var(--spacing-sm);
    padding: var(--spacing-sm);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    background-color: var(--color-surface);
    display: flex;
    flex-direction: column;
    gap: var(--spacing-sm);
  }

  .child-topic-description {
    min-height: 60px;
    resize: vertical;
    font-family: var(--font-family-base);
  }

  .child-topic-history {
    margin-top: var(--spacing-sm);
    padding: var(--spacing-sm);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    background-color: var(--color-surface);
    display: flex;
    flex-direction: column;
    gap: var(--spacing-xs);
  }

  .child-topic-history__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .child-topic-history__clear {
    border: none;
    background: transparent;
    color: var(--color-primary);
    font-size: var(--font-size-xs);
    cursor: pointer;
    padding: 0;
  }

  .child-topic-history__list {
    display: flex;
    flex-direction: column;
    gap: var(--spacing-xs);
    max-height: 220px;
    overflow-y: auto;
  }

  .child-topic-history__item {
    display: flex;
    gap: var(--spacing-sm);
    align-items: flex-start;
    padding: var(--spacing-xs);
    border-radius: var(--border-radius-sm);
    background-color: color-mix(in srgb, var(--color-primary) 6%, var(--color-surface));
    cursor: pointer;
  }

  .child-topic-history__item input {
    margin-top: 4px;
  }

  .child-topic-history__content {
    display: flex;
    flex-direction: column;
    gap: 2px;
    width: 100%;
  }

  .child-topic-history__meta {
    display: flex;
    justify-content: space-between;
    gap: var(--spacing-sm);
    font-size: var(--font-size-xs);
    color: var(--color-text-light);
  }

  .child-topic-history__snippet {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
