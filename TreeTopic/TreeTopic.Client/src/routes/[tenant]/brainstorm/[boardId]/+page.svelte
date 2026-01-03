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
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load brainstorm board';
      error = errorMessage;
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
  <div class="border-b border-border p-6 flex items-center justify-between bg-white shadow-sm">
    <div class="flex items-center gap-5">
      <button
        on:click={goBack}
        class="px-4 py-2 text-text-light hover:text-primary rounded hover:bg-surface transition-colors font-medium"
        title="Go back"
      >
        Back
      </button>
      <div>
        <h1 class="text-2xl font-bold text-text">Brainstorm Board</h1>
        <p class="text-sm text-text-light mt-1">Collaborative idea development</p>
      </div>
    </div>

    <div class="flex items-center gap-3">
      <Button variant="secondary" size="small" on:click={loadBoard}>
        Refresh
      </Button>
      <Button variant="secondary" size="small" on:click={goBack}>
        Close
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
      <div class="flex flex-col items-center justify-center h-full gap-6">
        <div class="text-center">
          <p class="text-xl font-semibold text-text mb-3">Error</p>
          <p class="text-text-light mb-5">{error}</p>
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
