<script lang="ts">
  import { activeModals, ui } from '$lib/stores/ui';
  import UserSettingModal from '../user/UserSettingModal.svelte';

  let activeModalsList = $derived.by(() => $activeModals);
</script>

{#each activeModalsList as modal (modal.id)}
  <div class="modal-overlay" class:active={activeModalsList.length > 0}>
    {#if modal.id === 'user-setting' && modal.data}
      <UserSettingModal
        {...modal.data}
        roomId={modal.data.roomId}
        onclose={() => ui.closeModal(modal.id)}
      />
    {/if}
  </div>
{/each}

<style>
  .modal-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
    opacity: 0;
    visibility: hidden;
    transition: opacity 0.3s ease, visibility 0.3s ease;
  }

  .modal-overlay.active {
    opacity: 1;
    visibility: visible;
  }
</style>
