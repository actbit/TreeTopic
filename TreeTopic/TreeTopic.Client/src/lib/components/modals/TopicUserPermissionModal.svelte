<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { onMount } from 'svelte';
  import { api } from '$lib/api/client';
  import { ui, activeModals } from '$lib/stores/ui';
  import { page } from '$app/stores';
  import type { PermissionDefinition } from '$lib/types/permissions';

  const modalId = 'topic-user-permission';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let tenant = $derived.by(() => (modal?.data?.tenant ?? $page.params.tenant ?? '') as string);
  let roomId = $derived.by(() => (modal?.data?.roomId ?? '') as string);
  let topicId = $derived.by(() => (modal?.data?.topicId ?? '') as string);

  let availablePermissions = $state<PermissionDefinition[]>([]);
  let roomUsers = $state<{ id: string; displayName: string; userName?: string; roomRoleName?: string }[]>([]);
  let userPermissions = $state<Record<string, string[]>>({});
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let showAddUser = $state(false);
  let selectedUserId = $state('');
  let selectedPermission = $state('');

  $effect(() => {
    if (isOpen && tenant && roomId && topicId) {
      loadData();
    }
  });

  async function loadData() {
    try {
      isLoading = true;

      // Fetch available topic permissions
      const availablePermsData = await api.get<any>(`/${tenant}/api/permissions/available`);
      availablePermissions = availablePermsData.topic || [];

      // Fetch room users
      const usersData = await api.get<any>(`/${tenant}/api/roomusers/room/${roomId}`);
      roomUsers = usersData;

      // Fetch permissions for each user
      const permPromises = roomUsers.map(async (user) => {
        try {
          const perms = await api.get<any>(`/${tenant}/api/topics/${topicId}/permissions/users/${user.id}`);
          return { userId: user.id, permissions: perms.permissions || [] };
        } catch {
          return { userId: user.id, permissions: [] };
        }
      });

      const userPermData = await Promise.all(permPromises);
      userPermissions = {};
      userPermData.forEach((p) => {
        userPermissions[p.userId] = p.permissions;
      });

      error = null;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load data';
    } finally {
      isLoading = false;
    }
  }

  async function togglePermission(userId: string, permissionName: string) {
    try {
      const currentPerms = userPermissions[userId] || [];
      const hasPermission = currentPerms.includes(permissionName);

      if (hasPermission) {
        // Remove permission
        await api.delete(`/${tenant}/api/topics/${topicId}/permissions/users/${userId}/${encodeURIComponent(permissionName)}`);
        userPermissions[userId] = currentPerms.filter((p) => p !== permissionName);
      } else {
        // Add permission
        await api.post(`/${tenant}/api/topics/${topicId}/permissions/users`, {
          roomUserId: userId,
          permissionName
        });
        userPermissions[userId] = [...currentPerms, permissionName];
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to update permissions';
    }
  }

  async function addPermissionToUser() {
    if (!selectedUserId || !selectedPermission) {
      error = 'Please select a user and permission';
      return;
    }

    try {
      await api.post(`/${tenant}/api/topics/${topicId}/permissions/users`, {
        roomUserId: selectedUserId,
        permissionName: selectedPermission
      });

      const currentPerms = userPermissions[selectedUserId] || [];
      userPermissions[selectedUserId] = [...currentPerms, selectedPermission];

      showAddUser = false;
      selectedUserId = '';
      selectedPermission = '';
      error = null;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to add permission';
    }
  }

  async function removePermission(userId: string, permissionName: string) {
    try {
      await api.delete(`/${tenant}/api/topics/${topicId}/permissions/users/${userId}/${encodeURIComponent(permissionName)}`);
      userPermissions[userId] = (userPermissions[userId] || []).filter((p) => p !== permissionName);
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to remove permission';
    }
  }

  function getDisplayName(user: { displayName?: string; userName?: string }): string {
    return user.displayName || user.userName || 'Unknown';
  }

  function hasPermission(userId: string, permissionName: string): boolean {
    return (userPermissions[userId] || []).includes(permissionName);
  }

  function formatPermissionName(name: string): string {
    return name
      .split('.')
      .map((part, i) => {
        if (part === 'topic') return '';
        return part.charAt(0).toUpperCase() + part.slice(1);
      })
      .join(' ')
      .trim();
  }

  function handleClose() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Topic User Permission Management" onClose={handleClose} size="large" closeButton={!isLoading}>
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
      {:else if roomUsers.length === 0}
        <div class="border border-border rounded-lg p-8 text-center text-text-light">
          <p>No members in this room</p>
        </div>
      {:else}
        <!-- Add permission button -->
        <div class="flex justify-between items-center">
          <h3 class="text-lg font-semibold text-text">User Permissions</h3>
          <button
            onclick={() => (showAddUser = true)}
            disabled={showAddUser}
            class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium disabled:opacity-50"
          >
            + Add Permission
          </button>
        </div>

        {#if showAddUser}
          <div class="border border-border rounded-lg p-4 bg-surface">
            <h4 class="font-medium text-text mb-3">Add Permission to User</h4>
            <div class="space-y-3">
              <div>
                <label for="user-select" class="block text-sm font-medium text-text mb-1">User</label>
                <select
                  id="user-select"
                  bind:value={selectedUserId}
                  class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary"
                >
                  <option value="">Select a user</option>
                  {#each roomUsers as user}
                    <option value={user.id}>{getDisplayName(user)}</option>
                  {/each}
                </select>
              </div>
              <div>
                <label for="permission-select" class="block text-sm font-medium text-text mb-1">Permission</label>
                <select
                  id="permission-select"
                  bind:value={selectedPermission}
                  class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary"
                >
                  <option value="">Select a permission</option>
                  {#each availablePermissions as perm}
                    <option value={perm.name}>{formatPermissionName(perm.name)}</option>
                  {/each}
                </select>
              </div>
              <div class="flex gap-2">
                <button
                  onclick={addPermissionToUser}
                  disabled={!selectedUserId || !selectedPermission}
                  class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium disabled:opacity-50"
                >
                  Add
                </button>
                <button
                  onclick={() => (showAddUser = false)}
                  class="px-4 py-2 bg-surface border border-border rounded hover:bg-opacity-80 transition-colors text-sm font-medium"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        {/if}

        <!-- User permission list -->
        <div class="border border-border rounded-lg overflow-hidden">
          <div class="bg-surface p-4 border-b border-border">
            <h3 class="font-semibold text-text">Member List</h3>
          </div>

          <div class="divide-y divide-border">
            {#each roomUsers as user}
              {@const perms = userPermissions[user.id] || []}
              <div class="p-4">
                <div class="flex items-center justify-between mb-3">
                  <div>
                    <p class="font-medium text-text">{getDisplayName(user)}</p>
                    <p class="text-sm text-text-light">@{user.userName}</p>
                  </div>
                  {#if user.roomRoleName}
                    <span class="px-2 py-1 bg-secondary bg-opacity-20 text-secondary text-xs rounded">
                      {user.roomRoleName}
                    </span>
                  {/if}
                </div>

                <div class="space-y-2">
                  <div>
                    <p class="text-xs text-text-light mb-2">Individual Permissions</p>
                    <div class="flex flex-wrap gap-2">
                      {#each availablePermissions as perm}
                        {@const hasPerm = hasPermission(user.id, perm.name)}
                        <button
                          onclick={() => togglePermission(user.id, perm.name)}
                          class="px-3 py-1.5 rounded-full border transition-colors text-xs font-medium {hasPerm
                            ? 'bg-primary bg-opacity-10 border-primary text-primary'
                            : 'border-border hover:bg-surface'}"
                        >
                          {formatPermissionName(perm.name)}
                        </button>
                      {/each}
                    </div>
                  </div>

                  {#if perms.length > 0}
                    <div class="pt-2 border-t border-border">
                      <p class="text-xs text-text-light">
                        Permissions granted: {perms.map((p) => formatPermissionName(p)).join(', ')}
                      </p>
                    </div>
                  {/if}
                </div>
              </div>
            {/each}
          </div>
        </div>

        <!-- Permission reference list -->
        <div class="border border-border rounded-lg overflow-hidden">
          <div class="bg-surface p-4 border-b border-border">
            <h3 class="font-semibold text-text">Topic Permission List</h3>
          </div>

          <div class="p-4 space-y-2">
            {#each availablePermissions as perm}
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div class="flex-1">
                  <p class="font-medium text-text text-sm">{formatPermissionName(perm.name)}</p>
                  <p class="text-xs text-text-light">{perm.name}</p>
                </div>
              </div>
            {/each}
          </div>
        </div>
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
