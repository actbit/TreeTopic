<script lang="ts">
  import { activeModals, ui } from '$lib/stores/ui';
  import UserSettingModal from '../user/UserSettingModal.svelte';
  import RoomRolePermissionModal from './RoomRolePermissionModal.svelte';
  import TopicUserPermissionModal from './TopicUserPermissionModal.svelte';
  import RoomUserPermissionModal from './RoomUserPermissionModal.svelte';
  import RoomJoinPermissionModal from './RoomJoinPermissionModal.svelte';
  import TenantRolePermissionModal from './TenantRolePermissionModal.svelte';
  import TenantCreateModal from './TenantCreateModal.svelte';
  import ForbiddenAccessModal from './ForbiddenAccessModal.svelte';
  import AddRoomMemberModal from './AddRoomMemberModal.svelte';
  import TopicSettingsModal from '../topics/TopicSettingsModal.svelte';

  let activeModalsList = $derived.by(() => $activeModals);
</script>

{#each activeModalsList as modal (modal.id)}
  <div class="modal-overlay" class:active={activeModalsList.length > 0}>
    {#if modal.id === 'user-setting' && modal.data}
      <UserSettingModal
        {...modal.data}
        roomId={(modal.data as { roomId?: string }).roomId ?? ''}
        onclose={() => ui.closeModal(modal.id)}
      />
    {/if}

    {#if modal.id === 'room-role-permission'}
      <RoomRolePermissionModal />
    {/if}

    {#if modal.id === 'topic-user-permission'}
      <TopicUserPermissionModal />
    {/if}

    {#if modal.id === 'room-user-permission'}
      <RoomUserPermissionModal />
    {/if}

    {#if modal.id === 'room-join-permission'}
      <RoomJoinPermissionModal />
    {/if}

    {#if modal.id === 'tenant-role-permission'}
      <TenantRolePermissionModal />
    {/if}

    {#if modal.id === 'tenant-create'}
      <TenantCreateModal />
    {/if}

    {#if modal.id === 'forbidden-access'}
      <ForbiddenAccessModal />
    {/if}

    {#if modal.id === 'add-room-member'}
      <AddRoomMemberModal />
    {/if}

    {#if modal.id === 'topic-settings'}
      <TopicSettingsModal />
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
