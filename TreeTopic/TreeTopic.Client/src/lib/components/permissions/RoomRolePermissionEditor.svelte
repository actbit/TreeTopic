<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '$lib/api/client';

  interface Props {
    tenant: string;
    roomId: string;
  }

  let { tenant, roomId }: Props = $props();

  let roles = $state<any[]>([]);
  let availablePermissions = $state<any>({ room: [], topic: [] });
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let showCreateRole = $state(false);
  let newRoleName = $state('');
  let newRoleDescription = $state('');

  // 権限のチェック状態
  let rolePermissions = $state<Record<string, string[]>>({});

  onMount(async () => {
    await loadData();
  });

  async function loadData() {
    try {
      isLoading = true;

      // ロール一覧を取得
      const rolesData = await api.get<any[]>(`/${tenant}/api/roomroles`);
      roles = rolesData;

      // 各ロールの権限を取得
      const permPromises = roles.map(async (role) => {
        const perms = await api.get<any>(`/${tenant}/api/roomroles/${role.id}/permissions`);
        return { roleId: role.id, permissions: perms.permissions || [] };
      });

      const permsData = await Promise.all(permPromises);
      rolePermissions = {};
      permsData.forEach(p => {
        rolePermissions[p.roleId] = p.permissions;
      });

      // 利用可能な権限一覧を取得
      availablePermissions = await api.get<any>(`/${tenant}/api/permissions/available`);

      error = null;
    } catch (err: any) {
      error = err.message || 'データの読み込みに失敗しました';
    } finally {
      isLoading = false;
    }
  }

  async function togglePermission(roleId: string, permissionName: string) {
    try {
      const currentPerms = rolePermissions[roleId] || [];
      const hasPermission = currentPerms.includes(permissionName);

      if (hasPermission) {
        // 削除
        await api.delete(`/${tenant}/api/roomroles/${roleId}/permissions/${encodeURIComponent(permissionName)}`);
        rolePermissions[roleId] = currentPerms.filter(p => p !== permissionName);
      } else {
        // 追加
        await api.post(`/${tenant}/api/roomroles/${roleId}/permissions`, { permissionName });
        rolePermissions[roleId] = [...currentPerms, permissionName];
      }
    } catch (err: any) {
      error = err.message || '権限の更新に失敗しました';
    }
  }

  async function createRole() {
    if (!newRoleName.trim()) return;

    try {
      await api.post(`/${tenant}/api/roomroles`, {
        name: newRoleName.trim(),
        description: newRoleDescription.trim()
      });

      newRoleName = '';
      newRoleDescription = '';
      showCreateRole = false;
      await loadData();
    } catch (err: any) {
      error = err.message || 'ロールの作成に失敗しました';
    }
  }

  async function deleteRole(roleId: string) {
    if (!confirm('このロールを削除しますか？')) return;

    try {
      await api.delete(`/${tenant}/api/roomroles/${roleId}`);
      await loadData();
    } catch (err: any) {
      error = err.message || 'ロールの削除に失敗しました';
    }
  }

  function hasPermission(roleId: string, permissionName: string): boolean {
    return (rolePermissions[roleId] || []).includes(permissionName);
  }

  function formatPermissionName(name: string): string {
    return name
      .split('.')
      .map((part, i) => {
        if (part === 'room' || part === 'topic') return '';
        return part.charAt(0).toUpperCase() + part.slice(1);
      })
      .join(' ')
      .trim();
  }
</script>

<div class="space-y-6">
  {#if error}
    <div class="p-4 bg-red-50 border border-red-200 rounded text-red-800 text-sm">
      {error}
      <button
        onclick={() => (error = null)}
        class="ml-2 underline hover:no-underline"
      >
        閉じる
      </button>
    </div>
  {/if}

  <!-- 新規ロール作成 -->
  <div class="flex justify-between items-center">
    <h3 class="text-lg font-semibold text-text">ロール一覧</h3>
    <button
      onclick={() => (showCreateRole = true)}
      class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
    >
      + ロール作成
    </button>
  </div>

  {#if showCreateRole}
    <div class="border border-border rounded-lg p-4 bg-surface">
      <h4 class="font-medium text-text mb-3">新規ロール作成</h4>
      <div class="space-y-3">
        <div>
          <label class="block text-sm font-medium text-text mb-1">ロール名</label>
          <input
            type="text"
            bind:value={newRoleName}
            placeholder="例: メンバー、モデレーター"
            class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary"
          />
        </div>
        <div>
          <label class="block text-sm font-medium text-text mb-1">説明</label>
          <input
            type="text"
            bind:value={newRoleDescription}
            placeholder="ロールの説明..."
            class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary"
          />
        </div>
        <div class="flex gap-2">
          <button
            onclick={createRole}
            disabled={!newRoleName.trim()}
            class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium disabled:opacity-50"
          >
            作成
          </button>
          <button
            onclick={() => (showCreateRole = false)}
            class="px-4 py-2 bg-surface border border-border rounded hover:bg-opacity-80 transition-colors text-sm font-medium"
          >
            キャンセル
          </button>
        </div>
      </div>
    </div>
  {/if}

  {#if isLoading}
    <div class="text-center py-8">
      <p class="text-text-light">読み込み中...</p>
    </div>
  {:else if roles.length === 0}
    <div class="border border-border rounded-lg p-8 text-center text-text-light">
      <p>ロールがありません</p>
    </div>
  {:else}
    <!-- ロールと権限一覧 -->
    <div class="space-y-4">
      {#each roles as role}
        {@const perms = rolePermissions[role.id] || []}
        <div class="border border-border rounded-lg overflow-hidden">
          <div class="bg-surface p-4 border-b border-border flex justify-between items-center">
            <div>
              <p class="font-semibold text-text">{role.name}</p>
              {#if role.description}
                <p class="text-sm text-text-light">{role.description}</p>
              {/if}
            </div>
            <button
              onclick={() => deleteRole(role.id)}
              class="text-danger hover:text-red-700 transition-colors text-sm"
            >
              削除
            </button>
          </div>

          <div class="p-4">
            <h4 class="text-sm font-medium text-text mb-3">権限</h4>

            <!-- ルーム権限 -->
            <div class="mb-4">
              <p class="text-xs text-text-light mb-2 font-medium">ルーム権限</p>
              <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2">
                {#each availablePermissions.room as perm}
                  {@const hasPerm = hasPermission(role.id, perm.name)}
                  <button
                    onclick={() => togglePermission(role.id, perm.name)}
                    class="flex items-center gap-2 p-2 rounded border transition-colors text-left text-sm {hasPerm
                      ? 'bg-primary bg-opacity-10 border-primary text-primary'
                      : 'border-border hover:bg-surface'}"
                  >
                    <span class="w-4 h-4 rounded border flex items-center justify-center {hasPerm
                      ? 'bg-primary border-primary'
                      : 'border-border'}">
                      {#if hasPerm}
                        <svg class="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
                        </svg>
                      {/if}
                    </span>
                    <span class="text-xs">{perm.label}</span>
                  </button>
                {/each}
              </div>
            </div>

            <!-- トピック権限 -->
            <div>
              <p class="text-xs text-text-light mb-2 font-medium">トピック権限</p>
              <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2">
                {#each availablePermissions.topic as perm}
                  {@const hasPerm = hasPermission(role.id, perm.name)}
                  <button
                    onclick={() => togglePermission(role.id, perm.name)}
                    class="flex items-center gap-2 p-2 rounded border transition-colors text-left text-sm {hasPerm
                      ? 'bg-primary bg-opacity-10 border-primary text-primary'
                      : 'border-border hover:bg-surface'}"
                  >
                    <span class="w-4 h-4 rounded border flex items-center justify-center {hasPerm
                      ? 'bg-primary border-primary'
                      : 'border-border'}">
                      {#if hasPerm}
                        <svg class="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
                        </svg>
                      {/if}
                    </span>
                    <span class="text-xs">{perm.label}</span>
                  </button>
                {/each}
              </div>
            </div>

            {#if perms.length > 0}
              <div class="mt-3 pt-3 border-t border-border">
                <p class="text-xs text-text-light">
                  付与済み権限: <span class="font-medium">{perms.length}</span>件
                </p>
              </div>
            {/if}
          </div>
        </div>
      {/each}
    </div>
  {/if}
</div>
