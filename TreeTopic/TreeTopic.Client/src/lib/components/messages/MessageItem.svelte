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
  class="rounded-lg border border-border bg-surface p-3 hover:shadow-md transition-shadow group"
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
      <div class="flex items-baseline gap-2 mb-1">
        <span class="font-semibold text-text">{message.userDisplayName || message.userName}</span>
        <span class="text-xs text-text-light">{formatTime(message.createdAt)}</span>
        {#if message.updatedAt && message.updatedAt.getTime() !== message.createdAt.getTime()}
          <span class="text-xs text-text-light">(edited)</span>
        {/if}
      </div>

      {#if message.subject}
        <div class="font-semibold text-base text-text mb-1">{message.subject}</div>
      {/if}

      <div class="text-text whitespace-pre-wrap break-words mb-2">{message.content}</div>

      {#if message.attachments.length > 0}
        <div class="mt-2 space-y-1 border-t border-border pt-2">
          {#each message.attachments as attachment (attachment.id)}
            <a
              href={attachment.url}
              class="flex items-center gap-2 text-sm text-primary hover:text-primary-hover transition-colors"
              download
              title={attachment.fileName}
            >
              <span>📎</span>
              <span class="truncate">{attachment.fileName}</span>
              <span class="text-xs text-text-light">({formatFileSize(attachment.size)})</span>
            </a>
          {/each}
        </div>
      {/if}

      {#if message.reactions && message.reactions.length > 0}
        <div class="mt-2 flex flex-wrap gap-1">
          {#each message.reactions as reaction (reaction.emoji)}
            <button
              class="px-2 py-1 text-xs bg-primary-light rounded-full hover:bg-primary-hover-light transition-colors flex items-center gap-1"
            >
              <span>{reaction.emoji}</span>
              <span class="text-text-light">{reaction.userIds.length}</span>
            </button>
          {/each}
        </div>
      {/if}
    </div>

    {#if message.canEdit || message.canDelete}
      <button
        on:click={handleContextMenu}
        class="p-1 opacity-0 group-hover:opacity-100 text-text-light hover:text-primary rounded hover:bg-white transition-all"
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
  :global(.bg-primary-light) {
    background-color: rgba(74, 144, 226, 0.1);
  }

  :global(.hover\:bg-primary-hover-light:hover) {
    background-color: rgba(74, 144, 226, 0.2);
  }
</style>
