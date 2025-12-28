<script lang="ts">
  import IdeaCard from './IdeaCard.svelte';
  import { brainstormBoard, ideas, addIdea, deleteIdea, updateIdeaPosition } from '$lib/stores/brainstorm';
  import { api } from '$lib/api/client';

  interface Props {
    boardId: string;
  }

  let { boardId }: Props = $props();

  let boardContainer: HTMLDivElement | undefined = $state();
  let newIdeaText = $state('');
  let isAnonymous = $state(false);
  let draggedIdea: string | null = $state(null);
  let offsetX = $state(0);
  let offsetY = $state(0);

  let boardIdeas = $derived.by(() => {
    return $ideas.filter((i) => i.boardId === boardId);
  });

  function handleAddIdea() {
    if (!newIdeaText.trim()) return;

    const newIdea: any = {
      id: `idea_${Date.now()}`,
      boardId,
      text: newIdeaText.trim(),
      x: Math.random() * 300 + 100,
      y: Math.random() * 200 + 100,
      isAnonymous,
      marks: [],
      userName: isAnonymous ? undefined : 'Current User',
      userDisplayName: isAnonymous ? 'Anonymous' : 'Current User',
    };

    addIdea(newIdea);

    // Save to server
    api.post(`/api/brainstorm/${boardId}/ideas`, {
      text: newIdea.text,
      x: newIdea.x,
      y: newIdea.y,
      isAnonymous,
    });

    newIdeaText = '';
    isAnonymous = false;
  }

  function handleDragStart(ideaId: string, e: DragEvent) {
    if (!e.dataTransfer) return;

    draggedIdea = ideaId;
    e.dataTransfer.effectAllowed = 'move';

    const ideaEl = (e.target as HTMLElement).closest('[data-idea-id]');
    if (ideaEl) {
      const rect = ideaEl.getBoundingClientRect();
      offsetX = (e.clientX || 0) - rect.left;
      offsetY = (e.clientY || 0) - rect.top;
    }
  }

  function handleDragOver(e: DragEvent) {
    if (!e.dataTransfer) return;
    e.dataTransfer.dropEffect = 'move';
    e.preventDefault();
  }

  function handleDrop(e: DragEvent) {
    if (!draggedIdea || !boardContainer) return;
    e.preventDefault();

    const rect = boardContainer.getBoundingClientRect();
    const x = (e.clientX || 0) - rect.left - offsetX;
    const y = (e.clientY || 0) - rect.top - offsetY;

    // Constrain to board
    const constrainedX = Math.max(0, Math.min(x, rect.width - 200));
    const constrainedY = Math.max(0, Math.min(y, rect.height - 100));

    updateIdeaPosition(draggedIdea, constrainedX, constrainedY);

    // Save to server
    api.patch(`/api/brainstorm/${boardId}/ideas/${draggedIdea}`, {
      x: constrainedX,
      y: constrainedY,
    });

    draggedIdea = null;
  }

  function handleDeleteIdea(ideaId: string) {
    deleteIdea(ideaId);

    // Delete from server
    api.delete(`/api/brainstorm/${boardId}/ideas/${ideaId}`);
  }
</script>

<div class="flex h-full bg-white gap-4 p-4">
  <!-- Input panel -->
  <div class="w-64 border-r border-border pr-4 overflow-y-auto">
    <div class="space-y-4">
      <h2 class="font-bold text-lg text-text">Add Ideas</h2>

      <div class="space-y-2">
        <label class="block text-sm font-semibold text-text">Your Idea</label>
        <textarea
          bind:value={newIdeaText}
          placeholder="Type your idea here..."
          class="w-full p-3 border border-border rounded-lg text-sm bg-white focus:outline-none focus:border-primary resize-none"
          rows="4"
        />
      </div>

      <label class="flex items-center gap-3 cursor-pointer">
        <input
          type="checkbox"
          bind:checked={isAnonymous}
          class="w-4 h-4 accent-primary"
        />
        <span class="text-sm text-text">Post anonymously</span>
      </label>

      <button
        on:click={handleAddIdea}
        disabled={!newIdeaText.trim()}
        class="w-full px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary-hover transition-colors disabled:opacity-60 disabled:cursor-not-allowed font-semibold"
      >
        Add Idea
      </button>

      <div class="pt-4 border-t border-border">
        <p class="text-xs text-text-light mb-3">Voting Marks:</p>
        <div class="space-y-1 text-xs text-text-light">
          <p>● Circle - Agree</p>
          <p>■ Square - Consider</p>
          <p>▲ Triangle - Priority</p>
          <p>× Cross - Disagree</p>
        </div>
      </div>

      <div class="pt-4 border-t border-border">
        <p class="text-sm font-semibold text-text mb-2">Stats</p>
        <div class="text-xs text-text-light space-y-1">
          <p>Total Ideas: {boardIdeas.length}</p>
          <p>Your Ideas: {boardIdeas.filter((i) => !i.isAnonymous).length}</p>
        </div>
      </div>
    </div>
  </div>

  <!-- Canvas -->
  <div
    bind:this={boardContainer}
    on:dragover={handleDragOver}
    on:drop={handleDrop}
    class="flex-1 border-2 border-dashed border-border rounded-lg bg-surface overflow-hidden relative"
  >
    {#if boardIdeas.length === 0}
      <div class="absolute inset-0 flex items-center justify-center text-text-light">
        <div class="text-center">
          <p class="text-lg font-semibold mb-2">💡 Brainstorm Board</p>
          <p class="text-sm">Drag ideas around the board</p>
        </div>
      </div>
    {:else}
      <div class="relative w-full h-full">
        {#each boardIdeas as idea (idea.id)}
          <div
            data-idea-id={idea.id}
            class="absolute w-48 transition-shadow {draggedIdea === idea.id ? 'z-50' : 'z-10'}"
            style="left: {idea.x}px; top: {idea.y}px;"
            on:dragstart={(e) => handleDragStart(idea.id, e)}
            role="none"
          >
            <IdeaCard
              {idea}
              isDragging={draggedIdea === idea.id}
              onDelete={() => handleDeleteIdea(idea.id)}
            />
          </div>
        {/each}
      </div>
    {/if}
  </div>
</div>

<style>
  div[data-idea-id] {
    touch-action: none;
  }
</style>
