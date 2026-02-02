<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { onMount } from 'svelte';
  import { api } from '$lib/api/client';
  import { ui, activeModals } from '$lib/stores/ui';
  import { page } from '$app/stores';

  const modalId = 'identity-role-permission';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let tenant = $derived.by(() => modal?.data?.tenant ?? $page.params.tenant ?? '');

  let roles = $state<any[]>([]);
  let availablePermissions = $state<any[]>([]);
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let showCreateRole = $state(false);
  let newRoleName = $state('');

  // Permission assignment state
  let rolePermissions = $state<Record<string, string[]>>({});

  $effect(() => {
    if (isOpen && tenant) {
      loadData();
    }
  });

  async function loadData() {
    try {
      isLoading = true;

      // Fetch roles
      const rolesData = await api.get<any[]>(`/${tenant}/api/roles`);
      roles = rolesData;

      // Fetch permissions for each role
      const permPromises = roles.map(async (role) => {
        try {
          const perms = await api.get<any>(`/${tenant}/api/identityroles/${role.name}/permissions`);
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
      availablePermissions = await api.get<any>(`/${tenant}/api/identityroles/${roles[0]?.name || '_'}/permissions/available`);
      if (!Array.isArray(availablePermissions)) {
        availablePermissions = [];
      }

      error = null;
    } catch (err: any) {
      error = err.message || 'Failed to load data';
    } finally {
      isLoading = false;
    }
  }

  async function togglePermission(roleName: string, permissionName: string) {
    try {
      const currentPerms = rolePermissions[roleName] || [];
      const hasPermission = currentPerms.includes(permissionName);

      if (hasPermission) {
        // Remove permission
        await api.delete(`/${tenant}/api/identityroles/${roleName}/permissions/${encodeURIComponent(permissionName)}`);
        rolePermissions[roleName] = currentPerms.filter((p) => p !== permissionName);
      } else {
        // Add permission
        await api.post(`/${tenant}/api/identityroles/${roleName}/permissions`, { permissionName });
        rolePermissions[roleName] = [...currentPerms, permissionName];
      }
    } catch (err: any) {
      error = err.message || 'Failed to update permissions';
    }
  }

  async function createRole() {
    if (!newRoleName.trim()) return;

    try {
      await api.post(`/${tenant}/api/roles`, {
        name: newRoleName.trim()
      });

      newRoleName = '';
      showCreateRole = false;
      await loadData();
    } catch (err: any) {
      error = err.message || 'Failed to create role';
    }
  }

  async function deleteRole(roleName: string) {
    if (!confirm('このロールを削除しますか？')) return;

    try {
      await api.delete(`/${tenant}/api/roles/${roleName}`);
      await loadData();
    } catch (err: any) {
      error = err.message || 'Failed to delete role';
    }
  }

  function hasPermission(roleName: string, permissionName: string): boolean {
    return (rolePermissions[roleName] || []).includes(permissionName);
  }

  function formatPermissionName(name: string): string {
    return name
      .split('.')
      .map((part) => {
        if (part === 'identity') return '';
        return part.charAt(0).toUpperCase() + part.slice(1);
      })
      .filter(p => p !== '')
      .join(' ')
      .trim();
  }

  function handleClose() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Tenant Role Permission Management" onClose={handleClose} size="xlarge" closeButton={!isLoading}>
  <div class="flex flex-col h-full bg-white">
    <!-- Error message -->
    {#if error}
      <div class="p-4 bg-red-50 border-b border-red-200 text-red-800 text-sm flex justify-between items-center">
        <span>{error}</span>
        <button onclick={() => (error = null)} class="underline hover:no-underline">Close</button>
      </div>
    {/if}

    <!-- Content -->
    <div class="flex-1 overflow-auto p-6 space-y-6">
      {#if isLoading}
        <div class="text-center py-8">
          <div class="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          <p class="mt-2 text-sm text-text-light">Loading...</p>
        </div>
      {:else}
        <!-- Create new role button -->
        <div class="flex justify-between items-center">
          <h3 class="text-lg font-semibold text-text">Role List</h3>
          <button
            onclick={() => (showCreateRole = true)}
            disabled={showCreateRole}
            class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium disabled:opacity-50"
          >
            + Create Role
          </button>
        </div>

        {#if showCreateRole}
          <div class="border border-border rounded-lg p-4 bg-surface">
            <h4 class="font-medium text-text mb-3">Create New Role</h4>
            <div class="space-y-3">
              <div>
                <label for="identity-role-name-input" class="block text-sm font-medium text-text mb-1">Role Name</label>
                <input
                  id="identity-role-name-input"
                  type="text"
                  bind:value={newRoleName}
                  placeholder="e.g. Administrators, Moderators"
                  class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary"
                />
              </div>
              <div class="flex gap-2">
                <button
                  onclick={createRole}
                  disabled={!newRoleName.trim()}
                  class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium disabled:opacity-50"
                >
                  Create
                </button>
                <button
                  onclick={() => (showCreateRole = false)}
                  class="px-4 py-2 bg-surface border border-border rounded hover:bg-opacity-80 transition-colors text-sm font-medium"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        {/if}

        {#if roles.length === 0}
          <div class="border border-border rounded-lg p-8 text-center text-text-light">
            <p>No roles available</p>
          </div>
        {:else}
          <!-- Role and permission list -->
          <div class="space-y-4">
            {#each roles as role}
              {@const perms = rolePermissions[role.name] || []}
              <div class="border border-border rounded-lg overflow-hidden">
                <div class="bg-surface p-4 border-b border-border flex justify-between items-center">
                  <div>
                    <p class="font-semibold text-text">{role.name}</p>
                  </div>
                  <button
                    onclick={() => deleteRole(role.name)}
                    class="text-danger hover:text-red-700 transition-colors text-sm"
                  >
                    Delete
                  </button>
                </div>

                <div class="p-4">
                  <h4 class="text-sm font-medium text-text mb-3">Identity Permissions</h4>
                  <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2">
                    {#each availablePermissions as perm}
                      {@const hasPerm = hasPermission(role.name, perm.name)}
                      <button
                        onclick={() => togglePermission(role.name, perm.name)}
                        class="flex items-center gap-2 p-2 rounded border transition-colors text-left text-sm {hasPerm
                          ? 'bg-primary bg-opacity-10 border-primary text-primary'
                          : 'border-border hover:bg-surface'}"
                      >
                        <span class="w-4 h-4 rounded border flex items-center justify-center {hasPerm
                          ? 'bg-primary border-primary'
                          : 'border-border'}">
                          {#if hasPerm}
                            <svg class="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
                            </svg>
                          {/if}
                        </span>
                        <span class="text-xs">{perm.label}</span>
                      </button>
                    {/each}
                  </div>

                  {#if perms.length > 0}
                    <div class="mt-3 pt-3 border-t border-border">
                      <p class="text-xs text-text-light">
                        Permissions granted: <span class="font-medium">{perms.length}</span>
                      </p>
                    </div>
                  {/if}
                </div>
              </div>
            {/each}
          </div>
        {/if}
      {/if}
    </div>
  </div>
</Modal>

<style>
  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }

  :global(.animate-spin) {
    animation: spin 1s linear infinite;
  }
</style>
