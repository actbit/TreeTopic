<script lang="ts">
  import { formatTime } from '$lib/utils/date';
  import { formatFileSize } from '$lib/utils/validation';
  import { goto } from '$app/navigation';
  import { page } from '$app/stores';
  import { api, getCurrentTenant } from '$lib/api/client';
  import ContextMenu from '../common/ContextMenu.svelte';
  import Modal from '../common/Modal.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { childTopicsBySourceMessage, type Topic } from '$lib/stores/topics';
  import type { ContextMenuItem } from '../common/ContextMenu.svelte';
  import type { Message } from '$lib/stores/messages';
  import { messageList, startReply } from '$lib/stores/messages';
  import { ui } from '$lib/stores/ui';
  import type { ModalConfig } from '$lib/types/ui';
  import { getMessageAnchorId } from '$lib/utils/messageAnchor';
  import { createTopicParentId } from '$lib/stores/topics';

  type MessageContentPart =
    | { type: 'text'; value: string }
    | { type: 'url'; value: string; href: string };

  interface Props {
    message: Message;
  }

  let { message }: Props = $props();
  let showContextMenu = $state(false);
  let contextMenuX = $state(0);
  let contextMenuY = $state(0);

  let replyTo = $derived(
    !message.replyToId ? null : ($messageList.find((m) => m.id === message.replyToId) ?? null)
  );

  interface PreviewMessage {
    id: string;
    subject: string;
    content: string;
    createdAt: Date;
    userName: string;
    userDisplayName: string;
    userAvatar?: string;
  }

  let childTopics = $derived.by(() => {
    const fromStore = $childTopicsBySourceMessage.get(message.id) ?? [];
    const result = [...fromStore];

    // Add child topic from message data if not already in store
    if (message.childTopicId && !result.some(t => t.id === message.childTopicId)) {
      result.push({
        id: message.childTopicId,
        roomId: '', // Unknown from message data
        title: message.childTopicTitle ?? 'Untitled',
        description: undefined,
        parentId: null,
        sourceMessageId: message.id,
        childIds: [],
        createdAt: message.createdAt,
        updatedAt: message.updatedAt,
        creatorId: message.userId,
        messageCount: 0,
        unreadCount: 0,
        userPermission: 'read',
        permissions: [],
        isArchived: false,
        tags: [],
        hasChildren: false,
      } as Topic);
    }

    return result;
  });
  let isPreviewOpen = $state(false);
  let previewTopic: Topic | null = $state(null);
  let previewMessages = $state<PreviewMessage[]>([]);
  let isPreviewLoading = $state(false);
  let previewError = $state<string | null>(null);

  function handleContextMenu(e: MouseEvent) {
    e.preventDefault();
    e.stopPropagation();
    contextMenuX = e.clientX;
    contextMenuY = e.clientY;
    showContextMenu = true;
  }

  function openEditModal() {
    const modal: ModalConfig = {
      id: 'message-edit',
      title: 'Edit Message',
      type: 'custom',
      data: { messageId: message.id },
    };
    ui.openModal(modal);
    showContextMenu = false;
  }

  function openDeleteModal() {
    const modal: ModalConfig = {
      id: 'message-delete',
      title: 'Delete Message',
      type: 'custom',
      data: { messageId: message.id },
    };
    ui.openModal(modal);
    showContextMenu = false;
  }

  function replyToMessage() {
    startReply(message.id);
    showContextMenu = false;
  }

  function getSuggestedTopicTitle(): string {
    const MAX_TITLE_LENGTH = 120;
    const subject = message.subject?.trim();
    if (subject) {
      return subject.length > MAX_TITLE_LENGTH ? `${subject.slice(0, MAX_TITLE_LENGTH).trim()}…` : subject;
    }

    const normalized = (message.content ?? '').replace(/\s+/g, ' ').trim();
    if (!normalized) {
      return '';
    }

    const firstLine = normalized.split('\n')[0] ?? normalized;
    if (firstLine.length <= MAX_TITLE_LENGTH) {
      return firstLine;
    }

    return `${firstLine.slice(0, MAX_TITLE_LENGTH).trim()}…`;
  }

  function openCreateChildTopicFromMessage() {
    const parentId = message.topicId;
    createTopicParentId.set(parentId);
    const modal: ModalConfig = {
      id: 'topic-create',
      title: 'Create Topic',
      type: 'custom',
      data: {
        parentId,
        prefillTitle: getSuggestedTopicTitle(),
        prefillDescription: (message.content ?? '').trim(),
        autoNavigate: true,
        transferHistory: true,
        sourceMessageId: message.id,
      },
    };
    ui.openModal(modal);
    showContextMenu = false;
  }

  async function navigateToChildTopic(topic: Topic) {
    const tenant = ($page.params as any)?.tenant ?? getCurrentTenant();
    if (!tenant) return;

    // RoomIdが空の場合はAPIを呼び出して取得
    if (!topic.roomId || topic.roomId === '') {
      try {
        const response = await api.get<any>(`/${tenant}/api/Topic/${topic.id}`);
        const topicData = response?.data ?? response;
        if (topicData?.roomId) {
          // 取得したRoomIdをトピックオブジェクトに更新（UIにも反映されるように）
          childTopics = childTopics.map(t =>
            t.id === topic.id ? { ...t, roomId: topicData.roomId } : t
          );
          goto(`/${tenant}/room/${topicData.roomId}/topic/${topic.id}`, {
            keepFocus: true,
            noScroll: true,
          });
        } else {
          throw new Error('RoomId not found in topic data');
        }
      } catch (error) {
        console.error('Failed to fetch topic RoomId:', error);
        ui.addNotification({
          type: 'error',
          message: 'Failed to navigate to topic. Please try again.'
        });
        return;
      }
    } else {
      // RoomIdがある場合は通常通り遷移
      goto(`/${tenant}/room/${topic.roomId}/topic/${topic.id}`, {
        keepFocus: true,
        noScroll: true,
      });
    }
  }

  async function openChildTopicPreview(child: Topic) {
    previewTopic = child;
    previewMessages = [];
    previewError = null;
    isPreviewOpen = true;
    isPreviewLoading = true;

    try {
      const tenant = ($page.params as any)?.tenant ?? getCurrentTenant();
      if (!tenant) throw new Error('Tenant not found');

      const response = await api.get<any[]>(`/${tenant}/api/Message/topic/${child.id}`);
      const normalized =
        Array.isArray(response) && response.length > 0
          ? response
              .map((raw) => ({
                id: raw?.id ?? raw?.Id ?? '',
                subject: raw?.subject ?? raw?.Subject ?? raw?.header ?? raw?.Header ?? '',
                content: raw?.content ?? raw?.Content ?? raw?.body ?? raw?.Body ?? '',
                createdAt: new Date(raw?.createdAt ?? raw?.CreatedAt ?? Date.now()),
                userName: raw?.userName ?? raw?.UserName ?? '',
                userDisplayName:
                  raw?.userDisplayName ?? raw?.UserDisplayName ?? raw?.userName ?? raw?.UserName ?? '',
                userAvatar: raw?.userAvatar ?? raw?.UserAvatar ?? undefined,
              }))
              .sort((a, b) => a.createdAt.getTime() - b.createdAt.getTime())
              .slice(-10)
          : [];

      previewMessages = normalized;
    } catch (err) {
      previewError = err instanceof Error ? err.message : 'Failed to load preview';
    } finally {
      isPreviewLoading = false;
    }
  }

  function closeChildTopicPreview() {
    isPreviewOpen = false;
    previewTopic = null;
    previewMessages = [];
    previewError = null;
  }

  async function copyMessageUrl() {
    if (typeof window === 'undefined') return;

    const url = new URL(window.location.href);
    url.hash = getMessageAnchorId(message.id);
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
      ui.addNotification({ type: 'success', message: 'Message URL copied' });
    } catch {
      ui.addNotification({ type: 'error', message: 'Failed to copy message URL' });
    } finally {
      showContextMenu = false;
    }
  }

  const contextMenuItems: ContextMenuItem[] = [
    {
      id: 'reply',
      label: 'Reply',
      icon: '↩',
      action: replyToMessage,
    },
    {
      id: 'create-child-topic',
      label: 'Create child topic',
      icon: '➕',
      action: openCreateChildTopicFromMessage,
    },
    {
      id: 'copy-url',
      label: 'Copy URL',
      action: copyMessageUrl,
    },
    {
      id: 'edit',
      label: 'Edit',
      icon: '✏️',
      action: openEditModal,

    },
    {
      id: 'delete',
      label: 'Delete',
      icon: '🗑️',
      action: openDeleteModal,
      isDangerous: true,

    },
  ];

  function linkifyMessageContent(content: string): MessageContentPart[] {
    const text = content ?? '';
    if (!text) return [{ type: 'text', value: '' }];

    const parts: MessageContentPart[] = [];
    const urlRegex = /((?:https?:\/\/|www\.)\S+)/gi;

    let lastIndex = 0;
    for (const match of text.matchAll(urlRegex)) {
      const matchIndex = match.index ?? 0;
      const raw = match[1] ?? '';

      if (matchIndex > lastIndex) {
        parts.push({ type: 'text', value: text.slice(lastIndex, matchIndex) });
      }

      let url = raw;
      let trailing = '';
      while (url.length > 0 && /[)\]}",'!.?:;>]+$/.test(url)) {
        trailing = url.slice(-1) + trailing;
        url = url.slice(0, -1);
      }

      const href = url.startsWith('www.') ? `https://${url}` : url;
      try {
        // Validate URL; if invalid, treat as plain text.
        new URL(href);
        parts.push({ type: 'url', value: url, href });
      } catch {
        parts.push({ type: 'text', value: raw });
      }

      if (trailing) parts.push({ type: 'text', value: trailing });

      lastIndex = matchIndex + raw.length;
    }

    if (lastIndex < text.length) {
      parts.push({ type: 'text', value: text.slice(lastIndex) });
    }

    return parts;
  }
</script>

<div
  id={getMessageAnchorId(message.id)}
  class={`card hoverable ${childTopics.length > 0 ? 'card-has-child-topic' : ''}`}
  role="button"
  tabindex="0"
  oncontextmenu={(e) => {
    e.stopPropagation();
    handleContextMenu(e);
  }}
  onkeydown={(e) => {
    if (e.key === 'ContextMenu' || (e.shiftKey && e.key === 'F10')) {
      e.preventDefault();
      handleContextMenu(e as unknown as MouseEvent);
    }
  }}
>
  <div class="flex items-start gap-3">
    {#if message.userAvatar}
      <img
        src={message.userAvatar}
        alt={message.userName}
        class="avatar avatar-xs bg-primary"
      />
    {:else}
      <div
        class="avatar avatar-xs bg-primary text-white"
      >
        {message.userName?.charAt(0) ?? 'U'}
      </div>
    {/if}

    <div class="flex-1 min-w-0">
      {#if replyTo}
        <div class="reply-preview">
          <div class="reply-preview__bar"></div>
          <div class="reply-preview__content">
            <div class="reply-preview__meta">
              <span class="text-small text-light">Reply to</span>
              <span class="text-small text-bold">{replyTo.userDisplayName || replyTo.userName}</span>
            </div>
            <div class="text-small text-light reply-preview__text">
              {replyTo.subject || replyTo.content}
            </div>
          </div>
        </div>
      {/if}
      <div class="flex items-baseline spacing-sm margin-bottom-xs">
        <span class="text-bold">{message.userDisplayName || message.userName}</span>
        <span class="text-small text-light">{formatTime(message.createdAt)}</span>
        {#if message.updatedAt && message.updatedAt.getTime() !== message.createdAt.getTime()}
          <span class="text-small text-light">(edited)</span>
        {/if}
      </div>

      {#if message.subject}
        <div class="text-base text-bold margin-bottom-xs">{message.subject}</div>
      {/if}

      <div class="text-base margin-bottom-sm message-content" style="white-space: pre-wrap; word-break: break-word;">
        {#each linkifyMessageContent(message.content) as part, i (i)}
          {#if part.type === 'url'}
            <a class="message-link" href={part.href} target="_blank" rel="noopener noreferrer">{part.value}</a>
          {:else}
            {part.value}
          {/if}
        {/each}
      </div>

      {#if childTopics.length > 0}
        <div class="message-child-topics">
          <span class="text-small text-light">Started child topic</span>
          <div class="child-topic-list">
            {#each childTopics as child (child.id)}
              <div class="child-topic-entry">
                <button
                  type="button"
                  class="child-topic-chip"
                  onclick={() => navigateToChildTopic(child)}
                >
                  {child.title}
                </button>
                <button
                  type="button"
                  class="child-topic-preview-button"
                  onclick={() => openChildTopicPreview(child)}
                >
                  Preview
                </button>
              </div>
            {/each}
          </div>
        </div>
      {/if}

      {#if message.attachments.length > 0}
        <div class="divider margin-top-sm margin-bottom-sm"></div>
        <div class="spacing-xs">
          {#each message.attachments as attachment (attachment.id)}
            <a
              href={attachment.url}
              class="flex items-center spacing-sm text-small text-primary hoverable"
              download
              title={attachment.fileName}
              style="text-decoration: none; transition: color var(--transition-fast);"
            >
              <span style="overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">{attachment.fileName}</span>
              <span class="text-small text-light">({formatFileSize(attachment.size)})</span>
            </a>
          {/each}
        </div>
      {/if}

      {#if message.reactions && message.reactions.length > 0}
        <div class="flex flex-wrap spacing-xs margin-top-sm">
          {#each message.reactions as reaction (reaction.emoji)}
            <button
              class="badge badge-primary clickable"
              style="padding: var(--spacing-xs) var(--spacing-sm); display: flex; align-items: center; gap: var(--spacing-xs);"
            >
              <span>{reaction.emoji}</span>
              <span class="text-light">{reaction.userIds.length}</span>
            </button>
          {/each}
        </div>
      {/if}
    </div>

      <button
        onclick={(e) => {
          e.stopPropagation();
          handleContextMenu(e as unknown as MouseEvent);
        }}
        class="button clickable message-options-button"
        title="Options"
      >
        ⋮
      </button>
  </div>
</div>

{#if showContextMenu}
  <ContextMenu
    items={contextMenuItems}
    x={contextMenuX}
    y={contextMenuY}
    onClose={() => (showContextMenu = false)}
  />
{/if}

{#if isPreviewOpen}
  <Modal
    isOpen={isPreviewOpen}
    title={`Preview: ${previewTopic?.title ?? 'Child topic'}`}
    size="xlarge"
    onClose={closeChildTopicPreview}
  >
    <div class="child-topic-preview">
      {#if isPreviewLoading}
        <div class="preview-loading">
          <LoadingSpinner message="Loading preview..." />
        </div>
      {:else if previewError}
        <p class="text-error text-small">{previewError}</p>
      {:else if previewMessages.length === 0}
        <p class="text-small text-light">No messages yet in this child topic.</p>
      {:else}
        <div class="preview-message-list">
          {#each previewMessages as msg (msg.id)}
            <div class="card hoverable">
              <div class="flex items-start gap-3">
                {#if msg.userAvatar}
                  <img
                    src={msg.userAvatar}
                    alt={msg.userName}
                    class="avatar avatar-xs bg-primary"
                  />
                {:else}
                  <div class="avatar avatar-xs bg-primary text-white">
                    {msg.userName?.charAt(0) ?? 'U'}
                  </div>
                {/if}

                <div class="flex-1 min-w-0">
                  <div class="flex items-baseline spacing-sm margin-bottom-xs">
                    <span class="text-bold">{msg.userDisplayName || msg.userName || 'Unknown'}</span>
                    <span class="text-small text-light">{formatTime(msg.createdAt)}</span>
                  </div>

                  {#if msg.subject}
                    <div class="text-base text-bold margin-bottom-xs">{msg.subject}</div>
                  {/if}

                  <div class="text-base margin-bottom-sm message-content" style="white-space: pre-wrap; word-break: break-word;">
                    {msg.content}
                  </div>
                </div>
              </div>
            </div>
          {/each}
        </div>
      {/if}
    </div>
  </Modal>
{/if}

<style>
  .reply-preview {
    display: flex;
    gap: var(--spacing-sm);
    padding: var(--spacing-xs) var(--spacing-sm);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    background-color: var(--color-surface);
    margin-bottom: var(--spacing-sm);
  }

  .reply-preview__bar {
    width: 3px;
    border-radius: 2px;
    background-color: var(--color-primary);
    flex-shrink: 0;
  }

  .reply-preview__content {
    min-width: 0;
    flex: 1;
  }

  .reply-preview__meta {
    display: flex;
    gap: var(--spacing-xs);
    align-items: baseline;
    margin-bottom: 2px;
  }

  .reply-preview__text {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .message-options-button {
    padding: var(--spacing-xs);
    opacity: 0;
    transition: opacity var(--transition-fast);
  }

  .card:hover .message-options-button {
    opacity: 1;
  }

  .card:target {
    outline: 2px solid var(--color-primary);
    outline-offset: 2px;
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--color-primary) 12%, transparent);
  }

  .message-link {
    color: var(--color-primary);
    text-decoration: underline;
    text-underline-offset: 2px;
    overflow-wrap: anywhere;
    word-break: break-word;
  }

  .message-child-topics {
    margin-top: var(--spacing-xs);
    display: flex;
    flex-direction: column;
    gap: var(--spacing-xs);
    padding: var(--spacing-xs) var(--spacing-sm);
    border-radius: var(--border-radius-sm);
    background-color: color-mix(in srgb, var(--color-primary) 6%, var(--color-surface));
  }

  .child-topic-list {
    display: flex;
    flex-wrap: wrap;
    gap: var(--spacing-xs);
  }

  .child-topic-chip {
    border: none;
    border-radius: var(--border-radius-md);
    background-color: var(--color-background);
    padding: 4px 8px;
    font-size: var(--font-size-xs);
    color: var(--color-text);
    cursor: pointer;
    transition: background-color var(--transition-fast);
  }

  .child-topic-chip:hover {
    background-color: color-mix(in srgb, var(--color-primary) 15%, var(--color-surface));
  }

  .card-has-child-topic {
    border-color: var(--color-primary);
    background-color: color-mix(in srgb, var(--color-primary) 6%, var(--color-surface));
  }

  .child-topic-entry {
    display: flex;
    align-items: center;
    gap: var(--spacing-xs);
  }

  .child-topic-preview-button {
    border: none;
    border-radius: var(--border-radius-md);
    background-color: var(--color-background);
    padding: 4px 8px;
    font-size: var(--font-size-xs);
    color: var(--color-text-light);
    cursor: pointer;
    transition: background-color var(--transition-fast);
  }

  .child-topic-preview-button:hover {
    background-color: color-mix(in srgb, var(--color-primary) 15%, var(--color-surface));
  }

  .child-topic-preview {
    min-height: 400px;
    display: flex;
    flex-direction: column;
  }

  .preview-loading {
    display: flex;
    justify-content: center;
    align-items: center;
    flex: 1;
  }

  .preview-message-list {
    display: flex;
    flex-direction: column;
    gap: var(--spacing-md);
    overflow-y: auto;
    max-height: 600px;
    padding: var(--spacing-md);
    background-color: var(--color-surface);
    border-radius: var(--border-radius-sm);
  }
</style>
