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
    class="flex items-center gap-1 px-2 py-1 rounded hover:bg-surface transition-colors cursor-pointer group"
    on:click={selectTopic}
    on:contextmenu={handleContextMenu}
    style="padding-left: {paddingLeft}px"
  >
    {#if node.children.length > 0}
      <button
        on:click|stopPropagation={toggleExpand}
        class="p-0.5 hover:bg-primary-light rounded transition-colors"
        title={node.isExpanded ? 'Collapse' : 'Expand'}
      >
        <span class="text-sm transition-transform {node.isExpanded ? 'rotate-90' : ''}"
          >▶</span
        >
      </button>
    {:else}
      <div class="w-4"></div>
    {/if}

    <div class="flex-1 min-w-0">
      <div
        class="text-sm truncate {$selectedTopic?.id === node.id
          ? 'font-semibold text-primary'
          : 'text-text'}"
      >
        {node.title}
      </div>
    </div>

    {#if node.unreadCount > 0}
      <span class="text-xs bg-error text-white rounded-full px-2 py-0.5">
        {node.unreadCount}
      </span>
    {/if}

    <button
      on:click|stopPropagation={handleContextMenu}
      class="p-1 opacity-0 group-hover:opacity-100 text-text-light hover:text-primary rounded hover:bg-surface transition-all"
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
  :global(.text-primary-light) {
    background-color: rgba(74, 144, 226, 0.1);
  }
</style>
