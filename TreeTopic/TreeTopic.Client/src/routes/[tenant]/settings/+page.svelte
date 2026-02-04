<script lang="ts">
  import { page } from '$app/stores';
  import { api, getTenantDetail, createUser, banUser, unbanUser, assignUserRole, removeUserRole } from '$lib/api/client';
  import { onMount } from 'svelte';

  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let activeTab = $state('users');

  const tenant = $page.params.tenant ?? '';

  // データ
  let roles = $state<any[]>([]);
  let users = $state<any[]>([]);
  let tenantDetail = $state<any>(null);
  let canManageUsers = $state(false);

  // Create user modal
  let showCreateUserModal = $state(false);
  let newEmail = $state('');

  // Ban modal
  let showBanModal = $state(false);
  let banTargetUser = $state<any>(null);
  let banReason = $state('');

  const tabs = [
    { id: 'users', label: 'ユーザー' },
    { id: 'roles', label: 'ロール' }
  ];

  onMount(async () => {
    await loadData();
    await loadTenantDetail();
  });

  async function loadData() {
    try {
      isLoading = true;
      error = null;

      // ロール一覧を取得
      const rolesData = await api.get<any[]>(`/${tenant}/api/roles`);
      roles = rolesData;

      // ユーザー一覧を取得
      const usersData = await api.get<any[]>(`/${tenant}/api/users`);
      users = usersData;
    } catch (err: any) {
      error = err.message || 'データの読み込みに失敗しました';
    } finally {
      isLoading = false;
    }
  }

  async function loadTenantDetail() {
    try {
      const detail = await getTenantDetail(tenant);
      tenantDetail = detail;
      // バックエンドで判定された結果を使用
      canManageUsers = tenantDetail.canCreateUsers;
    } catch (err: any) {
      console.error('Failed to load tenant detail:', err);
    }
  }

  async function handleCreateUser() {
    if (!newEmail.trim()) {
      error = 'メールアドレスを入力してください';
      return;
    }

    try {
      isLoading = true;
      error = null;

      await createUser(tenant, newEmail.trim());
      showCreateUserModal = false;
      newEmail = '';

      await loadData();
    } catch (err: any) {
      error = err.message || 'ユーザーの作成に失敗しました';
    } finally {
      isLoading = false;
    }
  }

  function openBanModal(user: any) {
    banTargetUser = user;
    banReason = '';
    showBanModal = true;
  }

  async function handleBan() {
    if (!banTargetUser || !banReason.trim()) {
      error = 'Ban理由を入力してください';
      return;
    }

    try {
      isLoading = true;
      error = null;

      await banUser(tenant, banTargetUser.id, banReason.trim());
      showBanModal = false;
      banTargetUser = null;
      banReason = '';

      await loadData();
    } catch (err: any) {
      error = err.message || 'Banに失敗しました';
    } finally {
      isLoading = false;
    }
  }

  async function handleUnban(user: any) {
    if (!confirm(`${user.displayName || user.userName}のBanを解除しますか？`)) {
      return;
    }

    try {
      isLoading = true;
      error = null;

      await unbanUser(tenant, user.id);
      await loadData();
    } catch (err: any) {
      error = err.message || 'Ban解除に失敗しました';
    } finally {
      isLoading = false;
    }
  }

  async function handleAddRole(user: any, roleName: string) {
    if (!roleName) return;

    try {
      isLoading = true;
      error = null;

      await assignUserRole(tenant, user.id, roleName);
      await loadData();
    } catch (err: any) {
      error = err.message || 'ロールの追加に失敗しました';
    } finally {
      isLoading = false;
    }
  }

  async function handleRemoveRole(user: any, roleName: string) {
    try {
      isLoading = true;
      error = null;

      await removeUserRole(tenant, user.id, roleName);
      await loadData();
    } catch (err: any) {
      error = err.message || 'ロールの削除に失敗しました';
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

  function formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString('ja-JP');
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

    <!-- Users Tab -->
    {#if activeTab === 'users'}
      <div class="space-y-6">
        <div class="flex justify-between items-center">
          <div>
            <h2 class="text-2xl font-semibold text-text">ユーザー管理</h2>
            <p class="text-sm text-text-light mt-1">テナントのユーザーとロール割り当てを管理します。</p>
          </div>
          {#if canManageUsers}
            <button
              onclick={() => (showCreateUserModal = true)}
              disabled={isLoading}
              class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium disabled:opacity-50"
            >
              ユーザーを作成
            </button>
          {/if}
        </div>

        {#if !canManageUsers}
          <div class="p-4 bg-blue-50 border border-blue-200 rounded text-blue-800 text-sm">
            <p>
              <strong>OIDCロール連携が有効です</strong>
            </p>
            <p class="mt-1">ユーザーはOIDCプロバイダーから自動的に作成・管理されます。</p>
          </div>
        {/if}

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
                <div class="p-4 {user.isBanned ? 'bg-red-50' : ''}">
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
                        <div class="flex items-center gap-2">
                          <p class="font-medium text-text">{getDisplayName(user)}</p>
                          {#if user.isBanned}
                            <span class="px-2 py-0.5 bg-red-100 text-red-800 text-xs rounded">BAN済み</span>
                          {/if}
                        </div>
                        <p class="text-sm text-text-light">@{user.userName}</p>
                        <p class="text-xs text-text-light">{user.email}</p>
                        {#if user.isBanned}
                          <p class="text-xs text-red-600 mt-1">
                            Ban理由: {user.banReason || 'なし'} |
                            Ban日時: {formatDate(user.bannedAt)}
                          </p>
                        {/if}
                      </div>
                    </div>

                    <div class="flex items-center gap-3">
                      <!-- 現在のロール -->
                      <div class="flex flex-wrap gap-1">
                        {#each getUserRoles(user) as roleName}
                          <span class="px-2 py-1 bg-surface border border-border rounded text-xs text-text flex items-center gap-1">
                            {roleName}
                            <button
                              onclick={() => handleRemoveRole(user, roleName)}
                              disabled={isLoading}
                              class="hover:text-red-600 disabled:opacity-50"
                              title="ロールを削除"
                            >
                              ×
                            </button>
                          </span>
                        {:else}
                          <span class="text-xs text-text-light">ロールなし</span>
                        {/each}
                      </div>

                      <!-- ロール追加ドロップダウン -->
                      <select
                        value=""
                        onchange={(e) => handleAddRole(user, e.currentTarget.value)}
                        disabled={isLoading}
                        class="px-3 py-1.5 border border-border rounded text-sm focus:outline-none focus:border-primary disabled:opacity-50"
                      >
                        <option value="">+ ロール追加</option>
                        {#each roles.filter(r => !getUserRoles(user).includes(r.name)) as role}
                          <option value={role.name}>{role.name}</option>
                        {/each}
                      </select>

                      <!-- Ban/Unban ボタン -->
                      {#if user.isBanned}
                        <button
                          onclick={() => handleUnban(user)}
                          disabled={isLoading}
                          class="px-3 py-1.5 bg-green-600 text-white rounded text-sm hover:bg-green-700 transition-colors disabled:opacity-50"
                          title="Banを解除"
                        >
                          解除
                        </button>
                      {:else}
                        <button
                          onclick={() => openBanModal(user)}
                          disabled={isLoading}
                          class="px-3 py-1.5 bg-red-600 text-white rounded text-sm hover:bg-red-700 transition-colors disabled:opacity-50"
                          title="Banする"
                        >
                          Ban
                        </button>
                      {/if}
                    </div>
                  </div>
                </div>
              {/each}
            </div>
          </div>
        {/if}
      </div>
    {:else if activeTab === 'roles'}
      <div class="space-y-6">
        <div>
          <h2 class="text-2xl font-semibold text-text">ロール管理</h2>
          <p class="text-sm text-text-light mt-1">テナントレベルのロールとIdentity権限を管理します。</p>
        </div>

        {#if isLoading}
          <div class="text-center py-8">
            <div class="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
            <p class="mt-2 text-sm text-text-light">読み込み中...</p>
          </div>
        {:else if roles.length === 0}
          <div class="text-center py-12 text-text-light">
            <p>ロールがありません</p>
          </div>
        {:else}
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {#each roles as role}
              <div class="border border-border rounded-lg p-4 hover:border-primary transition-colors">
                <p class="font-medium text-text">{role.name}</p>
                <p class="text-xs text-text-light mt-1">ID: {role.id}</p>
                {#if role.permissions && role.permissions.length > 0}
                  <div class="mt-2">
                    <p class="text-xs text-text-light mb-1">パーミッション:</p>
                    <div class="flex flex-wrap gap-1">
                      {#each role.permissions as perm}
                        <span class="px-1.5 py-0.5 bg-surface border border-border rounded text-xs text-text-light">
                          {perm}
                        </span>
                      {/each}
                    </div>
                  </div>
                {/if}
              </div>
            {/each}
          </div>
        {/if}
      </div>
    {/if}

    <!-- 権限説明 -->
    <div class="mt-8 bg-white rounded-lg shadow-sm border border-border overflow-hidden">
      <div class="p-6 border-b border-border">
        <h2 class="text-xl font-semibold text-text">テナント権限一覧</h2>
        <p class="text-sm text-text-light mt-1">テナントレベルで利用可能な権限です。</p>
      </div>

      <div class="p-6">
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <!-- テナント管理 -->
          <div>
            <h3 class="text-sm font-semibold text-text mb-3 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-purple-500"></span>
              テナント管理
            </h3>
            <div class="space-y-2">
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.read</p>
                  <p class="text-xs text-text-light">テナント情報を閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.manage</p>
                  <p class="text-xs text-text-light">テナントを管理できます</p>
                </div>
              </div>
            </div>
          </div>

          <!-- ユーザー権限 -->
          <div>
            <h3 class="text-sm font-semibold text-text mb-3 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-blue-500"></span>
              ユーザー管理
            </h3>
            <div class="space-y-2">
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.user.read</p>
                  <p class="text-xs text-text-light">ユーザー情報を閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.user.manage</p>
                  <p class="text-xs text-text-light">ユーザー情報を変更できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.usermanagement</p>
                  <p class="text-xs text-text-light">ユーザーの作成・Banができます</p>
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
                  <p class="font-medium text-text text-sm">tenant.role.read</p>
                  <p class="text-xs text-text-light">ロール情報を閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.role.manage</p>
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
                  <p class="font-medium text-text text-sm">tenant.permission.read</p>
                  <p class="text-xs text-text-light">権限設定を閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.permission.manage</p>
                  <p class="text-xs text-text-light">権限を管理できます</p>
                </div>
              </div>
            </div>
          </div>

          <!-- ルーム管理（テナントレベル） -->
          <div>
            <h3 class="text-sm font-semibold text-text mb-3 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-orange-500"></span>
              ルーム管理
            </h3>
            <div class="space-y-2">
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.room.read</p>
                  <p class="text-xs text-text-light">すべてのルームを閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.room.manage</p>
                  <p class="text-xs text-text-light">すべてのルームを管理できます</p>
                </div>
              </div>
            </div>
          </div>

          <!-- トピック管理（テナントレベル） -->
          <div>
            <h3 class="text-sm font-semibold text-text mb-3 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-pink-500"></span>
              トピック管理
            </h3>
            <div class="space-y-2">
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.topic.read</p>
                  <p class="text-xs text-text-light">すべてのトピックを閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.topic.manage</p>
                  <p class="text-xs text-text-light">すべてのトピックを管理できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.topic.readMessages</p>
                  <p class="text-xs text-text-light">すべてのトピックのメッセージを閲覧できます</p>
                </div>
              </div>
              <div class="flex items-center justify-between p-3 bg-surface rounded">
                <div>
                  <p class="font-medium text-text text-sm">tenant.topic.writeMessages</p>
                  <p class="text-xs text-text-light">すべてのトピックにメッセージを送信できます</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</div>

<!-- Create User Modal -->
{#if showCreateUserModal}
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50" onclick={(e) => e.target === e.currentTarget && (showCreateUserModal = false)}>
    <div class="bg-white rounded-lg shadow-xl p-6 w-full max-w-md">
      <h3 class="text-lg font-semibold text-text mb-4">ユーザーを作成</h3>

      <div class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-text mb-1">メールアドレス</label>
          <input
            type="email"
            bind:value={newEmail}
            placeholder="user@example.com"
            disabled={isLoading}
            class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary disabled:opacity-50"
          />
        </div>
      </div>

      <div class="mt-6 flex justify-end gap-3">
        <button
          onclick={() => (showCreateUserModal = false)}
          disabled={isLoading}
          class="px-4 py-2 border border-border rounded text-sm hover:bg-surface transition-colors disabled:opacity-50"
        >
          キャンセル
        </button>
        <button
          onclick={handleCreateUser}
          disabled={isLoading || !newEmail.trim()}
          class="px-4 py-2 bg-primary text-white rounded text-sm hover:bg-opacity-90 transition-colors disabled:opacity-50"
        >
          作成
        </button>
      </div>
    </div>
  </div>
{/if}

<!-- Ban Modal -->
{#if showBanModal && banTargetUser}
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50" onclick={(e) => e.target === e.currentTarget && (showBanModal = false)}>
    <div class="bg-white rounded-lg shadow-xl p-6 w-full max-w-md">
      <h3 class="text-lg font-semibold text-text mb-4">ユーザーをBan</h3>

      <p class="text-sm text-text-light mb-4">
        <strong>{getDisplayName(banTargetUser)}</strong>をBanします。このユーザーはログインできなくなります。
      </p>

      <div class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-text mb-1">Ban理由 *</label>
          <textarea
            bind:value={banReason}
            placeholder="Banの理由を入力してください"
            disabled={isLoading}
            rows="3"
            class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary disabled:opacity-50"
          ></textarea>
        </div>
      </div>

      <div class="mt-6 flex justify-end gap-3">
        <button
          onclick={() => (showBanModal = false)}
          disabled={isLoading}
          class="px-4 py-2 border border-border rounded text-sm hover:bg-surface transition-colors disabled:opacity-50"
        >
          キャンセル
        </button>
        <button
          onclick={handleBan}
          disabled={isLoading || !banReason.trim()}
          class="px-4 py-2 bg-red-600 text-white rounded text-sm hover:bg-red-700 transition-colors disabled:opacity-50"
        >
          Banする
        </button>
      </div>
    </div>
  </div>
{/if}

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
