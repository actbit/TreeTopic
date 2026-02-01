<script lang="ts">
  import { api } from '$lib/api/client';
  import { onMount } from 'svelte';

  interface Props {
    resourceType: 'room' | 'topic';
    resourceId: string;
    tenant: string;
    onClose?: () => void;
  }

  let { resourceType, resourceId, tenant, onClose }: Props = $props();

  let permissions = $state<any[]>([]);
  let roles = $state<any[]>([]);
  let availablePermissions = $state<string[]>([]);
  let isLoading = $state(true);
  let error = $state<string | null>(null);

  onMount(async () => {
    await loadRoles();
    await loadPermissions();
  });

  // 権限名を表示用に変換
  function formatPermissionName(perm: string): string {
    return perm
      .split('.')
      .map(part => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ');
  }

  async function loadRoles() {
    try {
      isLoading = true;
      const data = await api.get<any[]>(`/${tenant}/api/roomroles`);
      roles = data.map((r: any) => ({
        id: r.id,
        name: r.name,
        description: r.description,
        permissions: r.permissions || []
      }));
      error = null;
    } catch (err: any) {
      error = err.message || 'Failed to load roles';
    } finally {
      isLoading = false;
    }
  }

  async function loadPermissions() {
    try {
      const endpoint =
        resourceType === 'room'
          ? `/api/room/${resourceId}/permissions`
          : `/api/topic/${resourceId}/permissions`;

      const data = (await api.get<any[]>(endpoint)) ?? [];
      permissions = data;

      // 全てのユニークな権限名を収集
      const allPermissions = new Set<string>();
      roles.forEach(r => {
        (r.permissions || []).forEach((p: string) => {
          // "topic.read" -> "topic.read" (そのまま)
          // "read" -> "read" (そのまま)
          allPermissions.add(p);
        });
      });
      permissions.forEach((p: any) => {
        Object.keys(p).forEach(key => {
          if (typeof p[key] === 'boolean' && key.startsWith('can')) {
            // "canRead" -> "read"
            // "canReadTopic" -> "readTopic"
            const permName = key.replace('can', (match: string) => match.toLowerCase());
            allPermissions.add(permName);
          }
        });
      });
      availablePermissions = Array.from(allPermissions).sort();

      error = null;
    } catch (err: any) {
      error = err.message || 'Failed to load permissions';
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

      await loadPermissions();
    } catch (err: any) {
      error = err.message || 'Failed to update permission';
    }
  }

  async function setRole(userId: string, roleId: string | null) {
    try {
      const endpoint =
        resourceType === 'room'
          ? `/api/room/${resourceId}/permissions/${userId}/role`
          : `/api/topic/${resourceId}/permissions/${userId}/role`;

      await api.put(endpoint, { roleId });

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

      await loadPermissions();
    } catch (err: any) {
      error = err.message || 'Failed to remove user';
    }
  }
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
              {#each availablePermissions as perm}
                <th class="px-4 py-2 text-center text-sm font-semibold text-text">{formatPermissionName(perm)}</th>
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
                    value={userPerm.roomRoleId || ''}
                    onchange={(e) => setRole(userPerm.userId, e.currentTarget.value || null)}
                    class="px-2 py-1 border border-border rounded text-sm bg-white focus:outline-none focus:border-primary"
                  >
                    <option value="">No role</option>
                    {#each roles as role}
                      <option value={role.id}>{role.name}</option>
                    {/each}
                  </select>
                  {#if userPerm.roomRoleName}
                    <span class="text-xs text-text-light ml-2">{userPerm.roomRoleName}</span>
                  {/if}
                </td>
                {#each availablePermissions as perm}
                  {@const permKey = `can${perm.charAt(0).toUpperCase() + perm.slice(1)}`}
                  <td class="px-4 py-3 text-center">
                    <input
                      type="checkbox"
                      checked={userPerm[permKey] || false}
                      onchange={(e) =>
                        updatePermission(userPerm.userId, permKey, e.currentTarget.checked)}
                      class="w-4 h-4 accent-primary cursor-pointer"
                    />
                  </td>
                {/each}
                <td class="px-4 py-3 text-center">
                  <button
                    onclick={() => removeUser(userPerm.userId)}
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
            {#if role.description}
              <p class="text-xs text-text-light mb-2">{role.description}</p>
            {/if}
            <div class="flex flex-wrap gap-2">
              {#each role.permissions as perm}
                <span class="px-2 py-1 bg-primary bg-opacity-10 text-primary text-xs rounded">
                  {perm}
                </span>
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
        onclick={onClose}
        class="px-4 py-2 bg-secondary text-white rounded hover:bg-opacity-90 transition-colors"
      >
        Close
      </button>
    </div>
  {/if}
</div>
