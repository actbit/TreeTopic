<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/stores';
  import { topicList, addTopic, toggleTopicExpansion, setSelectedTopic, createTopicParentId, moveTopicParent, updateTopic } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import { ui } from '$lib/stores/ui';
  import { api, getCurrentTenant } from '$lib/api/client';
  import type { TopicTreeNode } from '$lib/types/ui';
  import type { ModalConfig } from '$lib/types/ui';
  import ContextMenu from '../common/ContextMenu.svelte';
  import type { ContextMenuItem } from '../common/ContextMenu.svelte';
  import { clearDragData, getDragData, preventDragDefaults, setDragData } from '$lib/utils/dragdrop';
  import TopicNode from './TopicNode.svelte';

  interface Props {
    node: TopicTreeNode;
    level?: number;
    selectedTopicId?: string | null;
  }

  let { node, level = 0, selectedTopicId = null }: Props = $props();
  let showContextMenu = $state(false);
  let contextMenuX = $state(0);
  let contextMenuY = $state(0);
  let isLoadingChildren = $state(false);
  let isDragOver = $state(false);
  let isDraggingSelf = $state(false);


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

  function wouldCreateCycle(topicId: string, proposedParentId: string): boolean {
    if (topicId === proposedParentId) return true;

    let cursorId: string | null = proposedParentId;
    const visited = new Set<string>();
    while (cursorId) {
      if (cursorId === topicId) return true;
      if (visited.has(cursorId)) return true;
      visited.add(cursorId);
      const cursor = $topicList.find((t) => t.id === cursorId);
      cursorId = cursor?.parentId ?? null;
    }

    return false;
  }

  async function refreshHasChildren(topicId: string) {
    try {
      const tenant = getCurrentTenant();
      const updated = await api.get<any>(`/${tenant}/api/Topic/${topicId}`);
      const hasChildren = updated?.hasChildren ?? updated?.HasChildren ?? undefined;
      if (typeof hasChildren === 'boolean') {
        updateTopic(topicId, { hasChildren });
      }
    } catch {
      // ignore
    }
  }

  async function moveTopic(draggedTopicId: string, newParentId: string | null) {
    const dragged = $topicList.find((t) => t.id === draggedTopicId);
    if (!dragged) return;

    if (newParentId && wouldCreateCycle(draggedTopicId, newParentId)) return;

    const tenant = getCurrentTenant();
    const oldParentId = dragged.parentId;

    await api.put(`/${tenant}/api/Topic/${draggedTopicId}`, {
      parentId: newParentId,
    });

    moveTopicParent(draggedTopicId, newParentId);

    if (oldParentId) await refreshHasChildren(oldParentId);
    if (newParentId) await refreshHasChildren(newParentId);
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
    if (!$currentRoom) return;

    const tenant = ($page.params as any)?.tenant ?? getCurrentTenant();
    if (!tenant) return;

    // If already selected, keep selection (don't toggle off).
    if (selectedTopicId === node.id) return;

    goto(`/${tenant}/room/${$currentRoom.id}/topic/${node.id}`, { keepFocus: true, noScroll: true });
  }

  function handleContextMenu(e: MouseEvent) {
    e.preventDefault();
    contextMenuX = e.clientX;
    contextMenuY = e.clientY;
    showContextMenu = true;
  }

  function handleDragStart(e: DragEvent) {
    if (!e.dataTransfer) return;
    const target = e.target as HTMLElement | null;
    if (target?.closest('button')) return;
    isDraggingSelf = true;
    setDragData(e, { type: 'topic', id: node.id });
    e.dataTransfer.effectAllowed = 'move';
  }

  function handleDragEnd() {
    isDraggingSelf = false;
    isDragOver = false;
    clearDragData();
  }

  function handleDragOver(e: DragEvent) {
    const payload = getDragData(e);
    if (!payload || payload.type !== 'topic') return;
    if (payload.id === node.id) return;
    preventDragDefaults(e);
    isDragOver = true;
  }

  function handleDragLeave() {
    isDragOver = false;
  }

  async function handleDrop(e: DragEvent) {
    const payload = getDragData(e);
    if (!payload || payload.type !== 'topic') return;
    preventDragDefaults(e);
    isDragOver = false;
    if (payload.id === node.id) return;

    try {
      await moveTopic(payload.id, node.id);
    } catch (err) {
      console.error('Failed to move topic:', err);
    } finally {
      clearDragData();
    }
  }

  function openEditModal() {
    // Ensure the topic to edit is selected
    const topic = $topicList.find((t) => t.id === node.id);
    if (topic) setSelectedTopic(topic);
    const modal: ModalConfig = {
      id: 'topic-edit',
      title: 'Edit Topic',
      type: 'custom',
      data: { topicId: node.id },
    };
    ui.openModal(modal);
    showContextMenu = false;
  }

  function openDeleteModal() {
    const topic = $topicList.find((t) => t.id === node.id);
    if (topic) setSelectedTopic(topic);
    const modal: ModalConfig = {
      id: 'topic-delete',
      title: 'Delete Topic',
      type: 'custom',
      data: { topicId: node.id },
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

  function getContextMenuItems(): ContextMenuItem[] {
    return [
      {
        id: 'edit',
        label: 'Edit',
        action: openEditModal,
      },
      {
        id: 'delete',
        label: 'Delete',
        action: openDeleteModal,
        isDangerous: true,
      },
    ];
  }
</script>

<div class="topic-item" style="--topic-level: {level}">
  <div
    class="topic-header {selectedTopicId === node.id ? 'topic-header-active' : ''} {isDragOver ? 'topic-header-drop' : ''} {isDraggingSelf ? 'topic-header-dragging' : ''}"
    onclick={selectTopic}
    oncontextmenu={handleContextMenu}
    draggable={true}
    ondragstart={handleDragStart}
    ondragend={handleDragEnd}
    ondragover={handleDragOver}
    ondragleave={handleDragLeave}
    ondrop={handleDrop}
    onkeydown={(e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        selectTopic();
      }
    }}
    role="button"
    tabindex="0"
  >
    {#if node.hasChildren}
      <button
        onclick={(e) => {
          e.stopPropagation();
          toggleExpand();
        }}
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
      onclick={(e) => {
        e.stopPropagation();
        openCreateChildTopicModal();
      }}
      class="button clickable topic-add-button"
      title="Add child topic"
    >
      +
    </button>

    <button
      onclick={(e) => {
        e.stopPropagation();
        handleContextMenu(e as unknown as MouseEvent);
      }}
      class="button clickable topic-options-button"
      title="Options"
    >
      ⋮
    </button>
  </div>

  {#if node.isExpanded && node.children.length > 0}
    <div class="topic-children">
      {#each node.children as childNode (childNode.id)}
        <TopicNode node={childNode} level={level + 1} selectedTopicId={selectedTopicId} />
      {/each}
    </div>
  {/if}
</div>

{#if showContextMenu}
  <ContextMenu
    items={getContextMenuItems()}
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

  .topic-header-drop {
    outline: 2px dashed var(--color-primary);
    outline-offset: 2px;
    background-color: color-mix(in srgb, var(--color-primary) 8%, var(--color-background));
  }

  .topic-header-dragging {
    opacity: 0.5;
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
