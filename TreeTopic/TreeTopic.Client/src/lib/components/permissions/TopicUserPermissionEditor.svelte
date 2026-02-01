<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '$lib/api/client';

  interface Props {
    tenant: string;
    roomId: string;
    topicId: string;
  }

  let { tenant, roomId, topicId }: Props = $props();

  let availablePermissions = $state<any[]>([]);
  let roomUsers = $state<any[]>([]);
  let userPermissions = $state<Record<string, string[]>>({});
  let isLoading = $state(true);
  let error = $state<string | null>(null);
  let showAddUser = $state(false);
  let selectedUserId = $state('');
  let selectedPermission = $state('');

  onMount(async () => {
    await loadData();
  });

  async function loadData() {
    try {
      isLoading = true;

      // 利用可能なトピック権限を取得
      const availablePermsData = await api.get<any>(`/${tenant}/api/permissions/available`);
      availablePermissions = availablePermsData.topic || [];

      // ルームユーザー一覧を取得
      const usersData = await api.get<any>(`/${tenant}/api/RoomUsers/room/${roomId}`);
      roomUsers = usersData;

      // 各ユーザーの権限を取得
      const permPromises = roomUsers.map(async (user) => {
        try {
          const perms = await api.get<any>(`/${tenant}/api/topics/${topicId}/user-permissions/user/${user.id}`);
          return { userId: user.id, permissions: perms.permissions || [] };
        } catch {
          return { userId: user.id, permissions: [] };
        }
      });

      const userPermData = await Promise.all(permPromises);
      userPermissions = {};
      userPermData.forEach(p => {
        userPermissions[p.userId] = p.permissions;
      });

      error = null;
    } catch (err: any) {
      error = err.message || 'データの読み込みに失敗しました';
    } finally {
      isLoading = false;
    }
  }

  async function togglePermission(userId: string, permissionName: string) {
    try {
      const currentPerms = userPermissions[userId] || [];
      const hasPermission = currentPerms.includes(permissionName);

      if (hasPermission) {
        // 削除
        await api.delete(`/${tenant}/api/topics/${topicId}/user-permissions/user/${userId}/${encodeURIComponent(permissionName)}`);
        userPermissions[userId] = currentPerms.filter(p => p !== permissionName);
      } else {
        // 追加
        await api.post(`/${tenant}/api/topics/${topicId}/user-permissions`, {
          roomUserId: userId,
          permissionName
        });
        userPermissions[userId] = [...currentPerms, permissionName];
      }
    } catch (err: any) {
      error = err.message || '権限の更新に失敗しました';
    }
  }

  async function addPermissionToUser() {
    if (!selectedUserId || !selectedPermission) {
      error = 'ユーザーと権限を選択してください';
      return;
    }

    try {
      await api.post(`/${tenant}/api/topics/${topicId}/user-permissions`, {
        roomUserId: selectedUserId,
        permissionName: selectedPermission
      });

      const currentPerms = userPermissions[selectedUserId] || [];
      userPermissions[selectedUserId] = [...currentPerms, selectedPermission];

      showAddUser = false;
      selectedUserId = '';
      selectedPermission = '';
      error = null;
    } catch (err: any) {
      error = err.message || '権限の追加に失敗しました';
    }
  }

  async function removePermission(userId: string, permissionName: string) {
    try {
      await api.delete(`/${tenant}/api/topics/${topicId}/user-permissions/user/${userId}/${encodeURIComponent(permissionName)}`);
      userPermissions[userId] = (userPermissions[userId] || []).filter(p => p !== permissionName);
    } catch (err: any) {
      error = err.message || '権限の削除に失敗しました';
    }
  }

  function getDisplayName(user: any): string {
    return user.displayName || user.userName || 'Unknown';
  }

  function hasPermission(userId: string, permissionName: string): boolean {
    return (userPermissions[userId] || []).includes(permissionName);
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

  <!-- 権限追加ボタン -->
  <div class="flex justify-between items-center">
    <h3 class="text-lg font-semibold text-text">ユーザー別権限</h3>
    <button
      onclick={() => (showAddUser = true)}
      class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
    >
      + 権限追加
    </button>
  </div>

  {#if showAddUser}
    <div class="border border-border rounded-lg p-4 bg-surface">
      <h4 class="font-medium text-text mb-3">ユーザーに権限を追加</h4>
      <div class="space-y-3">
        <div>
          <label class="block text-sm font-medium text-text mb-1">ユーザー</label>
          <select
            bind:value={selectedUserId}
            class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary"
          >
            <option value="">選択してください</option>
            {#each roomUsers as user}
              <option value={user.id}>{getDisplayName(user)}</option>
            {/each}
          </select>
        </div>
        <div>
          <label class="block text-sm font-medium text-text mb-1">権限</label>
          <select
            bind:value={selectedPermission}
            class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary"
          >
            <option value="">選択してください</option>
            {#each availablePermissions as perm}
              <option value={perm.name}>{perm.label} - {perm.description}</option>
            {/each}
          </select>
        </div>
        <div class="flex gap-2">
          <button
            onclick={addPermissionToUser}
            disabled={!selectedUserId || !selectedPermission}
            class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium disabled:opacity-50"
          >
            追加
          </button>
          <button
            onclick={() => (showAddUser = false)}
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
  {:else if roomUsers.length === 0}
    <div class="border border-border rounded-lg p-8 text-center text-text-light">
      <p>このルームにメンバーがいません</p>
    </div>
  {:else}
    <!-- ユーザー権限一覧 -->
    <div class="border border-border rounded-lg overflow-hidden">
      <div class="bg-surface p-4 border-b border-border">
        <h3 class="font-semibold text-text">メンバー一覧</h3>
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
                <p class="text-xs text-text-light mb-2">個別権限</p>
                <div class="flex flex-wrap gap-2">
                  {#each availablePermissions as perm}
                    {@const hasPerm = hasPermission(user.id, perm.name)}
                    <button
                      onclick={() => togglePermission(user.id, perm.name)}
                      class="px-3 py-1.5 rounded-full border transition-colors text-xs font-medium {hasPerm
                        ? 'bg-primary bg-opacity-10 border-primary text-primary'
                        : 'border-border hover:bg-surface'}"
                    >
                      {perm.label}
                    </button>
                  {/each}
                </div>
              </div>

              {#if perms.length > 0}
                <div class="pt-2 border-t border-border">
                  <p class="text-xs text-text-light">
                    付与済み: {perms.map(p => {
                      const perm = availablePermissions.find(ap => ap.name === p);
                      return perm?.label || p;
                    }).join(', ')}
                  </p>
                </div>
              {/if}
            </div>
          </div>
        {/each}
      </div>
    </div>
  {/if}

  <!-- 権限一覧参考 -->
  <div class="border border-border rounded-lg overflow-hidden">
    <div class="bg-surface p-4 border-b border-border">
      <h3 class="font-semibold text-text">トピック権限一覧</h3>
    </div>

    <div class="p-4 space-y-2">
      {#each availablePermissions as perm}
        <div class="flex items-center justify-between p-3 bg-surface rounded">
          <div class="flex-1">
            <p class="font-medium text-text text-sm">{perm.label}</p>
            <p class="text-xs text-text-light">{perm.description}</p>
          </div>
        </div>
      {/each}
    </div>
  </div>
</div>
