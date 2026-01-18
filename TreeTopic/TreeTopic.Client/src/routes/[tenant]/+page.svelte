<script lang="ts">
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { auth, isAuthenticated } from '$lib/stores/auth';
  import { roomList, setRooms, setCurrentRoom } from '$lib/stores/rooms';
  import { ui } from '$lib/stores/ui';
  import RoomCreateModal from '$lib/components/rooms/RoomCreateModal.svelte';
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

  async function loadTenantData() {
    isLoading = true;
    loadError = null;
    let tenant: string | null = null;

    try {
      tenant = $page.params.tenant ?? getCurrentTenant();
      if (!tenant) {
        throw new Error('Tenant is missing');
      }
      await auth.fetchCurrentUser(tenant);

      const response = await api.get<any[]>(`/${tenant}/api/Room`);
      const rooms = Array.isArray(response) ? response.map(normalizeRoom) : [];
      setRooms(rooms);
    } catch (error) {
      const resolvedTenant = tenant ?? ($page.params.tenant ?? getCurrentTenant());
      if (
        resolvedTenant &&
        error instanceof api.ApiError &&
        (error.status === 401 || error.status === 403)
      ) {
        auth.logout();
        redirectToTenantLogin(resolvedTenant);
        return;
      }
      loadError = error instanceof Error ? error.message : 'Failed to load rooms';
    } finally {
      isLoading = false;
    }
  }

  function openCreateModal() {
    ui.openModal({ id: 'room-create', title: 'Create Room', type: 'custom' });
  }

  function enterRoom(roomId: string) {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return;
    // roomListが初期化されているか確認
    if (!$roomList || $roomList.length === 0) return;
    const room = $roomList.find((r) => r.id === roomId);
    if (room) {
      setCurrentRoom(room);
    }
    goto(`/${tenant}/room/${roomId}`);
  }

  onMount(() => {
    loadTenantData();
  });

  function redirectToTenantLogin(tenant: string) {
    if (typeof window === 'undefined' || !tenant) return;
    const { pathname, search, hash } = window.location;
    const returnUrl = `${pathname}${search}${hash}`;
    window.location.href = `/${tenant}/auth/login?returnUrl=${encodeURIComponent(
      returnUrl
    )}`;
  }
</script>

<svelte:head>
  <title>TreeTopic - Select Room</title>
</svelte:head>

{#if isLoading}
  <div class="flex items-center justify-center h-screen bg-gradient-to-br from-primary to-secondary">
    <div class="text-center text-white">
      <h1 class="text-4xl font-bold mb-4">TreeTopic</h1>
      <p>Loading rooms...</p>
    </div>
  </div>
{:else if $isAuthenticated}
  <div class="min-h-screen flex items-center justify-center bg-background p-8">
    <div class="panel w-full max-w-lg room-select-panel">
      <div class="panel-header">
        <h1 class="panel-title">Select a room</h1>
        <button class="button button-secondary button-small" onclick={openCreateModal}>
          New Room
        </button>
      </div>

      <div class="panel-body">
        {#if loadError}
          <div class="message message-error">{loadError}</div>
        {/if}

        {#if $roomList.length === 0}
          <div class="text-center text-light">
            <p class="text-large text-bold margin-bottom-sm">No rooms yet</p>
            <p class="text-small margin-bottom-md">Create your first room to get started</p>
            <button class="button button-primary" onclick={openCreateModal}>
              Create Room
            </button>
          </div>
        {:else}
          <div class="list">
            {#each $roomList as room (room.id)}
              <button
                class="list-item clickable hoverable w-full text-left"
                onclick={() => enterRoom(room.id)}
              >
                <div class="flex items-center justify-between">
                  <div class="min-w-0">
                    <div class="text-bold text-base">{room.name}</div>
                    {#if room.description}
                      <div class="text-small text-light truncate">{room.description}</div>
                    {/if}
                  </div>
                  {#if room.unreadCount > 0}
                    <span class="badge badge-error">{room.unreadCount}</span>
                  {/if}
                </div>
              </button>
            {/each}
          </div>
        {/if}
      </div>
    </div>
  </div>

  <RoomCreateModal />
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
        onclick={() => {
          const tenant = $page.params.tenant ?? getCurrentTenant();
          const returnUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`;
          window.location.href = `/${tenant}/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
        }}
      >
        Go to login
      </button>
    </div>
  </div>
{/if}

<style>
  .room-select-panel {
    max-width: 512px;
  }
</style>
