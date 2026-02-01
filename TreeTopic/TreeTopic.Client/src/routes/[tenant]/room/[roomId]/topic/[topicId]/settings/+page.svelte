<script lang="ts">
  import { page } from '$app/stores';
  import { api } from '$lib/api/client';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import TopicUserPermissionEditor from '$lib/components/permissions/TopicUserPermissionEditor.svelte';
  import Breadcrumbs from '$lib/components/common/Breadcrumbs.svelte';

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

  import { onMount } from 'svelte';
  onMount(() => {
    loadTopicData();
  });
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
          <div class="mb-4 p-4 bg-red-50 border border-red-200 rounded text-red-800 text-sm">
            {error}
            <button
              onclick={() => (error = null)}
              class="ml-2 underline hover:no-underline"
            >
              閉じる
            </button>
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
                  <label class="block text-sm font-medium text-text-light mb-1">タイトル</label>
                  <p class="text-text">{topic.title}</p>
                </div>
                {#if topic.description}
                  <div>
                    <label class="block text-sm font-medium text-text-light mb-1">説明</label>
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
            <div>
              <h2 class="text-2xl font-bold text-text mb-4">トピック権限管理</h2>
              <p class="text-text-light mb-4">このトピックへのアクセス権限を管理します。</p>
            </div>

            {#if topic}
              <TopicUserPermissionEditor {tenant} {roomId} {topicId} />
            {/if}
          </div>
        {/if}
      </div>
    </div>
  {/snippet}
</AppLayout>
