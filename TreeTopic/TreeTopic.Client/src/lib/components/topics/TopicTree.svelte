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

<div class="flex flex-col gap-2 h-full">
  <div class="flex items-center justify-between px-2 mb-2">
    <h3 class="font-semibold text-text">Topics</h3>
    <button
      on:click={openCreateTopicModal}
      class="p-1 text-text-light hover:text-primary rounded hover:bg-surface transition-colors"
      title="Create topic"
    >
      +
    </button>
  </div>

  <div class="flex-1 overflow-y-auto space-y-1">
    {#each $topicTree as node (node.id)}
      <TopicNode {node} />
    {/each}

    {#if $topicTree.length === 0}
      <div class="text-center text-text-light text-sm py-8">
        <p>No topics yet</p>
        <button
          on:click={openCreateTopicModal}
          class="mt-2 px-3 py-1 bg-primary text-white rounded text-xs font-semibold hover:bg-primary-hover transition-colors"
        >
          Create First Topic
        </button>
      </div>
    {/if}
  </div>
</div>

<style>
  div {
    min-height: 0;
  }
</style>
