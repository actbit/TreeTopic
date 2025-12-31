<script lang="ts">
  import Button from '../common/Button.svelte';
  import { roomList, currentRoom, setCurrentRoom } from '$lib/stores/rooms';
  import { ui } from '$lib/stores/ui';
  import type { ModalConfig } from '$lib/types/ui';
  import { get } from 'svelte/store';

  let isOpen = $state(false);

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
    }
    isOpen = false;
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
</script>

<div class="flex items-center justify-between room-selector-container">
  <div class="relative w-full">
    <button
      on:click={() => (isOpen = !isOpen)}
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
            on:click={openCreateModal}
            variant="secondary"
            size="small"
            fullWidth
          >
            + New Room
          </Button>
        </div>

        <div class="list">
          {#each $roomList as room (room.id)}
            <div
              class="list-item clickable hoverable"
              on:click={() => selectRoom(room.id)}
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
                  on:click={(e) => openRoomSettings(room.id, e)}
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
</div>

<style>
  .room-selector-container {
    width: 100%;
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
</style>
