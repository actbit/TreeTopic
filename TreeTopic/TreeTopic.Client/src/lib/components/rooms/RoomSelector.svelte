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

<div class="flex items-center justify-between">
  <div class="relative flex-1">
    <button
      on:click={() => (isOpen = !isOpen)}
      class="w-full px-4 py-2 bg-primary text-white rounded-sm font-semibold flex items-center justify-between hover:bg-primary-hover transition-colors"
    >
      <span>{$currentRoom?.name ?? 'Select Room'}</span>
      <span class="transition-transform {isOpen ? 'rotate-180' : ''}"
        >▼</span
      >
    </button>

    {#if isOpen}
      <div
        class="absolute top-full left-0 right-0 mt-2 bg-white border border-border rounded-sm shadow-lg z-50 max-h-96 overflow-y-auto"
      >
        <div class="p-2 border-b border-border sticky top-0 bg-white">
          <Button
            on:click={openCreateModal}
            variant="secondary"
            size="small"
            fullWidth
          >
            + New Room
          </Button>
        </div>

        <div class="py-1">
          {#each $roomList as room (room.id)}
            <div
              class="flex items-center gap-2 px-4 py-2 hover:bg-surface transition-colors cursor-pointer"
              on:click={() => selectRoom(room.id)}
            >
              <div class="flex-1">
                <div class="font-semibold text-text">{room.name}</div>
                {#if room.description}
                  <div class="text-xs text-text-light">{room.description}</div>
                {/if}
                <div class="text-xs text-text-light mt-1">
                  {room.memberCount} member{room.memberCount !== 1 ? 's' : ''}
                  {#if room.unreadCount > 0}
                    · <span class="text-error font-semibold">{room.unreadCount} unread</span>
                  {/if}
                </div>
              </div>

              {#if room.canEdit}
                <button
                  on:click={(e) => openRoomSettings(room.id, e)}
                  class="p-1 hover:bg-error-light rounded transition-colors text-text-light hover:text-error"
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
  :global(.text-error-light) {
    color: rgba(231, 76, 60, 0.1);
  }
</style>
