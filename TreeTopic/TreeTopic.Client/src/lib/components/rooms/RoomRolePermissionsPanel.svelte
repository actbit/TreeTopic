<script lang="ts">
  import { api, roomRolePermissionsApi } from '$lib/api';
  import type { AvailablePermissions, Role } from '$lib/types';

  interface Props {
    tenant: string;
    roomId: string;
  }

  let { tenant, roomId }: Props = $props();

  let roles = $state<Role[]>([]);
  let availablePermissions = $state<AvailablePermissions>({ tenant: [], topic: [], room: [] });
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let showCreateRole = $state(false);
  let newRoleName = $state('');
  let newRoleDescription = $state('');
  let selectedRoleName = $state<string | null>(null);
  let rolePermissions = $state<Record<string, string[]>>({});

  $effect(() => {
    loadData();
  });

  $effect(() => {
    if (roles.length > 0 && !selectedRoleName) {
      selectedRoleName = roles[0].name;
    }
  });

  async function loadData() {
    try {
      isLoading = true;
      const rolesData = await api.get<Role[]>(`/${tenant}/api/rooms/${roomId}/RoomRoles`, { cache: false });
      roles = rolesData;

      const permPromises = roles.map(async (role) => {
        try {
          const perms = await api.get<{ permissions: string[] }>(`/${tenant}/api/rooms/${roomId}/roomroles/${role.name}/permissions`, { cache: false });
          return { roleName: role.name, permissions: perms.permissions || [] };
        } catch {
          return { roleName: role.name, permissions: [] };
        }
      });
      const permsData = await Promise.all(permPromises);
      rolePermissions = {};
      permsData.forEach((p) => { rolePermissions[p.roleName] = p.permissions; });

      availablePermissions = await api.get<AvailablePermissions>(`/${tenant}/api/permissions/available`, { cache: false });
      error = null;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load data';
    } finally {
      isLoading = false;
    }
  }

  async function togglePermission(roleName: string, permissionName: string) {
    try {
      const currentPerms = rolePermissions[roleName] || [];
      const hasPerm = currentPerms.includes(permissionName);
      if (hasPerm) {
        await roomRolePermissionsApi.removePermission(tenant, roomId, roleName, permissionName);
        rolePermissions[roleName] = currentPerms.filter((p) => p !== permissionName);
      } else {
        await roomRolePermissionsApi.addPermission(tenant, roomId, roleName, { permissionName });
        rolePermissions[roleName] = [...currentPerms, permissionName];
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to update permissions';
    }
  }

  async function createRole() {
    if (!newRoleName.trim()) return;
    const createdName = newRoleName.trim();
    try {
      await api.post(`/${tenant}/api/rooms/${roomId}/RoomRoles`, {
        name: createdName,
        description: newRoleDescription.trim()
      });
      newRoleName = '';
      newRoleDescription = '';
      showCreateRole = false;
      await loadData();
      selectedRoleName = createdName;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to create role';
    }
  }

  async function deleteRole(roleName: string) {
    if (!confirm('Delete this role?')) return;
    try {
      await api.delete(`/${tenant}/api/rooms/${roomId}/RoomRoles/${roleName}`);
      selectedRoleName = null;
      await loadData();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to delete role';
    }
  }

  function hasPermission(roleName: string, permissionName: string): boolean {
    return (rolePermissions[roleName] || []).includes(permissionName);
  }

  function formatPermissionName(name: string): string {
    return name.split('.')
      .filter((part) => part !== 'room')
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ');
  }

  let selectedRole = $derived.by(() => roles.find((r) => r.name === selectedRoleName) ?? null);
</script>

<div class="rrpp-root">
  {#if error}
    <div class="rrpp-error">
      <span>{error}</span>
      <button onclick={() => (error = null)}>Dismiss</button>
    </div>
  {/if}

  <div class="rrpp-content">
    {#if isLoading}
      <div class="rrpp-loading">
        <div class="rrpp-spinner"></div>
        <p>Loading...</p>
      </div>
    {:else}
      <div class="rrpp-layout">
        <!-- Left Panel: Role List -->
        <div class="rrpp-panel rrpp-panel--left">
          <div class="rrpp-panel-header">
            <span class="rrpp-panel-title">Roles</span>
          </div>

          <div class="rrpp-list">
            {#each roles as role}
              {@const perms = rolePermissions[role.name] || []}
              {@const isSelected = selectedRoleName === role.name}
              <button
                onclick={() => (selectedRoleName = role.name)}
                class="rrpp-list-item {isSelected ? 'rrpp-list-item--active' : ''}"
              >
                <span class="rrpp-list-item-name">{role.name}</span>
                <span class="rrpp-badge">{perms.length}</span>
              </button>
            {/each}

            {#if showCreateRole}
              <div class="rrpp-create-form">
                <input
                  type="text"
                  bind:value={newRoleName}
                  placeholder="Role name..."
                  class="rrpp-input"
                  onkeydown={(e) => e.key === 'Enter' && newRoleName.trim() && createRole()}
                />
                <textarea
                  bind:value={newRoleDescription}
                  placeholder="Description (optional)..."
                  class="rrpp-textarea"
                  rows="2"
                />
                <div class="rrpp-create-actions">
                  <button onclick={createRole} disabled={!newRoleName.trim()} class="rrpp-btn rrpp-btn--primary">
                    Create
                  </button>
                  <button onclick={() => { showCreateRole = false; newRoleName = ''; newRoleDescription = ''; }} class="rrpp-btn rrpp-btn--secondary">
                    Cancel
                  </button>
                </div>
              </div>
            {:else}
              <button onclick={() => (showCreateRole = true)} class="rrpp-add-btn">
                + Create Role
              </button>
            {/if}
          </div>
        </div>

        <!-- Right Panel: Permission Details -->
        <div class="rrpp-panel rrpp-panel--right">
          {#if selectedRole}
            {@const perms = rolePermissions[selectedRole.name] || []}
            <div class="rrpp-panel-header">
              <div>
                <span class="rrpp-panel-title">{selectedRole.name}</span>
                <span class="rrpp-panel-sub">{perms.length} permission(s)</span>
              </div>
              <button
                onclick={() => deleteRole(selectedRole.name)}
                class="rrpp-btn rrpp-btn--danger"
              >
                Delete
              </button>
            </div>

            <div class="rrpp-perm-list">
              <p class="rrpp-section-label">Room Permissions</p>
              {#each (availablePermissions.room || []) as perm}
                {@const hasPerm = hasPermission(selectedRole.name, perm.name)}
                <button
                  onclick={() => togglePermission(selectedRole.name, perm.name)}
                  class="rrpp-perm-item {hasPerm ? 'rrpp-perm-item--active' : ''}"
                >
                  <div class="rrpp-checkbox {hasPerm ? 'rrpp-checkbox--checked' : ''}">
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
            <div class="rrpp-empty-panel">
              <p>Select a role to edit permissions</p>
            </div>
          {/if}
        </div>
      </div>
    {/if}
  </div>
</div>

<style>
  :global {
    .rrpp-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      padding: 0;
    }

    .rrpp-error {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      background-color: #fee;
      border-bottom: 1px solid #fcc;
      color: #c33;
      font-size: 14px;
    }

    .rrpp-error button {
      background: none;
      border: none;
      color: inherit;
      text-decoration: underline;
      cursor: pointer;
    }

    .rrpp-content {
      flex: 1;
      overflow-y: auto;
      padding: 0;
    }

    .rrpp-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100%;
      gap: 16px;
      color: var(--color-text-light);
    }

    .rrpp-spinner {
      width: 40px;
      height: 40px;
      border: 3px solid var(--color-border);
      border-top-color: var(--color-primary);
      border-radius: 50%;
      animation: rrpp-spin 1s linear infinite;
    }

    @keyframes rrpp-spin {
      to { transform: rotate(360deg); }
    }

    .rrpp-layout {
      display: grid;
      grid-template-columns: 240px 1fr;
      height: 100%;
      gap: 0;
    }

    .rrpp-panel {
      display: flex;
      flex-direction: column;
      border-right: 1px solid var(--color-border);
    }

    .rrpp-panel--right {
      border-right: none;
    }

    .rrpp-panel-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      padding: 12px 16px;
      border-bottom: 1px solid var(--color-border);
      background-color: var(--color-surface);
    }

    .rrpp-panel-title {
      font-size: 14px;
      font-weight: 600;
      color: var(--color-text);
    }

    .rrpp-panel-sub {
      font-size: 12px;
      color: var(--color-text-light);
      margin-top: 4px;
      display: block;
    }

    .rrpp-list {
      flex: 1;
      overflow-y: auto;
      padding: 8px 0;
    }

    .rrpp-list-item {
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

    .rrpp-list-item:hover {
      background-color: var(--color-surface);
    }

    .rrpp-list-item--active {
      background-color: var(--color-surface);
      border-left-color: var(--color-primary);
      color: var(--color-primary);
      font-weight: 500;
    }

    .rrpp-list-item-name {
      font-size: 14px;
    }

    .rrpp-badge {
      font-size: 12px;
      padding: 2px 6px;
      background-color: var(--color-primary);
      color: white;
      border-radius: 3px;
    }

    .rrpp-add-btn {
      margin: 8px;
      padding: 8px 12px;
      border: 1px dashed var(--color-primary);
      background: transparent;
      color: var(--color-primary);
      border-radius: 6px;
      font-size: 13px;
      cursor: pointer;
      transition: all 0.2s;
    }

    .rrpp-add-btn:hover {
      background-color: rgba(var(--color-primary-rgb), 0.05);
    }

    .rrpp-create-form {
      margin: 8px;
      padding: 12px;
      border: 1px solid var(--color-border);
      border-radius: 6px;
      background-color: var(--color-surface);
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .rrpp-input,
    .rrpp-textarea {
      padding: 8px;
      border: 1px solid var(--color-border);
      border-radius: 4px;
      font-size: 13px;
      color: var(--color-text);
      background-color: var(--color-background);
      font-family: inherit;
    }

    .rrpp-input:focus,
    .rrpp-textarea:focus {
      outline: none;
      border-color: var(--color-primary);
    }

    .rrpp-create-actions {
      display: flex;
      gap: 6px;
    }

    .rrpp-btn {
      padding: 6px 12px;
      border: 1px solid var(--color-border);
      border-radius: 4px;
      font-size: 12px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
    }

    .rrpp-btn--primary {
      background-color: var(--color-primary);
      color: white;
      border-color: var(--color-primary);
    }

    .rrpp-btn--primary:hover:not(:disabled) {
      opacity: 0.9;
    }

    .rrpp-btn--primary:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .rrpp-btn--secondary {
      background-color: transparent;
      color: var(--color-text);
      border-color: var(--color-border);
    }

    .rrpp-btn--secondary:hover {
      background-color: var(--color-surface);
    }

    .rrpp-btn--danger {
      background-color: #dc2626;
      color: white;
      border-color: #dc2626;
      padding: 6px 12px;
      font-size: 12px;
    }

    .rrpp-btn--danger:hover {
      opacity: 0.9;
    }

    .rrpp-perm-list {
      flex: 1;
      overflow-y: auto;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .rrpp-section-label {
      font-size: 12px;
      font-weight: 600;
      color: var(--color-text-light);
      text-transform: uppercase;
      margin: 0;
      margin-top: 8px;
    }

    .rrpp-perm-item {
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

    .rrpp-perm-item:hover {
      background-color: var(--color-surface);
    }

    .rrpp-perm-item--active {
      background-color: rgba(var(--color-primary-rgb), 0.1);
      border-color: var(--color-primary);
    }

    .rrpp-checkbox {
      width: 20px;
      height: 20px;
      border: 2px solid var(--color-border);
      border-radius: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .rrpp-checkbox--checked {
      background-color: var(--color-primary);
      border-color: var(--color-primary);
      color: white;
    }

    .rrpp-checkbox svg {
      width: 16px;
      height: 16px;
    }

    .rrpp-empty-panel {
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
