<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { getMessagesByTopic, messagesLoading, updateMessageOrder } from '$lib/stores/messages';
  import { selectedTopic } from '$lib/stores/topics';
  import { onMount } from 'svelte';
  import { api } from '$lib/api/client';

  let messagesContainer: HTMLDivElement | undefined = $state();
  let draggedMessageId: string | null = $state(null);
  let dragOverMessageId: string | null = $state(null);

  let topicMessages = $derived.by(() => {
    return $selectedTopic ? getMessagesByTopic($selectedTopic.id) : [];
  });

  onMount(() => {
    const scrollToBottom = () => {
      if (messagesContainer && !draggedMessageId) {
        setTimeout(() => {
          messagesContainer?.scrollTo(0, messagesContainer.scrollHeight);
        }, 0);
      }
    };

    scrollToBottom();

    const observer = new MutationObserver(scrollToBottom);
    if (messagesContainer) {
      observer.observe(messagesContainer, {
        childList: true,
        subtree: true,
      });
    }

    return () => observer.disconnect();
  });

  function handleDragStart(messageId: string, e: DragEvent) {
    if (!e.dataTransfer) return;
    draggedMessageId = messageId;
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/html', '');
  }

  function handleDragOver(messageId: string, e: DragEvent) {
    if (!draggedMessageId) return;
    e.preventDefault();
    if (e.dataTransfer) {
      e.dataTransfer.dropEffect = 'move';
    }
    dragOverMessageId = messageId;
  }

  function handleDrop(targetMessageId: string, e: DragEvent) {
    if (!draggedMessageId || !e.dataTransfer) return;
    e.preventDefault();

    if (draggedMessageId === targetMessageId) {
      draggedMessageId = null;
      dragOverMessageId = null;
      return;
    }

    // Find the indices of the messages
    const draggedIndex = topicMessages.findIndex((m) => m.id === draggedMessageId);
    const targetIndex = topicMessages.findIndex((m) => m.id === targetMessageId);

    if (draggedIndex === -1 || targetIndex === -1) return;

    // Create new sorted array
    const newMessages = [...topicMessages];
    const [draggedMessage] = newMessages.splice(draggedIndex, 1);
    newMessages.splice(targetIndex, 0, draggedMessage);

    // Update store
    updateMessageOrder(newMessages);

    // Send to API
    if ($selectedTopic) {
      const tenant = api.getCurrentTenant();
      api.post(`/${tenant}/api/Message/reorder`, {
        topicId: $selectedTopic.id,
        messageIds: newMessages.map((m) => m.id),
      });
    }

    draggedMessageId = null;
    dragOverMessageId = null;
  }

  function handleDragEnd() {
    draggedMessageId = null;
    dragOverMessageId = null;
  }
</script>

<div
  bind:this={messagesContainer}
  class="flex-1 overflow-y-auto p-6 space-y-4 bg-white"
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
    {#each topicMessages as message (message.id)}
      <div
        class="transition-all duration-200 {draggedMessageId === message.id
          ? 'opacity-50 scale-95'
          : ''} {dragOverMessageId === message.id
          ? 'border-l-4 border-primary pl-3 bg-primary bg-opacity-5'
          : ''}"
        draggable={true}
        on:dragstart={(e) => handleDragStart(message.id, e)}
        on:dragover={(e) => handleDragOver(message.id, e)}
        on:drop={(e) => handleDrop(message.id, e)}
        on:dragend={handleDragEnd}
        on:dragleave={() => (dragOverMessageId = null)}
        role="button"
        tabindex="0"
      >
        <div class="flex items-start gap-2 group">
          <div class="pt-1 text-text-light text-xs cursor-move opacity-0 group-hover:opacity-100 transition-opacity duration-200 select-none">
            ⋮
          </div>
          <div class="flex-1">
            <MessageItem {message} />
          </div>
        </div>
      </div>
    {/each}
  {/if}
</div>
