<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals, modals } from '$lib/stores/ui';
  import { currentRoom, updateRoom, deleteRoom as deleteRoomStore } from '$lib/stores/rooms';
  import { isRequired } from '$lib/utils/validation';
  import { api } from '$lib/api/client';
  import { page } from '$app/stores';

  const modalId = 'room-settings';
  let isOpen = $derived.by(() => $activeModals.some((m) => m.id === modalId));

  let name = $state($currentRoom?.name ?? '');
  let description = $state($currentRoom?.description ?? '');
  let joinPolicy = $state<number>($currentRoom?.joinPolicy ?? 0);
  let isLoading = $state(false);
  let isDeleting = $state(false);
  let error = $state<string | null>(null);
  let nameError = $state<string | null>(null);
  let canManageRoom = $state(false);
  let canManageRoles = $state(false);
  let canManageUsers = $state(false);
  let canManageJoinPermissions = $state(false);

  // タブ状態を永続化
  const getStoredTab = (): string => {
    if (typeof window === 'undefined') return 'general';
    return localStorage.getItem('room_settings_active_tab') || 'general';
  };
  const setStoredTab = (tab: string) => {
    if (typeof window === 'undefined') return;
    localStorage.setItem('room_settings_active_tab', tab);
  };
  let activeTab = $state(getStoredTab());

  // タブ変更時に保存
  $effect(() => {
    setStoredTab(activeTab);
  });

  $effect(() => {
    if ($currentRoom) {
      name = $currentRoom.name;
      description = $currentRoom.description ?? '';
      joinPolicy = $currentRoom?.joinPolicy ?? 0;
      void loadCapabilities();
    }
  });

  async function loadCapabilities() {
    if (!$currentRoom) return;
    try {
      const tenant = api.getCurrentTenant();
      const [roomPermRes, tenantPermRes] = await Promise.all([
        api.get<{ permissions?: string[] }>(`/${tenant}/api/room/${$currentRoom.id}/my/permissions`),
        api.get<{ permissions?: string[] }>(`/${tenant}/auth/me/permissions`)
      ]);

      const roomPerms = new Set(roomPermRes?.permissions ?? []);
      const tenantPerms = new Set(tenantPermRes?.permissions ?? []);
      const isTenantRoomManage = tenantPerms.has('tenant.room.manage');

      canManageRoom = isTenantRoomManage || roomPerms.has('room.manage');
      canManageRoles = isTenantRoomManage || roomPerms.has('room.manageRoles');
      canManageUsers = isTenantRoomManage || roomPerms.has('room.manageUsers');
      canManageJoinPermissions = isTenantRoomManage || roomPerms.has('room.manage');
    } catch {
      canManageRoom = false;
      canManageRoles = false;
      canManageUsers = false;
      canManageJoinPermissions = false;
    }
  }

  async function handleSave(e: Event) {
    e.preventDefault();

    nameError = null;
    error = null;

    if (!isRequired(name)) {
      nameError = 'Room name is required';
      return;
    }
    if (!canManageRoom) {
      error = 'You do not have permission to manage this room';
      return;
    }

    isLoading = true;

    try {
      if ($currentRoom) {
        const tenant = api.getCurrentTenant();
        await api.put(`/${tenant}/api/room/${$currentRoom.id}`, {
          name: name.trim(),
          description: description.trim(),
          joinPolicy: Number(joinPolicy),
        });

        updateRoom($currentRoom.id, {
          name,
          description,
          joinPolicy: Number(joinPolicy),
        });
      }

      ui.closeModal(modalId);
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to update room';
    } finally {
      isLoading = false;
    }
  }

  async function handleDelete() {
    if (!confirm('Are you sure you want to delete this room? This action cannot be undone.')) {
      return;
    }

    isDeleting = true;
    error = null;

    try {
      if ($currentRoom) {
        const tenant = api.getCurrentTenant();
        await api.delete(`/${tenant}/api/room/${$currentRoom.id}`);
        deleteRoomStore($currentRoom.id);
        ui.closeModal(modalId);
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to delete room';
    } finally {
      isDeleting = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Room Settings" onClose={handleClose} size="large">
  <div class="flex flex-col bg-white">
    <!-- Error message -->
    {#if error}
      <div class="p-4 bg-red-50 border-b border-red-200 text-red-800 text-sm flex justify-between items-center">
        <span>{error}</span>
        <button onclick={() => (error = null)} class="underline hover:no-underline">Close</button>
      </div>
    {/if}

    <!-- Tabs -->
    <div class="flex border-b border-border">
      <button
        onclick={() => activeTab = 'general'}
        class="px-6 py-3 text-sm font-medium {activeTab === 'general'
          ? 'border-b-2 border-primary text-primary'
          : 'text-text hover:text-text-light'}"
      >
        General
      </button>
      {#if canManageRoles || canManageUsers || canManageJoinPermissions}
        <button
          onclick={() => activeTab = 'permissions'}
          class="px-6 py-3 text-sm font-medium {activeTab === 'permissions'
            ? 'border-b-2 border-primary text-primary'
            : 'text-text hover:text-text-light'}"
        >
          Permissions
        </button>
      {/if}
    </div>

    <!-- Content -->
    <div class="overflow-auto p-6" style="max-height: calc(100vh - 220px);">
      {#if activeTab === 'general'}
        <form onsubmit={handleSave} class="space-y-6">
          <div>
            <label class="block text-sm font-medium text-text mb-2">Room Name</label>
            <input
              type="text"
              bind:value={name}
              placeholder="Enter room name"
              disabled={isLoading || isDeleting || !canManageRoom}
              class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary disabled:opacity-50"
              required
            />
            {#if nameError}
              <p class="text-sm text-red-600 mt-1">{nameError}</p>
            {/if}
          </div>

          <div>
            <label class="block text-sm font-medium text-text mb-2">Description</label>
            <textarea
              bind:value={description}
              placeholder="Enter room description (optional)"
              disabled={isLoading || isDeleting || !canManageRoom}
              class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary disabled:opacity-50 resize-vertical"
              rows="3"
            ></textarea>
          </div>

          <div>
            <label class="block text-sm font-medium text-text mb-2">Join Policy</label>
            <select
              bind:value={joinPolicy}
              disabled={isLoading || isDeleting || !canManageRoom}
              class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary disabled:opacity-50"
            >
              <option value={0}>Public (any authenticated user can join)</option>
              <option value={1}>Invite Only (only allowed users/roles can join)</option>
            </select>
          </div>

          {#if $currentRoom?.memberCount}
            <div class="text-sm text-text-light bg-surface rounded-lg p-3">
              <span class="font-semibold text-text">{$currentRoom.memberCount}</span> member{$currentRoom
                .memberCount !== 1
                ? 's'
                : ''} in this room
            </div>
          {/if}

          <div class="flex gap-3 pt-4 border-t border-border">
            <Button
              type="submit"
              variant="primary"
              disabled={isLoading || isDeleting || !canManageRoom}
            >
              {isLoading ? 'Saving...' : 'Save Changes'}
            </Button>
            <Button
              type="button"
              variant="secondary"
              disabled={isLoading || isDeleting}
              onclick={handleClose}
            >
              Cancel
            </Button>
          </div>

          {#if $currentRoom?.canDelete}
            <div class="pt-4 border-t border-border">
              <Button
                type="button"
                variant="danger"
                disabled={isLoading || isDeleting}
                onclick={handleDelete}
              >
                {isDeleting ? 'Deleting...' : 'Delete Room'}
              </Button>
            </div>
          {/if}
        </form>

      {:else if activeTab === 'permissions' && (canManageRoles || canManageUsers || canManageJoinPermissions)}
        <div class="space-y-6">
          <p class="text-sm text-text-light">Manage room permissions and access control.</p>

          <div class="grid grid-cols-1 {canManageRoles && canManageUsers && canManageJoinPermissions
            ? 'lg:grid-cols-3'
            : 'md:grid-cols-2'} gap-4">
            {#if canManageRoles}
              <button
                onclick={() => modals.open('room-role-permission', 'Role Permissions', {
                  tenant: $page.params.tenant,
                  roomId: $currentRoom?.id
                })}
                class="p-4 border border-border rounded-lg hover:bg-surface transition-colors text-left"
              >
                <h3 class="font-semibold text-text mb-1">Role Permissions</h3>
                <p class="text-sm text-text-light">Manage permissions for room roles</p>
              </button>
            {/if}

            {#if canManageUsers}
              <button
                onclick={() => modals.open('room-user-permission', 'User Permissions', {
                  tenant: $page.params.tenant,
                  roomId: $currentRoom?.id
                })}
                class="p-4 border border-border rounded-lg hover:bg-surface transition-colors text-left"
              >
                <h3 class="font-semibold text-text mb-1">User Permissions</h3>
                <p class="text-sm text-text-light">Manage individual user permissions</p>
              </button>
            {/if}

            {#if canManageJoinPermissions}
              <button
                onclick={() => modals.open('room-join-permission', 'Join Permissions', {
                  tenant: $page.params.tenant,
                  roomId: $currentRoom?.id
                })}
                class="p-4 border border-border rounded-lg hover:bg-surface transition-colors text-left"
              >
                <h3 class="font-semibold text-text mb-1">Join Permissions</h3>
                <p class="text-sm text-text-light">Manage who can join this room</p>
              </button>
            {/if}
          </div>
        </div>
      {/if}
    </div>
  </div>
</Modal>

<style>
  textarea {
    font-family: var(--font-family-base);
    resize: vertical;
    min-height: 80px;
  }
</style>
