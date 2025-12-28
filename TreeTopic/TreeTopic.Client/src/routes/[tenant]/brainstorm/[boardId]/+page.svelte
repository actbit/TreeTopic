<script lang="ts">
  import { brainstorm } from '$lib/stores/brainstorm';
  import { onMount } from 'svelte';
  import BrainstormBoard from '$lib/components/brainstorming/BrainstormBoard.svelte';
  import LoadingSpinner from '$lib/components/common/LoadingSpinner.svelte';
  import Button from '$lib/components/common/Button.svelte';
  import { api } from '$lib/api/client';

  interface PageData {
    boardId: string;
  }

  let data: PageData = $props();
  let isLoading = $state(true);
  let error = $state<string | null>(null);

  onMount(() => {
    loadBoard();
  });

  async function loadBoard() {
    try {
      isLoading = true;
      error = null;

      const boardData = await api.get(`/api/brainstorm/${data.boardId}`);
      brainstorm.setCurrentBoard(boardData);
    } catch (err: any) {
      error = err.message || 'Failed to load brainstorm board';
    } finally {
      isLoading = false;
    }
  }

  function goBack() {
    window.history.back();
  }
</script>

<svelte:head>
  <title>Brainstorm Board - TreeTopic</title>
</svelte:head>

<div class="flex flex-col h-screen bg-white">
  <!-- Header -->
  <div class="border-b border-border p-4 flex items-center justify-between bg-white shadow-sm">
    <div class="flex items-center gap-4">
      <button
        on:click={goBack}
        class="p-2 text-text-light hover:text-primary rounded hover:bg-surface transition-colors"
        title="Go back"
      >
        ← Back
      </button>
      <div>
        <h1 class="text-2xl font-bold text-text">💡 Brainstorm Board</h1>
        <p class="text-sm text-text-light">Collaborative idea development</p>
      </div>
    </div>

    <div class="flex items-center gap-2">
      <Button variant="secondary" size="small" on:click={loadBoard}>
        🔄 Refresh
      </Button>
      <Button variant="secondary" size="small" on:click={goBack}>
        ✕ Close
      </Button>
    </div>
  </div>

  <!-- Content -->
  <div class="flex-1 overflow-hidden">
    {#if isLoading}
      <div class="flex items-center justify-center h-full">
        <LoadingSpinner message="Loading brainstorm board..." />
      </div>
    {:else if error}
      <div class="flex flex-col items-center justify-center h-full gap-4">
        <div class="text-center">
          <p class="text-xl font-semibold text-text mb-2">⚠️ Error</p>
          <p class="text-text-light mb-4">{error}</p>
        </div>
        <div class="flex gap-3">
          <Button variant="primary" on:click={loadBoard}>Retry</Button>
          <Button variant="secondary" on:click={goBack}>Go Back</Button>
        </div>
      </div>
    {:else if $brainstorm.currentBoard}
      <BrainstormBoard boardId={data.boardId} />
    {:else}
      <div class="flex flex-col items-center justify-center h-full gap-4">
        <p class="text-text-light">Board not found</p>
        <Button variant="secondary" on:click={goBack}>Go Back</Button>
      </div>
    {/if}
  </div>
</div>

<style>
  :global(body) {
    overflow: hidden;
  }
</style>
