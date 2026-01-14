<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { messageList, messagesLoading } from '$lib/stores/messages';
  import { selectedTopic } from '$lib/stores/topics';
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import { getMessageAnchorIdFromHash, scrollToMessageAnchor } from '$lib/utils/messageAnchor';

  let messagesContainer: HTMLDivElement | undefined = $state();
  let didScrollToAnchor = $state(false);

  let targetAnchorId = $derived.by(() => getMessageAnchorIdFromHash($page.url.hash));
  let topicMessages = $derived.by(() => {
    return $selectedTopic
      ? $messageList.filter((m) => m.topicId === $selectedTopic.id)
      : [];
  });

  function tryScrollToTarget(behavior: ScrollBehavior = 'auto'): boolean {
    if (!targetAnchorId) return false;
    if (didScrollToAnchor) return true;
    const ok = scrollToMessageAnchor(targetAnchorId, behavior);
    if (ok) didScrollToAnchor = true;
    return ok;
  }

  onMount(() => {
    // Auto-scroll to bottom when new messages arrive
    const scrollToBottom = () => {
      // If URL targets a message, don't force scroll-to-bottom (it fights deep links).
      if (targetAnchorId) {
        tryScrollToTarget('auto');
        return;
      }
      if (messagesContainer) {
        setTimeout(() => {
          messagesContainer?.scrollTo(0, messagesContainer.scrollHeight);
        }, 0);
      }
    };

    // Initial scroll
    if (!tryScrollToTarget('auto')) scrollToBottom();

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

  // If user navigates back/forward to a different hash, allow re-scrolling to the new target.
  $effect(() => {
    didScrollToAnchor = false;
    if (!targetAnchorId) return;
    if ($messagesLoading) return;
    setTimeout(() => {
      tryScrollToTarget('auto');
    }, 0);
  });
</script>

<div
  bind:this={messagesContainer}
  class="flex-1 overflow-y-auto padding-md spacing-md bg-surface"
>
  {#if $messagesLoading}
    <div class="flex items-center justify-center h-full">
      <LoadingSpinner message="Loading messages..." />
    </div>
  {:else if topicMessages.length === 0}
    <div class="flex items-center justify-center h-full">
      <div class="text-center text-light">
        <p class="text-large text-bold margin-bottom-sm">No messages yet</p>
        <p class="text-small">Start the conversation by sending a message</p>
      </div>
    </div>
  {:else}
    {#each topicMessages as message (message.id)}
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
