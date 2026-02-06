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
  <div class="space-y-6">
    {#if error}
      <div class="p-3 bg-red-50 border border-red-200 rounded text-sm text-red-800">{error}</div>
    {/if}

    {#if isLoading}
      <div class="text-sm text-text-light">Loading...</div>
    {:else}
      <section class="space-y-2">
        <h3 class="text-sm font-semibold text-text">Join Policy</h3>
        <div class="flex gap-2 items-center">
          <select bind:value={joinPolicy} class="flex-1">
            <option value={0}>Public</option>
            <option value={1}>Invite Only</option>
          </select>
          <button
            type="button"
            onclick={savePolicy}
            disabled={isSavingPolicy}
            class="px-3 py-2 rounded bg-primary text-white text-sm disabled:opacity-50"
          >
            {isSavingPolicy ? 'Saving...' : 'Save'}
          </button>
        </div>
      </section>

      <section class="space-y-2">
        <h3 class="text-sm font-semibold text-text">Allowed Users</h3>
        <div class="flex gap-2">
          <select bind:value={selectedUserId} class="flex-1">
            <option value="">Select user</option>
            {#each availableUsers.filter((u) => !allowedUsers.some((a) => a.userId === u.userId)) as user}
              <option value={user.userId}>{displayUser(user)}</option>
            {/each}
          </select>
          <button type="button" onclick={addUser} class="px-3 py-2 rounded border border-border text-sm">Add</button>
        </div>
        <div class="space-y-2">
          {#each allowedUsers as user}
            <div class="flex items-center justify-between p-2 rounded bg-surface border border-border">
              <div class="text-sm">
                <div class="font-medium">{displayUser(user)}</div>
                <div class="text-text-light text-xs">{user.email}</div>
              </div>
              <button type="button" onclick={() => removeUser(user.userId)} class="text-sm text-danger">Remove</button>
            </div>
          {:else}
            <p class="text-sm text-text-light">No allowed users.</p>
          {/each}
        </div>
      </section>

      <section class="space-y-2">
        <h3 class="text-sm font-semibold text-text">Allowed Roles</h3>
        <div class="flex gap-2">
          <select bind:value={selectedRoleId} class="flex-1">
            <option value="">Select role</option>
            {#each availableRoles.filter((r) => !allowedRoles.some((a) => a.roleId === r.roleId)) as role}
              <option value={role.roleId}>{role.roleName}</option>
            {/each}
          </select>
          <button type="button" onclick={addRole} class="px-3 py-2 rounded border border-border text-sm">Add</button>
        </div>
        <div class="space-y-2">
          {#each allowedRoles as role}
            <div class="flex items-center justify-between p-2 rounded bg-surface border border-border">
              <div class="text-sm font-medium">{role.roleName}</div>
              <button type="button" onclick={() => removeRole(role.roleId)} class="text-sm text-danger">Remove</button>
            </div>
          {:else}
            <p class="text-sm text-text-light">No allowed roles.</p>
          {/each}
        </div>
      </section>
    {/if}
  </div>
</Modal>
