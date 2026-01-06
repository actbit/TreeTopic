<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { currentRoom, updateRoom, deleteRoom as deleteRoomStore } from '$lib/stores/rooms';
  import { isRequired } from '$lib/utils/validation';
  import { api } from '$lib/api/client';

  const modalId = 'room-settings';
  let isOpen = $derived.by(() => $activeModals.some((m) => m.id === modalId));

  let name = $state($currentRoom?.name ?? '');
  let description = $state($currentRoom?.description ?? '');
  let isLoading = $state(false);
  let isDeleting = $state(false);
  let error = $state<string | null>(null);
  let nameError = $state<string | null>(null);

  $effect(() => {
    if ($currentRoom) {
      name = $currentRoom.name;
      description = $currentRoom.description ?? '';
    }
  });

  async function handleSave(e: Event) {
    e.preventDefault();

    nameError = null;
    error = null;

    if (!isRequired(name)) {
      nameError = 'Room name is required';
      return;
    }

    isLoading = true;

    try {
      if ($currentRoom) {
        const tenant = api.getCurrentTenant();
        await api.put(`/${tenant}/api/Room/${$currentRoom.id}`, {
          name: name.trim(),
          description: description.trim(),
        });

        updateRoom($currentRoom.id, {
          name,
          description,
        });
      }

      ui.closeModal(modalId);
    } catch (err: any) {
      error = err.message || 'Failed to update room';
    } finally {
      isLoading = false;
    }
  }

  async function handleDelete() {
    if (!confirm('Are you sure you want to delete this room? This action cannot be undone.')) {
      return;
    }

    isDeleting = true;
    error = null;

    try {
      if ($currentRoom) {
        const tenant = api.getCurrentTenant();
        await api.del(`/${tenant}/api/Room/${$currentRoom.id}`);
        deleteRoomStore($currentRoom.id);
        ui.closeModal(modalId);
      }
    } catch (err: any) {
      error = err.message || 'Failed to delete room';
    } finally {
      isDeleting = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Room Settings" onClose={handleClose} size="medium">
  <form on:submit={handleSave} class="space-y-6">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <Input
      label="Room Name"
      type="text"
      bind:value={name}
      placeholder="Enter room name"
      error={nameError}
      disabled={isLoading || isDeleting}
      required
    />

    <div class="flex flex-col gap-1">
      <label class="text-sm font-semibold text-text">Description</label>
      <textarea
        bind:value={description}
        placeholder="Enter room description (optional)"
        disabled={isLoading || isDeleting}
      />
    </div>

    {#if $currentRoom?.memberCount}
      <div class="text-sm text-text-light bg-surface rounded-lg p-3">
        <span class="font-semibold text-text">{$currentRoom.memberCount}</span> member{$currentRoom
          .memberCount !== 1
          ? 's'
          : ''} in this room
      </div>
    {/if}

    <div class="flex gap-4 pt-8">
      <Button
        type="submit"
        variant="primary"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading || isDeleting}
      >
        {#if isLoading}
          Saving...
        {:else}
          Save Changes
        {/if}
      </Button>
      <Button
        type="button"
        variant="secondary"
        size="base"
        fullWidth
        disabled={isLoading || isDeleting}
        on:click={handleClose}
      >
        Cancel
      </Button>
    </div>

    {#if $currentRoom?.canDelete}
      <div class="border-t border-border pt-8">
        <Button
          type="button"
          variant="danger"
          size="base"
          fullWidth
          loading={isDeleting}
          disabled={isLoading || isDeleting}
          on:click={handleDelete}
        >
          {#if isDeleting}
            Deleting...
          {:else}
            Delete Room
          {/if}
        </Button>
      </div>
    {/if}
  </form>
</Modal>

<style>
  textarea {
    font-family: var(--font-family-base);
    resize: vertical;
    min-height: 80px;
  }
</style>
