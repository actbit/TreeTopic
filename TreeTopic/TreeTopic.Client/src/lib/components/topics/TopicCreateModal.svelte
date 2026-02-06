<script lang="ts">
  import { goto } from '$app/navigation';
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import {
    topicList,
    addTopic,
    updateTopic,
    setSelectedTopic,
    createTopicParentId,
    expandedTopics,
    toggleTopicExpansion,
  } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import { isRequired, minLength } from '$lib/utils/validation';
  import { api } from '$lib/api/client';

  const modalId = 'topic-create';
  let modalConfig = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modalConfig !== null);
  let modalData = $derived.by(() => modalConfig?.data ?? {} as Record<string, unknown>);
  let parentId = $derived.by(() => (modalData.parentId ?? null) as string | null);
  let prefillTitle = $derived.by(() => (modalData.prefillTitle ?? '') as string);
  let prefillDescription = $derived.by(() => (modalData.prefillDescription ?? '') as string);
  let navigateOnCreate = $derived.by(() => (modalData.autoNavigate ?? false) as boolean);
  let sourceMessageId = $derived.by(() => (modalData.sourceMessageId ?? null) as string | null);
  let transferHistory = $state(false);

  let title = $state('');
  let description = $state('');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let titleError = $state<string | undefined>(undefined);
  let hasInitializedModal = $state(false);

  $effect(() => {
    if (isOpen) {
      if (!hasInitializedModal) {
        title = prefillTitle ?? '';
        description = prefillDescription ?? '';
        error = null;
        titleError = undefined;
        transferHistory = (modalData.transferHistory ?? false) as boolean;
        hasInitializedModal = true;
      }
    } else {
      hasInitializedModal = false;
    }
  });

  async function handleCreate(e: Event) {
    e.preventDefault();

    const activeParentId = parentId ?? null;

    titleError = undefined;
    error = null;

    if (!isRequired(title)) {
      titleError = 'Topic title is required';
      return;
    }

    if (!minLength(title, 2)) {
      titleError = 'Topic title must be at least 2 characters';
      return;
    }

    if (!$currentRoom) {
      error = 'Please select a room first';
      return;
    }

    isLoading = true;

    try {
      const tenant = api.getCurrentTenant();
      const response = (await api.post(`/${tenant}/api/topic`, {
        roomId: $currentRoom.id,
        title: title.trim(),
        description: description.trim(),
        parentId: activeParentId,
        sourceMessageId,
      })) as Record<string, any>;

      // Normalize response to ensure all required fields exist
      const normalizedTopic = {
        id: response.id || response.Id || '',
        roomId: response.roomId || response.RoomId || $currentRoom.id,
        title: response.title || response.Title || '',
        description: response.description || response.Description || undefined,
        parentId: response.parentId || response.ParentId || null,
        sourceMessageId: response.sourceMessageId || response.SourceMessageId || null,
        childIds: response.childIds || response.ChildIds || [],
        createdAt:
          response.createdAt || response.CreatedAt
            ? new Date(response.createdAt || response.CreatedAt)
            : new Date(),
        updatedAt:
          response.updatedAt || response.UpdatedAt
            ? new Date(response.updatedAt || response.UpdatedAt)
            : new Date(),
        creatorId: response.creatorId || response.CreatorId || '',
        messageCount: response.messageCount || response.MessageCount || 0,
        unreadCount: response.unreadCount || response.UnreadCount || 0,
        userPermission: response.userPermission || response.UserPermission || 'admin',
        permissions: response.permissions || response.Permissions || [],
        isArchived: response.isArchived || response.IsArchived || false,
        tags: response.tags || response.Tags || [],
        hasChildren: response.hasChildren || response.HasChildren || false,
      };

      addTopic(normalizedTopic);

      if (activeParentId && !$expandedTopics.has(activeParentId)) {
        toggleTopicExpansion(activeParentId);
      }

      const shouldNavigate = navigateOnCreate;
      if (shouldNavigate) {
        setSelectedTopic(normalizedTopic);
      }

      const shouldTransferHistory =
        transferHistory && activeParentId && sourceMessageId;
      if (shouldTransferHistory) {
        try {
          await api.post(`/${tenant}/api/message/move`, {
            sourceTopicId: activeParentId,
            targetTopicId: normalizedTopic.id,
            anchorMessageId: sourceMessageId,
            includeAnchorMessage: false,
          });
        } catch (moveError) {
          console.error('Failed to transfer earlier messages to child topic:', moveError);
        }
      }

      resetForm();
      ui.closeModal(modalId);

      if (shouldNavigate && normalizedTopic.roomId && normalizedTopic.id) {
        try {
          await goto(`/${tenant}/room/${normalizedTopic.roomId}/topic/${normalizedTopic.id}`, {
            keepFocus: true,
            noScroll: true,
          });
        } catch (navigateError) {
          console.error('Failed to navigate to new topic:', navigateError);
        }
      }
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to create topic';
    } finally {
      isLoading = false;
    }
  }

  function resetForm() {
    title = '';
    description = '';
    createTopicParentId.set(null);
    transferHistory = false;
  }

  function handleClose() {
    ui.closeModal(modalId);
    resetForm();
  }
</script>

<Modal {isOpen} title="Create Topic" onClose={handleClose} size="medium">
  <form onsubmit={handleCreate} class="spacing-md">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <Input
      label="Topic Title"
      type="text"
      bind:value={title}
      placeholder="Enter topic title"
      error={titleError}
      disabled={isLoading}
      required
    />

    <div class="form-group">
      <label for="topic-description" class="form-label">Description</label>
      <textarea
        id="topic-description"
        bind:value={description}
        placeholder="Enter topic description (optional)"
        disabled={isLoading}
        class="form-input"
        style="resize: vertical; min-height: 80px;"
      ></textarea>
    </div>

    {#if sourceMessageId}
      <div class="transfer-history-group">
        <label class="transfer-toggle">
          <input
            type="checkbox"
            bind:checked={transferHistory}
            disabled={isLoading}
          />
          <span>Move earlier messages into this child topic</span>
        </label>
        <p class="text-small text-light transfer-helper">
          Moves every message sent before the highlighted message so the new topic starts at that point.
        </p>
      </div>
    {/if}

    <div class="flex spacing-md padding-top-md">
      <Button
        type="submit"
        variant="primary"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading}
      >
        Create Topic
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
  </form>
</Modal>

<style>
  .transfer-history-group {
    margin-top: var(--spacing-md);
  }

  .transfer-toggle {
    display: flex;
    align-items: center;
    gap: var(--spacing-sm);
    font-size: var(--font-size-sm);
    color: var(--color-text);
  }

  .transfer-toggle input[type='checkbox'] {
    width: 16px;
    height: 16px;
  }

  .transfer-helper {
    margin-top: 4px;
    font-size: var(--font-size-xs);
    color: var(--color-text-light);
  }
</style>
