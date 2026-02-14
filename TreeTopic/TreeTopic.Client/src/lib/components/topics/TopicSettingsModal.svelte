<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { ui, activeModals, modals } from '$lib/stores/ui';
  import { page } from '$app/stores';
  import { api } from '$lib/api/client';
  import { permissionsApi } from '$lib/api/permissions';
  import type { AvailablePermissions } from '$lib/types/permissions';
  import { selectedTopic } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import { formatPermissionName } from '$lib/utils/permission';

  const modalId = 'topic-settings';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let tenant = $derived.by(() => (modal?.data?.tenant ?? $page.params.tenant ?? '') as string);
  let roomId = $derived.by(() => (modal?.data?.roomId ?? '') as string);
  let topicId = $derived.by(() => (modal?.data?.topicId ?? '') as string);

  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let canManageTopic = $state(false);
  let activeTab = $state('general');

  // Topic data
  let topic = $state<{ title?: string; description?: string | null; createdAt?: string | Date; parentId?: string | null } | null>(null);
  let availablePermissions = $state<AvailablePermissions>({ tenant: [], topic: [], room: [] });

  $effect(() => {
    if (isOpen && tenant && topicId) {
      loadTopicData();
    }
  });

  async function loadTopicData() {
    try {
      isLoading = true;
      const topicData = await api.get<{ title?: string; description?: string | null; createdAt?: string | Date; parentId?: string | null }>(`/${tenant}/api/Topic/${topicId}`);
      topic = topicData;

      // Check permissions for management
      const permRes = await api.get<{ permissions?: string[] }>(`/${tenant}/api/Topic/${topicId}/my/permissions`);
      const perms = new Set(permRes?.permissions ?? []);
      canManageTopic = perms.has('topic.manage') || perms.has('topic.write');

      // Fetch available permissions
      availablePermissions = await permissionsApi.getAvailablePermissions(tenant);
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to load topic data';
    } finally {
      isLoading = false;
    }
  }

  function openUserPermissionModal() {
    if (!roomId || !topicId) return;
    modals.open('topic-user-permission', 'Topic User Permissions', {
      tenant,
      roomId,
      topicId
    });
  }

  function openEditModal() {
    if (!topicId) return;
    modals.open('topic-edit', 'Edit Topic', {
      tenant,
      roomId,
      topicId,
      currentTopic: topic
    });
  }

  function openDeleteModal() {
    if (!topicId) return;
    modals.open('topic-delete', 'Delete Topic', {
      tenant,
      roomId,
      topicId,
      currentTopic: topic
    });
  }

  function handleClose() {
    ui.closeModal(modalId);
  }

  function formatDate(date: string | Date | null | undefined): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
</script>

<Modal {isOpen} title="Topic Settings" onClose={handleClose} size="large">
  <div class="flex flex-col h-full bg-white">
    <!-- Error message -->
    {#if error}
      <div class="p-4 bg-red-50 border-b border-red-200 text-red-800 text-sm flex justify-between items-center">
        <span>{error}</span>
        <button onclick={() => (error = null)} class="underline hover:no-underline">Close</button>
      </div>
    {/if}

    <!-- Tabs -->
    <div class="flex border-b border-border">
      <button
        onclick={() => activeTab = 'general'}
        class="px-6 py-3 text-sm font-medium {activeTab === 'general'
          ? 'border-b-2 border-primary text-primary'
          : 'text-text hover:text-text-light'}"
      >
        General
      </button>
      <button
        onclick={() => activeTab = 'permissions'}
        class="px-6 py-3 text-sm font-medium {activeTab === 'permissions'
          ? 'border-b-2 border-primary text-primary'
          : 'text-text hover:text-text-light'}"
      >
        Permissions
      </button>
    </div>

    <!-- Content -->
    <div class="flex-1 overflow-auto p-6">
      {#if isLoading}
        <div class="text-center py-8">
          <div class="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          <p class="mt-2 text-sm text-text-light">Loading...</p>
        </div>
      {:else if activeTab === 'general'}
        <div class="space-y-6">
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-text-light mb-1">Title</label>
              <p class="text-text">{topic?.title || '-'}</p>
            </div>

            {#if topic?.description}
              <div>
                <label class="block text-sm font-medium text-text-light mb-1">Description</label>
                <p class="text-text">{topic.description}</p>
              </div>
            {/if}

            <div class="grid grid-cols-2 gap-4 text-sm">
              <div>
                <span class="text-text-light">Created:</span>
                <span class="text-text ml-2">{formatDate(topic?.createdAt)}</span>
              </div>
              {#if topic?.parentId}
                <div>
                  <span class="text-text-light">Parent Topic:</span>
                  <span class="text-text ml-2">{topic.parentId}</span>
                </div>
              {/if}
            </div>
          </div>

          <div class="flex gap-3 pt-4 border-t border-border">
            {#if canManageTopic}
              <button
                onclick={openEditModal}
                class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
              >
                Edit Topic
              </button>
            {:else}
              <button
                disabled
                class="px-4 py-2 bg-surface border border-border rounded opacity-50 text-sm font-medium"
              >
                Edit Topic
              </button>
            {/if}

            {#if canManageTopic}
              <button
                onclick={openDeleteModal}
                class="px-4 py-2 bg-danger text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
              >
                Delete Topic
              </button>
            {:else}
              <button
                disabled
                class="px-4 py-2 bg-surface border border-border rounded opacity-50 text-sm font-medium"
              >
                Delete Topic
              </button>
            {/if}
          </div>
        </div>

      {:else if activeTab === 'permissions'}
        <div class="space-y-6">
          <p class="text-sm text-text-light">Manage who can access this topic and what they can do.</p>

          <div class="border border-border rounded-lg p-6">
            <h3 class="font-semibold text-text mb-4">User Permissions</h3>
            <p class="text-sm text-text-light mb-4">Manage individual user permissions for this topic.</p>

            <button
              onclick={openUserPermissionModal}
              class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium"
            >
              Manage User Permissions
            </button>
          </div>

          <div class="bg-surface border border-border rounded-lg p-6">
            <h3 class="font-semibold text-text mb-4">Available Topic Permissions</h3>
            <div class="space-y-3">
              {#each availablePermissions.topic as perm}
                <div class="flex items-center justify-between p-3 bg-white rounded">
                  <div>
                    <p class="font-medium text-text text-sm">{formatPermissionName(perm.name)}</p>
                    <p class="text-xs text-text-light">{perm.name}</p>
                  </div>
                </div>
              {/each}
            </div>
          </div>
        </div>
      {/if}
    </div>
  </div>
</Modal>

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
