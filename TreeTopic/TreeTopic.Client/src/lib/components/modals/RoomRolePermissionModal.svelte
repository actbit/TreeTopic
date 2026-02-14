<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { api } from '$lib/api/client';
  import { roomRolePermissionsApi } from '$lib/api/permissions';
  import type { AvailablePermissions, Role } from '$lib/types/permissions';
  import { ui, activeModals } from '$lib/stores/ui';
  import { page } from '$app/stores';
  import { formatPermissionName } from '$lib/utils/permission';

  const modalId = 'room-role-permission';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let tenant = $derived.by(() => (modal?.data?.tenant ?? $page.params.tenant ?? '') as string);
  let roomId = $derived.by(() => (modal?.data?.roomId ?? '') as string);

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
    if (isOpen && tenant && roomId) {
      loadData();
      return () => resetState();
    }
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

  async function deleteRole(roleId: string) {
    if (!confirm('Delete this role?')) return;
    try {
      await api.delete(`/${tenant}/api/rooms/${roomId}/RoomRoles/${roleId}`);
      selectedRoleName = null;
      await loadData();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to delete role';
    }
  }

  function hasPermission(roleName: string, permissionName: string): boolean {
    return (rolePermissions[roleName] || []).includes(permissionName);
  }

  let selectedRole = $derived.by(() => roles.find((r) => r.name === selectedRoleName) ?? null);

  function resetState() {
    roles = [];
    rolePermissions = {};
    availablePermissions = { tenant: [], topic: [], room: [] };
    selectedRoleName = null;
    error = null;
    isLoading = true;
  }

  function handleClose() { ui.closeModal(modalId); }
</script>

<Modal {isOpen} title="Room Role Management" onClose={handleClose} size="xlarge" closeButton={!isLoading}>
  <div class="rrm-root">
    {#if error}
      <div class="rrm-error">
        <span>{error}</span>
        <button onclick={() => (error = null)}>Dismiss</button>
      </div>
    {/if}

    <div class="rrm-content">
      {#if isLoading}
        <div class="rrm-loading">
          <div class="rrm-spinner"></div>
          <p>Loading...</p>
        </div>
      {:else}
        <!-- Left Panel -->
        <div class="rrm-panel rrm-panel--left">
          <div class="rrm-panel-header">
            <span class="rrm-panel-title">Roles</span>
          </div>

          <div class="rrm-list">
            {#if roles.length === 0 && !showCreateRole}
              <div class="rrm-empty"><p>No roles</p></div>
            {:else}
              {#each roles as role}
                {@const perms = rolePermissions[role.name] || []}
                {@const isSelected = selectedRoleName === role.name}
                <button
                  onclick={() => (selectedRoleName = role.name)}
                  class="rrm-list-item {isSelected ? 'rrm-list-item--active' : ''}"
                >
                  <div class="rrm-list-item-info">
                    <span class="rrm-list-item-name">{role.name}</span>
                    {#if role.description}
                      <span class="rrm-list-item-sub">{role.description}</span>
                    {/if}
                  </div>
                  <span class="rrm-badge">{perms.length}</span>
                </button>
              {/each}
            {/if}

            {#if showCreateRole}
              <div class="rrm-create-form">
                <input
                  type="text"
                  bind:value={newRoleName}
                  placeholder="Role name..."
                  class="rrm-input"
                  onkeydown={(e) => e.key === 'Enter' && newRoleName.trim() && createRole()}
                />
                <input
                  type="text"
                  bind:value={newRoleDescription}
                  placeholder="Description (optional)..."
                  class="rrm-input"
                />
                <div class="rrm-create-actions">
                  <button onclick={createRole} disabled={!newRoleName.trim()} class="rrm-btn rrm-btn--primary">Create</button>
                  <button onclick={() => { showCreateRole = false; newRoleName = ''; newRoleDescription = ''; }} class="rrm-btn rrm-btn--secondary">Cancel</button>
                </div>
              </div>
            {:else}
              <button onclick={() => (showCreateRole = true)} class="rrm-add-btn">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
                </svg>
                Create Role
              </button>
            {/if}
          </div>
        </div>

        <!-- Right Panel -->
        <div class="rrm-panel rrm-panel--right">
          {#if selectedRole}
            {@const perms = rolePermissions[selectedRole.name] || []}
            <div class="rrm-panel-header">
              <div>
                <span class="rrm-panel-title">{selectedRole.name}</span>
                <span class="rrm-panel-sub">{perms.length}  permission(s)</span>
              </div>
              <button onclick={() => deleteRole(selectedRole.id)} class="rrm-btn rrm-btn--danger">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
                Delete
              </button>
            </div>

            <div class="rrm-perm-list">
              {#if availablePermissions.room?.length}
                <p class="rrm-section-label">Room Permissions</p>
                {#each availablePermissions.room as perm}
                  {@const hasPerm = hasPermission(selectedRole.name, perm.name)}
                  <button onclick={() => togglePermission(selectedRole.name, perm.name)} class="rrm-perm-item {hasPerm ? 'rrm-perm-item--active' : ''}">
                    <div class="rrm-checkbox {hasPerm ? 'rrm-checkbox--checked' : ''}">
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

              {#if availablePermissions.topic?.length}
                <p class="rrm-section-label" style="margin-top: 16px;">Topic Permissions</p>
                {#each availablePermissions.topic as perm}
                  {@const hasPerm = hasPermission(selectedRole.name, perm.name)}
                  <button onclick={() => togglePermission(selectedRole.name, perm.name)} class="rrm-perm-item {hasPerm ? 'rrm-perm-item--active' : ''}">
                    <div class="rrm-checkbox {hasPerm ? 'rrm-checkbox--checked' : ''}">
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

              {#if !availablePermissions.room?.length && !availablePermissions.topic?.length}
                <div class="rrm-empty"><p>No permissions available</p></div>
              {/if}
            </div>
          {:else}
            <div class="rrm-empty-panel">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
              <p>Select a role to edit permissions</p>
            </div>
          {/if}
        </div>
      {/if}
    </div>
  </div>
</Modal>

<style>
  .rrm-root { display: flex; flex-direction: column; height: 600px; }

  .rrm-error {
    display: flex; justify-content: space-between; align-items: center;
    margin: 16px 24px 0; padding: 10px 14px;
    background: var(--color-error-light, #fef2f2);
    border: 1px solid var(--color-error, #ef4444);
    border-radius: 8px; font-size: 13px; color: var(--color-error, #ef4444);
  }
  .rrm-error button { font-size: 12px; text-decoration: underline; background: none; border: none; cursor: pointer; color: inherit; margin-left: 12px; }

  .rrm-content { flex: 1; display: flex; gap: 16px; padding: 16px 24px 24px; overflow: hidden; min-height: 0; }

  .rrm-loading { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; color: var(--color-text-light); font-size: 13px; }
  .rrm-spinner { width: 28px; height: 28px; border: 3px solid var(--color-border); border-top-color: var(--color-primary); border-radius: 50%; animation: rrm-spin 0.8s linear infinite; }

  .rrm-panel { display: flex; flex-direction: column; border: 1px solid var(--color-border); border-radius: 10px; overflow: hidden; background: var(--color-background); }
  .rrm-panel--left { width: 240px; flex-shrink: 0; }
  .rrm-panel--right { flex: 1; min-width: 0; }

  .rrm-panel-header { display: flex; align-items: center; justify-content: space-between; padding: 14px 16px; border-bottom: 1px solid var(--color-border); background: var(--color-surface); flex-shrink: 0; gap: 8px; }
  .rrm-panel-title { font-size: 14px; font-weight: 600; color: var(--color-text); }
  .rrm-panel-sub { display: block; font-size: 12px; color: var(--color-text-light); margin-top: 2px; }

  .rrm-list { flex: 1; overflow-y: auto; padding: 8px; display: flex; flex-direction: column; gap: 2px; }
  .rrm-list-item { display: flex; align-items: center; justify-content: space-between; padding: 9px 12px; border-radius: 7px; border: 1px solid transparent; background: none; cursor: pointer; text-align: left; transition: background 0.12s, border-color 0.12s; width: 100%; }
  .rrm-list-item:hover { background: var(--color-surface); border-color: var(--color-border); }
  .rrm-list-item--active { background: color-mix(in srgb, var(--color-primary) 10%, transparent); border-color: var(--color-primary); }
  .rrm-list-item--active .rrm-list-item-name { color: var(--color-primary); }
  .rrm-list-item-info { display: flex; flex-direction: column; min-width: 0; flex: 1; }
  .rrm-list-item-name { font-size: 13px; font-weight: 500; color: var(--color-text); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .rrm-list-item-sub { font-size: 11px; color: var(--color-text-light); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; margin-top: 1px; }

  .rrm-add-btn { display: flex; align-items: center; gap: 6px; padding: 8px 12px; width: 100%; border-radius: 7px; border: 1px dashed var(--color-border); background: none; color: var(--color-text-light); font-size: 13px; cursor: pointer; transition: background 0.12s, color 0.12s, border-color 0.12s; margin-top: 4px; }
  .rrm-add-btn svg { width: 14px; height: 14px; flex-shrink: 0; }
  .rrm-add-btn:hover { background: color-mix(in srgb, var(--color-primary) 6%, transparent); color: var(--color-primary); border-color: var(--color-primary); }

  .rrm-create-form { display: flex; flex-direction: column; gap: 8px; padding: 4px 0; margin-top: 4px; }
  .rrm-input { width: 100%; padding: 8px 10px; font-size: 13px; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-background); color: var(--color-text); outline: none; box-sizing: border-box; }
  .rrm-input:focus { border-color: var(--color-primary); box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary) 20%, transparent); }
  .rrm-create-actions { display: flex; gap: 8px; }

  .rrm-btn { display: inline-flex; align-items: center; gap: 6px; padding: 7px 14px; font-size: 13px; font-weight: 500; border-radius: 6px; border: 1px solid transparent; cursor: pointer; transition: background 0.15s, opacity 0.15s; }
  .rrm-btn svg { width: 14px; height: 14px; }
  .rrm-btn--primary { flex: 1; justify-content: center; background: var(--color-primary); color: white; border-color: var(--color-primary); }
  .rrm-btn--primary:hover:not(:disabled) { opacity: 0.9; }
  .rrm-btn--primary:disabled { opacity: 0.4; cursor: not-allowed; }
  .rrm-btn--secondary { flex: 1; justify-content: center; background: var(--color-background); color: var(--color-text); border-color: var(--color-border); }
  .rrm-btn--secondary:hover { background: var(--color-surface); }
  .rrm-btn--danger { background: none; color: var(--color-error, #ef4444); border-color: var(--color-error, #ef4444); }
  .rrm-btn--danger:hover { background: color-mix(in srgb, var(--color-error, #ef4444) 8%, transparent); }

  .rrm-badge { font-size: 11px; color: var(--color-text-light); background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 20px; padding: 1px 8px; flex-shrink: 0; }

  .rrm-perm-list { flex: 1; overflow-y: auto; padding: 16px 20px; display: flex; flex-direction: column; gap: 6px; }
  .rrm-section-label { font-size: 11px; font-weight: 600; color: var(--color-text-light); text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 6px; }
  .rrm-perm-item { display: flex; align-items: center; gap: 12px; padding: 10px 12px; border-radius: 7px; border: 1px solid var(--color-border); background: var(--color-background); cursor: pointer; text-align: left; font-size: 13px; color: var(--color-text); transition: background 0.12s, border-color 0.12s; width: 100%; }
  .rrm-perm-item:hover { background: var(--color-surface); border-color: color-mix(in srgb, var(--color-primary) 40%, transparent); }
  .rrm-perm-item--active { background: color-mix(in srgb, var(--color-primary) 6%, var(--color-background)); border-color: var(--color-primary); }

  .rrm-checkbox { width: 18px; height: 18px; border-radius: 4px; border: 2px solid var(--color-border); display: flex; align-items: center; justify-content: center; flex-shrink: 0; transition: background 0.12s, border-color 0.12s; }
  .rrm-checkbox svg { width: 11px; height: 11px; stroke: white; }
  .rrm-checkbox--checked { background: var(--color-primary); border-color: var(--color-primary); }

  .rrm-empty { padding: 24px 16px; text-align: center; color: var(--color-text-light); font-size: 13px; }
  .rrm-empty-panel { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; color: var(--color-text-light); font-size: 13px; }
  .rrm-empty-panel svg { width: 48px; height: 48px; opacity: 0.3; }

  @keyframes rrm-spin { to { transform: rotate(360deg); } }
</style>
