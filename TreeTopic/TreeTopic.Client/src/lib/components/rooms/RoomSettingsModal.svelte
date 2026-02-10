<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import RoomRolePermissionsPanel from './RoomRolePermissionsPanel.svelte';
  import RoomUserPermissionsPanel from './RoomUserPermissionsPanel.svelte';
  import RoomUserDirectPermissionsPanel from './RoomUserDirectPermissionsPanel.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { currentRoom, updateRoom, deleteRoom as deleteRoomStore } from '$lib/stores/rooms';
  import { api } from '$lib/api/client';
  import { page } from '$app/stores';

  const modalId = 'room-settings';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);

  // Tab state
  type Tab = 'general' | 'rolePermissions' | 'userRoles' | 'userPermissions';
  let activeTab = $state<Tab>('general');


  // Room settings
  let name = $state('');
  let description = $state('');
  let joinPolicy = $state(0);
  let isLoading = $state(false);
  let isDeleting = $state(false);
  let nameError = $state<string | null>(null);
  let error = $state<string | null>(null);

  // Permissions state
  let canManageRoom = $state(false);

  $effect(() => {
    if (isOpen && $currentRoom) {
      loadData();
      return () => resetState();
    }
  });

  function resetState() {
    name = '';
    description = '';
    joinPolicy = 0;
    nameError = null;
    error = null;
    isLoading = false;
    canManageRoom = false;
  }

  async function loadData() {
    if (!$currentRoom) return;
    try {
      isLoading = true;
      name = $currentRoom.name;
      description = $currentRoom.description ?? '';
      joinPolicy = $currentRoom.joinPolicy ?? 0;

      await loadCapabilities();
      error = null;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load data';
    } finally {
      isLoading = false;
    }
  }

  async function loadCapabilities() {
    if (!$currentRoom) return;
    try {
      const tenant = api.getCurrentTenant();
      const roomPermRes = await api.get<{ permissions?: string[] }>(`/${tenant}/api/room/${$currentRoom.id}/my/permissions`, { cache: false });
      const roomPerms = new Set(roomPermRes?.permissions ?? []);
      canManageRoom = roomPerms.has('room.manage');
    } catch {
      canManageRoom = false;
    }
  }

  async function handleSave(e: Event) {
    e.preventDefault();
    nameError = null;
    error = null;

    if (!name.trim()) {
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

<Modal {isOpen} title="Room Settings" onClose={handleClose} size="xlarge" closeButton={!isLoading}>
  <div class="rsm-root">
    <!-- Error message -->
    {#if error}
      <div class="rsm-error">
        <span>{error}</span>
        <button onclick={() => (error = null)}>Dismiss</button>
      </div>
    {/if}

    <!-- Tabs -->
    <div class="rsm-tabs">
      <button
        onclick={() => (activeTab = 'general')}
        class="rsm-tab {activeTab === 'general' ? 'rsm-tab--active' : ''}"
      >
        General
      </button>
      <button
        onclick={() => (activeTab = 'rolePermissions')}
        class="rsm-tab {activeTab === 'rolePermissions' ? 'rsm-tab--active' : ''}"
      >
        Role Permissions
      </button>
      <button
        onclick={() => (activeTab = 'userRoles')}
        class="rsm-tab {activeTab === 'userRoles' ? 'rsm-tab--active' : ''}"
      >
        User Roles
      </button>
      <button
        onclick={() => (activeTab = 'userPermissions')}
        class="rsm-tab {activeTab === 'userPermissions' ? 'rsm-tab--active' : ''}"
      >
        User Permissions
      </button>
    </div>

    <!-- Content -->
    <div class="rsm-content">
      {#if isLoading}
        <div class="rsm-loading">
          <div class="rsm-spinner"></div>
          <p>Loading...</p>
        </div>
      {:else if activeTab === 'general'}
        <form onsubmit={handleSave} class="rsm-form">
          <div class="rsm-form-group">
            <label class="rsm-label">Room Name</label>
            <input
              type="text"
              bind:value={name}
              placeholder="Enter room name"
              disabled={isLoading || isDeleting || !canManageRoom}
              class="rsm-input"
              required
            />
            {#if nameError}
              <p class="rsm-error-text">{nameError}</p>
            {/if}
          </div>

          <div class="rsm-form-group">
            <label class="rsm-label">Description</label>
            <textarea
              bind:value={description}
              placeholder="Enter room description (optional)"
              disabled={isLoading || isDeleting || !canManageRoom}
              class="rsm-textarea"
              rows="3"
            ></textarea>
          </div>

          <div class="rsm-form-group">
            <label class="rsm-label">Join Policy</label>
            <select
              bind:value={joinPolicy}
              disabled={isLoading || isDeleting || !canManageRoom}
              class="rsm-select"
            >
              <option value={0}>Public (any authenticated user can join)</option>
              <option value={1}>Invite Only (only allowed users/roles can join)</option>
            </select>
          </div>

          {#if $currentRoom?.memberCount}
            <div class="rsm-info">
              <span class="rsm-info-label">{$currentRoom.memberCount}</span> member{$currentRoom.memberCount !== 1 ? 's' : ''} in this room
            </div>
          {/if}

          <div class="rsm-actions">
            <button
              type="submit"
              disabled={isLoading || isDeleting || !canManageRoom}
              class="rsm-btn rsm-btn--primary"
            >
              {isLoading ? 'Saving...' : 'Save Changes'}
            </button>
            <button
              type="button"
              disabled={isLoading || isDeleting}
              onclick={handleClose}
              class="rsm-btn rsm-btn--secondary"
            >
              Cancel
            </button>
          </div>

          {#if $currentRoom?.canDelete}
            <div class="rsm-danger-zone">
              <button
                type="button"
                disabled={isLoading || isDeleting}
                onclick={handleDelete}
                class="rsm-btn rsm-btn--danger"
              >
                {isDeleting ? 'Deleting...' : 'Delete Room'}
              </button>
            </div>
          {/if}
        </form>

      {:else if activeTab === 'rolePermissions' && $currentRoom}
        <RoomRolePermissionsPanel tenant={$page.params.tenant ?? ''} roomId={$currentRoom.id} />
      {:else if activeTab === 'userRoles' && $currentRoom}
        <RoomUserPermissionsPanel tenant={$page.params.tenant ?? ''} roomId={$currentRoom.id} />
      {:else if activeTab === 'userPermissions' && $currentRoom}
        <RoomUserDirectPermissionsPanel tenant={$page.params.tenant ?? ''} roomId={$currentRoom.id} />
      {/if}
    </div>
  </div>
</Modal>

<style>
  :global {
    .rsm-root {
      display: flex;
      flex-direction: column;
      height: 600px;
    }

    .rsm-error {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      background-color: #fee;
      border-bottom: 1px solid #fcc;
      color: #c33;
      font-size: 14px;
    }

    .rsm-error button {
      background: none;
      border: none;
      color: inherit;
      text-decoration: underline;
      cursor: pointer;
    }

    .rsm-tabs {
      display: flex;
      gap: 0;
      border-bottom: 1px solid var(--color-border);
      padding: 0;
      background-color: var(--color-surface);
    }

    .rsm-tab {
      flex: 1;
      padding: 12px 16px;
      border: none;
      background: transparent;
      color: var(--color-text-light);
      font-size: 14px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
      border-bottom: 2px solid transparent;
    }

    .rsm-tab:hover {
      color: var(--color-text);
    }

    .rsm-tab--active {
      color: var(--color-primary);
      border-bottom-color: var(--color-primary);
    }

    .rsm-content {
      flex: 1;
      overflow-y: auto;
      padding: 0;
    }

    .rsm-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100%;
      gap: 16px;
      color: var(--color-text-light);
    }

    .rsm-spinner {
      width: 40px;
      height: 40px;
      border: 3px solid var(--color-border);
      border-top-color: var(--color-primary);
      border-radius: 50%;
      animation: rsm-spin 1s linear infinite;
    }

    @keyframes rsm-spin {
      to { transform: rotate(360deg); }
    }

    .rsm-form {
      padding: 24px;
      max-width: 600px;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .rsm-form-group {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .rsm-label {
      font-size: 14px;
      font-weight: 500;
      color: var(--color-text);
    }

    .rsm-input,
    .rsm-select,
    .rsm-textarea {
      padding: 8px 12px;
      border: 1px solid var(--color-border);
      border-radius: 6px;
      font-size: 14px;
      color: var(--color-text);
      background-color: var(--color-background);
      font-family: inherit;
    }

    .rsm-input:focus,
    .rsm-select:focus,
    .rsm-textarea:focus {
      outline: none;
      border-color: var(--color-primary);
      box-shadow: 0 0 0 3px rgba(var(--color-primary-rgb), 0.1);
    }

    .rsm-input:disabled,
    .rsm-select:disabled,
    .rsm-textarea:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .rsm-textarea {
      resize: vertical;
    }

    .rsm-error-text {
      font-size: 12px;
      color: #c33;
    }

    .rsm-info {
      padding: 12px;
      background-color: var(--color-surface);
      border-radius: 6px;
      font-size: 14px;
      color: var(--color-text-light);
    }

    .rsm-info-label {
      font-weight: 600;
      color: var(--color-text);
    }

    .rsm-actions {
      display: flex;
      gap: 12px;
      padding-top: 12px;
      border-top: 1px solid var(--color-border);
    }

    .rsm-danger-zone {
      padding-top: 12px;
      border-top: 1px solid var(--color-border);
    }

    .rsm-btn {
      padding: 8px 16px;
      border: 1px solid var(--color-border);
      border-radius: 6px;
      font-size: 14px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
    }

    .rsm-btn--primary {
      background-color: var(--color-primary);
      color: white;
      border-color: var(--color-primary);
    }

    .rsm-btn--primary:hover:not(:disabled) {
      opacity: 0.9;
    }

    .rsm-btn--secondary {
      background-color: transparent;
      color: var(--color-text);
      border-color: var(--color-border);
    }

    .rsm-btn--secondary:hover:not(:disabled) {
      background-color: var(--color-surface);
    }

    .rsm-btn--danger {
      background-color: #dc2626;
      color: white;
      border-color: #dc2626;
    }

    .rsm-btn--danger:hover:not(:disabled) {
      opacity: 0.9;
    }

    .rsm-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

  }
</style>
