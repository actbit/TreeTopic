<script lang="ts">
  import { page } from '$app/stores';
  import { api } from '$lib/api/client';
  import { ui } from '$lib/stores/ui';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import Breadcrumbs from '$lib/components/common/Breadcrumbs.svelte';
  import { onMount } from 'svelte';

  let activeTab = $state('permissions');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let topic = $state<any>(null);
  let room = $state<any>(null);

  const tenant = $page.params.tenant || '';
  const roomId = $page.params.roomId || '';
  const topicId = $page.params.topicId || '';

  const tabs = [
    { id: 'general', label: '基本設定' },
    { id: 'permissions', label: '権限' }
  ];

  onMount(() => {
    loadTopicData();
  });

  async function loadTopicData() {
    try {
      isLoading = true;

      // トピック情報を取得
      const topicData = await api.get<any>(`/${tenant}/api/Topics/${topicId}`);
      topic = topicData;

      // ルーム情報を取得
      if (roomId) {
        try {
          const roomData = await api.get<any>(`/${tenant}/api/Room/${roomId}`);
          room = roomData;
        } catch {
          // ルーム情報が取得できなくても無視
        }
      }

      error = null;
    } catch (err: any) {
      error = err.message || 'データの読み込みに失敗しました';
    } finally {
      isLoading = false;
    }
  }

  function openUserPermissionModal() {
    ui.openModal({
      id: 'topic-user-permission',
      title: 'トピックユーザー権限管理',
      type: 'custom',
      data: { tenant, roomId, topicId }
    });
  }
</script>

<svelte:head>
  <title>トピック設定 - TreeTopic</title>
</svelte:head>

<AppLayout>
  {#snippet headerContent()}
    <div class="flex items-center gap-4">
      <Breadcrumbs
        items={[
          { label: room?.name || 'ルーム', href: `/${tenant}/room/${roomId}` },
          { label: topic?.title || 'トピック', href: `/${tenant}/room/${roomId}/topic/${topicId}` },
          { label: '設定' }
        ]}
      />
      <h1 class="text-xl font-bold text-text">トピック設定</h1>
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

        {#if isLoading}
          <div class="text-center py-8">
            <p class="text-text-light">読み込み中...</p>
          </div>
        {:else if activeTab === 'general'}
          <div class="space-y-6">
            <div>
              <h2 class="text-2xl font-bold text-text mb-4">基本設定</h2>
            </div>

            {#if topic}
              <div class="border border-border rounded-lg p-6 space-y-4">
                <div>
                  <span class="block text-sm font-medium text-text-light mb-1">タイトル</span>
                  <p class="text-text">{topic.title}</p>
                </div>
                {#if topic.description}
                  <div>
                    <span class="block text-sm font-medium text-text-light mb-1">説明</span>
                    <p class="text-text">{topic.description}</p>
                  </div>
                {/if}
                <div class="grid grid-cols-2 gap-4 text-sm">
                  <div>
                    <span class="text-text-light">作成日:</span>
                    <span class="text-text ml-2">{new Date(topic.createdAt).toLocaleDateString('ja-JP')}</span>
                  </div>
                  {#if topic.parentId}
                    <div>
                      <span class="text-text-light">親トピック:</span>
                      <span class="text-text ml-2">{topic.parentId}</span>
                    </div>
                  {/if}
                </div>
              </div>
            {/if}
          </div>
        {:else if activeTab === 'permissions'}
          <div class="space-y-6">
            <div class="flex justify-between items-center">
              <div>
                <h2 class="text-2xl font-bold text-text mb-2">トピック権限管理</h2>
                <p class="text-text-light">このトピックへのアクセス権限を管理します。</p>
              </div>
              <button
                onclick={openUserPermissionModal}
                class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
              >
                権限を管理
              </button>
            </div>

            <!-- 権限説明 -->
            <div class="bg-surface border border-border rounded-lg p-6">
              <h3 class="text-lg font-semibold text-text mb-4">トピック権限について</h3>
              <p class="text-sm text-text-light mb-4">
                トピック権限は、特定のトピックに対するユーザーとロールのアクセスを制御します。
              </p>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div class="space-y-2">
                  <h4 class="text-sm font-semibold text-text">ユーザー権限</h4>
                  <p class="text-xs text-text-light">特定のユーザーに直接権限を割り当てます。ロール権限に追加されます。</p>
                </div>
                <div class="space-y-2">
                  <h4 class="text-sm font-semibold text-text">ロール権限</h4>
                  <p class="text-xs text-text-light">ルームロールにトピック権限を割り当てます。そのロールを持つ全ユーザーに適用されます。</p>
                </div>
              </div>
            </div>

            <!-- 利用可能な権限一覧 -->
            <div class="border border-border rounded-lg overflow-hidden">
              <div class="bg-surface p-4 border-b border-border">
                <h3 class="font-semibold text-text">利用可能なトピック権限</h3>
              </div>
              <div class="p-4 space-y-2">
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.read</p>
                    <p class="text-xs text-text-light">トピックを閲覧できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.write</p>
                    <p class="text-xs text-text-light">トピックを作成・編集できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.delete</p>
                    <p class="text-xs text-text-light">トピックを削除できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.manage</p>
                    <p class="text-xs text-text-light">トピック権限を管理できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.readMessages</p>
                    <p class="text-xs text-text-light">メッセージを閲覧できます</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.writeMessages</p>
                    <p class="text-xs text-text-light">メッセージを投稿・編集できます</p>
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
