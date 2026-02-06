<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { topicList, deleteTopic, updateTopic, addTopic } from '$lib/stores/topics';
  import { api } from '$lib/api/client';

  const modalId = 'topic-delete';

  let modalConfig = $derived.by(
    () => $activeModals.find((m) => m.id === modalId) ?? null
  );
  let isOpen = $derived.by(() => modalConfig !== null);
  let topicId = $derived.by(() => modalConfig?.data?.topicId as string | undefined);

  let topic = $derived.by(() => {
    if (!topicId) return null;
    return $topicList.find((t) => t.id === topicId) ?? null;
  });

  type DeleteMode = 'cascade' | 'reparent';
  let deleteMode = $state<DeleteMode>('cascade');

  let isLoading = $state(false);
  let error = $state<string | null>(null);

  $effect(() => {
    if (!isOpen) return;
    deleteMode = 'cascade';
    error = null;
  });

  function collectDescendants(startId: string, topicsById: Map<string, (typeof $topicList)[number]>): string[] {
    const result: string[] = [];
    const queue: string[] = [startId];

    while (queue.length > 0) {
      const currentId = queue.shift()!;
      const current = topicsById.get(currentId);
      if (!current) continue;

      for (const childId of current.childIds ?? []) {
        result.push(childId);
        queue.push(childId);
      }
    }

    return result;
  }

  async function handleDelete() {
    error = null;

    if (!topicId || !topic) {
      error = 'Topic not found';
      return;
    }

    isLoading = true;
    try {
      const tenant = api.getCurrentTenant();
      const strategy = deleteMode === 'reparent' ? 'ReparentToParent' : 'Cascade';
      await api.delete(`/${tenant}/api/topic/${topicId}?strategy=${strategy}`);

      const topicsById = new Map($topicList.map((t) => [t.id, t]));
      const parentId = topic.parentId;

      if (deleteMode === 'cascade') {
        const descendants = collectDescendants(topicId, topicsById);
        for (const id of descendants) deleteTopic(id);
        deleteTopic(topicId);

        if (parentId) {
          const parent = topicsById.get(parentId);
          if (parent) {
            const nextChildIds = (parent.childIds ?? []).filter((id) => id !== topicId);
            updateTopic(parent.id, {
              childIds: nextChildIds,
              hasChildren: nextChildIds.length > 0,
            });
          }
        }
      } else {
        const loadedChildIds = topic.childIds ?? [];

        // Re-parent loaded children in the client store
        for (const childId of loadedChildIds) {
          updateTopic(childId, { parentId: parentId ?? null });
        }

        // Update parent linkage
        if (parentId) {
          const parent = topicsById.get(parentId);
          if (parent) {
            const kept = (parent.childIds ?? []).filter((id) => id !== topicId);
            const merged = Array.from(new Set([...kept, ...loadedChildIds]));
            updateTopic(parent.id, {
              childIds: merged,
              hasChildren: merged.length > 0,
            });
          }
        }

        deleteTopic(topicId);

        // If children existed but weren't loaded, fetch and merge to keep UI consistent
        if (topic.hasChildren && loadedChildIds.length === 0) {
          if (parentId) {
            const childrenResponse = await api.get<any[]>(`/${tenant}/api/topic/parent/${parentId}`);
            const normalized = Array.isArray(childrenResponse)
              ? childrenResponse.map((raw) => ({
                  id: raw?.id ?? raw?.Id ?? '',
                  roomId: raw?.roomId ?? raw?.RoomId ?? '',
                  title: raw?.title ?? raw?.Title ?? '',
                  description: raw?.description ?? raw?.Description,
                  parentId: raw?.parentId ?? raw?.ParentId ?? null,
                  childIds: raw?.childIds ?? raw?.ChildIds ?? [],
                  createdAt: raw?.createdAt ?? raw?.CreatedAt ? new Date(raw?.createdAt ?? raw?.CreatedAt) : new Date(),
                  updatedAt: raw?.updatedAt ?? raw?.UpdatedAt ? new Date(raw?.updatedAt ?? raw?.UpdatedAt) : new Date(),
                  creatorId: raw?.creatorId ?? raw?.CreatorId ?? '',
                  messageCount: raw?.messageCount ?? raw?.MessageCount ?? 0,
                  unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
                  userPermission: raw?.userPermission ?? raw?.UserPermission ?? 'read',
                  permissions: raw?.permissions ?? raw?.Permissions ?? [],
                  isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
                  tags: raw?.tags ?? raw?.Tags ?? [],
                  hasChildren: raw?.hasChildren ?? raw?.HasChildren ?? false,
                }))
              : [];

            normalized.forEach((t) => {
              if (!$topicList.find((x) => x.id === t.id)) addTopic(t);
            });
          } else {
            const rootsResponse = await api.get<any[]>(`/${tenant}/api/topic/room/${topic.roomId}/root`);
            const normalized = Array.isArray(rootsResponse)
              ? rootsResponse.map((raw) => ({
                  id: raw?.id ?? raw?.Id ?? '',
                  roomId: raw?.roomId ?? raw?.RoomId ?? '',
                  title: raw?.title ?? raw?.Title ?? '',
                  description: raw?.description ?? raw?.Description,
                  parentId: raw?.parentId ?? raw?.ParentId ?? null,
                  childIds: raw?.childIds ?? raw?.ChildIds ?? [],
                  createdAt: raw?.createdAt ?? raw?.CreatedAt ? new Date(raw?.createdAt ?? raw?.CreatedAt) : new Date(),
                  updatedAt: raw?.updatedAt ?? raw?.UpdatedAt ? new Date(raw?.updatedAt ?? raw?.UpdatedAt) : new Date(),
                  creatorId: raw?.creatorId ?? raw?.CreatorId ?? '',
                  messageCount: raw?.messageCount ?? raw?.MessageCount ?? 0,
                  unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
                  userPermission: raw?.userPermission ?? raw?.UserPermission ?? 'read',
                  permissions: raw?.permissions ?? raw?.Permissions ?? [],
                  isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
                  tags: raw?.tags ?? raw?.Tags ?? [],
                  hasChildren: raw?.hasChildren ?? raw?.HasChildren ?? false,
                }))
              : [];

            normalized.forEach((t) => {
              if (!$topicList.find((x) => x.id === t.id)) addTopic(t);
            });
          }
        }
      }

      ui.closeModal(modalId);
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to delete topic';
    } finally {
      isLoading = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Delete Topic" onClose={handleClose} size="small">
  <div class="spacing-md">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <p class="text-small">
      {#if topic}
        Delete topic "{topic.title}"?
      {:else}
        Delete this topic?
      {/if}
    </p>

    {#if topic && topic.hasChildren}
      <div class="form-group">
        <div class="form-label">When this topic has children:</div>
        <div class="spacing-sm">
          <label class="flex items-center spacing-sm clickable" style="gap: 8px;">
            <input
              type="radio"
              name="deleteMode"
              value="cascade"
              checked={deleteMode === 'cascade'}
              onchange={() => (deleteMode = 'cascade')}
              disabled={isLoading}
            />
            <span class="text-small">Delete this topic and all child topics</span>
          </label>
          <label class="flex items-center spacing-sm clickable" style="gap: 8px;">
            <input
              type="radio"
              name="deleteMode"
              value="reparent"
              checked={deleteMode === 'reparent'}
              onchange={() => (deleteMode = 'reparent')}
              disabled={isLoading}
            />
            <span class="text-small">Keep child topics and attach them to the parent</span>
          </label>
        </div>
      </div>
    {/if}

    <div class="flex spacing-md padding-top-md">
      <Button
        type="button"
        variant="danger"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading}
        onclick={handleDelete}
      >
        Delete
      </Button>
      <Button
        type="button"
        variant="secondary"
        size="base"
        fullWidth
        disabled={isLoading}
        onclick={handleClose}
      >
        Cancel
      </Button>
    </div>
  </div>
</Modal>
