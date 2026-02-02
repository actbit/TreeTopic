<script lang="ts">
  import { page } from '$app/stores';
  import { api } from '$lib/api/client';
  import { ui } from '$lib/stores/ui';
  import { onMount } from 'svelte';

  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let activeTab = $state('roles');

  const tenant = $page.params.tenant ?? '';

  // データ
  let roles = $state<any[]>([]);
  let users = $state<any[]>([]);

  const tabs = [
    { id: 'roles', label: 'ロール' },
    { id: 'users', label: 'ユーザー' }
  ];

  onMount(async () => {
    await loadData();
  });

  async function loadData() {
    try {
      isLoading = true;

      // ロール一覧を取得
      const rolesData = await api.get<any[]>(`/${tenant}/api/roles`);
      roles = rolesData;

      // ユーザー一覧を取得
      const usersData = await api.get<any[]>(`/${tenant}/api/users`);
      users = usersData;

      error = null;
    } catch (err: any) {
      error = err.message || 'データの読み込みに失敗しました';
    } finally {
      isLoading = false;
    }
  }

  function openRoleModal() {
    ui.openModal({
      id: 'identity-role-permission',
      title: 'テナントロール権限管理',
      type: 'custom',
      data: { tenant }
    });
  }

  async function updateUserRole(userId: string, roleName: string | null) {
    try {
      isLoading = true;
      if (roleName) {
        // ロールを追加
        await api.post(`/${tenant}/api/users/${userId}/roles`, { roleName });
      }
      await loadData();
    } catch (err: any) {
      error = err.message || 'ロールの更新に失敗しました';
    } finally {
      isLoading = false;
    }
  }

  function getUserRoles(user: any): string[] {
    return user.roles || [];
  }

  function getDisplayName(user: any): string {
    return user.displayName || user.userName || 'Unknown';
  }
</script>

<svelte:head>
  <title>テナント設定 - TreeTopic</title>
</svelte:head>

<div class="min-h-screen bg-background">
  <div class="max-w-6xl mx-auto px-4 py-8">
    <!-- Header -->
    <div class="mb-8">
      <h1 class="text-3xl font-bold text-text mb-2">テナント設定</h1>
      <p class="text-text-light">テナント全体の設定を管理します。</p>
    </div>

    {#if error}
      <div class="mb-4 p-4 bg-red-50 border border-red-200 rounded text-red-800 text-sm flex justify-between items-center">
        <span>{error}</span>
        <button onclick={() => (error = null)} class="underline hover:no-underline">閉じる</button>
      </div>
    {/if}

    <!-- Tabs -->
    <div class="mb-6 border-b border-border">
      <div class="flex gap-8">
        {#each tabs as tab}
          <button
            onclick={() => (activeTab = tab.id)}
            class="pb-4 px-2 border-b-2 transition-colors {activeTab === tab.id
              ? 'border-primary text-primary font-semibold'
              : 'border-transparent text-text-light hover:text-text'}"
          >
            {tab.label}
          </button>
        {/each}
      </div>
    </div>

    <!-- Roles Tab -->
    {#if activeTab === 'roles'}
      <div class="space-y-6">
        <div class="flex justify-between items-center">
          <div>
            <h2 class="text-2xl font-semibold text-text">ロール管理</h2>
            <p class="text-sm text-text-light mt-1">テナントレベルのロールとIdentity権限を管理します。</p>
          </div>
          <button
            onclick={openRoleModal}
            class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
          >
            ロール権限を管理
          </button>
        </div>

        {#if isLoading}
          <div class="text-center py-8">
            <div class="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
            <p class="mt-2 text-sm text-text-light">読み込み中...</p>
          </div>
        {:else if roles.length === 0}
          <div class="text-center py-12 text-text-light">
            <p>ロールがありません</p>
            <p class="text-sm mt-2">「ロール権限を管理」ボタンからロールを作成してください。</p>
          </div>
        {:else}
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {#each roles as role}
              <div class="border border-border rounded-lg p-4 hover:border-primary transition-colors">
                <p class="font-medium text-text">{role.name}</p>
                <p class="text-xs text-text-light mt-1">ID: {role.id}</p>
              </div>
            {/each}
          </div>
        {/if}
      </div>
    {:else if activeTab === 'users'}
      <div class="space-y-6">
        <div>
          <h2 class="text-2xl font-semibold text-text">ユーザー管理</h2>
          <p class="text-sm text-text-light mt-1">テナントのユーザーとロール割り当てを管理します。</p>
        </div>

        {#if isLoading}
          <div class="text-center py-8">
            <div class="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
            <p class="mt-2 text-sm text-text-light">読み込み中...</p>
          </div>
        {:else if users.length === 0}
          <div class="text-center py-12 text-text-light">
            <p>ユーザーがいません</p>
          </div>
        {:else}
          <div class="border border-border rounded-lg overflow-hidden">
            <div class="bg-surface p-4 border-b border-border">
              <h3 class="font-semibold text-text">ユーザー一覧</h3>
            </div>
            <div class="divide-y divide-border">
              {#each users as user}
                <div class="p-4">
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-4">
                      {#if user.iconUrl}
                        <img
                          src={user.iconUrl}
                          alt={user.userName}
                          class="w-10 h-10 rounded-full object-cover"
                        />
                      {:else}
                        <div class="w-10 h-10 rounded-full bg-surface flex items-center justify-center">
                          <span class="text-text-light font-semibold">
                            {user.userName?.charAt(0).toUpperCase() || '?'}
                          </span>
                        </div>
                      {/if}
                      <div>
                        <p class="font-medium text-text">{getDisplayName(user)}</p>
                        <p class="text-sm text-text-light">@{user.userName}</p>
                      </div>
                    </div>
                    <div class="flex items-center gap-4">
                      <!-- 現在のロール -->
                      <div class="flex flex-wrap gap-1">
                        {#each getUserRoles(user) as roleName}
                          <span class="px-2 py-1 bg-surface border border-border rounded text-xs text-text-light">
                            {roleName}
                          </span>
                        {:else}
                          <span class="text-xs text-text-light">ロールなし</span>
                        {/each}
                      </div>
                      <!-- ロール追加ドロップダウン -->
                      <select
                        value=""
                        onchange={(e) => updateUserRole(user.id, e.currentTarget.value || null)}
                        disabled={isLoading}
                        class="px-3 py-1.5 border border-border rounded text-sm focus:outline-none focus:border-primary disabled:opacity-50"
                      >
                        <option value="">+ ロール追加</option>
                        {#each roles as role}
                          <option value={role.name}>{role.name}</option>
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

    <!-- Identity権限説明 -->
    <div class="mt-8 bg-white rounded-lg shadow-sm border border-border overflow-hidden">
      <div class="p-6 border-b border-border">
        <h2 class="text-xl font-semibold text-text">Identity権限一覧</h2>
        <p class="text-sm text-text-light mt-1">テナントレベルで利用可能な権限です。</p>
      </div>

      <div class="p-6">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <!-- ユーザー権限 -->
          <div>
            <h3 class="text-sm font-semibold text-text mb-3 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-blue-500"></span>
              ユーザー管理
            </h3>
            <div class="space-y-2">
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">identity.users.read</p>
                  <p class="text-xs text-text-light">ユーザー情報を閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">identity.users.manage</p>
                  <p class="text-xs text-text-light">ユーザー情報を変更できます</p>
                </div>
              </div>
            </div>
          </div>

          <!-- ロール権限 -->
          <div>
            <h3 class="text-sm font-semibold text-text mb-3 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-green-500"></span>
              ロール管理
            </h3>
            <div class="space-y-2">
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">identity.roles.read</p>
                  <p class="text-xs text-text-light">ロール情報を閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">identity.roles.manage</p>
                  <p class="text-xs text-text-light">ロールを管理できます</p>
                </div>
              </div>
            </div>
          </div>

          <!-- 権限管理 -->
          <div>
            <h3 class="text-sm font-semibold text-text mb-3 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-yellow-500"></span>
              権限管理
            </h3>
            <div class="space-y-2">
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">identity.permissions.read</p>
                  <p class="text-xs text-text-light">権限設定を閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">identity.permissions.manage</p>
                  <p class="text-xs text-text-light">権限を管理できます</p>
                </div>
              </div>
            </div>
          </div>

          <!-- テナント管理 -->
          <div>
            <h3 class="text-sm font-semibold text-text mb-3 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-purple-500"></span>
              テナント管理
            </h3>
            <div class="space-y-2">
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">identity.tenants.read</p>
                  <p class="text-xs text-text-light">テナント情報を閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">identity.tenants.manage</p>
                  <p class="text-xs text-text-light">テナントを管理できます</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</div>

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
