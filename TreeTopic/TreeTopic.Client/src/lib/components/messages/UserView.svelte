<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import LoadingSpinner from '../common/LoadingSpinner.svelte';
  import { messageList, messagesLoading } from '$lib/stores/messages';
  import { selectedTopic } from '$lib/stores/topics';
  import { page } from '$app/stores';
  import { getMessageAnchorIdFromHash, scrollToMessageAnchor } from '$lib/utils/messageAnchor';

  let messagesContainer: HTMLDivElement | undefined = $state();
  let selectedUser = $state<string | null>(null);
  let targetAnchorId = $derived.by(() => getMessageAnchorIdFromHash($page.url.hash));

  let topicMessages = $derived.by(() => {
    if (!$selectedTopic) return [];
    return $messageList.filter((m) => m.topicId === $selectedTopic.id);
  });

  let messagesByUser = $derived.by(() => {
    const grouped: Record<string, typeof topicMessages> = {};

    topicMessages.forEach((msg) => {
      const userName = msg.userDisplayName || msg.userName;
      if (!grouped[userName]) {
        grouped[userName] = [];
      }
      grouped[userName].push(msg);
    });

    // Sort by message count (descending)
    return Object.entries(grouped)
      .sort((a, b) => b[1].length - a[1].length)
      .map(([user, messages]) => ({
        user,
        messages,
        count: messages.length,
        avatar: messages[0]?.userAvatar,
      }));
  });

  let filteredMessages = $derived.by(() => {
    if (!selectedUser) return topicMessages;
    return topicMessages.filter((msg) => (msg.userDisplayName || msg.userName) === selectedUser);
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
  <!-- User list sidebar -->
  <div class="w-48 border-r border-border overflow-y-auto bg-surface">
    <div class="sticky top-0 bg-surface border-b border-border p-4">
      <h3 class="font-semibold text-text">Users ({messagesByUser.length})</h3>
    </div>

    <div class="space-y-1 p-2">
      {#each messagesByUser as { user, count, avatar }}
        <button
          on:click={() => (selectedUser = selectedUser === user ? null : user)}
          class="w-full flex items-center gap-3 p-3 rounded transition-colors {selectedUser === user
            ? 'bg-primary bg-opacity-10 border-l-4 border-primary'
            : 'hover:bg-white'}"
        >
          {#if avatar}
            <img
              src={avatar}
              alt={user}
              class="w-8 h-8 rounded-full flex-shrink-0"
            />
          {:else}
            <div
              class="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center text-xs font-bold flex-shrink-0"
            >
              {user?.charAt(0) ?? 'U'}
            </div>
          {/if}

          <div class="flex-1 min-w-0 text-left">
            <p class="text-sm font-medium text-text truncate">{user}</p>
            <p class="text-xs text-text-light">{count} message{count !== 1 ? 's' : ''}</p>
          </div>
        </button>
      {/each}
    </div>
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
    {:else if selectedUser}
      <div class="border-b border-border p-4 bg-white">
        <h3 class="text-lg font-semibold text-text">Messages from {selectedUser}</h3>
        <p class="text-sm text-text-light">
          {filteredMessages.length} message{filteredMessages.length !== 1 ? 's' : ''}
        </p>
      </div>

      <div class="flex-1 overflow-y-auto p-4 space-y-3">
        {#each filteredMessages as message (message.id)}
          <MessageItem {message} />
        {/each}
      </div>
    {:else if topicMessages.length === 0}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">No messages yet</p>
          <p class="text-sm">Select a user to view their messages</p>
        </div>
      </div>
    {:else}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-text-light">
          <p class="text-lg font-semibold mb-2">Select a user</p>
          <p class="text-sm">Click on a user to view their messages</p>
        </div>
      </div>
    {/if}
  </div>
</div>
