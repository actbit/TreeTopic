<script lang="ts">
  import IdeaCard from './IdeaCard.svelte';
  import { brainstormBoard, ideas, addIdea, deleteIdea, updateIdeaPosition } from '$lib/stores/brainstorm';
  import { api } from '$lib/api/client';
  import type { IdeaCardView } from '$lib/types/ui';

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

    const newIdea: Omit<IdeaCardView, 'canEdit' | 'canDelete'> = {
      id: `idea_${Date.now()}`,
      boardId,
      text: newIdeaText.trim(),
      x: Math.random() * 300 + 100,
      y: Math.random() * 200 + 100,
      isAnonymous,
      votes: { circle: 0, square: 0, triangle: 0, cross: 0 },
      userVotes: { circle: false, square: false, triangle: false, cross: false },
      userName: isAnonymous ? undefined : 'Current User',
      isEditing: false,
      createdAt: new Date(),
    };

    addIdea(newIdea);

    // Save to server
    const tenant = api.getCurrentTenant();
    api.post(`/${tenant}/api/brainstorm/${boardId}/ideas`, {
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

    const constrainedX = Math.max(0, Math.min(x, rect.width - 200));
    const constrainedY = Math.max(0, Math.min(y, rect.height - 100));

    updateIdeaPosition(draggedIdea, constrainedX, constrainedY);

    const tenant = api.getCurrentTenant();
    api.patch(`/${tenant}/api/brainstorm/${boardId}/ideas/${draggedIdea}`, {
      x: constrainedX,
      y: constrainedY,
    });

    draggedIdea = null;
  }

  function handleDeleteIdea(ideaId: string) {
    deleteIdea(ideaId);

    const tenant = api.getCurrentTenant();
    api.delete(`/${tenant}/api/brainstorm/${boardId}/ideas/${ideaId}`);
  }
</script>

<div class="flex h-full bg-white gap-6 p-6">
  <aside class="w-72 border-r border-border pr-6 overflow-y-auto" aria-label="Ideas panel">
    <div class="space-y-6">
      <h2 class="font-bold text-xl text-text">Add Ideas</h2>

      <div class="space-y-3">
        <label class="block text-sm font-semibold text-text">Your Idea</label>
        <textarea
          bind:value={newIdeaText}
          placeholder="Type your idea here..."
          class="w-full p-4 border border-border rounded-lg text-sm bg-white focus:outline-none focus:border-primary focus:ring-2 focus:ring-primary focus:ring-opacity-10 resize-none transition-all hover:border-border-hover"
          rows="5"
        />
      </div>

      <label class="flex items-center gap-3 cursor-pointer py-2">
        <input
          type="checkbox"
          bind:checked={isAnonymous}
          class="w-5 h-5 accent-primary"
        />
        <span class="text-sm text-text">Post anonymously</span>
      </label>

      <button
        on:click={handleAddIdea}
        disabled={!newIdeaText.trim()}
        class="w-full px-5 py-4 bg-primary text-white rounded-lg hover:bg-primary-hover transition-all hover:shadow-md disabled:opacity-60 disabled:cursor-not-allowed font-semibold transform hover:scale-105 active:scale-95"
      >
        Add Idea
      </button>

      <div class="pt-6 border-t border-border">
        <p class="text-sm text-text-light mb-4 font-semibold">Voting Marks:</p>
        <div class="space-y-2 text-sm text-text-light">
          <p>Circle - Agree</p>
          <p>Square - Consider</p>
          <p>Triangle - Priority</p>
          <p>Cross - Disagree</p>
        </div>
      </div>

      <div class="pt-6 border-t border-border">
        <p class="text-sm font-semibold text-text mb-4">Stats</p>
        <div class="space-y-3">
          <div class="flex items-center justify-between bg-surface rounded-lg p-3">
            <span class="text-sm text-text-light">Total Ideas:</span>
            <span class="text-base font-bold text-primary">{boardIdeas.length}</span>
          </div>
          <div class="flex items-center justify-between bg-surface rounded-lg p-3">
            <span class="text-sm text-text-light">Your Ideas:</span>
            <span class="text-base font-bold text-primary">{boardIdeas.filter((i) => !i.isAnonymous).length}</span>
          </div>
        </div>
      </div>
    </div>
  </aside>

  <main
    bind:this={boardContainer}
    on:dragover={handleDragOver}
    on:drop={handleDrop}
    class="flex-1 border-2 border-dashed border-border rounded-lg bg-surface overflow-hidden relative transition-colors {draggedIdea ? 'bg-opacity-50 border-primary' : ''}"
    role="region"
    aria-label="Brainstorm board"
  >
    {#if boardIdeas.length === 0}
      <div class="absolute inset-0 flex items-center justify-center text-text-light">
        <div class="text-center">
          <p class="text-lg font-semibold mb-2">Brainstorm Board</p>
          <p class="text-sm">Drag ideas around to organize them</p>
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
  </main>
</div>

<style>
  div[data-idea-id] {
    touch-action: none;
  }
</style>
