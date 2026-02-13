<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { api, tenantRolePermissionsApi, assignUserRole, removeUserRole } from '$lib/api';
  import type { AvailablePermissions, Role } from '$lib/types';
  import { ui, activeModals } from '$lib/stores/ui';
  import { page } from '$app/stores';

  const modalId = 'tenant-role-permission';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let tenant = $derived.by(() => (modal?.data?.tenant ?? $page.params.tenant ?? '') as string);

  // Tab state
  type Tab = 'roles' | 'users';
  let activeTab = $state<Tab>('roles');

  let roles = $state<Role[]>([]);
  let availablePermissions = $state<AvailablePermissions>({ tenant: [], topic: [], room: [] });
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let showCreateRole = $state(false);
  let newRoleName = $state('');

  // Permission assignment state
  let rolePermissions = $state<Record<string, string[]>>({});

  // Selected role for master/detail view
  let selectedRoleName = $state<string | null>(null);

  // User role assignment state
  interface UserSummary {
    id: string;
    userName: string;
    email: string;
    displayName: string;
    iconUrl?: string;
    roles: string[];
    isBanned?: boolean;
  }
  let users = $state<UserSummary[]>([]);
  let selectedUserId = $state<string | null>(null);
  let isLoadingUsers = $state(false);

  $effect(() => {
    if (isOpen && tenant) {
      loadData();
      return () => resetState();
    }
  });

  $effect(() => {
    // Auto-select first role when roles are loaded
    if (roles.length > 0 && !selectedRoleName) {
      selectedRoleName = roles[0].name;
    }
  });

  async function loadData() {
    try {
      isLoading = true;

      // Fetch roles
      const rolesData = await api.get<Role[]>(`/${tenant}/api/roles`, { cache: false });
      roles = rolesData;

      // Fetch permissions for each role
      const permPromises = roles.map(async (role) => {
        try {
          const perms = await api.get<{ permissions: string[] }>(`/${tenant}/api/tenantroles/${role.name}/permissions`, { cache: false });
          return { roleName: role.name, permissions: perms.permissions || [] };
        } catch {
          return { roleName: role.name, permissions: [] };
        }
      });

      const permsData = await Promise.all(permPromises);
      rolePermissions = {};
      permsData.forEach((p) => {
        rolePermissions[p.roleName] = p.permissions;
      });

      // Fetch available permissions
      availablePermissions = await api.get<AvailablePermissions>(`/${tenant}/api/permissions/available`, { cache: false });

      error = null;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load data';
    } finally {
      isLoading = false;
    }
  }

  async function loadUsers() {
    if (users.length > 0) return;
    try {
      isLoadingUsers = true;
      const userData = await api.get<UserSummary[]>(`/${tenant}/api/users`, { cache: false });
      users = userData;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load users';
    } finally {
      isLoadingUsers = false;
    }
  }

  $effect(() => {
    if (activeTab === 'users' && users.length === 0) {
      loadUsers();
    }
  });

  async function togglePermission(roleName: string, permissionName: string) {
    try {
      const currentPerms = rolePermissions[roleName] || [];
      const hasPermission = currentPerms.includes(permissionName);

      if (hasPermission) {
        // Remove permission
        await tenantRolePermissionsApi.removePermission(tenant, roleName, permissionName);
        rolePermissions[roleName] = currentPerms.filter((p) => p !== permissionName);
      } else {
        // Add permission
        await tenantRolePermissionsApi.addPermission(tenant, roleName, { permissionName });
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
      await api.post(`/${tenant}/api/roles`, {
        name: createdName
      });

      newRoleName = '';
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
      await api.delete(`/${tenant}/api/roles/${roleName}`);
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
    return name
      .split('.')
      .map((part) => {
        if (part === 'tenant') return '';
        return part.charAt(0).toUpperCase() + part.slice(1);
      })
      .filter(p => p !== '')
      .join(' ')
      .trim();
  }

  let selectedRole = $derived.by(() => roles.find((r) => r.name === selectedRoleName) ?? null);
  let selectedUser = $derived.by(() => users.find((u) => u.id === selectedUserId) ?? null);

  function resetState() {
    roles = [];
    users = [];
    rolePermissions = {};
    availablePermissions = { tenant: [], topic: [], room: [] };
    selectedRoleName = null;
    selectedUserId = null;
    error = null;
    isLoading = true;
  }

  function handleClose() {
    ui.closeModal(modalId);
  }

  function selectRole(roleName: string) {
    selectedRoleName = roleName;
  }

  function selectUser(userId: string) {
    selectedUserId = userId;
  }

  async function toggleUserRole(userId: string, roleName: string) {
    try {
      const user = users.find(u => u.id === userId);
      if (!user) return;

      const hasRole = user.roles.includes(roleName);

      if (hasRole) {
        // Remove role - use the imported function
        await removeUserRole(tenant, userId, roleName);
        // Optimistically update UI
        user.roles = user.roles.filter(r => r !== roleName);
      } else {
        // Add role - use the imported function
        await assignUserRole(tenant, userId, roleName);
        // Optimistically update UI
        user.roles = [...user.roles, roleName];
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to update user role';
      // Revert by reloading user data on error
      await refreshUser(userId);
    }
  }

  async function refreshUser(userId: string) {
    try {
      const userData = await api.get<UserSummary>(`/${tenant}/api/users/${userId}`, { cache: false });
      const index = users.findIndex(u => u.id === userId);
      if (index !== -1) {
        users[index] = userData;
      }
    } catch (err) {
    }
  }
</script>

<Modal {isOpen} title="Tenant Management" onClose={handleClose} size="xlarge" closeButton={!isLoading}>
  <div class="trm-root">
    <!-- Error message -->
    {#if error}
      <div class="trm-error">
        <span>{error}</span>
        <button onclick={() => (error = null)}>Dismiss</button>
      </div>
    {/if}

    <!-- Tabs -->
    <div class="trm-tabs">
      <button
        onclick={() => (activeTab = 'roles')}
        class="trm-tab {activeTab === 'roles' ? 'trm-tab--active' : ''}"
      >
        ロール権限
      </button>
      <button
        onclick={() => (activeTab = 'users')}
        class="trm-tab {activeTab === 'users' ? 'trm-tab--active' : ''}"
      >
        ユーザーロール
      </button>
    </div>

    <!-- Content -->
    <div class="trm-content">
      {#if isLoading}
        <div class="trm-loading">
          <div class="trm-spinner"></div>
          <p>Loading...</p>
        </div>
      {:else if activeTab === 'roles'}
        <!-- Left Panel: Role List -->
        <div class="trm-panel trm-panel--left">
          <div class="trm-panel-header">
            <span class="trm-panel-title">Roles</span>
          </div>

          <div class="trm-list">
            {#if roles.length === 0 && !showCreateRole}
              <div class="trm-empty">
                <p>No roles</p>
              </div>
            {:else}
              {#each roles as role}
                {@const perms = rolePermissions[role.name] || []}
                {@const isSelected = selectedRoleName === role.name}
                <button
                  onclick={() => selectRole(role.name)}
                  class="trm-list-item {isSelected ? 'trm-list-item--active' : ''}"
                >
                  <span class="trm-list-item-name">{role.name}</span>
                  <span class="trm-badge">{perms.length}</span>
                </button>
              {/each}
            {/if}

            {#if showCreateRole}
              <div class="trm-create-form">
                <input
                  type="text"
                  bind:value={newRoleName}
                  placeholder="Role name..."
                  class="trm-input"
                  onkeydown={(e) => e.key === 'Enter' && newRoleName.trim() && createRole()}
                />
                <div class="trm-create-actions">
                  <button onclick={createRole} disabled={!newRoleName.trim()} class="trm-btn trm-btn--primary">
                    作成
                  </button>
                  <button onclick={() => { showCreateRole = false; newRoleName = ''; }} class="trm-btn trm-btn--secondary">
                    キャンセル
                  </button>
                </div>
              </div>
            {:else}
              <button onclick={() => (showCreateRole = true)} class="trm-add-btn">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
                </svg>
                ロールを作成
              </button>
            {/if}
          </div>
        </div>

        <!-- Right Panel: Permission Details -->
        <div class="trm-panel trm-panel--right">
          {#if selectedRole}
            {@const perms = rolePermissions[selectedRole.name] || []}
            <div class="trm-panel-header">
              <div>
                <span class="trm-panel-title">{selectedRole.name}</span>
                <span class="trm-panel-sub">{perms.length}  permission(s)</span>
              </div>
              <button
                onclick={() => deleteRole(selectedRole.name)}
                class="trm-btn trm-btn--danger"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
                削除
              </button>
            </div>

            <div class="trm-perm-list">
              <p class="trm-section-label">Tenant Permissions</p>
              {#each (availablePermissions.tenant || []) as perm}
                {@const hasPerm = hasPermission(selectedRole.name, perm.name)}
                <button
                  onclick={() => togglePermission(selectedRole.name, perm.name)}
                  class="trm-perm-item {hasPerm ? 'trm-perm-item--active' : ''}"
                >
                  <div class="trm-checkbox {hasPerm ? 'trm-checkbox--checked' : ''}">
                    {#if hasPerm}
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                      </svg>
                    {/if}
                  </div>
                  <span>{formatPermissionName(perm.name)}</span>
                </button>
              {/each}
              {#if !availablePermissions.tenant?.length}
                <div class="trm-empty">
                  <p>No tenant permissions available</p>
                </div>
              {/if}
            </div>
          {:else}
            <div class="trm-empty-panel">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
              <p>Select a role to edit permissions</p>
            </div>
          {/if}
        </div>

      {:else}
        <!-- Left Panel: User List -->
        <div class="trm-panel trm-panel--left">
          <div class="trm-panel-header">
            <span class="trm-panel-title">Users</span>
          </div>

          <div class="trm-list">
            {#if isLoadingUsers}
              <div class="trm-loading">
                <div class="trm-spinner trm-spinner--sm"></div>
              </div>
            {:else if users.length === 0}
              <div class="trm-empty">
                <p>No users</p>
              </div>
            {:else}
              {#each users as user}
                {@const isSelected = selectedUserId === user.id}
                <button
                  onclick={() => selectUser(user.id)}
                  class="trm-user-item {isSelected ? 'trm-list-item--active' : ''}"
                >
                  <div class="trm-avatar">
                    {#if user.iconUrl}
                      <img src={user.iconUrl} alt={user.displayName} />
                    {:else}
                      {user.displayName?.charAt(0)?.toUpperCase() || user.userName?.charAt(0)?.toUpperCase() || '?'}
                    {/if}
                  </div>
                  <span class="trm-user-name">{user.displayName || user.userName}</span>
                  <span class="trm-badge">{user.roles.length}</span>
                </button>
              {/each}
            {/if}
          </div>
        </div>

        <!-- Right Panel: User Role Assignment -->
        <div class="trm-panel trm-panel--right">
          {#if selectedUser}
            <div class="trm-panel-header">
              <div class="trm-user-header">
                <div class="trm-avatar trm-avatar--lg">
                  {#if selectedUser.iconUrl}
                    <img src={selectedUser.iconUrl} alt={selectedUser.displayName} />
                  {:else}
                    {selectedUser.displayName?.charAt(0)?.toUpperCase() || selectedUser.userName?.charAt(0)?.toUpperCase() || '?'}
                  {/if}
                </div>
                <div>
                  <span class="trm-panel-title">{selectedUser.displayName || selectedUser.userName}</span>
                  <span class="trm-panel-sub">{selectedUser.roles.length}  role(s)</span>
                </div>
              </div>
            </div>

            <div class="trm-perm-list">
              <p class="trm-section-label">Assign Roles</p>
              {#each roles as role}
                {@const hasRole = selectedUser.roles.includes(role.name)}
                <button
                  onclick={() => toggleUserRole(selectedUser.id, role.name)}
                  class="trm-perm-item {hasRole ? 'trm-perm-item--active' : ''}"
                >
                  <div class="trm-checkbox {hasRole ? 'trm-checkbox--checked' : ''}">
                    {#if hasRole}
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                      </svg>
                    {/if}
                  </div>
                  <span>{role.name}</span>
                </button>
              {/each}
              {#if roles.length === 0}
                <div class="trm-empty">
                  <p>No roles</p>
                  <p class="trm-empty-sub">Create one in the Role Permissions tab</p>
                </div>
              {/if}
            </div>
          {:else}
            <div class="trm-empty-panel">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
              <p>Select a user to manage roles</p>
            </div>
          {/if}
        </div>
      {/if}
    </div>
  </div>
</Modal>

<style>
  .trm-root {
    display: flex;
    flex-direction: column;
    height: 600px;
  }

  /* Error */
  .trm-error {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin: 16px 24px 0;
    padding: 10px 14px;
    background: var(--color-error-light, #fef2f2);
    border: 1px solid var(--color-error, #ef4444);
    border-radius: 8px;
    font-size: 13px;
    color: var(--color-error, #ef4444);
  }
  .trm-error button {
    font-size: 12px;
    text-decoration: underline;
    background: none;
    border: none;
    cursor: pointer;
    color: inherit;
    flex-shrink: 0;
    margin-left: 12px;
  }

  /* Tabs */
  .trm-tabs {
    display: flex;
    gap: 0;
    border-bottom: 1px solid var(--color-border);
    margin: 0 24px;
    padding-top: 16px;
    flex-shrink: 0;
  }
  .trm-tab {
    padding: 10px 20px;
    font-size: 13px;
    font-weight: 500;
    border: none;
    background: none;
    cursor: pointer;
    color: var(--color-text-light);
    border-bottom: 2px solid transparent;
    margin-bottom: -1px;
    transition: color 0.15s, border-color 0.15s;
  }
  .trm-tab:hover {
    color: var(--color-text);
  }
  .trm-tab--active {
    color: var(--color-primary);
    border-bottom-color: var(--color-primary);
  }

  /* Content Area */
  .trm-content {
    flex: 1;
    display: flex;
    gap: 16px;
    padding: 16px 24px 24px;
    overflow: hidden;
    min-height: 0;
  }

  /* Loading */
  .trm-loading {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 10px;
    color: var(--color-text-light);
    font-size: 13px;
  }
  .trm-spinner {
    width: 28px;
    height: 28px;
    border: 3px solid var(--color-border);
    border-top-color: var(--color-primary);
    border-radius: 50%;
    animation: trm-spin 0.8s linear infinite;
  }
  .trm-spinner--sm {
    width: 20px;
    height: 20px;
    border-width: 2px;
  }

  /* Panel */
  .trm-panel {
    display: flex;
    flex-direction: column;
    border: 1px solid var(--color-border);
    border-radius: 10px;
    overflow: hidden;
    background: var(--color-background);
  }
  .trm-panel--left {
    width: 240px;
    flex-shrink: 0;
  }
  .trm-panel--right {
    flex: 1;
    min-width: 0;
  }

  .trm-panel-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 14px 16px;
    border-bottom: 1px solid var(--color-border);
    background: var(--color-surface);
    flex-shrink: 0;
    gap: 8px;
  }
  .trm-panel-title {
    font-size: 14px;
    font-weight: 600;
    color: var(--color-text);
  }
  .trm-panel-sub {
    display: block;
    font-size: 12px;
    color: var(--color-text-light);
    margin-top: 2px;
  }

  /* Create form */
  .trm-add-btn {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 8px 12px;
    width: 100%;
    border-radius: 7px;
    border: 1px dashed var(--color-border);
    background: none;
    color: var(--color-text-light);
    font-size: 13px;
    cursor: pointer;
    transition: background 0.12s, color 0.12s, border-color 0.12s;
    margin-top: 4px;
  }
  .trm-add-btn svg {
    width: 14px;
    height: 14px;
    flex-shrink: 0;
  }
  .trm-add-btn:hover {
    background: color-mix(in srgb, var(--color-primary) 6%, transparent);
    color: var(--color-primary);
    border-color: var(--color-primary);
  }

  .trm-create-form {
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 4px 0;
    margin-top: 4px;
  }
  .trm-input {
    width: 100%;
    padding: 8px 10px;
    font-size: 13px;
    border: 1px solid var(--color-border);
    border-radius: 6px;
    background: var(--color-background);
    color: var(--color-text);
    outline: none;
    box-sizing: border-box;
  }
  .trm-input:focus {
    border-color: var(--color-primary);
    box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary) 20%, transparent);
  }
  .trm-create-actions {
    display: flex;
    gap: 8px;
  }

  /* Buttons */
  .trm-btn {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 7px 14px;
    font-size: 13px;
    font-weight: 500;
    border-radius: 6px;
    border: 1px solid transparent;
    cursor: pointer;
    transition: background 0.15s, opacity 0.15s;
  }
  .trm-btn svg {
    width: 14px;
    height: 14px;
  }
  .trm-btn--primary {
    flex: 1;
    justify-content: center;
    background: var(--color-primary);
    color: white;
    border-color: var(--color-primary);
  }
  .trm-btn--primary:hover:not(:disabled) {
    opacity: 0.9;
  }
  .trm-btn--primary:disabled {
    opacity: 0.4;
    cursor: not-allowed;
  }
  .trm-btn--secondary {
    flex: 1;
    justify-content: center;
    background: var(--color-background);
    color: var(--color-text);
    border-color: var(--color-border);
  }
  .trm-btn--secondary:hover {
    background: var(--color-surface);
  }
  .trm-btn--danger {
    background: none;
    color: var(--color-error, #ef4444);
    border-color: var(--color-error, #ef4444);
  }
  .trm-btn--danger:hover {
    background: color-mix(in srgb, var(--color-error, #ef4444) 8%, transparent);
  }

  /* List */
  .trm-list {
    flex: 1;
    overflow-y: auto;
    padding: 8px;
    display: flex;
    flex-direction: column;
    gap: 2px;
  }
  .trm-list-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 9px 12px;
    border-radius: 7px;
    border: 1px solid transparent;
    background: none;
    cursor: pointer;
    text-align: left;
    transition: background 0.12s, border-color 0.12s;
    width: 100%;
  }
  .trm-list-item:hover {
    background: var(--color-surface);
    border-color: var(--color-border);
  }
  .trm-list-item--active {
    background: color-mix(in srgb, var(--color-primary) 10%, transparent);
    border-color: var(--color-primary);
  }
  .trm-list-item--active .trm-list-item-name {
    color: var(--color-primary);
  }
  .trm-list-item-name {
    font-size: 13px;
    font-weight: 500;
    color: var(--color-text);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  /* User item */
  .trm-user-item {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 12px;
    border-radius: 7px;
    border: 1px solid transparent;
    background: none;
    cursor: pointer;
    text-align: left;
    transition: background 0.12s, border-color 0.12s;
    width: 100%;
  }
  .trm-user-item:hover {
    background: var(--color-surface);
    border-color: var(--color-border);
  }
  .trm-user-item.trm-list-item--active {
    background: color-mix(in srgb, var(--color-primary) 10%, transparent);
    border-color: var(--color-primary);
  }
  .trm-user-name {
    flex: 1;
    min-width: 0;
    font-size: 13px;
    font-weight: 500;
    color: var(--color-text);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .trm-user-header {
    display: flex;
    align-items: center;
    gap: 12px;
    min-width: 0;
  }

  /* Avatar */
  .trm-avatar {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    background: linear-gradient(135deg, var(--color-primary), color-mix(in srgb, var(--color-primary) 60%, white));
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 13px;
    font-weight: 600;
    color: white;
    flex-shrink: 0;
    overflow: hidden;
  }
  .trm-avatar img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }
  .trm-avatar--lg {
    width: 40px;
    height: 40px;
    font-size: 16px;
  }

  /* Badge */
  .trm-badge {
    font-size: 11px;
    color: var(--color-text-light);
    background: var(--color-surface);
    border: 1px solid var(--color-border);
    border-radius: 20px;
    padding: 1px 8px;
    flex-shrink: 0;
  }

  /* Permission list */
  .trm-perm-list {
    flex: 1;
    overflow-y: auto;
    padding: 16px 20px;
    display: flex;
    flex-direction: column;
    gap: 6px;
  }
  .trm-section-label {
    font-size: 11px;
    font-weight: 600;
    color: var(--color-text-light);
    text-transform: uppercase;
    letter-spacing: 0.05em;
    margin-bottom: 6px;
  }
  .trm-perm-item {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 12px;
    border-radius: 7px;
    border: 1px solid var(--color-border);
    background: var(--color-background);
    cursor: pointer;
    text-align: left;
    font-size: 13px;
    color: var(--color-text);
    transition: background 0.12s, border-color 0.12s;
    width: 100%;
  }
  .trm-perm-item:hover {
    background: var(--color-surface);
    border-color: color-mix(in srgb, var(--color-primary) 40%, transparent);
  }
  .trm-perm-item--active {
    background: color-mix(in srgb, var(--color-primary) 6%, var(--color-background));
    border-color: var(--color-primary);
  }

  .trm-checkbox {
    width: 18px;
    height: 18px;
    border-radius: 4px;
    border: 2px solid var(--color-border);
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    transition: background 0.12s, border-color 0.12s;
  }
  .trm-checkbox svg {
    width: 11px;
    height: 11px;
    stroke: white;
  }
  .trm-checkbox--checked {
    background: var(--color-primary);
    border-color: var(--color-primary);
  }

  /* Empty states */
  .trm-empty {
    padding: 24px 16px;
    text-align: center;
    color: var(--color-text-light);
    font-size: 13px;
  }
  .trm-empty-sub {
    font-size: 11px;
    margin-top: 4px;
    color: var(--color-text-light);
    opacity: 0.7;
  }
  .trm-empty-panel {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 12px;
    color: var(--color-text-light);
    font-size: 13px;
  }
  .trm-empty-panel svg {
    width: 48px;
    height: 48px;
    opacity: 0.3;
  }

  @keyframes trm-spin {
    to { transform: rotate(360deg); }
  }
</style>
