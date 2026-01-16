<script lang="ts">
  import { brainstorm } from '$lib/stores/brainstorm';
  import type { BrainstormBoard as BrainstormBoardType } from '$lib/stores/brainstorm';
  import { auth } from '$lib/stores/auth';
  import BrainstormBoard from '$lib/components/brainstorming/BrainstormBoard.svelte';
  import LoadingSpinner from '$lib/components/common/LoadingSpinner.svelte';
  import Button from '$lib/components/common/Button.svelte';
  import RoomUserJoinModal from '$lib/components/rooms/RoomUserJoinModal.svelte';
  import { rooms } from '$lib/stores/rooms';
  import { ui } from '$lib/stores/ui';
  import { api } from '$lib/api/client';
  import { page } from '$app/stores';

  interface PageData {
    boardId: string;
    tenant?: string;
    board?: BrainstormBoardType;
    loadError?: string | null;
  }

  let data: PageData = $props();
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let loadErrorFallback = $state<string | null>(data.loadError ?? null);

  const isInvalidBoardId = (value?: string | null) =>
    !value || value === 'undefined' || value === 'null';
  const isRawGuid = (value?: string | null) =>
    !!value && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);

  const getBoardIdFromPath = () => {
    if (typeof window === 'undefined') return '';
    const segments = window.location.pathname.split('/').filter(Boolean);
    const idx = segments.indexOf('brainstorm');
    if (idx !== -1 && segments.length > idx + 1) {
      return segments[idx + 1] || '';
    }
    return '';
  };

  let resolvedBoardId = $derived.by(() => data.boardId || $page.params.boardId || getBoardIdFromPath());

  let lastHandledBoardId = '';

  $effect(() => {
    const boardId = resolvedBoardId;
    if (!boardId) return;
    if (boardId === lastHandledBoardId) return;
    lastHandledBoardId = boardId;

    if (isInvalidBoardId(boardId)) {
      error = 'Board ID is required';
      isLoading = false;
      return;
    }

    if (data.board) {
      brainstorm.setCurrentBoard(data.board);
      isLoading = false;
      return;
    }

    loadBoard();
  });

  async function loadBoard() {
    let tenant: string | null = null;

    try {
      isLoading = true;
      error = null;

      if (isInvalidBoardId(resolvedBoardId)) {
        throw new Error('Board ID is required');
      }

      tenant =
        data.tenant ||
        api.getCurrentTenant() ||
        (typeof window !== 'undefined' ? window.location.pathname.split('/').filter(Boolean)[0] : '');
      if (!tenant) {
        throw new Error('Tenant is required');
      }
      api.configureApiClient(tenant);
      await auth.fetchCurrentUser(tenant);
      const boardData = (await api.get(
        `/${tenant}/api/Brainstorm/${resolvedBoardId}`
      )) as BrainstormBoardType;
      brainstorm.setCurrentBoard(boardData);
      void ensureRoomUserForTopic(tenant, boardData.topicId);
    } catch (err: unknown) {
      if (tenant && handleUnauthorizedError(err, tenant)) {
        return;
      }

      const errorMessage = err instanceof Error ? err.message : 'Failed to load brainstorm board';
      error = loadErrorFallback && errorMessage === 'Failed to load brainstorm board'
        ? loadErrorFallback
        : errorMessage;
    } finally {
      isLoading = false;
    }
  }

  function buildReturnUrl(): string {
    if (typeof window === 'undefined') return '/';
    const { pathname, search, hash } = window.location;
    return `${pathname}${search}${hash}`;
  }

  function redirectToTenantLogin(tenant: string): void {
    if (!tenant || typeof window === 'undefined') return;
    const returnUrl = buildReturnUrl();
    window.location.href = `/${tenant}/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  function handleUnauthorizedError(error: unknown, tenant: string): boolean {
    if (
      error instanceof api.ApiError &&
      (error.status === 401 || error.status === 403)
    ) {
      auth.logout();
      redirectToTenantLogin(tenant);
      return true;
    }
    return false;
  }

  async function handleRoomUserNotFound(tenant: string, roomId: string): Promise<void> {
    try {
      await auth.fetchCurrentUser(tenant);
      ui.openModal({
        id: 'room-user-join',
        title: 'Set your name',
        type: 'custom',
        data: { roomId },
      });
    } catch (error: unknown) {
      if (handleUnauthorizedError(error, tenant)) {
        return;
      }

      if (error instanceof api.ApiError && error.status === 404) {
        auth.logout();
        redirectToTenantLogin(tenant);
        return;
      }

      console.error('Failed to refresh ApplicationUser after missing RoomUser:', error);
    }
  }

  async function ensureRoomUserForTopic(tenant: string, topicId: string): Promise<void> {
    if (!tenant || !topicId) return;
    let roomId = '';

    try {
      const topic = await api.get<any>(`/${tenant}/api/Topic/${topicId}`);
      roomId = topic?.roomId ?? topic?.RoomId ?? '';
      if (!roomId) return;
    } catch (error: unknown) {
      if (handleUnauthorizedError(error, tenant)) {
        return;
      }

      console.error('Failed to load topic for brainstorm board:', error);
      return;
    }

    try {
      const roomUserData = await api.get<any>(`/${tenant}/api/RoomUsers/room/${roomId}/me`);
      if (roomUserData) {
        rooms.setCurrentRoomUser({
          id: roomUserData.id ?? roomUserData.Id ?? '',
          displayName: roomUserData.displayName ?? roomUserData.DisplayName ?? '',
          iconUrl: roomUserData.iconUrl ?? roomUserData.IconUrl,
          useMainIcon: roomUserData.useMainIcon ?? roomUserData.UseMainIcon ?? false,
        });
      }
    } catch (error: unknown) {
      if (handleUnauthorizedError(error, tenant)) {
        return;
      }

      if (error instanceof api.ApiError && error.status === 404) {
        await handleRoomUserNotFound(tenant, roomId);
        return;
      }

      console.error('Failed to fetch RoomUser for brainstorm board:', error);
    }
  }

  function goBack() {
    window.history.back();
  }
</script>

<svelte:head>
  <title>Brainstorm Board - TreeTopic</title>
</svelte:head>

<div class="brainstorm-page">
  <!-- Header -->
  <header class="brainstorm-header">
    <div class="brainstorm-header__left">
      <button onclick={goBack} class="brainstorm-back" title="Go back">
        Back
      </button>
      <div>
        <h1 class="brainstorm-title">Brainstorm Board</h1>
        <p class="brainstorm-subtitle">Drag cards to organize. Click a card to edit.</p>
      </div>
    </div>

    <div class="brainstorm-header__actions">
      <Button variant="secondary" size="small" onclick={loadBoard}>
        Refresh
      </Button>
      <Button variant="secondary" size="small" onclick={goBack}>
        Close
      </Button>
    </div>
  </header>

  <!-- Content -->
  <div class="brainstorm-content">
    {#if isLoading}
      <div class="flex items-center justify-center h-full">
        <LoadingSpinner message="Loading brainstorm board..." />
      </div>
    {:else if error}
      <div class="flex flex-col items-center justify-center h-full gap-6">
        <div class="text-center">
          <p class="text-xl font-semibold text-text mb-3">Error</p>
          <p class="text-text-light mb-5">{error}</p>
        </div>
        <div class="flex gap-3">
          <Button variant="primary" onclick={loadBoard}>Retry</Button>
          <Button variant="secondary" onclick={goBack}>Go Back</Button>
        </div>
      </div>
    {:else if $brainstorm.currentBoard}
      <BrainstormBoard boardId={$brainstorm.currentBoard?.id ?? data.boardId} />
    {:else}
      <div class="flex flex-col items-center justify-center h-full gap-4">
        <p class="text-text-light">Board not found</p>
        <Button variant="secondary" onclick={goBack}>Go Back</Button>
      </div>
    {/if}
  </div>
</div>

<RoomUserJoinModal />

<style>
  :global(body) {
    overflow: hidden;
    background: #121212;
    color: #e5e7eb;
  }

  .brainstorm-page {
    display: flex;
    flex-direction: column;
    height: 100vh;
    background: #121212;
  }

  .brainstorm-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px 22px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    background: #151515;
  }

  .brainstorm-header__left {
    display: flex;
    align-items: center;
    gap: 16px;
  }

  .brainstorm-back {
    padding: 6px 14px;
    border-radius: 10px;
    background: #2d5d9f;
    color: #e5f0ff;
    border: 1px solid rgba(96, 165, 250, 0.5);
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
  }

  .brainstorm-back:hover {
    background: #3a6db3;
  }

  .brainstorm-title {
    font-size: 20px;
    font-weight: 700;
    margin: 0;
    color: #f8fafc;
  }

  .brainstorm-subtitle {
    font-size: 12px;
    margin: 4px 0 0;
    color: #94a3b8;
  }

  .brainstorm-header__actions {
    display: flex;
    gap: 10px;
  }

  .brainstorm-content {
    flex: 1;
    overflow: hidden;
  }

  :global(.brainstorm-page .btn) {
    background: rgba(255, 255, 255, 0.06);
    border: 1px solid rgba(255, 255, 255, 0.12);
    color: #e2e8f0;
  }

  :global(.brainstorm-page .btn:hover:not(:disabled)) {
    background: rgba(255, 255, 255, 0.12);
  }
</style>
