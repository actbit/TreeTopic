<script lang="ts">
  import { api } from '$lib/api/client';
  import type { AvailablePermissions } from '$lib/types/permissions';
  import { formatPermissionName } from '$lib/utils/permission';

  interface Props {
    tenant: string;
    roomId: string;
  }

  interface RoomUserDto {
    id: string;
    displayName: string;
    userName: string;
  }

  interface PermissionDto {
    name: string;
    scope: string;
  }

  let { tenant, roomId }: Props = $props();

  let users = $state<RoomUserDto[]>([]);
  let availablePermissions = $state<PermissionDto[]>([]);
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let selectedUserId = $state<string | null>(null);
  let userPermissions = $state<Record<string, string[]>>({});

  $effect(() => {
    loadData();
  });

  $effect(() => {
    if (users.length > 0 && !selectedUserId) {
      selectedUserId = users[0].id;
    }
  });

  async function loadData() {
    try {
      isLoading = true;
      const usersData = await api.get<RoomUserDto[]>(`/${tenant}/api/roomusers/room/${roomId}`, { cache: false });
      users = usersData;

      const permsData = await api.get<PermissionDto[]>(`/${tenant}/api/roomusers/0/permissions/available`, { cache: false });
      availablePermissions = permsData || [];

      const permPromises = users.map(async (user) => {
        try {
          const userPermsRes = await api.get<{ permissions: string[] }>(`/${tenant}/api/roomusers/${user.id}/permissions`, { cache: false });
          return { userId: user.id, permissions: userPermsRes.permissions || [] };
        } catch {
          return { userId: user.id, permissions: [] };
        }
      });
      const permsData2 = await Promise.all(permPromises);
      userPermissions = {};
      permsData2.forEach((p) => { userPermissions[p.userId] = p.permissions; });

      error = null;
    } catch (err) {
      // Silently fail if endpoint doesn't exist
      if (isLoading) {
        error = null;
      } else {
        error = err instanceof Error ? err.message : 'Failed to load data';
      }
    } finally {
      isLoading = false;
    }
  }

  async function togglePermission(userId: string, permissionName: string) {
    try {
      const currentPerms = userPermissions[userId] || [];
      const hasPerm = currentPerms.includes(permissionName);
      if (hasPerm) {
        await api.delete(`/${tenant}/api/roomusers/${userId}/permissions/${permissionName}`);
        userPermissions[userId] = currentPerms.filter((p) => p !== permissionName);
      } else {
        await api.post(`/${tenant}/api/roomusers/${userId}/permissions`, { permissionName });
        userPermissions[userId] = [...currentPerms, permissionName];
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to update permissions';
    }
  }

  async function removeUser(userId: string) {
    if (!confirm('Remove this user from the room?')) return;
    try {
      await api.delete(`/${tenant}/api/roomusers/${userId}`);
      selectedUserId = null;
      await loadData();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to remove user';
    }
  }

  function hasPermission(userId: string, permissionName: string): boolean {
    return (userPermissions[userId] || []).includes(permissionName);
  }

  let selectedUser = $derived.by(() => users.find((u) => u.id === selectedUserId) ?? null);
</script>

<div class="rudp-root">
  {#if error}
    <div class="rudp-error">
      <span>{error}</span>
      <button onclick={() => (error = null)}>Dismiss</button>
    </div>
  {/if}

  <div class="rudp-content">
    {#if isLoading}
      <div class="rudp-loading">
        <div class="rudp-spinner"></div>
        <p>Loading...</p>
      </div>
    {:else}
      <div class="rudp-layout">
        <!-- Left Panel: User List -->
        <div class="rudp-panel rudp-panel--left">
          <div class="rudp-panel-header">
            <span class="rudp-panel-title">Users</span>
          </div>

          <div class="rudp-list">
            {#each users as user}
              {@const userPerms = userPermissions[user.id] || []}
              {@const isSelected = selectedUserId === user.id}
              <button
                onclick={() => (selectedUserId = user.id)}
                class="rudp-list-item {isSelected ? 'rudp-list-item--active' : ''}"
              >
                <span class="rudp-list-item-name">{user.displayName || user.userName}</span>
                <span class="rudp-badge">{userPerms.length}</span>
              </button>
            {/each}
          </div>
        </div>

        <!-- Right Panel: Permission Management -->
        <div class="rudp-panel rudp-panel--right">
          {#if selectedUser}
            {@const userPerms = userPermissions[selectedUser.id] || []}
            <div class="rudp-panel-header">
              <div>
                <span class="rudp-panel-title">{selectedUser.displayName || selectedUser.userName}</span>
                <span class="rudp-panel-sub">{userPerms.length} permission(s)</span>
              </div>
              <button
                onclick={() => removeUser(selectedUser.id)}
                class="rudp-btn rudp-btn--danger"
              >
                Remove
              </button>
            </div>

            <div class="rudp-perm-list">
              <p class="rudp-section-label">Room Permissions</p>
              {#each availablePermissions as perm}
                {@const hasPerm = hasPermission(selectedUser.id, perm.name)}
                <button
                  onclick={() => togglePermission(selectedUser.id, perm.name)}
                  class="rudp-perm-item {hasPerm ? 'rudp-perm-item--active' : ''}"
                >
                  <div class="rudp-checkbox {hasPerm ? 'rudp-checkbox--checked' : ''}">
                    {#if hasPerm}
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                      </svg>
                    {/if}
                  </div>
                  <span>{formatPermissionName(perm.name)}</span>
                </button>
              {/each}
            </div>
          {:else}
            <div class="rudp-empty-panel">
              <p>Select a user to assign permissions</p>
            </div>
          {/if}
        </div>
      </div>
    {/if}
  </div>
</div>

<style>
  :global {
    .rudp-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      padding: 0;
    }

    .rudp-error {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      background-color: #fee;
      border-bottom: 1px solid #fcc;
      color: #c33;
      font-size: 14px;
    }

    .rudp-error button {
      background: none;
      border: none;
      color: inherit;
      text-decoration: underline;
      cursor: pointer;
    }

    .rudp-content {
      flex: 1;
      overflow-y: auto;
      padding: 0;
    }

    .rudp-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100%;
      gap: 16px;
      color: var(--color-text-light);
    }

    .rudp-spinner {
      width: 40px;
      height: 40px;
      border: 3px solid var(--color-border);
      border-top-color: var(--color-primary);
      border-radius: 50%;
      animation: rudp-spin 1s linear infinite;
    }

    @keyframes rudp-spin {
      to { transform: rotate(360deg); }
    }

    .rudp-layout {
      display: grid;
      grid-template-columns: 240px 1fr;
      height: 100%;
      gap: 0;
    }

    .rudp-panel {
      display: flex;
      flex-direction: column;
      border-right: 1px solid var(--color-border);
    }

    .rudp-panel--right {
      border-right: none;
    }

    .rudp-panel-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      padding: 12px 16px;
      border-bottom: 1px solid var(--color-border);
      background-color: var(--color-surface);
    }

    .rudp-panel-title {
      font-size: 14px;
      font-weight: 600;
      color: var(--color-text);
    }

    .rudp-panel-sub {
      font-size: 12px;
      color: var(--color-text-light);
      margin-top: 4px;
      display: block;
    }

    .rudp-list {
      flex: 1;
      overflow-y: auto;
      padding: 8px 0;
    }

    .rudp-list-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      border: none;
      background: transparent;
      color: var(--color-text);
      text-align: left;
      cursor: pointer;
      transition: all 0.2s;
      border-left: 3px solid transparent;
    }

    .rudp-list-item:hover {
      background-color: var(--color-surface);
    }

    .rudp-list-item--active {
      background-color: var(--color-surface);
      border-left-color: var(--color-primary);
      color: var(--color-primary);
      font-weight: 500;
    }

    .rudp-list-item-name {
      font-size: 14px;
    }

    .rudp-badge {
      font-size: 12px;
      padding: 2px 6px;
      background-color: var(--color-primary);
      color: white;
      border-radius: 3px;
    }

    .rudp-perm-list {
      flex: 1;
      overflow-y: auto;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .rudp-section-label {
      font-size: 12px;
      font-weight: 600;
      color: var(--color-text-light);
      text-transform: uppercase;
      margin: 0;
      margin-top: 8px;
    }

    .rudp-perm-item {
      display: flex;
      align-items: center;
      padding: 8px 12px;
      border: 1px solid var(--color-border);
      background: transparent;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s;
      gap: 8px;
    }

    .rudp-perm-item:hover {
      background-color: var(--color-surface);
    }

    .rudp-perm-item--active {
      background-color: rgba(var(--color-primary-rgb), 0.1);
      border-color: var(--color-primary);
    }

    .rudp-checkbox {
      width: 20px;
      height: 20px;
      border: 2px solid var(--color-border);
      border-radius: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .rudp-checkbox--checked {
      background-color: var(--color-primary);
      border-color: var(--color-primary);
      color: white;
    }

    .rudp-checkbox svg {
      width: 16px;
      height: 16px;
    }

    .rudp-btn {
      padding: 6px 12px;
      border: 1px solid var(--color-border);
      border-radius: 4px;
      font-size: 12px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
    }

    .rudp-btn--danger {
      background-color: #dc2626;
      color: white;
      border-color: #dc2626;
      padding: 6px 12px;
      font-size: 12px;
    }

    .rudp-btn--danger:hover {
      opacity: 0.9;
    }

    .rudp-empty-panel {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 100%;
      color: var(--color-text-light);
      font-size: 14px;
      text-align: center;
    }
  }
</style>
