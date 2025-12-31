<script lang="ts">
  import { formatDate, formatTime } from '$lib/utils/date';
  import { formatFileSize } from '$lib/utils/validation';
  import ContextMenu from '../common/ContextMenu.svelte';
  import type { ContextMenuItem } from '../common/ContextMenu.svelte';
  import type { Message } from '$lib/stores/messages';
  import { ui } from '$lib/stores/ui';
  import type { ModalConfig } from '$lib/types/ui';

  interface Props {
    message: Message;
  }

  let { message }: Props = $props();
  let showContextMenu = $state(false);
  let contextMenuX = $state(0);
  let contextMenuY = $state(0);

  function handleContextMenu(e: MouseEvent) {
    e.preventDefault();
    contextMenuX = e.clientX;
    contextMenuY = e.clientY;
    showContextMenu = true;
  }

  function openEditModal() {
    const modal: ModalConfig = {
      id: 'message-edit',
      title: 'Edit Message',
      type: 'custom',
    };
    ui.openModal(modal);
    showContextMenu = false;
  }

  function openDeleteModal() {
    const modal: ModalConfig = {
      id: 'message-delete',
      title: 'Delete Message',
      type: 'custom',
    };
    ui.openModal(modal);
    showContextMenu = false;
  }

  const contextMenuItems: ContextMenuItem[] = [
    {
      id: 'edit',
      label: 'Edit',
      icon: '✏️',
      action: openEditModal,
      isVisible: message.canEdit,
    },
    {
      id: 'delete',
      label: 'Delete',
      icon: '🗑️',
      action: openDeleteModal,
      isDangerous: true,
      isVisible: message.canDelete,
    },
  ];
</script>

<div
  class="card hoverable"
  on:contextmenu={handleContextMenu}
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

      <div class="text-base margin-bottom-sm" style="white-space: pre-wrap; word-break: break-word;">{message.content}</div>

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

    {#if message.canEdit || message.canDelete}
      <button
        on:click={handleContextMenu}
        class="button clickable message-options-button"
        title="Options"
      >
        ⋮
      </button>
    {/if}
  </div>
</div>

{#if showContextMenu}
  <ContextMenu
    items={contextMenuItems.filter((item) => item.isVisible !== false)}
    x={contextMenuX}
    y={contextMenuY}
    onClose={() => (showContextMenu = false)}
  />
{/if}

<style>
  .message-options-button {
    padding: var(--spacing-xs);
    opacity: 0;
    transition: opacity var(--transition-fast);
  }

  .card:hover .message-options-button {
    opacity: 1;
  }
</style>
