<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { messageList, deleteMessage } from '$lib/stores/messages';
  import { api } from '$lib/api/client';

  const modalId = 'message-delete';

  let modalConfig = $derived.by(
    () => $activeModals.find((m) => m.id === modalId) ?? null
  );
  let isOpen = $derived.by(() => modalConfig !== null);
  let messageId = $derived.by(() => modalConfig?.data?.messageId as string | undefined);

  let message = $derived.by(() => {
    if (!messageId) return null;
    return $messageList.find((m) => m.id === messageId) ?? null;
  });

  let isLoading = $state(false);
  let error = $state<string | null>(null);

  async function handleDelete() {
    error = null;

    if (!messageId) {
      error = 'Message not found';
      return;
    }

    isLoading = true;
    try {
      const tenant = api.getCurrentTenant();
      await api.delete(`/${tenant}/api/Message/${messageId}`);
      deleteMessage(messageId);
      ui.closeModal(modalId);
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to delete message';
    } finally {
      isLoading = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Delete Message" onClose={handleClose} size="small">
  <div class="spacing-md">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <p class="text-small">
      {#if message}
        Delete this message?
      {:else}
        Delete this message?
      {/if}
    </p>

    <div class="flex spacing-md padding-top-md">
      <Button
        type="button"
        variant="danger"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading}
        on:click={handleDelete}
      >
        Delete
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
  </div>
</Modal>

