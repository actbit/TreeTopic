<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { currentUser } from '$lib/stores/auth';
  import { rooms } from '$lib/stores/rooms';
  import { api } from '$lib/api/client';

  const modalId = 'room-user-join';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let roomId = $derived.by(() => modal?.data?.roomId ?? null);

  let name = $state('');
  let useMainName = $state(true);
  let useMainIcon = $state(true);
  let isLoading = $state(false);
  let error = $state<string | null>(null);

  $effect(() => {
    if (isOpen) {
      name = '';
      useMainName = true;
      useMainIcon = true;
      error = null;
    }
  });

  async function handleSubmit(e: Event) {
    e.preventDefault();
    if (!roomId) return;

    isLoading = true;
    error = null;

    try {
      const tenant = api.getCurrentTenant();
      const payload: Record<string, any> = {
        useMainName,
        useMainIcon,
      };

      const trimmed = name.trim();
      if (trimmed) {
        payload.name = trimmed;
      }

      const response = await api.post<any>(`/${tenant}/api/roomusers/room/${roomId}/join`, payload);

      // Update currentRoomUser with the response (DisplayName and IconUrl are already resolved by backend)
      if (response) {
        rooms.setCurrentRoomUser({
          id: response.id ?? response.Id ?? '',
          displayName: response.displayName ?? response.DisplayName ?? '',
          iconUrl: response.iconUrl ?? response.IconUrl,
          useMainIcon: response.useMainIcon ?? response.UseMainIcon ?? false,
        });

        // 参加完了イベントを発火
        window.dispatchEvent(new CustomEvent('room-user-joined', {
          detail: { roomId, roomUser: response }
        }));
      }

      ui.closeModal(modalId);
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to set room name';
    } finally {
      isLoading = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Set your name" onClose={handleClose} size="medium" closeButton={!isLoading}>
  <form onsubmit={handleSubmit} class="spacing-md">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <div class="text-sm text-text-light">
      Choose the name shown in this room. Special characters only will sync to your main name.
    </div>

    <Input
      label="Room name"
      type="text"
      bind:value={name}
      placeholder={$currentUser?.displayName ?? $currentUser?.userName ?? 'Your name'}
      disabled={isLoading || useMainName}
    />

    <div class="flex items-center form-checkbox-group">
      <input
        type="checkbox"
        id="useMainName"
        bind:checked={useMainName}
        disabled={isLoading}
        class="form-checkbox cursor-pointer"
      />
      <label for="useMainName" class="form-label cursor-pointer">
        Use main name ({$currentUser?.displayName ?? $currentUser?.userName ?? 'Unknown'})
      </label>
    </div>

    <div class="flex items-center form-checkbox-group">
      <input
        type="checkbox"
        id="useMainIcon"
        bind:checked={useMainIcon}
        disabled={isLoading}
        class="form-checkbox cursor-pointer"
      />
      <label for="useMainIcon" class="form-label cursor-pointer">
        Use main icon
      </label>
    </div>

    <div class="flex form-actions">
      <Button
        type="submit"
        variant="primary"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading || !roomId}
      >
        Save
      </Button>
      <Button
        type="button"
        variant="secondary"
        size="base"
        fullWidth
        disabled={isLoading}
        onclick={handleClose}
      >
        Later
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
