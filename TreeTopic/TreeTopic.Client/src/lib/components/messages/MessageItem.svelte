<script lang="ts">
  import { formatDate, formatTime } from '$lib/utils/date';
  import { formatFileSize } from '$lib/utils/validation';
  import ContextMenu from '../common/ContextMenu.svelte';
  import type { ContextMenuItem } from '../common/ContextMenu.svelte';
  import type { Message } from '$lib/stores/messages';
  import { messageList, startReply } from '$lib/stores/messages';
  import { ui } from '$lib/stores/ui';
  import type { ModalConfig } from '$lib/types/ui';
  import { getMessageAnchorId } from '$lib/utils/messageAnchor';

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

  let replyTo = $derived.by(() => {
    if (!message.replyToId) return null;
    return $messageList.find((m) => m.id === message.replyToId) ?? null;
  });

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
  class="card hoverable"
  on:contextmenu|stopPropagation={handleContextMenu}
>
  <div class="flex items-start gap-3">
    {#if message.userAvatar}
      <img
        src={message.userAvatar}
        alt={message.userName}
        class="w-8 h-8 rounded-full flex-shrink-0 bg-primary"
      />
    {:else}
      <div
        class="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center text-xs font-bold flex-shrink-0"
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
        on:click|stopPropagation={handleContextMenu}
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
</style>
