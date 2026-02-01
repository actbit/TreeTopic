<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/stores';
  import { onMount } from 'svelte';
  import { topicList, addTopic, toggleTopicExpansion, setSelectedTopic, createTopicParentId, moveTopicParent, updateTopic, expandedTopics } from '$lib/stores/topics';
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
  let hasUnreadChildrenState = $state(false);

  // 未読チェック完了フラグ（常にtrue: 全トピックがAPIでロードされているため）
  let hasCheckedUnread = $derived(true);

  // 子孫を含む全未読数を計算
  function calculateTotalUnreadDescendants(topicId: string): number {
    let total = 0;

    // 直近の子トピックを取得
    const children = $topicList.filter(t => t.parentId === topicId);

    for (const child of children) {
      // 子の自未読を加算
      total += child.unreadCount;
      // 子の子孫の未読を再帰的に加算
      total += calculateTotalUnreadDescendants(child.id);
    }

    return total;
  }

  // 子孫の未読数（派生ステート）
  // $derivedはSvelte 4ストア($topicList)の変更を追跡できないため、$stateで管理
  let descendantsUnreadCount = $state(0);

  function updateDescendantsUnreadCount() {
    const count = calculateTotalUnreadDescendants(node.id);
    descendantsUnreadCount = count;
    hasUnreadChildrenState = count > 0;
    console.log(`[TopicNode ${node.id}] Updated descendantsUnreadCount:`, {
      count,
      hasUnread: hasUnreadChildrenState,
      topicListSize: $topicList.length
    });
  }


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

  // 指定したトピックIDの子トピックを取得
  async function fetchChildrenByParentId(parentId: string): Promise<any[]> {
    const tenant = getCurrentTenant();
    const response = await api.get<any[]>(`/${tenant}/api/Topic/parent/${parentId}`);
    const childTopics = Array.isArray(response) ? response.map(normalizeTopic) : [];

    // 子トピックをストアに追加または更新
    childTopics.forEach((topic) => {
      const existing = $topicList.find((t) => t.id === topic.id);
      if (!existing) {
        addTopic(topic);
      } else {
        // 既存のトピックの場合は未読カウントなどの情報を更新
        updateTopic(topic.id, {
          unreadCount: topic.unreadCount,
          messageCount: topic.messageCount,
          hasChildren: topic.hasChildren,
        });
      }
    });

    return childTopics;
  }

  async function fetchChildTopics() {
    if (!$currentRoom || isLoadingChildren) return;

    isLoadingChildren = true;
    try {
      // 子トピックを取得
      const childTopics = await fetchChildrenByParentId(node.id);

      // 子がいたらhasChildrenをtrueに更新
      if (childTopics.length > 0) {
        updateTopic(node.id, { hasChildren: true });
      }

      // 未読状態をログ出力（$effectで自動更新される）
      if (node.hasChildren) {
        console.log(`[TopicNode ${node.id}] After fetching children:`, {
          childTopics: childTopics.map(c => ({ id: c.id, title: c.title, unreadCount: c.unreadCount })),
          totalDescendantsUnread: descendantsUnreadCount
        });
      }
    } catch (err) {
      console.error('Failed to fetch child topics:', err);
    } finally {
      isLoadingChildren = false;
    }
  }

  async function toggleExpand() {
    if (!node.isExpanded) {
      // Expanding - always load child topics from backend
      await fetchChildTopics();
    }

    toggleTopicExpansion(node.id);
  }

  async function selectTopic() {
    if (!$currentRoom) return;

    const tenant = ($page.params as any)?.tenant ?? getCurrentTenant();
    if (!tenant) return;

    // If already selected, keep selection (don't toggle off).
    if (selectedTopicId === node.id) return;

    // 子トピックを持っている場合、子トピックを読み込んでおく
    if (node.hasChildren) {
      // topicListから子トピックをチェック
      const childTopicsInList = $topicList.filter(t => t.parentId === node.id);
      const needsLoad = childTopicsInList.length === 0 && !isLoadingChildren;

      if (needsLoad) {
        await fetchChildTopics();
      }
    }

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

  // コンポーネントマウント時に子トピックの未読状態をチェック
  onMount(async () => {
    // 子トピックがまだロードされていない場合はロード
    if (node.hasChildren && !isLoadingChildren) {
      const childTopics = $topicList.filter(t => t.parentId === node.id);
      if (childTopics.length === 0) {
        await fetchChildTopics();
      } else {
        // 既に子トピックがロードされている場合は未読状態をチェック
        console.log(`[TopicNode ${node.id}] Initial unread check on mount (children already loaded):`, {
          childTopics: childTopics.map(c => ({ id: c.id, title: c.title, unreadCount: c.unreadCount })),
          totalDescendantsUnread: descendantsUnreadCount
        });
      }
    }

    // $effectが自動的に未読状態を更新するため、手動の更新は不要
  });

  // ノードが展開状態のときに子トピックをロード
  $effect(() => {
    if (node.isExpanded && node.hasChildren) {
      // 子トピックがロードされているかチェック
      const childTopics = $topicList.filter(t => t.parentId === node.id);
      if (childTopics.length === 0 && !isLoadingChildren) {
        // 子トピックをロード（非同期処理はawaitせずに実行）
        fetchChildTopics();
      }
    }
  });

  // $topicListの変更を監視して子孫の未読数を更新
  // Svelte 5の$derivedはSvelte 4ストア($topicList)の変更を追跡できないため、$effectで監視
  $effect(() => {
    // $topicListを参照して変更を監視
    const topicListSnapshot = $topicList;
    updateDescendantsUnreadCount();
  });

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

<div class="topic-node">
  <div class="topic-row" style="--indent-level: {level}">
    <div class="topic-spacer"></div>
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

      {#if node.hasChildren && hasUnreadChildrenState && hasCheckedUnread}
        <span class="child-unread-badge" title="子孫トピックの未読メッセージ">
          {descendantsUnreadCount}
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
  </div>

  {#if node.isExpanded && node.children.length > 0}
    <div class="topic-children">
      {#each node.children.filter(c => c?.id) as childNode (childNode.id)}
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
  .topic-node {
    display: contents;
  }

  .topic-row {
    display: grid;
    grid-template-columns: calc(var(--indent-level) * 8px) 1fr;
  }

  .topic-spacer {
    flex-shrink: 0;
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
    width: 180px;  /* 固定幅 */
    max-width: 180px;
  }

  .topic-header:hover {
    background-color: var(--color-surface);
  }

  .topic-header-active {
    background-color: color-mix(in srgb, var(--color-primary) 5%, var(--color-background));
  }

  .child-unread-badge {
    background-color: var(--color-warning);
    color: var(--color-background);
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border-radius: 12px;
    padding: 2px 6px;
    min-width: 20px;
    height: 20px;
    font-size: 11px;
    font-weight: bold;
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

  .topic-children {
    margin-left: 8px;
    padding-left: 8px;
    border-left: 1px solid var(--color-border);
  }
</style>
