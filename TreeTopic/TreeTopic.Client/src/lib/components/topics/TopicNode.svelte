<script lang="ts">
  import { topics, updateTopic, toggleTopicExpansion, selectedTopic } from '$lib/stores/topics';
  import { ui } from '$lib/stores/ui';
  import type { TopicTreeNode } from '$lib/types/ui';
  import type { ModalConfig } from '$lib/types/ui';
  import ContextMenu from '../common/ContextMenu.svelte';
  import type { ContextMenuItem } from '../common/ContextMenu.svelte';

  interface Props {
    node: TopicTreeNode;
    level?: number;
  }

  let { node, level = 0 }: Props = $props();
  let showContextMenu = $state(false);
  let contextMenuX = $state(0);
  let contextMenuY = $state(0);

  const paddingLeft = level * 16 + 8;

  function toggleExpand() {
    toggleTopicExpansion(node.id);
  }

  function selectTopic() {
    const topic = ($topics).find((t) => t.id === node.id);
    if (topic) {
      // selectedTopic.set(topic);
    }
  }

  function handleContextMenu(e: MouseEvent) {
    e.preventDefault();
    contextMenuX = e.clientX;
    contextMenuY = e.clientY;
    showContextMenu = true;
  }

  function openEditModal() {
    const modal: ModalConfig = {
      id: 'topic-edit',
      title: 'Edit Topic',
      type: 'custom',
    };
    ui.openModal(modal);
    showContextMenu = false;
  }

  function openDeleteModal() {
    const modal: ModalConfig = {
      id: 'topic-delete',
      title: 'Delete Topic',
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
      isVisible: node.canWrite,
    },
    {
      id: 'delete',
      label: 'Delete',
      icon: '🗑️',
      action: openDeleteModal,
      isDangerous: true,
      isVisible: node.canDelete,
    },
  ];
</script>

<div>
  <div
    class="list-item clickable hoverable {$selectedTopic?.id === node.id ? 'list-item-active' : ''}"
    on:click={selectTopic}
    on:contextmenu={handleContextMenu}
    style="padding-left: {paddingLeft}px"
  >
    {#if node.children.length > 0}
      <button
        on:click|stopPropagation={toggleExpand}
        class="button clickable topic-expand-button"
        title={node.isExpanded ? 'Collapse' : 'Expand'}
      >
        <span class="topic-expand-arrow {node.isExpanded ? 'topic-expand-arrow-open' : ''}"
          >▶</span
        >
      </button>
    {:else}
      <div class="topic-spacer"></div>
    {/if}

    <div class="topic-content">
      <div class="text-small {$selectedTopic?.id === node.id ? 'text-bold text-primary' : ''}">
        {node.title}
      </div>
    </div>

    {#if node.unreadCount > 0}
      <span class="badge badge-error">
        {node.unreadCount}
      </span>
    {/if}

    <button
      on:click|stopPropagation={handleContextMenu}
      class="button clickable topic-options-button"
      title="Options"
    >
      ⋮
    </button>
  </div>

  {#if node.isExpanded}
    {#each node.children as childNode (childNode.id)}
      <svelte:self node={childNode} level={level + 1} />
    {/each}
  {/if}
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
  .list-item {
    display: flex;
    align-items: center;
    gap: var(--spacing-sm);
  }

  .topic-expand-button {
    padding: 2px;
    background-color: transparent;
    border: none;
  }

  .topic-expand-button:hover {
    background-color: rgba(74, 144, 226, 0.1);
  }

  .topic-expand-arrow {
    font-size: var(--font-size-sm);
    transition: transform var(--transition-fast);
    display: inline-block;
  }

  .topic-expand-arrow-open {
    transform: rotate(90deg);
  }

  .topic-spacer {
    width: 16px;
  }

  .topic-content {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .topic-options-button {
    padding: var(--spacing-xs);
    background-color: transparent;
    border: none;
    color: var(--color-text-light);
    opacity: 0;
    transition: opacity var(--transition-fast);
  }

  .list-item:hover .topic-options-button {
    opacity: 1;
  }

  .topic-options-button:hover {
    color: var(--color-primary);
    background-color: var(--color-surface);
  }
</style>
