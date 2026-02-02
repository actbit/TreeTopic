<script lang="ts">
  import { page } from '$app/stores';
  import { api } from '$lib/api/client';
  import { ui } from '$lib/stores/ui';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import { onMount } from 'svelte';

  let activeTab = $state('roles');
  let isLoading = $state(false);
  let error = $state<string | null>(null);

  const tenant = $page.params.tenant ?? '';
  const roomId = $page.params.roomId ?? '';

  // メンバーデータ
  let roomUsers = $state<any[]>([]);
  let roomRoles = $state<any[]>([]);

  const tabs = [
    { id: 'roles', label: 'ロール' },
    { id: 'members', label: 'メンバー' }
  ];

  onMount(async () => {
    await loadMembersData();
  });

  async function loadMembersData() {
    try {
      isLoading = true;

      // ルームユーザー一覧を取得
      const usersData = await api.get<any>(`/${tenant}/api/RoomUsers/room/${roomId}`);
      roomUsers = usersData;

      // ルームロール一覧を取得
      const rolesData = await api.get<any[]>(`/${tenant}/api/roomroles`);
      roomRoles = rolesData;

      error = null;
    } catch (err: any) {
      error = err.message || 'データの読み込みに失敗しました';
    } finally {
      isLoading = false;
    }
  }

  function openRoleModal() {
    ui.openModal({
      id: 'room-role-permission',
      title: 'ロール権限管理',
      type: 'custom',
      data: { tenant, roomId }
    });
  }

  function openUserPermissionModal() {
    ui.openModal({
      id: 'room-user-permission',
      title: 'ルームユーザー権限管理',
      type: 'custom',
      data: { tenant, roomId }
    });
  }

  async function updateUserRole(roomUserId: string, roleId: string | null) {
    try {
      isLoading = true;
      await api.put(`/${tenant}/api/RoomUsers/${roomUserId}/role`, { roomId });
      await loadMembersData();
    } catch (err: any) {
      error = err.message || 'ロールの更新に失敗しました';
    } finally {
      isLoading = false;
    }
  }

  function getDisplayName(user: any): string {
    return user.displayName || user.userName || 'Unknown';
  }

  function getRoleName(roleId: string): string {
    const role = roomRoles.find((r) => r.id === roleId);
    return role?.name || '未割り当て';
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

        {#if activeTab === 'roles'}
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

        {:else if activeTab === 'members'}
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
