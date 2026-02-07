<script lang="ts">
  import MessageList from './MessageList.svelte';
  import UserView from './UserView.svelte';
  import DocumentView from './DocumentView.svelte';
  import ImageView from './ImageView.svelte';
  import SearchView from './SearchView.svelte';
  import TopicView from './TopicView.svelte';
  import { ui } from '$lib/stores/ui';

  let currentMode = $derived.by(() => {
    return $ui.viewMode || 'default';
  });
</script>

<div class="messages-view">
  {#if currentMode === 'default'}
    <MessageList />
  {:else if currentMode === 'user'}
    <UserView />
  {:else if currentMode === 'document'}
    <DocumentView />
  {:else if currentMode === 'image'}
    <ImageView />
  {:else if currentMode === 'search'}
    <SearchView />
  {:else if currentMode === 'topic'}
    <TopicView />
  {:else}
    <MessageList />
  {/if}
</div>

<style>
  .messages-view {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
  }
</style>
