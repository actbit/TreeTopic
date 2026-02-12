<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { api } from '$lib/api/client';
  import { ui, activeModals } from '$lib/stores/ui';
  import { page } from '$app/stores';

  interface AllowedUser {
    userId: string;
    userName?: string;
    displayName?: string;
    email?: string;
  }

  interface AllowedRole {
    roleId: string;
    roleName?: string;
  }

  interface JoinPermissionsResponse {
    joinPolicy: number;
    users: AllowedUser[];
    roles: AllowedRole[];
  }

  interface AvailableUsersResponse {
    users: AllowedUser[];
  }

  interface AvailableRolesResponse {
    roles: AllowedRole[];
  }

  const modalId = 'room-join-permission';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let tenant = $derived.by(() => (modal?.data?.tenant ?? $page.params.tenant ?? '') as string);
  let roomId = $derived.by(() => (modal?.data?.roomId ?? '') as string);

  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let joinPolicy = $state<number>(0);
  let allowedUsers = $state<AllowedUser[]>([]);
  let allowedRoles = $state<AllowedRole[]>([]);
  let availableUsers = $state<AllowedUser[]>([]);
  let availableRoles = $state<AllowedRole[]>([]);
  let selectedUserId = $state('');
  let selectedRoleId = $state('');
  let isSavingPolicy = $state(false);

  $effect(() => {
    if (isOpen && tenant && roomId) {
      void loadData();
    }
  });

  async function loadData() {
    try {
      isLoading = true;
      error = null;

      const [current, users, roles] = await Promise.all([
        api.get<JoinPermissionsResponse>(`/${tenant}/api/rooms/${roomId}/join-permissions`),
        api.get<AvailableUsersResponse>(`/${tenant}/api/rooms/${roomId}/join-permissions/available-users`),
        api.get<AvailableRolesResponse>(`/${tenant}/api/rooms/${roomId}/join-permissions/available-roles`),
      ]);

      joinPolicy = Number(current.joinPolicy ?? 0);
      allowedUsers = current.users ?? [];
      allowedRoles = current.roles ?? [];
      availableUsers = users.users ?? [];
      availableRoles = roles.roles ?? [];
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load room join permissions';
    } finally {
      isLoading = false;
    }
  }

  async function savePolicy() {
    try {
      isSavingPolicy = true;
      await api.put(`/${tenant}/api/rooms/${roomId}/join-permissions/policy`, {
        joinPolicy: Number(joinPolicy),
      });
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to update join policy';
    } finally {
      isSavingPolicy = false;
    }
  }

  async function addUser() {
    if (!selectedUserId) return;
    try {
      await api.post(`/${tenant}/api/rooms/${roomId}/join-permissions/users`, {
        userId: selectedUserId,
      });
      selectedUserId = '';
      await loadData();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to add user';
    }
  }

  async function removeUser(userId: string) {
    try {
      await api.delete(`/${tenant}/api/rooms/${roomId}/join-permissions/users/${userId}`);
      await loadData();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to remove user';
    }
  }

  async function addRole() {
    if (!selectedRoleId) return;
    try {
      await api.post(`/${tenant}/api/rooms/${roomId}/join-permissions/roles`, {
        roleId: selectedRoleId,
      });
      selectedRoleId = '';
      await loadData();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to add role';
    }
  }

  async function removeRole(roleId: string) {
    try {
      await api.delete(`/${tenant}/api/rooms/${roomId}/join-permissions/roles/${roleId}`);
      await loadData();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to remove role';
    }
  }

  function close() {
    ui.closeModal(modalId);
  }

  function displayUser(user: AllowedUser): string {
    return user.displayName || user.userName || user.email || user.userId;
  }
</script>

<Modal {isOpen} title="Room Join Permissions" onClose={close} size="large" closeButton={!isLoading}>
  <div class="flex flex-col bg-white">
    <!-- Error message -->
    {#if error}
      <div class="p-4 bg-red-50 border-b border-red-200 text-red-800 text-sm flex justify-between items-center">
        <span>{error}</span>
        <button onclick={() => (error = null)} class="underline hover:no-underline">Close</button>
      </div>
    {/if}

    <!-- Content -->
    <div class="overflow-auto p-6 space-y-6" style="max-height: calc(100vh - 250px);">
      {#if isLoading}
        <div class="text-center py-8">
          <div class="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          <p class="mt-2 text-sm text-text-light">Loading...</p>
        </div>
      {:else}
        <!-- Join Policy Section -->
        <div class="border border-border rounded-lg overflow-hidden">
          <div class="bg-surface p-4 border-b border-border">
            <h3 class="font-semibold text-text">Join Policy</h3>
          </div>
          <div class="p-4">
            <div class="flex gap-3 items-center">
              <select
                bind:value={joinPolicy}
                disabled={isSavingPolicy}
                class="flex-1 px-3 py-2 border border-border rounded focus:outline-none focus:border-primary disabled:opacity-50"
              >
                <option value={0}>Public (any authenticated user can join)</option>
                <option value={1}>Invite Only (only allowed users/roles can join)</option>
              </select>
              <button
                type="button"
                onclick={savePolicy}
                disabled={isSavingPolicy}
                class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium disabled:opacity-50"
              >
                {isSavingPolicy ? 'Saving...' : 'Save'}
              </button>
            </div>
          </div>
        </div>

        <!-- Allowed Users Section -->
        <div class="border border-border rounded-lg overflow-hidden">
          <div class="bg-surface p-4 border-b border-border">
            <h3 class="font-semibold text-text">Allowed Users</h3>
          </div>
          <div class="p-4 space-y-4">
            <div class="flex gap-2">
              <select bind:value={selectedUserId} class="flex-1 px-3 py-2 border border-border rounded focus:outline-none focus:border-primary">
                <option value="">Select user</option>
                {#each availableUsers.filter((u) => !allowedUsers.some((a) => a.userId === u.userId)) as user}
                  <option value={user.userId}>{displayUser(user)}</option>
                {/each}
              </select>
              <button
                type="button"
                onclick={addUser}
                class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
              >
                Add
              </button>
            </div>
            <div class="space-y-2">
              {#each allowedUsers as user}
                <div class="flex items-center justify-between p-3 rounded bg-surface border border-border">
                  <div class="text-sm">
                    <div class="font-medium text-text">{displayUser(user)}</div>
                    <div class="text-text-light text-xs">{user.email}</div>
                  </div>
                  <button
                    type="button"
                    onclick={() => removeUser(user.userId)}
                    class="text-sm text-danger hover:text-red-700 transition-colors"
                  >
                    Remove
                  </button>
                </div>
              {:else}
                <p class="text-sm text-text-light">No allowed users.</p>
              {/each}
            </div>
          </div>
        </div>

        <!-- Allowed Roles Section -->
        <div class="border border-border rounded-lg overflow-hidden">
          <div class="bg-surface p-4 border-b border-border">
            <h3 class="font-semibold text-text">Allowed Roles</h3>
          </div>
          <div class="p-4 space-y-4">
            <div class="flex gap-2">
              <select bind:value={selectedRoleId} class="flex-1 px-3 py-2 border border-border rounded focus:outline-none focus:border-primary">
                <option value="">Select role</option>
                {#each availableRoles.filter((r) => !allowedRoles.some((a) => a.roleId === r.roleId)) as role}
                  <option value={role.roleId}>{role.roleName}</option>
                {/each}
              </select>
              <button
                type="button"
                onclick={addRole}
                class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
              >
                Add
              </button>
            </div>
            <div class="space-y-2">
              {#each allowedRoles as role}
                <div class="flex items-center justify-between p-3 rounded bg-surface border border-border">
                  <div class="text-sm font-medium text-text">{role.roleName}</div>
                  <button
                    type="button"
                    onclick={() => removeRole(role.roleId)}
                    class="text-sm text-danger hover:text-red-700 transition-colors"
                  >
                    Remove
                  </button>
                </div>
              {:else}
                <p class="text-sm text-text-light">No allowed roles.</p>
              {/each}
            </div>
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
