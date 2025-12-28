<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { getMessagesByTopic, messagesLoading } from '$lib/stores/messages';
  import { selectedTopic } from '$lib/stores/topics';
  import { onMount } from 'svelte';

  let messagesContainer: HTMLDivElement | undefined = $state();
  let topicMessages = $derived.by(() => {
    return $selectedTopic ? getMessagesByTopic($selectedTopic.id) : [];
  });

  onMount(() => {
    // Auto-scroll to bottom when new messages arrive
    const scrollToBottom = () => {
      if (messagesContainer) {
        setTimeout(() => {
          messagesContainer?.scrollTo(0, messagesContainer.scrollHeight);
        }, 0);
      }
    };

    // Initial scroll
    scrollToBottom();

    // Watch for changes
    const observer = new MutationObserver(scrollToBottom);
    if (messagesContainer) {
      observer.observe(messagesContainer, {
        childList: true,
        subtree: true,
      });
    }

    return () => observer.disconnect();
  });
</script>

<div
  bind:this={messagesContainer}
  class="flex-1 overflow-y-auto p-4 space-y-3 bg-white"
>
  {#if $messagesLoading}
    <div class="flex items-center justify-center h-full">
      <LoadingSpinner message="Loading messages..." />
    </div>
  {:else if $topicMessages.length === 0}
    <div class="flex items-center justify-center h-full">
      <div class="text-center text-text-light">
        <p class="text-lg font-semibold mb-2">No messages yet</p>
        <p class="text-sm">Start the conversation by sending a message</p>
      </div>
    </div>
  {:else}
    {#each $topicMessages as message (message.id)}
      <MessageItem {message} />
    {/each}
  {/if}
</div>

<style>
  div {
    display: flex;
    flex-direction: column;
  }
</style>
