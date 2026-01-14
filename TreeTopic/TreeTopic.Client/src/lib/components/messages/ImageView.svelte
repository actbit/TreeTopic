<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { messageList, messagesLoading } from '$lib/stores/messages';
  import { selectedTopic } from '$lib/stores/topics';
  import { page } from '$app/stores';
  import { getMessageAnchorIdFromHash, scrollToMessageAnchor } from '$lib/utils/messageAnchor';

  let messagesContainer: HTMLDivElement | undefined = $state();
  let selectedImage = $state<string | null>(null);
  let targetAnchorId = $derived.by(() => getMessageAnchorIdFromHash($page.url.hash));

  let topicMessages = $derived.by(() => {
    if (!$selectedTopic) return [];
    return $messageList.filter((m) => m.topicId === $selectedTopic.id);
  });

  let messagesWithImages = $derived.by(() => {
    return topicMessages.filter((msg) =>
      msg.attachments.some((a) => a.fileType === 'image')
    );
  });

  let imageGroups = $derived.by(() => {
    const grouped: Record<string, typeof topicMessages> = {};

    messagesWithImages.forEach((msg) => {
      msg.attachments
        .filter((a) => a.fileType === 'image')
        .forEach((attachment) => {
          const imgKey = attachment.id;
          if (!grouped[imgKey]) {
            grouped[imgKey] = [];
          }
          grouped[imgKey].push(msg);
        });
    });

    return Object.entries(grouped).map(([id, messages]) => {
      const attachment = messagesWithImages
        .flatMap((m) => m.attachments)
        .find((a) => a.id === id && a.fileType === 'image');

      return {
        id,
        fileName: attachment?.fileName || 'Unknown',
        url: attachment?.url || '',
        messages,
      };
    });
  });

  let filteredMessages = $derived.by(() => {
    if (!selectedImage) return [];
    return topicMessages.filter((msg) =>
      msg.attachments.some((a) => a.id === selectedImage && a.fileType === 'image')
    );
  });

  $effect(() => {
    if ($messagesLoading) return;
    if (!targetAnchorId) return;
    setTimeout(() => {
      scrollToMessageAnchor(targetAnchorId, 'auto');
    }, 0);
  });
</script>

<div class="flex h-full bg-white">
  <!-- Image gallery sidebar -->
  <div class="w-40 border-r border-border overflow-y-auto bg-surface">
    <div class="sticky top-0 bg-surface border-b border-border p-4">
      <h3 class="font-semibold text-text text-sm">Images ({imageGroups.length})</h3>
    </div>

    {#if imageGroups.length === 0}
      <div class="p-4 text-center text-text-light">
        <p class="text-sm">No images attached</p>
      </div>
    {:else}
      <div class="grid grid-cols-2 gap-2 p-2">
        {#each imageGroups as { id, fileName, url }}
          <button
            onclick={() => (selectedImage = selectedImage === id ? null : id)}
            class="aspect-square rounded border-2 overflow-hidden transition-all {selectedImage === id
              ? 'border-primary shadow-md'
              : 'border-border hover:border-primary'}"
            title={fileName}
          >
            <img
              src={url}
              alt={fileName}
              class="w-full h-full object-cover"
              loading="lazy"
            />
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
    {:else if selectedImage}
      <div class="border-b border-border p-4 bg-white">
        <h3 class="text-lg font-semibold text-text">
          {imageGroups.find((g) => g.id === selectedImage)?.fileName}
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
    {:else if messagesWithImages.length === 0}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">No images</p>
          <p class="text-sm">Messages with image attachments will appear here</p>
        </div>
      </div>
    {:else}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">Select an image</p>
          <p class="text-sm">Click on an image to view its messages</p>
        </div>
      </div>
    {/if}
  </div>
</div>
