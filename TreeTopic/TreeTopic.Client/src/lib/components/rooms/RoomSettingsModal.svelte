<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals, modals } from '$lib/stores/ui';
  import { currentRoom, updateRoom, deleteRoom as deleteRoomStore } from '$lib/stores/rooms';
  import { isRequired } from '$lib/utils/validation';
  import { api } from '$lib/api/client';
  import { goto } from '$app/navigation';
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

  $effect(() => {
    if ($currentRoom) {
      name = $currentRoom.name;
      description = $currentRoom.description ?? '';
      joinPolicy = $currentRoom.joinPolicy ?? 0;
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
        await api.del(`/${tenant}/api/room/${$currentRoom.id}`);
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

<Modal {isOpen} title="Room Settings" onClose={handleClose} size="medium">
  <form onsubmit={handleSave} class="space-y-6">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <Input
      label="Room Name"
      type="text"
      bind:value={name}
      placeholder="Enter room name"
      error={nameError}
      disabled={isLoading || isDeleting || !canManageRoom}
      required
    />

    <div class="flex flex-col gap-1">
      <label for="room-settings-description" class="text-sm font-semibold text-text">Description</label>
      <textarea
        id="room-settings-description"
        bind:value={description}
        placeholder="Enter room description (optional)"
        disabled={isLoading || isDeleting || !canManageRoom}
      ></textarea>
    </div>

    <div class="flex flex-col gap-1">
      <label for="room-settings-join-policy" class="text-sm font-semibold text-text">Join Policy</label>
      <select
        id="room-settings-join-policy"
        bind:value={joinPolicy}
        disabled={isLoading || isDeleting || !canManageRoom}
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

    <div class="flex gap-4 pt-8">
      <Button
        type="submit"
        variant="primary"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading || isDeleting || !canManageRoom}
      >
        {#if isLoading}
          Saving...
        {:else}
          Save Changes
        {/if}
      </Button>
      <Button
        type="button"
        variant="secondary"
        size="base"
        fullWidth
        disabled={isLoading || isDeleting}
        onclick={handleClose}
      >
        Cancel
      </Button>
    </div>

    {#if canManageRoles || canManageUsers || canManageJoinPermissions}
      <div class="border-t border-border pt-8">
        <div class="flex gap-4 flex-wrap">
          {#if canManageRoles}
            <button
              type="button"
              class="flex-1 px-4 py-3 border border-primary text-primary rounded-lg hover:bg-primary hover:bg-opacity-10 transition-colors font-medium"
              onclick={() => modals.open('room-role-permission', 'Room Role Permissions', {
                tenant: $page.params.tenant,
                roomId: $currentRoom?.id
              })}
            >
              ロール権限管理
            </button>
          {/if}
          {#if canManageUsers}
            <button
              type="button"
              class="flex-1 px-4 py-3 border border-primary text-primary rounded-lg hover:bg-primary hover:bg-opacity-10 transition-colors font-medium"
              onclick={() => modals.open('room-user-permission', 'Room User Permissions', {
                tenant: $page.params.tenant,
                roomId: $currentRoom?.id
              })}
            >
              ユーザー権限管理
            </button>
          {/if}
          {#if canManageJoinPermissions}
            <button
              type="button"
              class="flex-1 px-4 py-3 border border-primary text-primary rounded-lg hover:bg-primary hover:bg-opacity-10 transition-colors font-medium"
              onclick={() => modals.open('room-join-permission', 'Room Join Permissions', {
                tenant: $page.params.tenant,
                roomId: $currentRoom?.id
              })}
            >
              参加権限管理
            </button>
          {/if}
        </div>
      </div>
    {/if}

    {#if $currentRoom?.canDelete}
      <div class="border-t border-border pt-8">
        <Button
          type="button"
          variant="danger"
          size="base"
          fullWidth
          loading={isDeleting}
          disabled={isLoading || isDeleting}
          onclick={handleDelete}
        >
          {#if isDeleting}
            Deleting...
          {:else}
            Delete Room
          {/if}
        </Button>
      </div>
    {/if}
  </form>
</Modal>

<style>
  textarea {
    font-family: var(--font-family-base);
    resize: vertical;
    min-height: 80px;
  }
</style>
