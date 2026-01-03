<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { rooms, addRoom } from '$lib/stores/rooms';
  import { isRequired, minLength } from '$lib/utils/validation';
  import { api } from '$lib/api/client';

  const modalId = 'room-create';
  let isOpen = $derived($activeModals.some((m) => m.id === modalId));

  let name = $state('');
  let description = $state('');
  let isPublic = $state(false);
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let nameError = $state<string | null>(null);

  async function handleCreate(e: Event) {
    e.preventDefault();

    // Validation
    nameError = null;
    error = null;

    if (!isRequired(name)) {
      nameError = 'Room name is required';
      return;
    }

    if (!minLength(name, 3)) {
      nameError = 'Room name must be at least 3 characters';
      return;
    }

    isLoading = true;

    try {
      const tenant = api.getCurrentTenant();
      const response = await api.post(`/${tenant}/api/Room`, {
        name: name.trim(),
        description: description.trim(),
        settings: {
          isPublic,
        },
      });

      addRoom(response);
      resetForm();
      ui.closeModal(modalId);
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to create room';
    } finally {
      isLoading = false;
    }
  }

  function resetForm() {
    name = '';
    description = '';
    isPublic = false;
  }

  function handleClose() {
    ui.closeModal(modalId);
    resetForm();
  }
</script>

<Modal {isOpen} title="Create Room" onClose={handleClose} size="medium">
  <form on:submit={handleCreate} class="spacing-md">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <Input
      label="Room Name"
      type="text"
      bind:value={name}
      placeholder="Enter room name"
      error={nameError}
      disabled={isLoading}
      required
    />

    <div class="form-group">
      <label class="form-label">Description</label>
      <textarea
        bind:value={description}
        placeholder="Enter room description (optional)"
        disabled={isLoading}
        class="form-input"
        style="resize: vertical; min-height: 80px;"
      />
    </div>

    <div class="flex items-center form-checkbox-group">
      <input
        type="checkbox"
        id="isPublic"
        bind:checked={isPublic}
        disabled={isLoading}
        class="form-checkbox cursor-pointer"
      />
      <label for="isPublic" class="form-label cursor-pointer">
        Make this room public
      </label>
    </div>

    <div class="flex form-actions">
      <Button
        type="submit"
        variant="primary"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading}
      >
        Create Room
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

<style>
  .form-checkbox-group {
    gap: var(--spacing-sm);
  }

  .form-checkbox {
    width: 16px;
    height: 16px;
  }

  .form-actions {
    gap: var(--spacing-sm);
    padding-top: var(--spacing-md);
  }
</style>
