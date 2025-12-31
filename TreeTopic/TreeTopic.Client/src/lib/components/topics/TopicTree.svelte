<script lang="ts">
  import TopicNode from './TopicNode.svelte';
  import { topicTree, selectedTopic } from '$lib/stores/topics';
  import { ui } from '$lib/stores/ui';
  import type { ModalConfig } from '$lib/types/ui';
  import type { TopicTreeNode } from '$lib/types/ui';

  function openCreateTopicModal() {
    const modal: ModalConfig = {
      id: 'topic-create',
      title: 'Create Topic',
      type: 'custom',
    };
    ui.openModal(modal);
  }
</script>

<div class="panel h-full">
  <div class="panel-header">
    <h3 class="panel-title">Topics</h3>
    <button
      on:click={openCreateTopicModal}
      class="button clickable topic-create-button"
      title="Create topic"
    >
      +
    </button>
  </div>

  <div class="panel-body overflow-y-auto">
    <div class="list">
      {#each $topicTree as node (node.id)}
        <TopicNode {node} />
      {/each}

      {#if $topicTree.length === 0}
        <div class="text-center text-light topic-tree-empty">
          <p>No topics yet</p>
          <button
            on:click={openCreateTopicModal}
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
