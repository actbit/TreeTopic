<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { messages, getMessagesByTopic, messagesLoading } from '$lib/stores/messages';
  import { topics } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';

  let messagesContainer: HTMLDivElement | undefined = $state();
  let selectedTopic = $state<string | null>(null);

  let roomTopics = $derived.by(() => {
    if (!$currentRoom) return [];
    return $topics.filter((t) => t.roomId === $currentRoom?.id);
  });

  let topicsWithMessages = $derived.by(() => {
    return roomTopics
      .map((topic) => ({
        topic,
        messageCount: $messages.filter((m) => m.topicId === topic.id).length,
      }))
      .filter((item) => item.messageCount > 0)
      .sort((a, b) => b.messageCount - a.messageCount);
  });

  let filteredMessages = $derived.by(() => {
    if (!selectedTopic) return [];
    return getMessagesByTopic(selectedTopic);
  });
</script>

<div class="flex h-full bg-white">
  <!-- Topic list sidebar -->
  <div class="w-56 border-r border-border overflow-y-auto bg-surface">
    <div class="sticky top-0 bg-surface border-b border-border p-4">
      <h3 class="font-semibold text-text">Topics ({topicsWithMessages.length})</h3>
    </div>

    {#if topicsWithMessages.length === 0}
      <div class="p-4 text-center text-text-light">
        <p class="text-sm">No topics with messages</p>
      </div>
    {:else}
      <div class="space-y-1 p-2">
        {#each topicsWithMessages as { topic, messageCount }}
          <button
            on:click={() => (selectedTopic = selectedTopic === topic.id ? null : topic.id)}
            class="w-full flex flex-col items-start gap-2 p-3 rounded transition-colors {selectedTopic === topic.id
              ? 'bg-primary bg-opacity-10 border-l-4 border-primary'
              : 'hover:bg-white'}"
          >
            <div class="flex items-center gap-2 w-full min-w-0">
              <span class="text-lg flex-shrink-0">📌</span>
              <p class="text-sm font-medium text-text truncate">{topic.title}</p>
            </div>
            <p class="text-xs text-text-light pl-6">
              {messageCount} message{messageCount !== 1 ? 's' : ''}
            </p>
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
      <div class="border-b border-border p-4 bg-white">
        <h3 class="text-lg font-semibold text-text">
          {topicsWithMessages.find((item) => item.topic.id === selectedTopic)?.topic.title}
        </h3>
        <p class="text-sm text-text-light">
          {filteredMessages.length} message{filteredMessages.length !== 1 ? 's' : ''}
        </p>
      </div>

      <div class="flex-1 overflow-y-auto p-4 space-y-3">
        {#each filteredMessages as message (message.id)}
          <MessageItem {message} />
        {/each}
      </div>
    {:else if topicsWithMessages.length === 0}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">No topics</p>
          <p class="text-sm">Create a topic to get started</p>
        </div>
      </div>
    {:else}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">Select a topic</p>
          <p class="text-sm">Click on a topic to view its messages</p>
        </div>
      </div>
    {/if}
  </div>
</div>
