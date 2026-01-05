<script lang="ts">
  import { topicList, addTopic, toggleTopicExpansion, selectedTopic, setSelectedTopic, createTopicParentId } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import { ui } from '$lib/stores/ui';
  import { api, getCurrentTenant } from '$lib/api/client';
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
  let isLoadingChildren = $state(false);

  const paddingLeft = level * 16 + 8;

  function normalizeTopic(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    return {
      id,
      roomId: raw?.roomId ?? raw?.RoomId ?? '',
      title: raw?.title ?? raw?.Title ?? '',
      description: raw?.description ?? raw?.Description,
      parentId: raw?.parentId ?? raw?.ParentId ?? null,
      childIds: raw?.childIds ?? raw?.ChildIds ?? [],
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : new Date(),
      creatorId: raw?.creatorId ?? raw?.CreatorId ?? '',
      messageCount: raw?.messageCount ?? raw?.MessageCount ?? 0,
      unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
      userPermission: raw?.userPermission ?? raw?.UserPermission ?? 'read',
      permissions: raw?.permissions ?? raw?.Permissions ?? [],
      isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
      tags: raw?.tags ?? raw?.Tags ?? [],
      hasChildren: raw?.hasChildren ?? raw?.HasChildren ?? false,
    };
  }

  async function fetchChildTopics() {
    if (!$currentRoom || isLoadingChildren) return;

    isLoadingChildren = true;
    try {
      const tenant = getCurrentTenant();
      const response = await api.get<any[]>(`/${tenant}/api/Topic/parent/${node.id}`);
      const childTopics = Array.isArray(response) ? response.map(normalizeTopic) : [];

      // Add child topics to store if not already present
      childTopics.forEach((topic) => {
        if (!$topicList.find((t) => t.id === topic.id)) {
          addTopic(topic);
        }
      });
    } catch (err) {
      console.error('Failed to fetch child topics:', err);
    } finally {
      isLoadingChildren = false;
    }
  }

  async function toggleExpand() {
    if (!node.isExpanded) {
      // Expanding - load child topics from backend
      if (node.hasChildren) {
        await fetchChildTopics();
      }
    }

    toggleTopicExpansion(node.id);
  }

  function selectTopic() {
    const topic = $topicList.find((t) => t.id === node.id);
    if (topic) {
      setSelectedTopic(topic);
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

  function openCreateChildTopicModal() {
    createTopicParentId.set(node.id);
    const modal: ModalConfig = {
      id: 'topic-create',
      title: 'Create Topic',
      type: 'custom',
    };
    ui.openModal(modal);
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

<div class="topic-item" style="--topic-level: {level}">
  <div
    class="topic-header {$selectedTopic?.id === node.id ? 'topic-header-active' : ''}"
    on:click={selectTopic}
    on:contextmenu={handleContextMenu}
    role="button"
    tabindex="0"
  >
    {#if node.hasChildren}
      <button
        on:click|stopPropagation={toggleExpand}
        class="topic-toggle-button"
        title={node.isExpanded ? 'Collapse' : 'Expand'}
        aria-expanded={node.isExpanded}
      >
        <span class="toggle-icon {node.isExpanded ? 'toggle-icon-open' : ''}">▶</span>
      </button>
    {:else}
      <div class="toggle-spacer"></div>
    {/if}

    <div class="topic-content">
      <div class="text-small">
        {node.title}
      </div>
    </div>

    {#if node.unreadCount > 0}
      <span class="badge badge-error">
        {node.unreadCount}
      </span>
    {/if}

    <button
      on:click|stopPropagation={openCreateChildTopicModal}
      class="button clickable topic-add-button"
      title="Add child topic"
    >
      +
    </button>

    <button
      on:click|stopPropagation={handleContextMenu}
      class="button clickable topic-options-button"
      title="Options"
    >
      ⋮
    </button>
  </div>

  {#if node.isExpanded && node.children.length > 0}
    <div class="topic-children">
      {#each node.children as childNode (childNode.id)}
        <svelte:self node={childNode} level={level + 1} />
      {/each}
    </div>
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
  .topic-item {
    margin: 0;
    padding: 0;
    /* Add left border for hierarchy visualization */
    border-left: 2px solid var(--color-border);
    padding-left: calc(var(--topic-level) * 16px);
  }

  .topic-item[style*="--topic-level: 0"] {
    border-left: none;
    padding-left: 0;
  }

  .topic-header {
    display: flex;
    align-items: center;
    gap: var(--spacing-sm);
    padding: var(--spacing-xs) var(--spacing-sm);
    cursor: pointer;
    transition: background-color var(--transition-fast);
    border-radius: var(--border-radius-sm);
    user-select: none;
    min-width: max-content;
  }

  .topic-header:hover {
    background-color: var(--color-surface);
  }

  .topic-header-active {
    background-color: color-mix(in srgb, var(--color-primary) 5%, var(--color-background));
  }

  .topic-toggle-button {
    padding: 2px 4px;
    background-color: transparent;
    border: none;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .topic-toggle-button:hover {
    background-color: rgba(74, 144, 226, 0.1);
  }

  .toggle-icon {
    font-size: var(--font-size-sm);
    transition: transform var(--transition-fast);
    display: inline-block;
    color: var(--color-text-light);
  }

  .toggle-icon-open {
    transform: rotate(90deg);
  }

  .toggle-spacer {
    width: 24px;
  }

  .topic-content {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .topic-children {
    margin: 0;
    padding: 0;
  }

  .topic-add-button {
    padding: var(--spacing-xs);
    background-color: transparent;
    border: none;
    color: var(--color-text-light);
    opacity: 0;
    transition: opacity var(--transition-fast);
    font-weight: 600;
  }

  .topic-header:hover .topic-add-button {
    opacity: 1;
  }

  .topic-add-button:hover {
    color: var(--color-primary);
    background-color: color-mix(in srgb, var(--color-primary) 10%, transparent);
  }

  .topic-options-button {
    padding: var(--spacing-xs);
    background-color: transparent;
    border: none;
    color: var(--color-text-light);
    opacity: 0;
    transition: opacity var(--transition-fast);
  }

  .topic-header:hover .topic-options-button {
    opacity: 1;
  }

  .topic-options-button:hover {
    color: var(--color-primary);
    background-color: var(--color-surface);
  }
</style>
