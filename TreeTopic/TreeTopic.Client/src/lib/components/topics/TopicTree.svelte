<script lang="ts">
  import TopicNode from './TopicNode.svelte';
  import { topicTree, selectedTopic, topicList, moveTopicParent, updateTopic } from '$lib/stores/topics';
  import { ui } from '$lib/stores/ui';
  import type { ModalConfig } from '$lib/types/ui';
  import type { TopicTreeNode } from '$lib/types/ui';
  import { api, getCurrentTenant } from '$lib/api/client';
  import { clearDragData, getDragData, preventDragDefaults } from '$lib/utils/dragdrop';

  function openCreateTopicModal() {
    const modal: ModalConfig = {
      id: 'topic-create',
      title: 'Create Topic',
      type: 'custom',
    };
    ui.openModal(modal);
  }

  let selectedTopicId = $derived.by(() => $selectedTopic?.id ?? null);
  let isRootDragOver = $state(false);

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

  function handleRootDragOver(e: DragEvent) {
    const payload = getDragData(e);
    if (!payload || payload.type !== 'topic') return;
    preventDragDefaults(e);
    isRootDragOver = true;
  }

  function handleRootDragLeave() {
    isRootDragOver = false;
  }

  async function handleRootDrop(e: DragEvent) {
    const payload = getDragData(e);
    if (!payload || payload.type !== 'topic') return;
    preventDragDefaults(e);
    isRootDragOver = false;

    const dragged = $topicList.find((t) => t.id === payload.id);
    if (!dragged) return;

    const oldParentId = dragged.parentId;

    try {
      const tenant = getCurrentTenant();
      await api.put(`/${tenant}/api/Topic/${payload.id}`, { parentId: null });
      moveTopicParent(payload.id, null);
      if (oldParentId) await refreshHasChildren(oldParentId);
    } catch (err) {
      console.error('Failed to move topic to root:', err);
    } finally {
      clearDragData();
    }
  }
</script>

<div class="panel h-full topic-panel">
  <div class="panel-header">
    <h3 class="panel-title">Topics</h3>
    <button
      onclick={openCreateTopicModal}
      class="button clickable topic-create-button"
      title="Create topic"
    >
      +
    </button>
  </div>

  <div
    class="panel-body overflow-y-auto overflow-x-auto topic-panel-body {isRootDragOver ? 'topic-root-drop' : ''}"
    ondragover={handleRootDragOver}
    ondragleave={handleRootDragLeave}
    ondrop={handleRootDrop}
    role="region"
    aria-label="Topic tree"
  >
    <div class="list topic-tree-list">
      {#each $topicTree as node (node.id)}
        <TopicNode node={node} selectedTopicId={selectedTopicId} />
      {/each}

      {#if $topicTree.length === 0}
        <div class="text-center text-light topic-tree-empty">
          <p>No topics yet</p>
          <button
            onclick={openCreateTopicModal}
            class="button button-primary button-small margin-top-sm"
          >
            Create First Topic
          </button>
        </div>
      {/if}
    </div>
  </div>
</div>

<style>
  .topic-panel {
    display: flex;
    flex-direction: column;
    min-height: 0;
  }

  .topic-panel-body {
    flex: 1;
    min-height: 0;
    padding: var(--spacing-md) var(--spacing-md) var(--spacing-md) 0;
  }

  .topic-tree-list {
    min-height: 100%;
    width: 100%;
    max-width: 100%;
  }

  .topic-root-drop {
    outline: 2px dashed var(--color-primary);
    outline-offset: -6px;
    background-color: color-mix(in srgb, var(--color-primary) 6%, var(--color-background));
  }

  .panel-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }

  .topic-create-button {
    padding: var(--spacing-xs);
    background-color: transparent;
    border: none;
    color: var(--color-text-light);
  }

  .topic-create-button:hover {
    color: var(--color-primary);
    background-color: var(--color-surface);
  }

  .topic-tree-empty {
    padding: var(--spacing-2xl) 0;
  }
</style>
