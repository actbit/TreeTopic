<script lang="ts">
  import { api } from '$lib/api/client';
  import { roomRolePermissionsApi } from '$lib/api/permissions';
  import type { AvailablePermissions, Role } from '$lib/types/permissions';

  interface Props {
    tenant: string;
    roomId: string;
  }

  let { tenant, roomId }: Props = $props();

  type Tab = 'roles' | 'users';
  let activeTab = $state<Tab>('roles');

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
      .map((part) => { if (part === 'room' || part === 'topic') return ''; return part.charAt(0).toUpperCase() + part.slice(1); })
      .filter((p) => p !== '')
      .join(' ').trim();
  }

  let selectedRole = $derived.by(() => roles.find((r) => r.name === selectedRoleName) ?? null);
</script>

<div class="rpp-root">
  {#if error}
    <div class="rpp-error">
      <span>{error}</span>
      <button onclick={() => (error = null)}>Dismiss</button>
    </div>
  {/if}

  <!-- Tabs -->
  <div class="rpp-tabs">
    <button
      onclick={() => (activeTab = 'roles')}
      class="rpp-tab {activeTab === 'roles' ? 'rpp-tab--active' : ''}"
    >
      Role Permissions
    </button>
    <button
      onclick={() => (activeTab = 'users')}
      class="rpp-tab {activeTab === 'users' ? 'rpp-tab--active' : ''}"
    >
      User Roles
    </button>
  </div>

  <!-- Content -->
  <div class="rpp-content">
    {#if isLoading}
      <div class="rpp-loading">
        <div class="rpp-spinner"></div>
        <p>Loading...</p>
      </div>
    {:else if activeTab === 'roles'}
      <div class="rpp-layout">
        <!-- Left Panel: Role List -->
        <div class="rpp-panel rpp-panel--left">
          <div class="rpp-panel-header">
            <span class="rpp-panel-title">Roles</span>
          </div>

          <div class="rpp-list">
            {#each roles as role}
              {@const perms = rolePermissions[role.name] || []}
              {@const isSelected = selectedRoleName === role.name}
              <button
                onclick={() => (selectedRoleName = role.name)}
                class="rpp-list-item {isSelected ? 'rpp-list-item--active' : ''}"
              >
                <span class="rpp-list-item-name">{role.name}</span>
                <span class="rpp-badge">{perms.length}</span>
              </button>
            {/each}

            {#if showCreateRole}
              <div class="rpp-create-form">
                <input
                  type="text"
                  bind:value={newRoleName}
                  placeholder="Role name..."
                  class="rpp-input"
                  onkeydown={(e) => e.key === 'Enter' && newRoleName.trim() && createRole()}
                />
                <textarea
                  bind:value={newRoleDescription}
                  placeholder="Description (optional)..."
                  class="rpp-textarea"
                  rows="2"
                ></textarea>
                <div class="rpp-create-actions">
                  <button onclick={createRole} disabled={!newRoleName.trim()} class="rpp-btn rpp-btn--primary">
                    Create
                  </button>
                  <button onclick={() => { showCreateRole = false; newRoleName = ''; newRoleDescription = ''; }} class="rpp-btn rpp-btn--secondary">
                    Cancel
                  </button>
                </div>
              </div>
            {:else}
              <button onclick={() => (showCreateRole = true)} class="rpp-add-btn">
                + Create Role
              </button>
            {/if}
          </div>
        </div>

        <!-- Right Panel: Permission Details -->
        <div class="rpp-panel rpp-panel--right">
          {#if selectedRole}
            {@const perms = rolePermissions[selectedRole.name] || []}
            <div class="rpp-panel-header">
              <div>
                <span class="rpp-panel-title">{selectedRole.name}</span>
                <span class="rpp-panel-sub">{perms.length} permission(s)</span>
              </div>
              <button
                onclick={() => deleteRole(selectedRole.name)}
                class="rpp-btn rpp-btn--danger"
              >
                Delete
              </button>
            </div>

            <div class="rpp-perm-list">
              <p class="rpp-section-label">Room Permissions</p>
              {#each (availablePermissions.room || []) as perm}
                {@const hasPerm = hasPermission(selectedRole.name, perm.name)}
                <button
                  onclick={() => togglePermission(selectedRole.name, perm.name)}
                  class="rpp-perm-item {hasPerm ? 'rpp-perm-item--active' : ''}"
                >
                  <div class="rpp-checkbox {hasPerm ? 'rpp-checkbox--checked' : ''}">
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
            <div class="rpp-empty-panel">
              <p>Select a role to edit permissions</p>
            </div>
          {/if}
        </div>
      </div>
    {:else}
      <!-- User Roles Tab -->
      <div class="rpp-placeholder">
        <p>User role assignment coming soon</p>
      </div>
    {/if}
  </div>
</div>

<style>
  :global {
    .rpp-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      padding: 0;
    }

    .rpp-error {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      background-color: #fee;
      border-bottom: 1px solid #fcc;
      color: #c33;
      font-size: 14px;
    }

    .rpp-error button {
      background: none;
      border: none;
      color: inherit;
      text-decoration: underline;
      cursor: pointer;
    }

    .rpp-tabs {
      display: flex;
      gap: 0;
      border-bottom: 1px solid var(--color-border);
      padding: 0;
      background-color: var(--color-surface);
    }

    .rpp-tab {
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

    .rpp-tab:hover {
      color: var(--color-text);
    }

    .rpp-tab--active {
      color: var(--color-primary);
      border-bottom-color: var(--color-primary);
    }

    .rpp-content {
      flex: 1;
      overflow-y: auto;
      padding: 0;
    }

    .rpp-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100%;
      gap: 16px;
      color: var(--color-text-light);
    }

    .rpp-spinner {
      width: 40px;
      height: 40px;
      border: 3px solid var(--color-border);
      border-top-color: var(--color-primary);
      border-radius: 50%;
      animation: rpp-spin 1s linear infinite;
    }

    @keyframes rpp-spin {
      to { transform: rotate(360deg); }
    }

    .rpp-layout {
      display: grid;
      grid-template-columns: 240px 1fr;
      height: 100%;
      gap: 0;
    }

    .rpp-panel {
      display: flex;
      flex-direction: column;
      border-right: 1px solid var(--color-border);
    }

    .rpp-panel--right {
      border-right: none;
    }

    .rpp-panel-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      padding: 12px 16px;
      border-bottom: 1px solid var(--color-border);
      background-color: var(--color-surface);
    }

    .rpp-panel-title {
      font-size: 14px;
      font-weight: 600;
      color: var(--color-text);
    }

    .rpp-panel-sub {
      font-size: 12px;
      color: var(--color-text-light);
      margin-top: 4px;
      display: block;
    }

    .rpp-list {
      flex: 1;
      overflow-y: auto;
      padding: 8px 0;
    }

    .rpp-list-item {
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

    .rpp-list-item:hover {
      background-color: var(--color-surface);
    }

    .rpp-list-item--active {
      background-color: var(--color-surface);
      border-left-color: var(--color-primary);
      color: var(--color-primary);
      font-weight: 500;
    }

    .rpp-list-item-name {
      font-size: 14px;
    }

    .rpp-badge {
      font-size: 12px;
      padding: 2px 6px;
      background-color: var(--color-primary);
      color: white;
      border-radius: 3px;
    }

    .rpp-add-btn {
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

    .rpp-add-btn:hover {
      background-color: rgba(var(--color-primary-rgb), 0.05);
    }

    .rpp-create-form {
      margin: 8px;
      padding: 12px;
      border: 1px solid var(--color-border);
      border-radius: 6px;
      background-color: var(--color-surface);
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .rpp-input,
    .rpp-textarea {
      padding: 8px;
      border: 1px solid var(--color-border);
      border-radius: 4px;
      font-size: 13px;
      color: var(--color-text);
      background-color: var(--color-background);
      font-family: inherit;
    }

    .rpp-input:focus,
    .rpp-textarea:focus {
      outline: none;
      border-color: var(--color-primary);
    }

    .rpp-create-actions {
      display: flex;
      gap: 6px;
    }

    .rpp-btn {
      padding: 6px 12px;
      border: 1px solid var(--color-border);
      border-radius: 4px;
      font-size: 12px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
    }

    .rpp-btn--primary {
      background-color: var(--color-primary);
      color: white;
      border-color: var(--color-primary);
    }

    .rpp-btn--primary:hover:not(:disabled) {
      opacity: 0.9;
    }

    .rpp-btn--primary:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .rpp-btn--secondary {
      background-color: transparent;
      color: var(--color-text);
      border-color: var(--color-border);
    }

    .rpp-btn--secondary:hover {
      background-color: var(--color-surface);
    }

    .rpp-btn--danger {
      background-color: #dc2626;
      color: white;
      border-color: #dc2626;
      padding: 6px 12px;
      font-size: 12px;
    }

    .rpp-btn--danger:hover {
      opacity: 0.9;
    }

    .rpp-perm-list {
      flex: 1;
      overflow-y: auto;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .rpp-section-label {
      font-size: 12px;
      font-weight: 600;
      color: var(--color-text-light);
      text-transform: uppercase;
      margin: 0;
      margin-top: 8px;
    }

    .rpp-perm-item {
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

    .rpp-perm-item:hover {
      background-color: var(--color-surface);
    }

    .rpp-perm-item--active {
      background-color: rgba(var(--color-primary-rgb), 0.1);
      border-color: var(--color-primary);
    }

    .rpp-checkbox {
      width: 20px;
      height: 20px;
      border: 2px solid var(--color-border);
      border-radius: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .rpp-checkbox--checked {
      background-color: var(--color-primary);
      border-color: var(--color-primary);
      color: white;
    }

    .rpp-checkbox svg {
      width: 16px;
      height: 16px;
    }

    .rpp-empty-panel {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 100%;
      color: var(--color-text-light);
      font-size: 14px;
      text-align: center;
    }

    .rpp-placeholder {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 100%;
      color: var(--color-text-light);
      font-size: 14px;
    }
  }
</style>
