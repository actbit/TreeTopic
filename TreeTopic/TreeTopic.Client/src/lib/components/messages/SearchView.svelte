<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import { messageList, messagesLoading } from '$lib/stores/messages';
  import { selectedTopic } from '$lib/stores/topics';
  import { ui } from '$lib/stores/ui';
  import { page } from '$app/stores';
  import { getMessageAnchorIdFromHash, scrollToMessageAnchor } from '$lib/utils/messageAnchor';

  let searchQuery = $state('');
  let messagesContainer: HTMLDivElement | undefined = $state();
  let targetAnchorId = $derived.by(() => getMessageAnchorIdFromHash($page.url.hash));

  let topicMessages = $derived.by(() => {
    if (!$selectedTopic) return [];
    return $messageList.filter((m) => m.topicId === $selectedTopic.id);
  });

  let searchResults = $derived.by(() => {
    if (!searchQuery.trim()) return [];

    const query = searchQuery.toLowerCase();
    return topicMessages.filter(
      (msg) =>
        msg.content.toLowerCase().includes(query) ||
        msg.subject?.toLowerCase().includes(query) ||
        msg.userDisplayName?.toLowerCase().includes(query) ||
        msg.userName?.toLowerCase().includes(query)
    );
  });

  function clearSearch() {
    searchQuery = '';
  }

  function highlightText(text: string, query: string): string {
    if (!query.trim()) return text;

    const regex = new RegExp(`(${query})`, 'gi');
    return text.replace(regex, '<mark>$1</mark>');
  }

  $effect(() => {
    if ($messagesLoading) return;
    if (!targetAnchorId) return;
    setTimeout(() => {
      scrollToMessageAnchor(targetAnchorId, 'auto');
    }, 0);
  });
</script>

<div class="flex flex-col h-full bg-white">
  <!-- Search bar -->
  <div class="border-b border-border p-4 bg-surface sticky top-0">
    <div class="relative">
      <input
        type="search"
        bind:value={searchQuery}
        placeholder="Search messages..."
        class="w-full px-4 py-2 pr-10 border border-border rounded-lg text-sm bg-white transition-all
          placeholder:text-text-light
          focus:outline-none focus:border-primary"
      />
      {#if searchQuery}
        <button
          type="button"
          onclick={clearSearch}
          class="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-text-light hover:text-primary transition-colors"
          title="Clear search"
        >
          ✕
        </button>
      {/if}
    </div>

    {#if searchQuery.trim()}
      <p class="text-xs text-text-light mt-2">
        Found {searchResults.length} result{searchResults.length !== 1 ? 's' : ''}
      </p>
    {/if}
  </div>

  <!-- Results -->
  <div
    bind:this={messagesContainer}
    class="flex-1 overflow-y-auto p-4"
  >
    {#if $messagesLoading}
      <div class="flex items-center justify-center h-full">
        <div class="text-text-light">Loading...</div>
      </div>
    {:else if !searchQuery.trim()}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">Search messages</p>
          <p class="text-sm">Enter keywords to find messages</p>
        </div>
      </div>
    {:else if searchResults.length === 0}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">No results found</p>
          <p class="text-sm">Try different keywords</p>
        </div>
      </div>
    {:else}
      <div class="space-y-3">
        {#each searchResults as message (message.id)}
          <div class="space-y-2">
            <div class="text-xs text-text-light">
              {new Date(message.createdAt).toLocaleString()}
            </div>
            <MessageItem {message} />
          </div>
        {/each}
      </div>
    {/if}
  </div>
</div>

<style>
  :global(mark) {
    background-color: #fff3cd;
    color: inherit;
    font-weight: 600;
    padding: 0 2px;
  }
</style>
