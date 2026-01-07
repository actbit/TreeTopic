<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { messageList, messagesLoading } from '$lib/stores/messages';
  import { selectedTopic } from '$lib/stores/topics';
  import { formatFileSize } from '$lib/utils/validation';
  import { page } from '$app/stores';
  import { getMessageAnchorIdFromHash, scrollToMessageAnchor } from '$lib/utils/messageAnchor';

  let messagesContainer: HTMLDivElement | undefined = $state();
  let selectedDoc = $state<string | null>(null);
  let targetAnchorId = $derived.by(() => getMessageAnchorIdFromHash($page.url.hash));

  let topicMessages = $derived.by(() => {
    if (!$selectedTopic) return [];
    return $messageList.filter((m) => m.topicId === $selectedTopic.id);
  });

  let messagesWithDocuments = $derived.by(() => {
    return topicMessages.filter((msg) => msg.attachments.length > 0);
  });

  let documentGroups = $derived.by(() => {
    const grouped: Record<string, typeof topicMessages> = {};

    messagesWithDocuments.forEach((msg) => {
      msg.attachments.forEach((attachment) => {
        const docKey = `${attachment.fileName}|${attachment.id}`;
        if (!grouped[docKey]) {
          grouped[docKey] = [];
        }
        grouped[docKey].push(msg);
      });
    });

    return Object.entries(grouped).map(([key, messages]) => {
      const [fileName] = key.split('|');
      const firstAttachment = messagesWithDocuments
        .find((m) => m.attachments.some((a) => a.fileName === fileName))
        ?.attachments.find((a) => a.fileName === fileName);

      return {
        key,
        fileName,
        size: firstAttachment?.size || 0,
        messages,
        count: messages.length,
      };
    });
  });

  let filteredMessages = $derived.by(() => {
    if (!selectedDoc) return [];
    const [fileName] = selectedDoc.split('|');
    return topicMessages.filter((msg) =>
      msg.attachments.some((a) => a.fileName === fileName)
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
  <!-- Document list sidebar -->
  <div class="w-56 border-r border-border overflow-y-auto bg-surface">
    <div class="sticky top-0 bg-surface border-b border-border p-4">
      <h3 class="font-semibold text-text">Documents ({documentGroups.length})</h3>
    </div>

    {#if documentGroups.length === 0}
      <div class="p-4 text-center text-text-light">
        <p class="text-sm">No documents attached</p>
      </div>
    {:else}
      <div class="space-y-1 p-2">
        {#each documentGroups as { key, fileName, size, count }}
          <button
            on:click={() => (selectedDoc = selectedDoc === key ? null : key)}
            class="w-full flex flex-col items-start gap-2 p-3 rounded transition-colors {selectedDoc === key
              ? 'bg-primary bg-opacity-10 border-l-4 border-primary'
              : 'hover:bg-white'}"
          >
            <div class="flex items-center gap-3 w-full min-w-0">
              <p class="text-sm font-medium text-text truncate">{fileName}</p>
            </div>
            <p class="text-xs text-text-light pl-6">
              {size > 0 ? formatFileSize(size) : ''} • {count} message{count !== 1 ? 's' : ''}
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
    {:else if selectedDoc}
      <div class="border-b border-border p-4 bg-white">
        <h3 class="text-lg font-semibold text-text">
          {selectedDoc.split('|')[0]}
        </h3>
        <p class="text-sm text-text-light">
          {filteredMessages.length} reference{filteredMessages.length !== 1 ? 's' : ''}
        </p>
      </div>

      <div class="flex-1 overflow-y-auto p-4 space-y-3">
        {#each filteredMessages as message (message.id)}
          <MessageItem {message} />
        {/each}
      </div>
    {:else if messagesWithDocuments.length === 0}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">No documents</p>
          <p class="text-sm">Messages with attachments will appear here</p>
        </div>
      </div>
    {:else}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">Select a document</p>
          <p class="text-sm">Click on a document to view its messages</p>
        </div>
      </div>
    {/if}
  </div>
</div>
