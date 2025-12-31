<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { topics, addTopic, selectedTopic } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import { isRequired, minLength } from '$lib/utils/validation';
  import { api } from '$lib/api/client';

  const modalId = 'topic-create';
  let isOpen = $derived($activeModals.some((m) => m.id === modalId));

  let title = $state('');
  let description = $state('');
  let parentId = $state<string | null>(null);
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let titleError = $state<string | null>(null);

  async function handleCreate(e: Event) {
    e.preventDefault();

    titleError = null;
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
      const response = await api.post(`/${tenant}/api/Topic`, {
        roomId: $currentRoom.id,
        title: title.trim(),
        description: description.trim(),
        parentId: parentId || null,
      });

      addTopic(response);
      resetForm();
      ui.closeModal(modalId);
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to create topic';
    } finally {
      isLoading = false;
    }
  }

  function resetForm() {
    title = '';
    description = '';
    parentId = null;
  }

  function handleClose() {
    ui.closeModal(modalId);
    resetForm();
  }
</script>

<Modal {isOpen} title="Create Topic" onClose={handleClose} size="medium">
  <form on:submit={handleCreate} class="spacing-md">
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
      />
    </div>

    <div class="form-group">
      <label for="parentId" class="form-label">Parent Topic (Optional)</label>
      <select
        id="parentId"
        bind:value={parentId}
        disabled={isLoading}
        class="form-input"
      >
        <option value={null}>None (Root level)</option>
        {#each $topics as topic (topic.id)}
          {#if !topic.parentId}
            <option value={topic.id}>{topic.title}</option>
          {/if}
        {/each}
      </select>
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
        Create Topic
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

