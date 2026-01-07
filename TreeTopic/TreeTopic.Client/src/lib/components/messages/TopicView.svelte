<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { messages, messageList, messagesLoading } from '$lib/stores/messages';
  import { topicList } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import { page } from '$app/stores';
  import { getMessageAnchorIdFromHash, scrollToMessageAnchor } from '$lib/utils/messageAnchor';

  let messagesContainer: HTMLDivElement | undefined = $state();
  let selectedTopic = $state<string | null>(null);
  let targetAnchorId = $derived.by(() => getMessageAnchorIdFromHash($page.url.hash));

  let roomTopics = $derived.by(() => {
    if (!$currentRoom) return [];
    return $topicList.filter((t) => t.roomId === $currentRoom?.id);
  });

  let topicsWithMessages = $derived.by(() => {
    return roomTopics
      .map((topic) => ({
        topic,
        messageCount: $messageList.filter((m) => m.topicId === topic.id).length,
      }))
      .filter((item) => item.messageCount > 0)
      .sort((a, b) => b.messageCount - a.messageCount);
  });

  let filteredMessages = $derived.by(() => {
    if (!selectedTopic) return [];
    return $messageList.filter((m) => m.topicId === selectedTopic);
  });

  $effect(() => {
    if ($messagesLoading) return;
    if (!targetAnchorId) return;
    setTimeout(() => {
      scrollToMessageAnchor(targetAnchorId, 'auto');
    }, 0);
  });
</script>

<div class="flex h-full bg-surface">
  <!-- Topic list sidebar -->
  <div class="panel view-sidebar">
    <div class="panel-header sticky top-0">
      <h3 class="panel-title">Topics ({topicsWithMessages.length})</h3>
    </div>

    {#if topicsWithMessages.length === 0}
      <div class="padding-md text-center text-light">
        <p class="text-small">No topics with messages</p>
      </div>
    {:else}
      <div class="list">
        {#each topicsWithMessages as { topic, messageCount }}
          <button
            on:click={() => (selectedTopic = selectedTopic === topic.id ? null : topic.id)}
            class="list-item clickable hoverable {selectedTopic === topic.id ? 'list-item-active' : ''}"
          >
            <div class="flex flex-col w-full" style="min-width: 0;">
              <p class="text-small text-bold" style="overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">{topic.title}</p>
              <p class="text-small text-light">
                {messageCount} message{messageCount !== 1 ? 's' : ''}
              </p>
            </div>
          </button>
        {/each}
      </div>
    {/if}
  </div>

  <!-- Messages area -->
  <div
    bind:this={messagesContainer}
    class="flex-1 flex flex-col overflow-hidden"
  >
    {#if $messagesLoading}
      <div class="flex items-center justify-center h-full">
        <LoadingSpinner message="Loading messages..." />
      </div>
    {:else if selectedTopic}
      <div class="panel-header">
        <h3 class="text-large text-bold">
          {topicsWithMessages.find((item) => item.topic.id === selectedTopic)?.topic.title}
        </h3>
        <p class="text-small text-light">
          {filteredMessages.length} message{filteredMessages.length !== 1 ? 's' : ''}
        </p>
      </div>

      <div class="flex-1 overflow-y-auto padding-md spacing-md">
        {#each filteredMessages as message (message.id)}
          <MessageItem {message} />
        {/each}
      </div>
    {:else if topicsWithMessages.length === 0}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-light">
          <p class="text-large text-bold margin-bottom-sm">No topics</p>
          <p class="text-small">Create a topic to get started</p>
        </div>
      </div>
    {:else}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-light">
          <p class="text-large text-bold margin-bottom-sm">Select a topic</p>
          <p class="text-small">Click on a topic to view its messages</p>
        </div>
      </div>
    {/if}
  </div>
</div>

<style>
  .view-sidebar {
    width: 224px;
    border-right: 1px solid var(--color-border);
    overflow-y: auto;
  }
</style>
