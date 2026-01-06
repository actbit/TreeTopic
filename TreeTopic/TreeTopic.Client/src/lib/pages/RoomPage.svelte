<script lang="ts">
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { auth, isAuthenticated } from '$lib/stores/auth';
  import { currentRoom, setRooms, setCurrentRoom } from '$lib/stores/rooms';
  import {
    selectedTopic,
    setSelectedTopic,
    setTopics,
    addTopic,
    topicList,
    updateTopic,
    expandedTopics,
    toggleTopicExpansion,
  } from '$lib/stores/topics';
  import { messages, setMessages } from '$lib/stores/messages';
  import { setFiles } from '$lib/stores/files';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import RoomSelector from '$lib/components/rooms/RoomSelector.svelte';
  import RoomCreateModal from '$lib/components/rooms/RoomCreateModal.svelte';
  import RoomSettingsModal from '$lib/components/rooms/RoomSettingsModal.svelte';
  import TopicTree from '$lib/components/topics/TopicTree.svelte';
  import TopicCreateModal from '$lib/components/topics/TopicCreateModal.svelte';
  import TopicEditModal from '$lib/components/topics/TopicEditModal.svelte';
  import TopicDeleteModal from '$lib/components/topics/TopicDeleteModal.svelte';
  import MessageInput from '$lib/components/messages/MessageInput.svelte';
  import MessagesView from '$lib/components/messages/MessagesView.svelte';
  import MessageEditModal from '$lib/components/messages/MessageEditModal.svelte';
  import MessageDeleteModal from '$lib/components/messages/MessageDeleteModal.svelte';
  import ViewModeSelector from '$lib/components/messages/ViewModeSelector.svelte';
  import MaterialList from '$lib/components/files/MaterialList.svelte';
  import FileUploadModal from '$lib/components/files/FileUploadModal.svelte';
  import { ui } from '$lib/stores/ui';
  import { api, getCurrentTenant } from '$lib/api/client';

  let isLoading = $state(true);
  let loadError = $state<string | null>(null);
  let loadedTopicId = $state<string | null>(null);
  let loadRequestId = $state(0);
  let loadedRoomFilesId = $state<string | null>(null);
  let filesLoadRequestId = $state(0);
  let suppressUrlSync = $state(false);
  let lastAppliedUrlTopicId = $state<string | null>(null);

  let urlTopicId = $derived.by(() => $page.url.searchParams.get('topicId'));

  function normalizeRoom(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const name = raw?.name ?? raw?.Name ?? '';
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    return {
      id,
      name,
      description: raw?.description ?? raw?.Description,
      avatar: raw?.avatar ?? raw?.Avatar,
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : new Date(),
      ownerId: raw?.ownerId ?? raw?.OwnerId ?? raw?.createdUserId ?? raw?.CreatedUserId ?? '',
      memberCount: raw?.memberCount ?? raw?.MemberCount ?? 0,
      unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
      isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
      settings: raw?.settings ?? raw?.Settings,
    };
  }

  function normalizeTopic(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    return {
      id,
      roomId: raw?.roomId ?? raw?.RoomId ?? '',
      title: raw?.title ?? raw?.Title ?? '',
      description: raw?.description ?? raw?.Description,
      parentId: raw?.parentId ?? raw?.ParentId ?? null,
      childIds: raw?.childIds ?? raw?.ChildIds ?? [],
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : new Date(),
      creatorId: raw?.creatorId ?? raw?.CreatorId ?? '',
      messageCount: raw?.messageCount ?? raw?.MessageCount ?? 0,
      unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
      userPermission: raw?.userPermission ?? raw?.UserPermission ?? 'read',
      permissions: raw?.permissions ?? raw?.Permissions ?? [],
      isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
      tags: raw?.tags ?? raw?.Tags ?? [],
      hasChildren: raw?.hasChildren ?? raw?.HasChildren ?? false,
    };
  }

  async function ensureTopicPathLoaded(tenant: string, topicId: string) {
    const chain: any[] = [];
    let cursorId: string | null = topicId;
    const visited = new Set<string>();

    while (cursorId && !visited.has(cursorId)) {
      visited.add(cursorId);
      const raw = await api.get<any>(`/${tenant}/api/Topic/${cursorId}`);
      const normalized = normalizeTopic(raw);
      chain.push(normalized);
      cursorId = normalized.parentId ?? null;
    }

    chain.reverse(); // root -> leaf

    for (const t of chain) {
      const existing = $topicList.find((x) => x.id === t.id);
      if (!existing) {
        addTopic(t);
      } else {
        updateTopic(t.id, {
          title: t.title,
          description: t.description,
          parentId: t.parentId,
          roomId: t.roomId,
          hasChildren: t.hasChildren,
          updatedAt: t.updatedAt,
        });
      }
    }

    // Expand ancestors (not the leaf) so the selected topic is visible.
    for (let i = 0; i < chain.length - 1; i++) {
      const id = chain[i].id;
      if (!$expandedTopics.has(id)) toggleTopicExpansion(id);
    }

    return chain[chain.length - 1] ?? null;
  }

  async function selectTopicFromUrl(tenant: string) {
    if (!$currentRoom) return;
    if (!urlTopicId) {
      if ($selectedTopic) {
        suppressUrlSync = true;
        setSelectedTopic(null);
        Promise.resolve().then(() => (suppressUrlSync = false));
      }
      return;
    }

    if ($selectedTopic?.id === urlTopicId) return;

    const existing = $topicList.find((t) => t.id === urlTopicId) ?? null;
    if (existing) {
      if (existing.roomId === $currentRoom.id) {
        suppressUrlSync = true;
        setSelectedTopic(existing);
        Promise.resolve().then(() => (suppressUrlSync = false));
      }
      return;
    }

    try {
      const loaded = await ensureTopicPathLoaded(tenant, urlTopicId);
      if (loaded && loaded.roomId === $currentRoom.id) {
        suppressUrlSync = true;
        setSelectedTopic(loaded);
        Promise.resolve().then(() => (suppressUrlSync = false));
      }
    } catch {
      // ignore
    }
  }

  function normalizeMessage(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    return {
      id,
      topicId: raw?.topicId ?? raw?.TopicId ?? '',
      userId:
        raw?.applicationUserId ?? raw?.ApplicationUserId ?? raw?.userId ?? raw?.UserId ?? '',
      userName: raw?.userName ?? raw?.UserName ?? '',
      userDisplayName:
        raw?.userDisplayName ?? raw?.UserDisplayName ?? raw?.userName ?? raw?.UserName ?? '',
      userAvatar: raw?.userAvatar ?? raw?.UserAvatar ?? undefined,
      subject: raw?.subject ?? raw?.Subject ?? raw?.header ?? raw?.Header ?? '',
      content: raw?.content ?? raw?.Content ?? raw?.body ?? raw?.Body ?? '',
      replyToId: raw?.replyToId ?? raw?.ReplyToId ?? raw?.replyId ?? raw?.ReplyId ?? undefined,
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : undefined,
      attachments: [],
      isOwner: false,
      canEdit: true,
      canDelete: true,
    };
  }

  function normalizeMaterial(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const createdAt = raw?.uploadedAt ?? raw?.UploadedAt ?? raw?.createdAt ?? raw?.CreatedAt ?? null;

    return {
      id,
      roomId: raw?.roomId ?? raw?.RoomId ?? '',
      messageId: raw?.messageId ?? raw?.MessageId ?? undefined,
      fileName: raw?.fileName ?? raw?.FileName ?? raw?.originalFileName ?? raw?.OriginalFileName ?? '',
      originalFileName: raw?.originalFileName ?? raw?.OriginalFileName ?? raw?.fileName ?? raw?.FileName ?? '',
      mimeType: raw?.mimeType ?? raw?.MimeType ?? 'application/octet-stream',
      size: raw?.size ?? raw?.Size ?? 0,
      url: raw?.url ?? raw?.Url ?? '',
      fileType: raw?.fileType ?? raw?.FileType ?? 'other',
      uploadedAt: createdAt ?? new Date().toISOString(),
      uploadedBy: raw?.uploadedBy ?? raw?.UploadedBy ?? '',
      uploadedByName: raw?.uploadedByName ?? raw?.UploadedByName ?? raw?.uploadedBy ?? raw?.UploadedBy ?? 'Unknown',
      versions: raw?.versions ?? raw?.Versions ?? [],
      isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
      tags: raw?.tags ?? raw?.Tags ?? undefined,
      description: raw?.description ?? raw?.Description ?? undefined,
    };
  }

  async function loadTenantData() {
    isLoading = true;
    loadError = null;

    try {
      const tenant = $page.params.tenant ?? getCurrentTenant();
      if (!tenant) throw new Error('Tenant not found in URL');

      api.configureApiClient(tenant);
      await auth.fetchCurrentUser(tenant);

      const response = await api.get<any[]>(`/${tenant}/api/Room`);
      const rooms = Array.isArray(response) ? response.map(normalizeRoom) : [];
      setRooms(rooms);

      const roomId = $page.params.roomId;
      const initialRoom = rooms.find((room) => room.id === roomId) ?? rooms[0] ?? null;
      setCurrentRoom(initialRoom);

      if (initialRoom) {
        try {
          const topicsResponse = await api.get<any[]>(`/${tenant}/api/Topic/room/${initialRoom.id}/root`);
          const topics = Array.isArray(topicsResponse) ? topicsResponse.map(normalizeTopic) : [];
          setTopics(topics);
          await selectTopicFromUrl(tenant);
        } catch (err) {
          console.error('Failed to load root topics:', err);
        }
      }

      if (initialRoom && initialRoom.id !== roomId) {
        const search = $page.url.search;
        goto(`/${tenant}/room/${initialRoom.id}${search}`, {
          replaceState: true,
          keepFocus: true,
          noScroll: true,
        });
      }
    } catch (error) {
      loadError = error instanceof Error ? error.message : 'Failed to load tenant data';
    } finally {
      isLoading = false;
    }
  }

  onMount(() => {
    loadTenantData();
  });

  // Reflect selected topic in the URL (path segment) so it can be bookmarked/shared.
  $effect(() => {
    if (!$currentRoom) return;
    if (suppressUrlSync) return;
    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return;

    const roomId = $currentRoom.id;
    const desiredTopicId = $selectedTopic?.id ?? null;

    const currentRoomId = $page.params.roomId;
    if (currentRoomId !== roomId) return;

    const currentTopicId = $page.url.searchParams.get('topicId');
    if (currentTopicId === desiredTopicId) return;

    const next = new URL($page.url);
    if (desiredTopicId) next.searchParams.set('topicId', desiredTopicId);
    else next.searchParams.delete('topicId');

    goto(`${next.pathname}${next.search}${next.hash}`, { replaceState: true, keepFocus: true, noScroll: true });
  });

  // If URL changes (back/forward) reflect it into selected topic.
  // Important: avoid depending on selectedTopic changes here, otherwise selection can get cleared
  // before the URL-sync effect sets the query param.
  $effect(() => {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return;
    if (!isLoading) {
      if (lastAppliedUrlTopicId === urlTopicId) return;
      lastAppliedUrlTopicId = urlTopicId ?? null;
      selectTopicFromUrl(tenant);
    }
  });

  $effect(() => {
    if (!$currentRoom || !$selectedTopic) return;

    if (loadedTopicId === $selectedTopic.id) return;
    loadedTopicId = $selectedTopic.id;

    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return;

    const requestId = ++loadRequestId;
    messages.setLoading(true);
    messages.setError(null);

    api.get<any[]>(`/${tenant}/api/Message/topic/${$selectedTopic.id}`)
      .then((response) => {
        if (requestId !== loadRequestId) return;
        const list = Array.isArray(response) ? response.map(normalizeMessage) : [];
        setMessages($selectedTopic.id, list);
      })
      .catch((err: unknown) => {
        if (requestId !== loadRequestId) return;
        const msg = err instanceof Error ? err.message : 'Failed to load messages';
        messages.setError(msg);
      })
      .finally(() => {
        if (requestId !== loadRequestId) return;
        messages.setLoading(false);
      });
  });

  $effect(() => {
    if (!$currentRoom) {
      loadedRoomFilesId = null;
      setFiles([]);
      return;
    }

    if (loadedRoomFilesId === $currentRoom.id) return;
    loadedRoomFilesId = $currentRoom.id;

    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return;

    const requestId = ++filesLoadRequestId;
    api.get<any[]>(`/${tenant}/api/File/room/${$currentRoom.id}`)
      .then((response) => {
        if (requestId !== filesLoadRequestId) return;
        const list = Array.isArray(response) ? response.map(normalizeMaterial) : [];
        setFiles(list);
      })
      .catch(() => {
        if (requestId !== filesLoadRequestId) return;
        setFiles([]);
      });
  });
</script>

<svelte:head>
  <title>TreeTopic - Collaborative Discussion</title>
</svelte:head>

{#if isLoading}
  <div class="flex items-center justify-center h-screen bg-gradient-to-br from-primary to-secondary">
    <div class="text-center text-white">
      <h1 class="text-4xl font-bold mb-4">TreeTopic</h1>
      <p>Loading...</p>
    </div>
  </div>
{:else if $isAuthenticated}
  <AppLayout subPanelTitle="Materials">
    <svelte:fragment slot="headerContent">
      <RoomSelector />
    </svelte:fragment>

    <svelte:fragment slot="sidebarContent">
      {#if $currentRoom}
        <div class="panel-header">
          <h3 class="panel-title">Top</h3>
          <span class="text-small text-light">Topics</span>
        </div>
        <TopicTree />
      {:else}
        <div class="p-4 text-center text-text-light">
          <p class="text-sm">Select a room to view topics</p>
        </div>
      {/if}
    </svelte:fragment>

    <svelte:fragment slot="mainContent">
      {#if $currentRoom && $selectedTopic}
        <div class="flex flex-col h-full">
          <div class="border-b border-border p-4 space-y-3">
            <div>
              <h2 class="text-lg font-semibold text-text">{$selectedTopic.title}</h2>
              {#if $selectedTopic.description}
                <p class="text-sm text-text-light mt-1">{$selectedTopic.description}</p>
              {/if}
            </div>
            <div class="pt-2 border-t border-border">
              <ViewModeSelector />
            </div>
          </div>
          <MessagesView />
          <MessageInput />
        </div>
      {:else if $currentRoom}
        <div class="flex items-center justify-center h-full text-center">
          <div>
            <h2 class="text-2xl font-bold text-text mb-2">{$currentRoom.name}</h2>
            <p class="text-text-secondary">Select a topic to view messages</p>
          </div>
        </div>
      {:else}
        <div class="flex items-center justify-center h-full text-center">
          <div>
            <h2 class="text-2xl font-bold text-text mb-2">Welcome to TreeTopic</h2>
            <p class="text-text-secondary">Select a room to get started</p>
            <div class="mt-4">
              <button class="button button-primary" on:click={() => ui.openModal({ id: 'room-create', title: 'Create Room', type: 'custom' })}>
                Create your first room
              </button>
            </div>
          </div>
        </div>
      {/if}
    </svelte:fragment>

    <svelte:fragment slot="subPanelContent">
      <MaterialList />
    </svelte:fragment>
  </AppLayout>

  <RoomCreateModal />
  <RoomSettingsModal />
  <TopicCreateModal />
  <TopicEditModal />
  <TopicDeleteModal />
  <FileUploadModal />
  <MessageEditModal />
  <MessageDeleteModal />
{:else}
  <div class="flex items-center justify-center h-screen bg-gradient-to-br from-primary to-secondary">
    <div class="text-center text-white">
      <h1 class="text-4xl font-bold mb-4">TreeTopic</h1>
      {#if loadError}
        <p class="mb-4">{loadError}</p>
      {:else}
        <p class="mb-4">Not authenticated</p>
      {/if}
      <button
        class="button button-secondary"
        on:click={() => {
          const tenant = $page.params.tenant ?? getCurrentTenant();
          window.location.href = `/${tenant}/login`;
        }}
      >
        Go to login
      </button>
    </div>
  </div>
{/if}
