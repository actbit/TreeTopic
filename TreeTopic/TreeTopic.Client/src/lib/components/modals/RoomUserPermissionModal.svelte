<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { api } from '$lib/api/client';
  import { ui, activeModals } from '$lib/stores/ui';
  import { page } from '$app/stores';

  interface RoomUser {
    id: string;
    userName?: string;
    displayName?: string;
  }

  interface Permission {
    name: string;
  }

  const modalId = 'room-user-permission';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let tenant = $derived.by(() => (modal?.data?.tenant ?? $page.params.tenant ?? '') as string);
  let roomId = $derived.by(() => (modal?.data?.roomId ?? '') as string);

  let availablePermissions = $state<Permission[]>([]);
  let roomUsers = $state<RoomUser[]>([]);
  let userPermissions = $state<Record<string, string[]>>({});
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let selectedUserId = $state<string | null>(null);
  let deletingUserId = $state<string | null>(null);

  $effect(() => {
    if (isOpen && tenant && roomId) {
      loadData();
      return () => resetState();
    }
  });

  $effect(() => {
    if (roomUsers.length > 0 && !selectedUserId) {
      selectedUserId = roomUsers[0].id;
    }
  });

  async function loadData() {
    try {
      isLoading = true;
      const availablePermsData = await api.get<any>(`/${tenant}/api/permissions/available`, { cache: false });
      availablePermissions = availablePermsData.room || [];

      const usersData = await api.get<any>(`/${tenant}/api/roomusers/room/${roomId}`, { cache: false });
      roomUsers = usersData;

      const permPromises = roomUsers.map(async (user) => {
        try {
          const perms = await api.get<any>(`/${tenant}/api/roomusers/${user.id}/permissions`, { cache: false });
          return { userId: user.id, permissions: perms.permissions || [] };
        } catch {
          return { userId: user.id, permissions: [] };
        }
      });
      const userPermData = await Promise.all(permPromises);
      userPermissions = {};
      userPermData.forEach((p) => { userPermissions[p.userId] = p.permissions; });

      error = null;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load data';
    } finally {
      isLoading = false;
    }
  }

  async function togglePermission(userId: string, permissionName: string) {
    try {
      const currentPerms = userPermissions[userId] || [];
      const hasPerm = currentPerms.includes(permissionName);
      if (hasPerm) {
        await api.delete(`/${tenant}/api/roomusers/${userId}/permissions/${encodeURIComponent(permissionName)}`);
        userPermissions[userId] = currentPerms.filter((p) => p !== permissionName);
      } else {
        await api.post(`/${tenant}/api/roomusers/${userId}/permissions`, { permissionName });
        userPermissions[userId] = [...currentPerms, permissionName];
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to update permissions';
    }
  }

  async function removeUserFromRoom(userId: string, userName: string) {
    if (!confirm(`"${userName}"  from this room?`)) return;
    deletingUserId = userId;
    try {
      await api.delete(`/${tenant}/api/roomusers/${userId}`);
      roomUsers = roomUsers.filter((u) => u.id !== userId);
      delete userPermissions[userId];
      if (selectedUserId === userId) {
        selectedUserId = roomUsers.length > 0 ? roomUsers[0].id : null;
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to remove user from room';
    } finally {
      deletingUserId = null;
    }
  }

  function getDisplayName(user: RoomUser): string {
    return user.displayName || user.userName || 'Unknown';
  }

  function hasPermission(userId: string, permissionName: string): boolean {
    return (userPermissions[userId] || []).includes(permissionName);
  }

  function formatPermissionName(name: string): string {
    return name.split('.')
      .map((part) => { if (part === 'room') return ''; return part.charAt(0).toUpperCase() + part.slice(1); })
      .filter((p) => p !== '')
      .join(' ').trim();
  }

  let selectedUser = $derived.by(() => roomUsers.find((u) => u.id === selectedUserId) ?? null);

  function resetState() {
    roomUsers = [];
    userPermissions = {};
    availablePermissions = [];
    selectedUserId = null;
    error = null;
    isLoading = true;
  }

  function handleClose() { ui.closeModal(modalId); }
</script>

<Modal {isOpen} title="Room User Permissions" onClose={handleClose} size="xlarge" closeButton={!isLoading}>
  <div class="rum-root">
    {#if error}
      <div class="rum-error">
        <span>{error}</span>
        <button onclick={() => (error = null)}>Dismiss</button>
      </div>
    {/if}

    <div class="rum-content">
      {#if isLoading}
        <div class="rum-loading">
          <div class="rum-spinner"></div>
          <p>Loading...</p>
        </div>
      {:else}
        <!-- Left Panel: User List -->
        <div class="rum-panel rum-panel--left">
          <div class="rum-panel-header">
            <span class="rum-panel-title">Members</span>
          </div>

          <div class="rum-list">
            {#if roomUsers.length === 0}
              <div class="rum-empty"><p>No members</p></div>
            {:else}
              {#each roomUsers as user}
                {@const perms = userPermissions[user.id] || []}
                {@const isSelected = selectedUserId === user.id}
                <button
                  onclick={() => (selectedUserId = user.id)}
                  class="rum-user-item {isSelected ? 'rum-list-item--active' : ''}"
                >
                  <div class="rum-avatar">
                    {getDisplayName(user).charAt(0).toUpperCase()}
                  </div>
                  <div class="rum-user-info">
                    <span class="rum-user-name">{getDisplayName(user)}</span>
                    {#if user.userName}
                      <span class="rum-user-sub">@{user.userName}</span>
                    {/if}
                  </div>
                  <span class="rum-badge">{perms.length}</span>
                </button>
              {/each}
            {/if}
          </div>
        </div>

        <!-- Right Panel: Permission Details -->
        <div class="rum-panel rum-panel--right">
          {#if selectedUser}
            {@const perms = userPermissions[selectedUser.id] || []}
            <div class="rum-panel-header">
              <div class="rum-user-header">
                <div class="rum-avatar rum-avatar--lg">
                  {getDisplayName(selectedUser).charAt(0).toUpperCase()}
                </div>
                <div>
                  <span class="rum-panel-title">{getDisplayName(selectedUser)}</span>
                  <span class="rum-panel-sub">{perms.length}  individual permission(s)</span>
                </div>
              </div>
              <button
                onclick={() => removeUserFromRoom(selectedUser.id, getDisplayName(selectedUser))}
                disabled={deletingUserId === selectedUser.id}
                class="rum-btn rum-btn--danger"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                </svg>
                {deletingUserId === selectedUser.id ? '削除中...' : 'ルームから削除'}
              </button>
            </div>

            <div class="rum-perm-list">
              <p class="rum-section-label">Individual Permissions (in addition to role permissions)</p>
              {#if availablePermissions.length === 0}
                <div class="rum-empty"><p>No permissions available</p></div>
              {:else}
                {#each availablePermissions as perm}
                  {@const hasPerm = hasPermission(selectedUser.id, perm.name)}
                  <button
                    onclick={() => togglePermission(selectedUser.id, perm.name)}
                    class="rum-perm-item {hasPerm ? 'rum-perm-item--active' : ''}"
                  >
                    <div class="rum-checkbox {hasPerm ? 'rum-checkbox--checked' : ''}">
                      {#if hasPerm}
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                          <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                        </svg>
                      {/if}
                    </div>
                    <span>{formatPermissionName(perm.name)}</span>
                  </button>
                {/each}
              {/if}
            </div>
          {:else}
            <div class="rum-empty-panel">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
              <p>Select a user to manage permissions</p>
            </div>
          {/if}
        </div>
      {/if}
    </div>
  </div>
</Modal>

<style>
  .rum-root { display: flex; flex-direction: column; height: 600px; }

  .rum-error {
    display: flex; justify-content: space-between; align-items: center;
    margin: 16px 24px 0; padding: 10px 14px;
    background: var(--color-error-light, #fef2f2);
    border: 1px solid var(--color-error, #ef4444);
    border-radius: 8px; font-size: 13px; color: var(--color-error, #ef4444);
  }
  .rum-error button { font-size: 12px; text-decoration: underline; background: none; border: none; cursor: pointer; color: inherit; margin-left: 12px; }

  .rum-content { flex: 1; display: flex; gap: 16px; padding: 16px 24px 24px; overflow: hidden; min-height: 0; }

  .rum-loading { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; color: var(--color-text-light); font-size: 13px; }
  .rum-spinner { width: 28px; height: 28px; border: 3px solid var(--color-border); border-top-color: var(--color-primary); border-radius: 50%; animation: rum-spin 0.8s linear infinite; }

  .rum-panel { display: flex; flex-direction: column; border: 1px solid var(--color-border); border-radius: 10px; overflow: hidden; background: var(--color-background); }
  .rum-panel--left { width: 240px; flex-shrink: 0; }
  .rum-panel--right { flex: 1; min-width: 0; }

  .rum-panel-header { display: flex; align-items: center; justify-content: space-between; padding: 14px 16px; border-bottom: 1px solid var(--color-border); background: var(--color-surface); flex-shrink: 0; gap: 8px; }
  .rum-panel-title { font-size: 14px; font-weight: 600; color: var(--color-text); }
  .rum-panel-sub { display: block; font-size: 12px; color: var(--color-text-light); margin-top: 2px; }

  .rum-list { flex: 1; overflow-y: auto; padding: 8px; display: flex; flex-direction: column; gap: 2px; }

  .rum-user-item { display: flex; align-items: center; gap: 10px; padding: 8px 12px; border-radius: 7px; border: 1px solid transparent; background: none; cursor: pointer; text-align: left; transition: background 0.12s, border-color 0.12s; width: 100%; }
  .rum-user-item:hover { background: var(--color-surface); border-color: var(--color-border); }
  .rum-list-item--active { background: color-mix(in srgb, var(--color-primary) 10%, transparent); border-color: var(--color-primary); }
  .rum-list-item--active .rum-user-name { color: var(--color-primary); }

  .rum-user-info { display: flex; flex-direction: column; min-width: 0; flex: 1; }
  .rum-user-name { font-size: 13px; font-weight: 500; color: var(--color-text); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .rum-user-sub { font-size: 11px; color: var(--color-text-light); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

  .rum-user-header { display: flex; align-items: center; gap: 12px; min-width: 0; }

  .rum-avatar { width: 32px; height: 32px; border-radius: 50%; background: linear-gradient(135deg, var(--color-primary), color-mix(in srgb, var(--color-primary) 60%, white)); display: flex; align-items: center; justify-content: center; font-size: 13px; font-weight: 600; color: white; flex-shrink: 0; }
  .rum-avatar--lg { width: 40px; height: 40px; font-size: 16px; }

  .rum-badge { font-size: 11px; color: var(--color-text-light); background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 20px; padding: 1px 8px; flex-shrink: 0; }

  .rum-btn { display: inline-flex; align-items: center; gap: 6px; padding: 7px 14px; font-size: 13px; font-weight: 500; border-radius: 6px; border: 1px solid transparent; cursor: pointer; transition: background 0.15s, opacity 0.15s; flex-shrink: 0; }
  .rum-btn svg { width: 14px; height: 14px; }
  .rum-btn--danger { background: none; color: var(--color-error, #ef4444); border-color: var(--color-error, #ef4444); }
  .rum-btn--danger:hover:not(:disabled) { background: color-mix(in srgb, var(--color-error, #ef4444) 8%, transparent); }
  .rum-btn--danger:disabled { opacity: 0.4; cursor: not-allowed; }

  .rum-perm-list { flex: 1; overflow-y: auto; padding: 16px 20px; display: flex; flex-direction: column; gap: 6px; }
  .rum-section-label { font-size: 11px; font-weight: 600; color: var(--color-text-light); text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 6px; }
  .rum-perm-item { display: flex; align-items: center; gap: 12px; padding: 10px 12px; border-radius: 7px; border: 1px solid var(--color-border); background: var(--color-background); cursor: pointer; text-align: left; font-size: 13px; color: var(--color-text); transition: background 0.12s, border-color 0.12s; width: 100%; }
  .rum-perm-item:hover { background: var(--color-surface); border-color: color-mix(in srgb, var(--color-primary) 40%, transparent); }
  .rum-perm-item--active { background: color-mix(in srgb, var(--color-primary) 6%, var(--color-background)); border-color: var(--color-primary); }

  .rum-checkbox { width: 18px; height: 18px; border-radius: 4px; border: 2px solid var(--color-border); display: flex; align-items: center; justify-content: center; flex-shrink: 0; transition: background 0.12s, border-color 0.12s; }
  .rum-checkbox svg { width: 11px; height: 11px; stroke: white; }
  .rum-checkbox--checked { background: var(--color-primary); border-color: var(--color-primary); }

  .rum-empty { padding: 24px 16px; text-align: center; color: var(--color-text-light); font-size: 13px; }
  .rum-empty-panel { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; color: var(--color-text-light); font-size: 13px; }
  .rum-empty-panel svg { width: 48px; height: 48px; opacity: 0.3; }

  @keyframes rum-spin { to { transform: rotate(360deg); } }
</style>
