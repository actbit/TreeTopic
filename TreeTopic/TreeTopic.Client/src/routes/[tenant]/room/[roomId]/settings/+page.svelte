<script lang="ts">
  import { page } from '$app/stores';
  import { api } from '$lib/api/client';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import Button from '$lib/components/common/Button.svelte';
  import RoomRolePermissionEditor from '$lib/components/permissions/RoomRolePermissionEditor.svelte';

  let activeTab = $state('roles');
  let isLoading = $state(false);
  let error = $state<string | null>(null);

  const tenant = $page.params.tenant ?? '';
  const roomId = $page.params.roomId ?? '';

  const tabs = [
    { id: 'roles', label: 'ロール' },
    { id: 'members', label: 'メンバー' },
    { id: 'permissions', label: '権限' }
  ];

  async function handleSave() {
    // 保存処理
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
          <div class="mb-4 p-4 bg-red-50 border border-red-200 rounded text-red-800 text-sm">
            {error}
          </div>
        {/if}

        {#if activeTab === 'roles'}
          <div class="space-y-6">
            <div>
              <h2 class="text-2xl font-bold text-text mb-4">ロール管理</h2>
              <p class="text-text-light mb-4">ルーム内のロールとその権限を管理します。</p>
            </div>

            <RoomRolePermissionEditor {tenant} {roomId} />
          </div>
        {:else if activeTab === 'members'}
          <div class="space-y-6">
            <div>
              <h2 class="text-2xl font-bold text-text mb-4">メンバー管理</h2>
              <p class="text-text-light mb-4">ルームメンバーとロール割り当てを管理します。</p>
            </div>

            <div class="border border-border rounded-lg p-8 text-center text-text-light">
              <p>準備中...</p>
            </div>
          </div>
        {:else if activeTab === 'permissions'}
          <div class="space-y-6">
            <div>
              <h2 class="text-2xl font-bold text-text mb-4">権限一覧</h2>
              <p class="text-text-light mb-4">利用可能な権限の一覧です。</p>
            </div>

            <div class="border border-border rounded-lg overflow-hidden">
              <div class="bg-surface p-4 border-b border-border">
                <h3 class="font-semibold text-text">ルーム権限</h3>
              </div>
              <div class="p-4 space-y-2">
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">room.join</p>
                    <p class="text-sm text-text-light">ルームに参加できます</p>
                  </div>
                  <span class="px-2 py-1 bg-green-100 text-green-800 text-xs rounded">基本</span>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">room.read</p>
                    <p class="text-sm text-text-light">ルーム情報を閲覧できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">room.write</p>
                    <p class="text-sm text-text-light">トピック作成、ファイルアップロード等ができます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">room.delete</p>
                    <p class="text-sm text-text-light">シェア、ファイル等を削除できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">room.manage</p>
                    <p class="text-sm text-text-light">ルーム設定を変更できます</p>
                  </div>
                  <span class="px-2 py-1 bg-yellow-100 text-yellow-800 text-xs rounded">管理者</span>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">room.manageUsers</p>
                    <p class="text-sm text-text-light">ルームメンバーを管理できます</p>
                  </div>
                  <span class="px-2 py-1 bg-yellow-100 text-yellow-800 text-xs rounded">管理者</span>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">room.manageRoles</p>
                    <p class="text-sm text-text-light">ルームロールを管理できます</p>
                  </div>
                  <span class="px-2 py-1 bg-yellow-100 text-yellow-800 text-xs rounded">管理者</span>
                </div>
              </div>
            </div>

            <div class="border border-border rounded-lg overflow-hidden">
              <div class="bg-surface p-4 border-b border-border">
                <h3 class="font-semibold text-text">トピック権限</h3>
              </div>
              <div class="p-4 space-y-2">
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">topic.read</p>
                    <p class="text-sm text-text-light">トピックを閲覧できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">topic.write</p>
                    <p class="text-sm text-text-light">トピックを作成・編集できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">topic.delete</p>
                    <p class="text-sm text-text-light">トピックを削除できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">topic.manage</p>
                    <p class="text-sm text-text-light">トピック権限を管理できます</p>
                  </div>
                  <span class="px-2 py-1 bg-yellow-100 text-yellow-800 text-xs rounded">管理者</span>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">topic.readMessages</p>
                    <p class="text-sm text-text-light">メッセージを閲覧できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text">topic.writeMessages</p>
                    <p class="text-sm text-text-light">メッセージを投稿・編集できます</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        {/if}
      </div>
    </div>
  {/snippet}
</AppLayout>
