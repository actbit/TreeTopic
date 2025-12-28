<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { getMessagesByTopic, messagesLoading } from '$lib/stores/messages';
  import { selectedTopic } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';

  let messagesContainer: HTMLDivElement | undefined = $state();

  let topicMessages = $derived.by(() => {
    if (!$selectedTopic) return [];
    const messages = getMessagesByTopic($selectedTopic.id);
    // Sort by creation date (newest first)
    return [...messages].sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    );
  });

  let messagesByDate = $derived.by(() => {
    const grouped: Record<string, typeof topicMessages> = {};

    topicMessages.forEach((msg) => {
      const date = new Date(msg.createdAt).toLocaleDateString('en-US', {
        weekday: 'long',
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      });

      if (!grouped[date]) {
        grouped[date] = [];
      }
      grouped[date].push(msg);
    });

    return Object.entries(grouped).reverse();
  });
</script>

<div
  bind:this={messagesContainer}
  class="flex-1 overflow-y-auto p-4 space-y-4 bg-white"
>
  {#if $messagesLoading}
    <div class="flex items-center justify-center h-full">
      <LoadingSpinner message="Loading messages..." />
    </div>
  {:else if topicMessages.length === 0}
    <div class="flex items-center justify-center h-full">
      <div class="text-center text-text-light">
        <p class="text-lg font-semibold mb-2">No messages yet</p>
        <p class="text-sm">Start the conversation by sending a message</p>
      </div>
    </div>
  {:else}
    {#each messagesByDate as [date, messages]}
      <div class="space-y-2">
        <div class="flex items-center gap-4 py-2">
          <div class="flex-1 h-px bg-border"></div>
          <h3 class="text-sm font-semibold text-text-light whitespace-nowrap">{date}</h3>
          <div class="flex-1 h-px bg-border"></div>
        </div>

        <div class="space-y-3">
          {#each messages as message (message.id)}
            <MessageItem {message} />
          {/each}
        </div>
      </div>
    {/each}
  {/if}
</div>

<style>
  div {
    display: flex;
    flex-direction: column;
  }
</style>
