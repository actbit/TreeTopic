<script lang="ts">
  import Button from '../common/Button.svelte';
  import { roomList, currentRoom, setCurrentRoom, currentRoomUser } from '$lib/stores/rooms';
  import { ui, modals } from '$lib/stores/ui';
  import type { ModalConfig } from '$lib/types/ui';
  import { get } from 'svelte/store';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { currentUser } from '$lib/stores/auth';

  let isOpen = $state(false);
  let { navigateOnSelect = true }: { navigateOnSelect?: boolean } = $props();

  function openCreateModal() {
    const modal: ModalConfig = {
      id: 'room-create',
      title: 'Create Room',
      type: 'custom',
    };
    ui.openModal(modal);
    isOpen = false;
  }

  function selectRoom(roomId: string) {
    const room = get(roomList).find((r) => r.id === roomId);
    if (room) {
      setCurrentRoom(room);
      if (navigateOnSelect) {
        syncRoomToUrl(room.id);
      }
    }
    isOpen = false;
  }

  function syncRoomToUrl(roomId: string | null) {
    const tenant = $page.params.tenant;
    if (!tenant || !roomId) return;
    const target = `/${tenant}/room/${roomId}`;
    if ($page.url.pathname !== target) {
      goto(target, { replaceState: false, keepFocus: true, noScroll: true });
    }
  }

  function openRoomSettings(roomId: string, e: Event) {
    e.stopPropagation();
    const modal: ModalConfig = {
      id: 'room-settings',
      title: 'Room Settings',
      type: 'custom',
    };
    ui.openModal(modal);
  }

  function openUserSettings(roomId: string, e: Event) {
    e.stopPropagation();
    modals.open('user-setting', 'User Settings', { roomId });
  }
</script>

<div class="flex items-center gap-md room-selector-container">
  <div class="relative flex-1 room-selector-wrapper">
    <button
      onclick={() => (isOpen = !isOpen)}
      class="button button-primary w-full room-selector-button"
    >
      <span>{$currentRoom?.name ?? 'Select Room'}</span>
      <span class="dropdown-arrow {isOpen ? 'dropdown-arrow-open' : ''}"
        >▼</span
      >
    </button>

    {#if isOpen}
      <div class="card room-dropdown">
        <div class="panel-header sticky top-0">
          <Button
            onclick={openCreateModal}
            variant="secondary"
            size="small"
            fullWidth
          >
            + New Room
          </Button>
        </div>

        <div class="list">
          {#each ($roomList || []).filter(r => r?.id) as room (room.id)}
            <div
              class="list-item clickable hoverable"
              role="button"
              tabindex="0"
              onclick={() => selectRoom(room.id)}
              onkeydown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  selectRoom(room.id);
                }
              }}
            >
              <div class="room-item-content">
                <div class="text-bold">{room.name}</div>
                {#if room.description}
                  <div class="text-small text-light">{room.description}</div>
                {/if}
                <div class="text-small text-light margin-top-xs">
                  {room.memberCount} member{room.memberCount !== 1 ? 's' : ''}
                  {#if room.unreadCount > 0}
                    · <span class="badge badge-error">{room.unreadCount} unread</span>
                  {/if}
                </div>
              </div>

              {#if room.canEdit}
                <button
                  onclick={(e) => openRoomSettings(room.id, e)}
                  class="button clickable room-settings-button"
                  title="Room settings"
                >
                  ⚙
                </button>
              {/if}
            </div>
          {/each}
        </div>
      </div>
    {/if}
  </div>

  <!-- User settings button -->
  {#if $currentRoomUser}
    <button
      type="button"
      class="user-settings-button"
      onclick={(e) => openUserSettings($currentRoom?.id?.toString() ?? '', e)}
      onkeydown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          openUserSettings($currentRoom?.id?.toString() ?? '', e);
        }
      }}
      title="User Settings"
      aria-label="Open User Settings"
    >
      {#if $currentRoomUser.iconUrl}
        <img
          src={$currentRoomUser.iconUrl}
          alt={$currentRoomUser.displayName}
          class="user-avatar"
        />
      {:else}
        <div class="user-avatar-placeholder">
          {$currentRoomUser.displayName?.charAt(0) ?? 'U'}
        </div>
      {/if}
      <span class="user-display-name">{$currentRoomUser.displayName}</span>
    </button>
  {:else if $currentUser}
    <button
      type="button"
      class="user-settings-button"
      onclick={(e) => openUserSettings($currentRoom?.id?.toString() ?? '', e)}
      onkeydown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          openUserSettings($currentRoom?.id?.toString() ?? '', e);
        }
      }}
      title="User Settings"
      aria-label="Open User Settings"
    >
      {#if $currentUser.avatar}
        <img
          src={$currentUser.avatar}
          alt={$currentUser.displayName}
          class="user-avatar"
        />
      {:else}
        <div class="user-avatar-placeholder">
          {$currentUser.displayName?.charAt(0) ?? 'U'}
        </div>
      {/if}
      <span class="user-display-name">{$currentUser.displayName}</span>
    </button>
  {/if}
</div>

<style>
  .room-selector-container {
    width: 100%;
  }

  .room-selector-wrapper {
    min-width: 0;
  }

  .room-selector-button {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }

  .dropdown-arrow {
    transition: transform var(--transition-fast);
  }

  .dropdown-arrow-open {
    transform: rotate(180deg);
  }

  .room-dropdown {
    position: absolute;
    top: 100%;
    left: 0;
    right: 0;
    margin-top: var(--spacing-sm);
    z-index: 50;
    max-height: 384px;
    overflow-y: auto;
  }

  .room-item-content {
    flex: 1;
  }

  .room-settings-button {
    padding: var(--spacing-xs);
    background-color: transparent;
    border: none;
    color: var(--color-text-light);
  }

  .room-settings-button:hover {
    background-color: var(--color-error);
    background-color: color-mix(in srgb, var(--color-error) 10%, transparent);
    color: var(--color-error);
  }

  .user-settings-button {
    display: flex;
    align-items: center;
    gap: var(--spacing-sm);
    padding: var(--spacing-xs) var(--spacing-sm);
    background: none;
    border: none;
    border-radius: var(--border-radius-md);
    cursor: pointer;
    transition: background-color 0.2s ease;
  }

  .user-settings-button:hover {
    background-color: var(--color-surface);
  }

  .user-avatar {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    object-fit: cover;
  }

  .user-avatar-placeholder {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    background-color: var(--color-primary);
    color: white;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.75rem;
    font-weight: 600;
  }

  .user-display-name {
    font-size: 0.875rem;
    font-weight: 500;
    max-width: 120px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
