<script lang="ts">
  import { page } from '$app/stores';
  import { api } from '$lib/api/client';
  import { ui } from '$lib/stores/ui';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import { onMount } from 'svelte';

  let activeTab = $state('roles');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let canManageRoles = $state(false);
  let canManageUsers = $state(false);

  const tenant = $page.params.tenant ?? '';
  const roomId = $page.params.roomId ?? '';

  // メンバーデータ
  let roomUsers = $state<Array<{ id: string; displayName?: string; userName?: string; roomRoleId?: string | null }>>([]);
  let roomRoles = $state<Array<{ id: string; name: string; description?: string | null }>>([]);

  let tabs = $derived.by(() => {
    const t: Array<{ id: string; label: string }> = [];
    if (canManageRoles) t.push({ id: 'roles', label: 'ロール' });
    if (canManageUsers) t.push({ id: 'members', label: 'メンバー' });
    return t;
  });

  onMount(async () => {
    await loadCapabilities();
    if (!canManageRoles && !canManageUsers) {
      error = 'この画面にアクセスする権限がありません';
      return;
    }

    if (canManageRoles && !canManageUsers) {
      activeTab = 'roles';
    } else if (canManageUsers && !canManageRoles) {
      activeTab = 'members';
    }
    await loadMembersData();
  });

  async function loadCapabilities() {
    try {
      const [roomPermRes, tenantPermRes] = await Promise.all([
        api.get<{ permissions?: string[] }>(`/${tenant}/api/room/${roomId}/my/permissions`),
        api.get<{ permissions?: string[] }>(`/${tenant}/auth/me/permissions`)
      ]);
      const roomPerms = new Set(roomPermRes?.permissions ?? []);
      const tenantPerms = new Set(tenantPermRes?.permissions ?? []);
      const isTenantRoomManage = tenantPerms.has('tenant.room.manage');

      canManageRoles = isTenantRoomManage || roomPerms.has('room.manageRoles');
      canManageUsers = isTenantRoomManage || roomPerms.has('room.manageUsers');
    } catch {
      canManageRoles = false;
      canManageUsers = false;
    }
  }

  async function loadMembersData() {
    try {
      isLoading = true;

      if (canManageUsers) {
        const usersData = await api.get<Array<{ id: string; displayName?: string; userName?: string; roomRoleId?: string | null }>>(`/${tenant}/api/roomusers/room/${roomId}`);
        roomUsers = usersData;
      }

      if (canManageRoles || canManageUsers) {
        const rolesData = await api.get<Array<{ id: string; name: string; description?: string | null }>>(`/${tenant}/api/roomroles`);
        roomRoles = rolesData;
      }

      error = null;
    } catch (err) {
      error = err instanceof Error ? err.message : 'データの読み込みに失敗しました';
    } finally {
      isLoading = false;
    }
  }

  function openRoleModal() {
    if (!canManageRoles) return;
    ui.openModal({
      id: 'room-role-permission',
      title: 'ロール権限管理',
      type: 'custom',
      data: { tenant, roomId }
    });
  }

  function openUserPermissionModal() {
    if (!canManageUsers) return;
    ui.openModal({
      id: 'room-user-permission',
      title: 'ルームユーザー権限管理',
      type: 'custom',
      data: { tenant, roomId }
    });
  }

  async function updateUserRole(roomUserId: string, roleId: string | null) {
    if (!canManageUsers) return;
    try {
      isLoading = true;
      await api.put(`/${tenant}/api/roomusers/${roomUserId}/role`, { roleId });
      await loadMembersData();
    } catch (err) {
      error = err instanceof Error ? err.message : 'ロールの更新に失敗しました';
    } finally {
      isLoading = false;
    }
  }

  function getDisplayName(user: { displayName?: string; userName?: string }): string {
    return user.displayName || user.userName || 'Unknown';
  }

</script>

<svelte:head>
  <title>ルーム設定 - TreeTopic</title>
</svelte:head>

<AppLayout>
  {#snippet headerContent()}
    <div class="flex items-center gap-4">
      <h1 class="text-xl font-bold text-text">ルーム設定</h1>
    </div>
  {/snippet}

  {#snippet sidebarContent()}
    <div class="space-y-2 p-5">
      {#each tabs as tab}
        <button
          onclick={() => (activeTab = tab.id)}
          class="w-full flex items-center gap-3 px-5 py-3 rounded-lg transition-colors {activeTab === tab.id
            ? 'bg-primary text-white'
            : 'text-text hover:bg-surface'}"
        >
          <span class="font-semibold">{tab.label}</span>
        </button>
      {/each}
    </div>
  {/snippet}

  {#snippet mainContent()}
    <div class="flex-1 overflow-y-auto p-8 bg-white">
      <div class="max-w-4xl">
        {#if error}
          <div class="mb-4 p-4 bg-red-50 border border-red-200 rounded text-red-800 text-sm flex justify-between items-center">
            <span>{error}</span>
            <button onclick={() => (error = null)} class="underline hover:no-underline">閉じる</button>
          </div>
        {/if}

        {#if !canManageRoles && !canManageUsers}
          <div class="border border-border rounded-lg p-8 text-center text-text-light">
            <p>この画面にアクセスする権限がありません。</p>
          </div>
        {:else if activeTab === 'roles' && canManageRoles}
          <div class="space-y-6">
            <div>
              <h2 class="text-2xl font-bold text-text mb-2">ロール管理</h2>
              <p class="text-text-light mb-4">ルーム内のロールとその権限を管理します。</p>
              <button
                onclick={openRoleModal}
                class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
              >
                ロール権限を管理
              </button>
            </div>

            <!-- ロール一覧（簡易表示） -->
            <div class="border border-border rounded-lg overflow-hidden">
              <div class="bg-surface p-4 border-b border-border">
                <h3 class="font-semibold text-text">ロール一覧</h3>
              </div>
              <div class="divide-y divide-border">
                {#each roomRoles as role}
                  <div class="p-4">
                    <p class="font-medium text-text">{role.name}</p>
                    {#if role.description}
                      <p class="text-sm text-text-light">{role.description}</p>
                    {/if}
                  </div>
                {:else}
                  <div class="p-8 text-center text-text-light">
                    <p>ロールがありません</p>
                  </div>
                {/each}
              </div>
            </div>
          </div>

        {:else if activeTab === 'members' && canManageUsers}
          <div class="space-y-6">
            <div class="flex justify-between items-center">
              <div>
                <h2 class="text-2xl font-bold text-text mb-2">メンバー管理</h2>
                <p class="text-text-light">ルームメンバーとロール割り当てを管理します。</p>
              </div>
              <button
                onclick={openUserPermissionModal}
                class="px-4 py-2 bg-secondary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
              >
                個別権限を管理
              </button>
            </div>

            {#if isLoading}
              <div class="text-center py-8">
                <div class="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
                <p class="mt-2 text-sm text-text-light">読み込み中...</p>
              </div>
            {:else if roomUsers.length === 0}
              <div class="border border-border rounded-lg p-8 text-center text-text-light">
                <p>メンバーがいません</p>
              </div>
            {:else}
              <!-- メンバー一覧 -->
              <div class="border border-border rounded-lg overflow-hidden">
                <div class="bg-surface p-4 border-b border-border">
                  <h3 class="font-semibold text-text">メンバー一覧</h3>
                </div>
                <div class="divide-y divide-border">
                  {#each roomUsers as user}
                    <div class="p-4">
                      <div class="flex items-center justify-between">
                        <div>
                          <p class="font-medium text-text">{getDisplayName(user)}</p>
                          <p class="text-sm text-text-light">@{user.userName}</p>
                        </div>
                        <div class="flex items-center gap-2">
                          <select
                            value={user.roomRoleId || ''}
                            onchange={(e) => updateUserRole(user.id, e.currentTarget.value || null)}
                            disabled={isLoading}
                            class="px-3 py-1.5 border border-border rounded text-sm focus:outline-none focus:border-primary disabled:opacity-50"
                          >
                            <option value="">未割り当て</option>
                            {#each roomRoles as role}
                              <option value={role.id}>{role.name}</option>
                            {/each}
                          </select>
                        </div>
                      </div>
                    </div>
                  {/each}
                </div>
              </div>
            {/if}
          </div>
        {/if}
      </div>
    </div>
  {/snippet}
</AppLayout>

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
