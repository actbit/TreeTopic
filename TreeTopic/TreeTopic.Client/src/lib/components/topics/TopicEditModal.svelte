<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { topicList, updateTopic } from '$lib/stores/topics';
  import { isRequired, minLength } from '$lib/utils/validation';
  import { api } from '$lib/api/client';

  const modalId = 'topic-edit';

  let modalConfig = $derived.by(
    () => $activeModals.find((m) => m.id === modalId) ?? null
  );
  let isOpen = $derived.by(() => modalConfig !== null);
  let topicId = $derived.by(() => modalConfig?.data?.topicId as string | undefined);

  let topic = $derived.by(() => {
    if (!topicId) return null;
    return $topicList.find((t) => t.id === topicId) ?? null;
  });

  let title = $state('');
  let description = $state('');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let titleError = $state<string | null>(null);

  $effect(() => {
    if (!isOpen) return;
    if (!topic) return;
    title = topic.title ?? '';
    description = topic.description ?? '';
    error = null;
    titleError = null;
  });

  async function handleSave(e: Event) {
    e.preventDefault();

    error = null;
    titleError = null;

    if (!topicId || !topic) {
      error = 'Topic not found';
      return;
    }

    if (!isRequired(title)) {
      titleError = 'Topic title is required';
      return;
    }

    if (!minLength(title, 2)) {
      titleError = 'Topic title must be at least 2 characters';
      return;
    }

    isLoading = true;

    try {
      const tenant = api.getCurrentTenant();
      const trimmedTitle = title.trim();
      const trimmedDescription = description.trim();

      await api.put(`/${tenant}/api/Topic/${topicId}`, {
        parentId: topic.parentId,
        title: trimmedTitle,
        description: trimmedDescription || null,
      });

      updateTopic(topicId, {
        title: trimmedTitle,
        description: trimmedDescription || undefined,
        updatedAt: new Date(),
      });

      ui.closeModal(modalId);
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to update topic';
    } finally {
      isLoading = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Edit Topic" onClose={handleClose} size="medium">
  <form on:submit={handleSave} class="spacing-md">
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
      <label class="form-label">Description</label>
      <textarea
        bind:value={description}
        placeholder="Enter topic description (optional)"
        disabled={isLoading}
        class="form-input"
        style="resize: vertical; min-height: 80px;"
      ></textarea>
    </div>

    <div class="flex spacing-md padding-top-md">
      <Button
        type="submit"
        variant="primary"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading}
      >
        Save
      </Button>
      <Button
        type="button"
        variant="secondary"
        size="base"
        fullWidth
        disabled={isLoading}
        on:click={handleClose}
      >
        Cancel
      </Button>
    </div>
  </form>
</Modal>

