<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import { activeModals, ui } from '$lib/stores/ui';

  const modalId = 'forbidden-access';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let message = $derived.by(
    () => (modal?.data?.message as string | undefined) ?? 'You do not have permission to perform this action.'
  );

  function closeModal() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Access Denied" onClose={closeModal} size="small">
  <div class="forbidden-body">
    <p>{message}</p>
    <div class="actions">
      <Button type="button" variant="primary" size="base" onclick={closeModal}>
        Close
      </Button>
    </div>
  </div>
</Modal>

<style>
  .forbidden-body {
    display: flex;
    flex-direction: column;
    gap: var(--spacing-md);
  }

  .actions {
    display: flex;
    justify-content: flex-end;
  }
</style>
