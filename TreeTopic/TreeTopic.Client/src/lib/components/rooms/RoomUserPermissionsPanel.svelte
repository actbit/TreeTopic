<script lang="ts">
  import { api } from '$lib/api';
  import type { Role } from '$lib/types';

  interface Props {
    tenant: string;
    roomId: string;
  }

  interface RoomUser {
    id: string;
    displayName: string;
    userName: string;
  }

  let { tenant, roomId }: Props = $props();

  interface RoomUserDto {
    id: string;
    displayName: string;
    userName: string;
  }

  let users = $state<RoomUserDto[]>([]);
  let roles = $state<Role[]>([]);
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let selectedUserId = $state<string | null>(null);
  let userRoles = $state<Record<string, string[]>>({});

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

      const rolesData = await api.get<Role[]>(`/${tenant}/api/rooms/${roomId}/RoomRoles`, { cache: false });
      roles = rolesData;

      const rolePromises = users.map(async (user) => {
        try {
          const userRolesRes = await api.get<{ roomUserId: string; roles: Array<{ id: string; roomRoleId: string; roleName: string; description: string }> }>(`/${tenant}/api/roomusers/${user.id}/roles`, { cache: false });
          return { userId: user.id, roles: (userRolesRes.roles || []).map((r) => r.roleName) };
        } catch {
          return { userId: user.id, roles: [] };
        }
      });
      const rolesData2 = await Promise.all(rolePromises);
      userRoles = {};
      rolesData2.forEach((r) => { userRoles[r.userId] = r.roles; });

      error = null;
    } catch (err) {
      // Don't show error for initial load if endpoint doesn't exist
      if (isLoading) {
        error = null;
      } else {
        error = err instanceof Error ? err.message : 'Failed to load data';
      }
    } finally {
      isLoading = false;
    }
  }

  async function toggleRole(userId: string, roleName: string) {
    try {
      const currentRoles = userRoles[userId] || [];
      const hasRole = currentRoles.includes(roleName);
      if (hasRole) {
        await api.delete(`/${tenant}/api/roomusers/${userId}/roles/${roleName}`);
        userRoles[userId] = currentRoles.filter((r) => r !== roleName);
      } else {
        await api.post(`/${tenant}/api/roomusers/${userId}/roles/${roleName}`, {});
        userRoles[userId] = [...currentRoles, roleName];
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to update role assignment';
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

  function hasRole(userId: string, roleName: string): boolean {
    return (userRoles[userId] || []).includes(roleName);
  }

  let selectedUser = $derived.by(() => users.find((u) => u.id === selectedUserId) ?? null);
</script>

<div class="rupp-root">
  {#if error}
    <div class="rupp-error">
      <span>{error}</span>
      <button onclick={() => (error = null)}>Dismiss</button>
    </div>
  {/if}

  <div class="rupp-content">
    {#if isLoading}
      <div class="rupp-loading">
        <div class="rupp-spinner"></div>
        <p>Loading...</p>
      </div>
    {:else}
      <div class="rupp-layout">
        <!-- Left Panel: User List -->
        <div class="rupp-panel rupp-panel--left">
          <div class="rupp-panel-header">
            <span class="rupp-panel-title">Users</span>
          </div>

          <div class="rupp-list">
            {#each users as user}
              {@const userRole = userRoles[user.id] || []}
              {@const isSelected = selectedUserId === user.id}
              <button
                onclick={() => (selectedUserId = user.id)}
                class="rupp-list-item {isSelected ? 'rupp-list-item--active' : ''}"
              >
                <span class="rupp-list-item-name">{user.displayName || user.userName}</span>
                <span class="rupp-badge">{userRole.length}</span>
              </button>
            {/each}
          </div>
        </div>

        <!-- Right Panel: Role Assignment -->
        <div class="rupp-panel rupp-panel--right">
          {#if selectedUser}
            {@const userRole = userRoles[selectedUser.id] || []}
            <div class="rupp-panel-header">
              <div>
                <span class="rupp-panel-title">{selectedUser.displayName || selectedUser.userName}</span>
                <span class="rupp-panel-sub">{userRole.length} role(s)</span>
              </div>
              <button
                onclick={() => removeUser(selectedUser.id)}
                class="rupp-btn rupp-btn--danger"
              >
                Remove
              </button>
            </div>

            <div class="rupp-role-list">
              <p class="rupp-section-label">Room Roles</p>
              {#each roles as role}
                {@const hasUserRole = hasRole(selectedUser.id, role.name)}
                <button
                  onclick={() => toggleRole(selectedUser.id, role.name)}
                  class="rupp-role-item {hasUserRole ? 'rupp-role-item--active' : ''}"
                >
                  <div class="rupp-checkbox {hasUserRole ? 'rupp-checkbox--checked' : ''}">
                    {#if hasUserRole}
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                      </svg>
                    {/if}
                  </div>
                  <span>{role.name}</span>
                </button>
              {/each}
            </div>
          {:else}
            <div class="rupp-empty-panel">
              <p>Select a user to assign roles</p>
            </div>
          {/if}
        </div>
      </div>
    {/if}
  </div>
</div>

<style>
  :global {
    .rupp-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      padding: 0;
    }

    .rupp-error {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      background-color: #fee;
      border-bottom: 1px solid #fcc;
      color: #c33;
      font-size: 14px;
    }

    .rupp-error button {
      background: none;
      border: none;
      color: inherit;
      text-decoration: underline;
      cursor: pointer;
    }

    .rupp-content {
      flex: 1;
      overflow-y: auto;
      padding: 0;
    }

    .rupp-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100%;
      gap: 16px;
      color: var(--color-text-light);
    }

    .rupp-spinner {
      width: 40px;
      height: 40px;
      border: 3px solid var(--color-border);
      border-top-color: var(--color-primary);
      border-radius: 50%;
      animation: rupp-spin 1s linear infinite;
    }

    @keyframes rupp-spin {
      to { transform: rotate(360deg); }
    }

    .rupp-layout {
      display: grid;
      grid-template-columns: 240px 1fr;
      height: 100%;
      gap: 0;
    }

    .rupp-panel {
      display: flex;
      flex-direction: column;
      border-right: 1px solid var(--color-border);
    }

    .rupp-panel--right {
      border-right: none;
    }

    .rupp-panel-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      padding: 12px 16px;
      border-bottom: 1px solid var(--color-border);
      background-color: var(--color-surface);
    }

    .rupp-panel-title {
      font-size: 14px;
      font-weight: 600;
      color: var(--color-text);
    }

    .rupp-panel-sub {
      font-size: 12px;
      color: var(--color-text-light);
      margin-top: 4px;
      display: block;
    }

    .rupp-list {
      flex: 1;
      overflow-y: auto;
      padding: 8px 0;
    }

    .rupp-list-item {
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

    .rupp-list-item:hover {
      background-color: var(--color-surface);
    }

    .rupp-list-item--active {
      background-color: var(--color-surface);
      border-left-color: var(--color-primary);
      color: var(--color-primary);
      font-weight: 500;
    }

    .rupp-list-item-name {
      font-size: 14px;
    }

    .rupp-badge {
      font-size: 12px;
      padding: 2px 6px;
      background-color: var(--color-primary);
      color: white;
      border-radius: 3px;
    }

    .rupp-role-list {
      flex: 1;
      overflow-y: auto;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .rupp-section-label {
      font-size: 12px;
      font-weight: 600;
      color: var(--color-text-light);
      text-transform: uppercase;
      margin: 0;
      margin-top: 8px;
    }

    .rupp-role-item {
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

    .rupp-role-item:hover {
      background-color: var(--color-surface);
    }

    .rupp-role-item--active {
      background-color: rgba(var(--color-primary-rgb), 0.1);
      border-color: var(--color-primary);
    }

    .rupp-checkbox {
      width: 20px;
      height: 20px;
      border: 2px solid var(--color-border);
      border-radius: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .rupp-checkbox--checked {
      background-color: var(--color-primary);
      border-color: var(--color-primary);
      color: white;
    }

    .rupp-checkbox svg {
      width: 16px;
      height: 16px;
    }

    .rupp-btn {
      padding: 6px 12px;
      border: 1px solid var(--color-border);
      border-radius: 4px;
      font-size: 12px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
    }

    .rupp-btn--danger {
      background-color: #dc2626;
      color: white;
      border-color: #dc2626;
      padding: 6px 12px;
      font-size: 12px;
    }

    .rupp-btn--danger:hover {
      opacity: 0.9;
    }

    .rupp-empty-panel {
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
