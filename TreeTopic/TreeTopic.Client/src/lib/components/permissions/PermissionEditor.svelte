<script lang="ts">
  import { api } from '$lib/api/client';

  interface Props {
    resourceType: 'room' | 'topic';
    resourceId: string;
    onClose?: () => void;
  }

  let { resourceType, resourceId, onClose }: Props = $props();

  let permissions = $state<any[]>([]);
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let selectedRole = $state('user');
  let newPermissions = $state({
    canRead: false,
    canWrite: false,
    canDelete: false,
    canManagePermissions: false,
  });

  const roles = [
    { id: 'owner', name: 'Owner', permissions: ['read', 'write', 'delete', 'manage'] },
    { id: 'admin', name: 'Admin', permissions: ['read', 'write', 'delete', 'manage'] },
    { id: 'editor', name: 'Editor', permissions: ['read', 'write'] },
    { id: 'user', name: 'User', permissions: ['read'] },
    { id: 'guest', name: 'Guest', permissions: [] },
  ];

  const permissions_list = [
    { id: 'read', name: 'Read', description: 'Can view content' },
    { id: 'write', name: 'Write', description: 'Can create and edit content' },
    { id: 'delete', name: 'Delete', description: 'Can delete content' },
    { id: 'manage', name: 'Manage Permissions', description: 'Can change user permissions' },
  ];

  async function loadPermissions() {
    try {
      isLoading = true;
      const endpoint =
        resourceType === 'room'
          ? `/api/room/${resourceId}/permissions`
          : `/api/topic/${resourceId}/permissions`;

      const data = await api.get(endpoint);
      permissions = data || [];
      error = null;
    } catch (err: any) {
      error = err.message || 'Failed to load permissions';
    } finally {
      isLoading = false;
    }
  }

  async function updatePermission(userId: string, permissionId: string, enabled: boolean) {
    try {
      const endpoint =
        resourceType === 'room'
          ? `/api/room/${resourceId}/permissions/${userId}`
          : `/api/topic/${resourceId}/permissions/${userId}`;

      await api.put(endpoint, {
        [permissionId]: enabled,
      });

      // Update local state
      const userPerm = permissions.find((p) => p.userId === userId);
      if (userPerm) {
        userPerm[permissionId] = enabled;
        permissions = permissions;
      }
    } catch (err: any) {
      error = err.message || 'Failed to update permission';
    }
  }

  async function setRole(userId: string, role: string) {
    try {
      const endpoint =
        resourceType === 'room'
          ? `/api/room/${resourceId}/permissions/${userId}/role`
          : `/api/topic/${resourceId}/permissions/${userId}/role`;

      await api.put(endpoint, { role });

      // Reload permissions
      await loadPermissions();
    } catch (err: any) {
      error = err.message || 'Failed to set role';
    }
  }

  async function removeUser(userId: string) {
    if (!confirm('Remove this user?')) return;

    try {
      const endpoint =
        resourceType === 'room'
          ? `/api/room/${resourceId}/permissions/${userId}`
          : `/api/topic/${resourceId}/permissions/${userId}`;

      await api.delete(endpoint);

      permissions = permissions.filter((p) => p.userId !== userId);
    } catch (err: any) {
      error = err.message || 'Failed to remove user';
    }
  }

  // Initialize
  loadPermissions();
</script>

<div class="space-y-6">
  {#if error}
    <div class="p-4 bg-red-50 border border-red-200 rounded text-red-800 text-sm">
      {error}
    </div>
  {/if}

  {#if isLoading}
    <div class="text-center py-8">
      <p class="text-text-light">Loading permissions...</p>
    </div>
  {:else}
    <!-- Permissions Table -->
    <div class="border border-border rounded-lg overflow-hidden">
      <div class="bg-surface p-4 border-b border-border">
        <h3 class="font-semibold text-text">User Permissions</h3>
      </div>

      {#if permissions.length === 0}
        <div class="p-4 text-center text-text-light">
          <p>No users with custom permissions</p>
        </div>
      {:else}
        <table class="w-full">
          <thead class="bg-surface border-b border-border">
            <tr>
              <th class="px-4 py-2 text-left text-sm font-semibold text-text">User</th>
              <th class="px-4 py-2 text-left text-sm font-semibold text-text">Role</th>
              {#each permissions_list as perm}
                <th class="px-4 py-2 text-center text-sm font-semibold text-text">{perm.name}</th>
              {/each}
              <th class="px-4 py-2 text-center text-sm font-semibold text-text">Actions</th>
            </tr>
          </thead>
          <tbody>
            {#each permissions as userPerm (userPerm.userId)}
              <tr class="border-b border-border hover:bg-surface transition-colors">
                <td class="px-4 py-3 text-sm text-text">
                  <div>
                    <p class="font-medium">{userPerm.userName}</p>
                    <p class="text-xs text-text-light">{userPerm.email}</p>
                  </div>
                </td>
                <td class="px-4 py-3 text-sm">
                  <select
                    value={userPerm.role || 'user'}
                    on:change={(e) => setRole(userPerm.userId, e.currentTarget.value)}
                    class="px-2 py-1 border border-border rounded text-sm bg-white focus:outline-none focus:border-primary"
                  >
                    {#each roles as role}
                      <option value={role.id}>{role.name}</option>
                    {/each}
                  </select>
                </td>
                {#each permissions_list as perm}
                  <td class="px-4 py-3 text-center">
                    <input
                      type="checkbox"
                      checked={userPerm[perm.id] || false}
                      on:change={(e) =>
                        updatePermission(userPerm.userId, perm.id, e.currentTarget.checked)}
                      title={perm.description}
                      class="w-4 h-4 accent-primary cursor-pointer"
                    />
                  </td>
                {/each}
                <td class="px-4 py-3 text-center">
                  <button
                    on:click={() => removeUser(userPerm.userId)}
                    class="text-danger hover:text-red-700 transition-colors text-sm font-medium"
                  >
                    Remove
                  </button>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      {/if}
    </div>

    <!-- Quick Role Reference -->
    <div class="border border-border rounded-lg overflow-hidden">
      <div class="bg-surface p-4 border-b border-border">
        <h3 class="font-semibold text-text">Role Reference</h3>
      </div>

      <div class="divide-y divide-border">
        {#each roles as role}
          <div class="p-4">
            <p class="font-semibold text-text mb-2">{role.name}</p>
            <div class="flex flex-wrap gap-2">
              {#each permissions_list as perm}
                {#if role.permissions.includes(perm.id)}
                  <span class="px-2 py-1 bg-primary bg-opacity-10 text-primary text-xs rounded">
                    {perm.name}
                  </span>
                {/if}
              {/each}
              {#if role.permissions.length === 0}
                <span class="text-xs text-text-light">No permissions</span>
              {/if}
            </div>
          </div>
        {/each}
      </div>
    </div>
  {/if}

  {#if onClose}
    <div class="flex justify-end pt-4">
      <button
        on:click={onClose}
        class="px-4 py-2 bg-secondary text-white rounded hover:bg-opacity-90 transition-colors"
      >
        Close
      </button>
    </div>
  {/if}
</div>
