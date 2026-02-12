<script lang="ts">
  import Modal from '../common/Modal.svelte';
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
  let selectedUserId = $state<string | null>(null);
  let applyToDescendants = $state(false);

  $effect(() => {
    if (isOpen && tenant && roomId && topicId) {
      loadData();
      return () => resetState();
    }
  });

  $effect(() => {
    if (roomUsers.length > 0 && !selectedUserId) {
      selectedUserId = roomUsers[0].id;
    }
  });

  async function loadData() {
    try {
      isLoading = true;
      const availablePermsData = await api.get<any>(`/${tenant}/api/permissions/available`, { cache: false });
      availablePermissions = availablePermsData.topic || [];

      const usersData = await api.get<any>(`/${tenant}/api/roomusers/room/${roomId}`, { cache: false });
      roomUsers = usersData;

      const permPromises = roomUsers.map(async (user) => {
        try {
          const perms = await api.get<any>(`/${tenant}/api/topics/${topicId}/permissions/users/${user.id}`, { cache: false });
          return { userId: user.id, permissions: perms.permissions || [] };
        } catch {
          return { userId: user.id, permissions: [] };
        }
      });
      const userPermData = await Promise.all(permPromises);
      userPermissions = {};
      userPermData.forEach((p) => { userPermissions[p.userId] = p.permissions; });

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
      const hasPerm = currentPerms.includes(permissionName);
      if (hasPerm) {
        const removeUrl =
          `/${tenant}/api/topics/${topicId}/permissions/users/${userId}/${encodeURIComponent(permissionName)}` +
          `?applyToDescendants=${applyToDescendants}`;
        await api.delete(removeUrl);
        userPermissions[userId] = currentPerms.filter((p) => p !== permissionName);
      } else {
        await api.post(`/${tenant}/api/topics/${topicId}/permissions/users`, {
          roomUserId: userId,
          permissionName,
          applyToDescendants
        });
        userPermissions[userId] = [...currentPerms, permissionName];
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to update permissions';
    }
  }

  function getDisplayName(user: { displayName?: string; userName?: string }): string {
    return user.displayName || user.userName || 'Unknown';
  }

  function hasPermission(userId: string, permissionName: string): boolean {
    return (userPermissions[userId] || []).includes(permissionName);
  }

  function formatPermissionName(name: string): string {
    return name.split('.')
      .map((part) => { if (part === 'topic') return ''; return part.charAt(0).toUpperCase() + part.slice(1); })
      .filter((p) => p !== '')
      .join(' ').trim();
  }

  let selectedUser = $derived.by(() => roomUsers.find((u) => u.id === selectedUserId) ?? null);

  function resetState() {
    roomUsers = [];
    userPermissions = {};
    availablePermissions = [];
    selectedUserId = null;
    error = null;
    isLoading = true;
  }

  function handleClose() { ui.closeModal(modalId); }
</script>

<Modal {isOpen} title="Topic User Permissions" onClose={handleClose} size="xlarge" closeButton={!isLoading}>
  <div class="tup-root">
    {#if error}
      <div class="tup-error">
        <span>{error}</span>
        <button onclick={() => (error = null)}>Dismiss</button>
      </div>
    {/if}

    <div class="tup-content">
      {#if isLoading}
        <div class="tup-loading">
          <div class="tup-spinner"></div>
          <p>Loading...</p>
        </div>
      {:else}
        <!-- Left Panel: User List -->
        <div class="tup-panel tup-panel--left">
          <div class="tup-panel-header">
            <span class="tup-panel-title">Members</span>
          </div>

          <div class="tup-list">
            {#if roomUsers.length === 0}
              <div class="tup-empty"><p>No members</p></div>
            {:else}
              {#each roomUsers as user}
                {@const perms = userPermissions[user.id] || []}
                {@const isSelected = selectedUserId === user.id}
                <button
                  onclick={() => (selectedUserId = user.id)}
                  class="tup-user-item {isSelected ? 'tup-list-item--active' : ''}"
                >
                  <div class="tup-avatar">
                    {getDisplayName(user).charAt(0).toUpperCase()}
                  </div>
                  <div class="tup-user-info">
                    <span class="tup-user-name">{getDisplayName(user)}</span>
                    {#if user.userName}
                      <span class="tup-user-sub">@{user.userName}</span>
                    {/if}
                  </div>
                  <span class="tup-badge">{perms.length}</span>
                </button>
              {/each}
            {/if}
          </div>
        </div>

        <!-- Right Panel -->
        <div class="tup-panel tup-panel--right">
          {#if selectedUser}
            {@const perms = userPermissions[selectedUser.id] || []}
            <div class="tup-panel-header">
              <div class="tup-user-header">
                <div class="tup-avatar tup-avatar--lg">
                  {getDisplayName(selectedUser).charAt(0).toUpperCase()}
                </div>
                <div>
                  <span class="tup-panel-title">{getDisplayName(selectedUser)}</span>
                  <span class="tup-panel-sub">
                    {perms.length}  individual permission(s)
                    {#if selectedUser.roomRoleName}
                      &nbsp;·&nbsp;{selectedUser.roomRoleName}
                    {/if}
                  </span>
                </div>
              </div>

              <label class="tup-toggle">
                <input type="checkbox" bind:checked={applyToDescendants} />
                <span class="tup-toggle-track"></span>
                <span class="tup-toggle-label">Apply to child topics</span>
              </label>
            </div>

            <div class="tup-perm-list">
              <p class="tup-section-label">Individual Permissions</p>
              {#if availablePermissions.length === 0}
                <div class="tup-empty"><p>No permissions available</p></div>
              {:else}
                {#each availablePermissions as perm}
                  {@const hasPerm = hasPermission(selectedUser.id, perm.name)}
                  <button
                    onclick={() => togglePermission(selectedUser.id, perm.name)}
                    class="tup-perm-item {hasPerm ? 'tup-perm-item--active' : ''}"
                  >
                    <div class="tup-checkbox {hasPerm ? 'tup-checkbox--checked' : ''}">
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
            </div>
          {:else}
            <div class="tup-empty-panel">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
              <p>Select a user to manage permissions</p>
            </div>
          {/if}
        </div>
      {/if}
    </div>
  </div>
</Modal>

<style>
  .tup-root { display: flex; flex-direction: column; height: 600px; }

  .tup-error {
    display: flex; justify-content: space-between; align-items: center;
    margin: 16px 24px 0; padding: 10px 14px;
    background: var(--color-error-light, #fef2f2);
    border: 1px solid var(--color-error, #ef4444);
    border-radius: 8px; font-size: 13px; color: var(--color-error, #ef4444);
  }
  .tup-error button { font-size: 12px; text-decoration: underline; background: none; border: none; cursor: pointer; color: inherit; margin-left: 12px; }

  .tup-content { flex: 1; display: flex; gap: 16px; padding: 16px 24px 24px; overflow: hidden; min-height: 0; }

  .tup-loading { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; color: var(--color-text-light); font-size: 13px; }
  .tup-spinner { width: 28px; height: 28px; border: 3px solid var(--color-border); border-top-color: var(--color-primary); border-radius: 50%; animation: tup-spin 0.8s linear infinite; }

  .tup-panel { display: flex; flex-direction: column; border: 1px solid var(--color-border); border-radius: 10px; overflow: hidden; background: var(--color-background); }
  .tup-panel--left { width: 240px; flex-shrink: 0; }
  .tup-panel--right { flex: 1; min-width: 0; }

  .tup-panel-header { display: flex; align-items: center; justify-content: space-between; padding: 14px 16px; border-bottom: 1px solid var(--color-border); background: var(--color-surface); flex-shrink: 0; gap: 12px; flex-wrap: wrap; }
  .tup-panel-title { font-size: 14px; font-weight: 600; color: var(--color-text); }
  .tup-panel-sub { display: block; font-size: 12px; color: var(--color-text-light); margin-top: 2px; }

  .tup-list { flex: 1; overflow-y: auto; padding: 8px; display: flex; flex-direction: column; gap: 2px; }

  .tup-user-item { display: flex; align-items: center; gap: 10px; padding: 8px 12px; border-radius: 7px; border: 1px solid transparent; background: none; cursor: pointer; text-align: left; transition: background 0.12s, border-color 0.12s; width: 100%; }
  .tup-user-item:hover { background: var(--color-surface); border-color: var(--color-border); }
  .tup-list-item--active { background: color-mix(in srgb, var(--color-primary) 10%, transparent); border-color: var(--color-primary); }
  .tup-list-item--active .tup-user-name { color: var(--color-primary); }

  .tup-user-info { display: flex; flex-direction: column; min-width: 0; flex: 1; }
  .tup-user-name { font-size: 13px; font-weight: 500; color: var(--color-text); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .tup-user-sub { font-size: 11px; color: var(--color-text-light); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

  .tup-user-header { display: flex; align-items: center; gap: 12px; min-width: 0; }

  .tup-avatar { width: 32px; height: 32px; border-radius: 50%; background: linear-gradient(135deg, var(--color-primary), color-mix(in srgb, var(--color-primary) 60%, white)); display: flex; align-items: center; justify-content: center; font-size: 13px; font-weight: 600; color: white; flex-shrink: 0; }
  .tup-avatar--lg { width: 40px; height: 40px; font-size: 16px; }

  .tup-badge { font-size: 11px; color: var(--color-text-light); background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 20px; padding: 1px 8px; flex-shrink: 0; }

  /* Toggle switch */
  .tup-toggle { display: flex; align-items: center; gap: 8px; cursor: pointer; flex-shrink: 0; }
  .tup-toggle input { display: none; }
  .tup-toggle-track {
    width: 34px; height: 20px; border-radius: 10px;
    background: var(--color-border);
    position: relative; flex-shrink: 0;
    transition: background 0.2s;
  }
  .tup-toggle-track::after {
    content: ''; position: absolute;
    top: 2px; left: 2px;
    width: 16px; height: 16px;
    border-radius: 50%; background: white;
    transition: transform 0.2s;
    box-shadow: 0 1px 3px rgba(0,0,0,0.2);
  }
  .tup-toggle input:checked ~ .tup-toggle-track { background: var(--color-primary); }
  .tup-toggle input:checked ~ .tup-toggle-track::after { transform: translateX(14px); }
  .tup-toggle-label { font-size: 12px; color: var(--color-text-light); white-space: nowrap; }

  .tup-perm-list { flex: 1; overflow-y: auto; padding: 16px 20px; display: flex; flex-direction: column; gap: 6px; }
  .tup-section-label { font-size: 11px; font-weight: 600; color: var(--color-text-light); text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 6px; }
  .tup-perm-item { display: flex; align-items: center; gap: 12px; padding: 10px 12px; border-radius: 7px; border: 1px solid var(--color-border); background: var(--color-background); cursor: pointer; text-align: left; font-size: 13px; color: var(--color-text); transition: background 0.12s, border-color 0.12s; width: 100%; }
  .tup-perm-item:hover { background: var(--color-surface); border-color: color-mix(in srgb, var(--color-primary) 40%, transparent); }
  .tup-perm-item--active { background: color-mix(in srgb, var(--color-primary) 6%, var(--color-background)); border-color: var(--color-primary); }

  .tup-checkbox { width: 18px; height: 18px; border-radius: 4px; border: 2px solid var(--color-border); display: flex; align-items: center; justify-content: center; flex-shrink: 0; transition: background 0.12s, border-color 0.12s; }
  .tup-checkbox svg { width: 11px; height: 11px; stroke: white; }
  .tup-checkbox--checked { background: var(--color-primary); border-color: var(--color-primary); }

  .tup-empty { padding: 24px 16px; text-align: center; color: var(--color-text-light); font-size: 13px; }
  .tup-empty-panel { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; color: var(--color-text-light); font-size: 13px; }
  .tup-empty-panel svg { width: 48px; height: 48px; opacity: 0.3; }

  @keyframes tup-spin { to { transform: rotate(360deg); } }
</style>
