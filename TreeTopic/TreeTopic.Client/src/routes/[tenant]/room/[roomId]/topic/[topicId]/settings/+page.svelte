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
  let topic = $state<{ title?: string; description?: string | null; createdAt?: string | Date; parentId?: string | null } | null>(null);
  let room = $state<{ name?: string } | null>(null);

  const tenant = $page.params.tenant || '';
  const roomId = $page.params.roomId || '';
  const topicId = $page.params.topicId || '';

  const tabs = [
    { id: 'general', label: 'Basic Settings' },
    { id: 'permissions', label: 'Permissions' }
  ];

  onMount(() => {
    loadTopicData();
  });

  async function loadTopicData() {
    try {
      isLoading = true;

      // トピック情報を取得
      const topicData = await api.get<{ title?: string; description?: string | null; createdAt?: string | Date; parentId?: string | null }>(`/${tenant}/api/Topic/${topicId}`);
      topic = topicData;

      // ルーム情報を取得
      if (roomId) {
        try {
          const roomData = await api.get<{ name?: string }>(`/${tenant}/api/room/${roomId}`);
          room = roomData;
        } catch {
          // ルーム情報が取得できなくても無視
        }
      }

      error = null;
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load data';
    } finally {
      isLoading = false;
    }
  }

  function openUserPermissionModal() {
    ui.openModal({
      id: 'topic-user-permission',
      title: 'Topic User Permissions',
      type: 'custom',
      data: { tenant, roomId, topicId }
    });
  }
</script>

<svelte:head>
  <title>Topic Settings - TreeTopic</title>
</svelte:head>

<AppLayout>
  {#snippet headerContent()}
    <div class="flex items-center gap-4">
      <Breadcrumbs
        items={[
          { label: room?.name || 'Room', href: `/${tenant}/room/${roomId}` },
          { label: topic?.title || 'Topic', href: `/${tenant}/room/${roomId}/topic/${topicId}` },
          { label: 'Settings' }
        ]}
      />
      <h1 class="text-xl font-bold text-text">Topic Settings</h1>
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
            <button onclick={() => (error = null)} class="underline hover:no-underline">Close</button>
          </div>
        {/if}

        {#if isLoading}
          <div class="text-center py-8">
            <p class="text-text-light">Loading...</p>
          </div>
        {:else if activeTab === 'general'}
          <div class="space-y-6">
            <div>
              <h2 class="text-2xl font-bold text-text mb-4">Basic Settings</h2>
            </div>

            {#if topic}
              <div class="border border-border rounded-lg p-6 space-y-4">
                <div>
                  <span class="block text-sm font-medium text-text-light mb-1">Title</span>
                  <p class="text-text">{topic.title}</p>
                </div>
                {#if topic.description}
                  <div>
                    <span class="block text-sm font-medium text-text-light mb-1">Description</span>
                    <p class="text-text">{topic.description}</p>
                  </div>
                {/if}
                <div class="grid grid-cols-2 gap-4 text-sm">
                  <div>
                    <span class="text-text-light">Created:</span>
                    <span class="text-text ml-2">{topic.createdAt ? new Date(topic.createdAt as string).toLocaleDateString() : ''}</span>
                  </div>
                  {#if topic.parentId}
                    <div>
                      <span class="text-text-light">Parent Topic:</span>
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
                <h2 class="text-2xl font-bold text-text mb-2">Topic Permission Management</h2>
                <p class="text-text-light">Manage access permissions for this topic.</p>
              </div>
              <button
                onclick={openUserPermissionModal}
                class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
              >
                Manage Permissions
              </button>
            </div>

            <!-- 権限説明 -->
            <div class="bg-surface border border-border rounded-lg p-6">
              <h3 class="text-lg font-semibold text-text mb-4">About Topic Permissions</h3>
              <p class="text-sm text-text-light mb-4">
                Topic permissions control access for users and roles to specific topics.
              </p>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div class="space-y-2">
                  <h4 class="text-sm font-semibold text-text">User Permissions</h4>
                  <p class="text-xs text-text-light">Assign permissions directly to specific users. Added to role permissions.</p>
                </div>
                <div class="space-y-2">
                  <h4 class="text-sm font-semibold text-text">Role Permissions</h4>
                  <p class="text-xs text-text-light">Assign topic permissions to room roles. Applied to all users with that role.</p>
                </div>
              </div>
            </div>

            <!-- 利用可能な権限一覧 -->
            <div class="border border-border rounded-lg overflow-hidden">
              <div class="bg-surface p-4 border-b border-border">
                <h3 class="font-semibold text-text">Available Topic Permissions</h3>
              </div>
              <div class="p-4 space-y-2">
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.read</p>
                    <p class="text-xs text-text-light">View topics</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.write</p>
                    <p class="text-xs text-text-light">Create and edit topics</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.delete</p>
                    <p class="text-xs text-text-light">Delete topics</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.manage</p>
                    <p class="text-xs text-text-light">Manage topic permissions</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.readMessages</p>
                    <p class="text-xs text-text-light">View messages</p>
                  </div>
                </div>
                <div class="flex items-center justify-between p-3 bg-surface rounded">
                  <div>
                    <p class="font-medium text-text text-sm">topic.writeMessages</p>
                    <p class="text-xs text-text-light">Post and edit messages</p>
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
