<script lang="ts">
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { auth, isAuthenticated } from '$lib/stores/auth';
  import { currentRoom, setRooms, setCurrentRoom } from '$lib/stores/rooms';
  import { selectedTopic, setTopics } from '$lib/stores/topics';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import RoomSelector from '$lib/components/rooms/RoomSelector.svelte';
  import RoomCreateModal from '$lib/components/rooms/RoomCreateModal.svelte';
  import RoomSettingsModal from '$lib/components/rooms/RoomSettingsModal.svelte';
  import TopicTree from '$lib/components/topics/TopicTree.svelte';
  import TopicCreateModal from '$lib/components/topics/TopicCreateModal.svelte';
  import MessageInput from '$lib/components/messages/MessageInput.svelte';
  import MessagesView from '$lib/components/messages/MessagesView.svelte';
  import ViewModeSelector from '$lib/components/messages/ViewModeSelector.svelte';
  import MaterialList from '$lib/components/files/MaterialList.svelte';
  import FileUploadModal from '$lib/components/files/FileUploadModal.svelte';
  import { ui } from '$lib/stores/ui';
  import { api, getCurrentTenant } from '$lib/api/client';

  let isLoading = $state(true);
  let loadError = $state<string | null>(null);

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

  async function loadTenantData() {
    isLoading = true;
    loadError = null;

    try {
      const tenant = $page.params.tenant ?? getCurrentTenant();
      if (!tenant) {
        throw new Error('Tenant is missing');
      }
      await auth.fetchCurrentUser(tenant);

      const response = await api.get<any[]>(`/${tenant}/api/Room`);
      const rooms = Array.isArray(response) ? response.map(normalizeRoom) : [];
      setRooms(rooms);

      const roomId = $page.params.roomId;
      const initialRoom =
        rooms.find((room) => room.id === roomId) ?? rooms[0] ?? null;
      setCurrentRoom(initialRoom);

      // Load root topics for the current room (children are loaded on demand)
      if (initialRoom) {
        try {
          const topicsResponse = await api.get<any[]>(`/${tenant}/api/Topic/room/${initialRoom.id}/root`);
          const topics = Array.isArray(topicsResponse) ? topicsResponse.map(normalizeTopic) : [];
          setTopics(topics);
        } catch (err) {
          console.error('Failed to load root topics:', err);
        }
      }

      if (initialRoom && initialRoom.id !== roomId) {
        goto(`/${tenant}/room/${initialRoom.id}`, {
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
  <FileUploadModal />
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
