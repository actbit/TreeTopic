<script lang="ts">
  import Button from '../common/Button.svelte';
  import { api } from '$lib/api/client';

  interface Props {
    resourceType: 'room' | 'topic';
    resourceId: string;
    users: any[];
    onPermissionChanged?: () => void;
  }

  let { resourceType, resourceId, users, onPermissionChanged }: Props = $props();

  let isLoading = $state(false);
  let error = $state<string | null>(null);

  const permissionLabels: Record<string, { label: string; short: string }> = {
    canRead: { label: 'Read', short: 'R' },
    canWrite: { label: 'Write', short: 'W' },
    canDelete: { label: 'Delete', short: 'D' },
    canManagePermissions: { label: 'Manage', short: 'M' },
  };

  async function togglePermission(
    userId: string,
    permission: string,
    currentValue: boolean
  ) {
    try {
      isLoading = true;
      error = null;

      const endpoint =
        resourceType === 'room'
          ? `/api/room/${resourceId}/permissions/${userId}`
          : `/api/topic/${resourceId}/permissions/${userId}`;

      await api.put(endpoint, {
        [permission]: !currentValue,
      });

      const user = users.find((u) => u.id === userId);
      if (user) {
        user[permission] = !currentValue;
        users = users;
      }

      onPermissionChanged?.();
    } catch (err: any) {
      error = err.message || 'Failed to update permission';
    } finally {
      isLoading = false;
    }
  }

  async function removeUser(userId: string) {
    if (!confirm('Remove this user from permissions?')) return;

    try {
      isLoading = true;
      error = null;

      const endpoint =
        resourceType === 'room'
          ? `/api/room/${resourceId}/permissions/${userId}`
          : `/api/topic/${resourceId}/permissions/${userId}`;

      await api.delete(endpoint);

      users = users.filter((u) => u.id !== userId);
      onPermissionChanged?.();
    } catch (err: any) {
      error = err.message || 'Failed to remove user';
    } finally {
      isLoading = false;
    }
  }
</script>

<div class="space-y-3">
  {#if error}
    <div class="p-3 bg-red-50 border border-red-200 rounded text-red-800 text-sm">
      {error}
    </div>
  {/if}

  {#if users.length === 0}
    <div class="text-center py-4 text-text-light">
      <p class="text-sm">No users</p>
    </div>
  {:else}
    <div class="space-y-2">
      {#each users as user (user.id)}
        <div class="flex items-center justify-between p-3 bg-surface rounded border border-border hover:border-primary transition-colors group">
          <div class="flex-1 min-w-0">
            <p class="text-sm font-medium text-text">{user.displayName}</p>
            <p class="text-xs text-text-light">{user.email}</p>
          </div>

          <div class="flex items-center gap-2 ml-4">
            {#each Object.entries(permissionLabels) as [permission, { label, short }]}
              <button
                type="button"
                on:click={() => togglePermission(user.id, permission, user[permission])}
                disabled={isLoading}
                class="w-8 h-8 flex items-center justify-center rounded text-xs font-bold transition-all {user[permission]
                  ? 'bg-primary text-white'
                  : 'bg-border text-text-light hover:bg-primary hover:text-white'} disabled:opacity-50"
                title={label}
              >
                {short}
              </button>
            {/each}

            <button
              type="button"
              on:click={() => removeUser(user.id)}
              disabled={isLoading}
              class="px-2 py-1 text-xs text-danger hover:text-red-700 opacity-0 group-hover:opacity-100 transition-all disabled:opacity-50"
            >
              Remove
            </button>
          </div>
        </div>
      {/each}
    </div>
  {/if}
</div>
