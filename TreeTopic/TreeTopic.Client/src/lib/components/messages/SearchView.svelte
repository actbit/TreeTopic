<script lang="ts">
  import MessageItem from './MessageItem.svelte';
  import { messagesLoading, type Message } from '$lib/stores/messages';
  import { selectedTopic } from '$lib/stores/topics';
  import { api } from '$lib/api/client';
  import { page } from '$app/stores';
  import { getMessageAnchorIdFromHash, scrollToMessageAnchor } from '$lib/utils/messageAnchor';

  type SearchMode = 'contains' | 'regex';

  interface RawMaterial {
    id?: string;
    Id?: string;
    fileName?: string;
    FileName?: string;
    fileType?: string;
    FileType?: string;
    size?: number;
    Size?: number;
    url?: string;
    Url?: string;
    createdAt?: string;
    CreatedAt?: string;
    uploadedBy?: string;
    UploadedBy?: string;
  }

  interface RawMessage {
    id?: string;
    Id?: string;
    topicId?: string;
    TopicId?: string;
    roomUserId?: string;
    RoomUserId?: string;
    applicationUserId?: string;
    ApplicationUserId?: string;
    userId?: string;
    UserId?: string;
    userName?: string;
    UserName?: string;
    userDisplayName?: string;
    UserDisplayName?: string;
    userAvatar?: string;
    UserAvatar?: string;
    subject?: string;
    Subject?: string;
    header?: string;
    Header?: string;
    content?: string;
    Content?: string;
    body?: string;
    Body?: string;
    replyToId?: string;
    ReplyToId?: string;
    replyId?: string;
    ReplyId?: string;
    childTopicId?: string;
    ChildTopicId?: string;
    childTopicTitle?: string;
    ChildTopicTitle?: string;
    createdAt?: string;
    CreatedAt?: string;
    updatedAt?: string;
    UpdatedAt?: string;
    files?: RawMaterial[];
    Files?: RawMaterial[];
  }

  let searchQuery = $state('');
  let searchMode = $state<SearchMode>('contains');
  let caseSensitive = $state(false);
  let searchResults = $state<Message[]>([]);
  let isSearching = $state(false);
  let searchError = $state<string | null>(null);
  let messagesContainer: HTMLDivElement | undefined = $state();
  let tenant = $derived.by(() => $page.params.tenant ?? '');
  let targetAnchorId = $derived.by(() => getMessageAnchorIdFromHash($page.url.hash));
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;
  let requestSerial = 0;

  function normalizeMessage(raw: RawMessage): Message {
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;
    const rawFiles = raw?.files ?? raw?.Files ?? [];

    return {
      id: raw?.id ?? raw?.Id ?? '',
      topicId: raw?.topicId ?? raw?.TopicId ?? '',
      userId:
        raw?.roomUserId ??
        raw?.RoomUserId ??
        raw?.applicationUserId ??
        raw?.ApplicationUserId ??
        raw?.userId ??
        raw?.UserId ??
        '',
      userName: raw?.userName ?? raw?.UserName ?? '',
      userDisplayName:
        raw?.userDisplayName ?? raw?.UserDisplayName ?? raw?.userName ?? raw?.UserName ?? '',
      userAvatar: raw?.userAvatar ?? raw?.UserAvatar ?? undefined,
      subject: raw?.subject ?? raw?.Subject ?? raw?.header ?? raw?.Header ?? '',
      content: raw?.content ?? raw?.Content ?? raw?.body ?? raw?.Body ?? '',
      replyToId: raw?.replyToId ?? raw?.ReplyToId ?? raw?.replyId ?? raw?.ReplyId ?? undefined,
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : undefined,
      attachments: Array.isArray(rawFiles)
        ? rawFiles.map((f) => ({
            id: f?.id ?? f?.Id ?? '',
            fileName: f?.fileName ?? f?.FileName ?? '',
            mimeType: f?.fileType ?? f?.FileType ?? 'application/octet-stream',
            size: f?.size ?? f?.Size ?? 0,
            url: f?.url ?? f?.Url ?? '',
            uploadedAt: (f?.createdAt ?? f?.CreatedAt) ? new Date(f.createdAt ?? f.CreatedAt ?? '') : new Date(),
            uploadedBy: f?.uploadedBy ?? f?.UploadedBy ?? '',
          }))
        : [],
      isOwner: false,
      canEdit: false,
      canDelete: false,
      childTopicId: (raw?.childTopicId ?? raw?.ChildTopicId) || undefined,
      childTopicTitle: (raw?.childTopicTitle ?? raw?.ChildTopicTitle) || undefined,
    };
  }

  async function runSearch(
    serial: number,
    topicId: string,
    query: string,
    mode: SearchMode,
    isCaseSensitive: boolean
  ) {
    if (!query || !topicId || !tenant) {
      searchResults = [];
      searchError = null;
      isSearching = false;
      return;
    }

    isSearching = true;
    searchError = null;
    try {
      const result = await api.get<RawMessage[]>(
        `/${tenant}/api/message/topic/${topicId}/search`,
        {
          params: {
            q: query,
            mode,
            caseSensitive: isCaseSensitive,
            take: 200,
          },
          cache: false,
        }
      );

      if (serial !== requestSerial) return;
      searchResults = Array.isArray(result)
        ? result.filter((m) => (m?.id ?? m?.Id)).map(normalizeMessage)
        : [];
    } catch (err) {
      if (serial !== requestSerial) return;
      searchResults = [];
      searchError = err instanceof Error ? err.message : 'Search failed';
    } finally {
      if (serial === requestSerial) {
        isSearching = false;
      }
    }
  }

  $effect(() => {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
      debounceTimer = null;
    }

    requestSerial += 1;
    const serial = requestSerial;
    const query = searchQuery.trim();
    const mode = searchMode;
    const cs = caseSensitive;
    const topicId = $selectedTopic?.id ?? '';

    if (!query) {
      searchResults = [];
      searchError = null;
      isSearching = false;
      return;
    }

    if (!topicId) {
      searchResults = [];
      searchError = null;
      isSearching = false;
      return;
    }

    debounceTimer = setTimeout(() => {
      void runSearch(serial, topicId, query, mode, cs);
    }, 300);
  });

  function clearSearch() {
    searchQuery = '';
    searchResults = [];
    searchError = null;
    isSearching = false;
  }

  $effect(() => {
    if ($messagesLoading || isSearching) return;
    if (!targetAnchorId) return;
    setTimeout(() => {
      scrollToMessageAnchor(targetAnchorId, 'auto');
    }, 0);
  });
</script>

<div class="flex flex-col h-full bg-white">
  <div class="border-b border-border p-4 bg-surface sticky top-0 space-y-3">
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

    <div class="flex items-center gap-4">
      <label class="text-xs text-text-light flex items-center gap-2">
        <span>Mode</span>
        <select bind:value={searchMode} class="px-2 py-1 border border-border rounded text-xs bg-white">
          <option value="contains">Text</option>
          <option value="regex">Regex</option>
        </select>
      </label>

      <label class="text-xs text-text-light flex items-center gap-2">
        <input type="checkbox" bind:checked={caseSensitive} />
        <span>Case sensitive</span>
      </label>
    </div>

    {#if searchQuery.trim()}
      <p class="text-xs text-text-light">
        Found {searchResults.length} result{searchResults.length !== 1 ? 's' : ''}
      </p>
    {/if}
  </div>

  <div bind:this={messagesContainer} class="flex-1 overflow-y-auto p-4">
    {#if $messagesLoading}
      <div class="flex items-center justify-center h-full">
        <div class="text-text-light">Loading...</div>
      </div>
    {:else if isSearching}
      <div class="flex items-center justify-center h-full">
        <div class="text-text-light">Searching...</div>
      </div>
    {:else if searchError}
      <div class="flex items-center justify-center h-full">
        <div class="text-center text-red-600 text-sm">{searchError}</div>
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
          <p class="text-sm">Try different keywords or regex</p>
        </div>
      </div>
    {:else}
      <div class="space-y-3">
        {#each searchResults.filter((m) => m?.id) as message (message.id)}
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
