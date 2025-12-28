<script lang="ts">
  import { isAuthenticated } from '$lib/stores/auth';
  import { currentRoom, roomList } from '$lib/stores/rooms';
  import { selectedTopic } from '$lib/stores/topics';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import RoomSelector from '$lib/components/rooms/RoomSelector.svelte';
  import RoomCreateModal from '$lib/components/rooms/RoomCreateModal.svelte';
  import RoomSettingsModal from '$lib/components/rooms/RoomSettingsModal.svelte';
  import TopicTree from '$lib/components/topics/TopicTree.svelte';
  import TopicCreateModal from '$lib/components/topics/TopicCreateModal.svelte';
  import MessageList from '$lib/components/messages/MessageList.svelte';
  import MessageInput from '$lib/components/messages/MessageInput.svelte';
  import MessagesView from '$lib/components/messages/MessagesView.svelte';
  import ViewModeSelector from '$lib/components/messages/ViewModeSelector.svelte';
  import MaterialList from '$lib/components/files/MaterialList.svelte';
  import FileUploadModal from '$lib/components/files/FileUploadModal.svelte';
  import { ui } from '$lib/stores/ui';
</script>

<svelte:head>
  <title>TreeTopic - Collaborative Discussion</title>
</svelte:head>

{#if $isAuthenticated}
  <AppLayout subPanelTitle="Materials">
    <svelte:fragment slot="headerContent">
      <RoomSelector />
    </svelte:fragment>

    <svelte:fragment slot="sidebarContent">
      {#if $currentRoom}
        <TopicTree />
      {:else}
        <div class="p-4 text-center text-text-light">
          <p class="text-sm">Select a room to view topics</p>
        </div>
      {/if}
    </svelte:fragment>

    <svelte:fragment slot="mainContent">
      {#if $currentRoom && $selectedTopic}
        <div class="flex flex-col h-full">
          <div class="border-b border-border p-4 space-y-3">
            <div>
              <h2 class="text-lg font-semibold text-text">{$selectedTopic.title}</h2>
              {#if $selectedTopic.description}
                <p class="text-sm text-text-light mt-1">{$selectedTopic.description}</p>
              {/if}
            </div>
            <div class="pt-2 border-t border-border">
              <ViewModeSelector />
            </div>
          </div>
          <MessagesView />
          <MessageInput />
        </div>
      {:else if $currentRoom}
        <div class="flex items-center justify-center h-full text-center">
          <div>
            <h2 class="text-2xl font-bold text-text mb-2">{$currentRoom.name}</h2>
            <p class="text-text-secondary">Select a topic to view messages</p>
          </div>
        </div>
      {:else}
        <div class="flex items-center justify-center h-full text-center">
          <div>
            <h2 class="text-2xl font-bold text-text mb-2">Welcome to TreeTopic</h2>
            <p class="text-text-secondary">Select a room to get started</p>
          </div>
        </div>
      {/if}
    </svelte:fragment>

    <svelte:fragment slot="subPanelContent">
      <MaterialList />
    </svelte:fragment>
  </AppLayout>

  <RoomCreateModal />
  <RoomSettingsModal />
  <TopicCreateModal />
  <FileUploadModal />
{/if}
