<script lang="ts">
  import IdeaCard from './IdeaCard.svelte';
  import { addIdea, brainstorm, brainstormBoard, deleteIdea, updateIdeaPosition } from '$lib/stores/brainstorm';
  import { currentUser } from '$lib/stores/auth';
  import { currentRoomUser } from '$lib/stores/rooms';
  import { api } from '$lib/api/client';

  interface Props {
    boardId?: string;
  }

  let { boardId }: Props = $props();

  let resolvedBoardId = $derived.by(() => boardId || $brainstormBoard?.id || '');

  const cardWidth = 220;
  const cardHeight = 132;

  let boardContainer: HTMLDivElement | undefined = $state();
  let draggedIdea: string | null = $state(null);
  let offsetX = $state(0);
  let offsetY = $state(0);
  let newIdeaText = $state('');
  let createError = $state<string | null>(null);
  let isCreating = $state(false);
  let votingIdeaId = $state<string | null>(null);
  let voteError = $state<string | null>(null);

  let boardIdeas = $derived.by(() => {
    if (!$brainstormBoard) return [];
    if (resolvedBoardId && $brainstormBoard.id !== resolvedBoardId) return [];
    return $brainstormBoard.ideas ?? [];
  });

  const currentUserId = $derived.by(() => $currentUser?.id ?? null);
  const trimmedIdea = $derived.by(() => newIdeaText.trim());

  function getDefaultIdeaPosition() {
    if (!boardContainer) {
      return { positionLeft: 24, positionTop: 24 };
    }

    const rect = boardContainer.getBoundingClientRect();
    const padding = 24;
    const gap = 16;
    const columns = Math.max(1, Math.floor((rect.width - padding) / (cardWidth + gap)));
    const index = boardIdeas.length;
    const column = index % columns;
    const row = Math.floor(index / columns);
    const positionLeft = Math.min(rect.width - cardWidth, padding + column * (cardWidth + gap));
    const positionTop = Math.min(rect.height - cardHeight, padding + row * (cardHeight + gap));

    return {
      positionLeft: Math.max(0, positionLeft),
      positionTop: Math.max(0, positionTop),
    };
  }

  async function handleCreateIdea() {
    if (!trimmedIdea || !resolvedBoardId || isCreating) return;

    createError = null;
    isCreating = true;

    try {
      const tenant =
        api.getCurrentTenant() ||
        (typeof window !== 'undefined' ? window.location.pathname.split('/').filter(Boolean)[0] : '');
      if (!tenant) {
        throw new Error('Tenant is required');
      }

      const { positionLeft, positionTop } = getDefaultIdeaPosition();
      const created = await api.post(`/${tenant}/api/brainstorm/${resolvedBoardId}/ideas`, {
        idea: trimmedIdea,
        positionLeft,
        positionTop,
      });
      addIdea(created);
      newIdeaText = '';
    } catch (err: unknown) {
      createError = err instanceof Error ? err.message : 'Failed to create idea';
    } finally {
      isCreating = false;
    }
  }

  function handleIdeaKeyDown(e: KeyboardEvent) {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      handleCreateIdea();
    }
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
    if (!resolvedBoardId) return;

    const rect = boardContainer.getBoundingClientRect();
    const x = (e.clientX || 0) - rect.left - offsetX;
    const y = (e.clientY || 0) - rect.top - offsetY;

    const constrainedX = Math.max(0, Math.min(x, rect.width - cardWidth));
    const constrainedY = Math.max(0, Math.min(y, rect.height - cardHeight));

    updateIdeaPosition(draggedIdea, constrainedX, constrainedY);

    const tenant = api.getCurrentTenant();
    api.patch(`/${tenant}/api/brainstorm/${resolvedBoardId}/ideas/${draggedIdea}`, {
      positionLeft: constrainedX,
      positionTop: constrainedY,
    });

    draggedIdea = null;
  }

  function handleDeleteIdea(ideaId: string) {
    deleteIdea(ideaId);

    if (!resolvedBoardId) return;
    const tenant = api.getCurrentTenant();
    api.delete(`/${tenant}/api/brainstorm/${resolvedBoardId}/ideas/${ideaId}`);
  }

  function getIdeaVotes(ideaId: string) {
    return boardIdeas.find((idea) => idea.id === ideaId)?.votes ?? [];
  }

  function setIdeaVotes(ideaId: string, votes: ReturnType<typeof getIdeaVotes>) {
    if (!resolvedBoardId) return;
    brainstorm.updateIdea(resolvedBoardId, ideaId, { votes });
  }

  async function handleVote(ideaId: string, voteType: string) {
    if (!resolvedBoardId) return;
    if (!currentUserId) {
      voteError = 'You must be signed in to vote.';
      return;
    }
    if (votingIdeaId === ideaId) return;

    voteError = null;
    votingIdeaId = ideaId;

    try {
      const tenant =
        api.getCurrentTenant() ||
        (typeof window !== 'undefined' ? window.location.pathname.split('/').filter(Boolean)[0] : '');
      if (!tenant) {
        throw new Error('Tenant is required');
      }

      const votes = getIdeaVotes(ideaId);
      const existingVote = votes.find(
        (vote) => vote.roomUserId === $currentRoomUser?.id && vote.voteType?.toLowerCase() === voteType.toLowerCase()
      );

      if (existingVote) {
        await api.delete(`/${tenant}/api/brainstorm/${resolvedBoardId}/ideas/${ideaId}/votes/${existingVote.id}`);
        setIdeaVotes(
          ideaId,
          votes.filter((vote) => vote.id !== existingVote.id)
        );
        return;
      }

      const created = await api.post(`/${tenant}/api/brainstorm/${resolvedBoardId}/ideas/${ideaId}/votes`, {
        voteType,
        value: 1,
      });

      const nextVotes = votes.filter((vote) => vote.roomUserId !== $currentRoomUser?.id);
      nextVotes.push(created);
      setIdeaVotes(ideaId, nextVotes);
    } catch (err: unknown) {
      voteError = err instanceof Error ? err.message : 'Failed to vote';
    } finally {
      votingIdeaId = null;
    }
  }
</script>

<div class="brainstorm-board">
  <aside class="brainstorm-sidebar" aria-label="Ideas panel">
    <div class="sidebar-section">
      <h2 class="sidebar-title">Ideas</h2>
      <p class="sidebar-subtitle">Organize and vote on concepts</p>
    </div>

    <div class="sidebar-card">
      <p class="sidebar-note">
        Add ideas from this panel, then drag cards to group and organize.
      </p>
    </div>

    <div class="sidebar-form">
      <label class="sidebar-label" for="idea-input">New idea</label>
      <textarea
        id="idea-input"
        class="sidebar-input"
        placeholder="Type an idea..."
        rows="3"
        bind:value={newIdeaText}
        onkeydown={handleIdeaKeyDown}
        disabled={isCreating || !resolvedBoardId}
      ></textarea>
      <div class="sidebar-actions">
        <button
          class="sidebar-btn"
          onclick={handleCreateIdea}
          disabled={!trimmedIdea || isCreating || !resolvedBoardId}
        >
          {isCreating ? 'Adding...' : 'Add idea'}
        </button>
        <span class="sidebar-hint">Ctrl/⌘ + Enter</span>
      </div>
      {#if createError}
        <p class="sidebar-error">{createError}</p>
      {/if}
    </div>

    <div class="sidebar-divider"></div>

    <div class="sidebar-section">
      <p class="sidebar-heading">Voting Marks</p>
      <div class="sidebar-list">
        <p>Circle — Agree</p>
        <p>Square — Consider</p>
        <p>Triangle — Priority</p>
        <p>Cross — Disagree</p>
      </div>
      {#if voteError}
        <p class="sidebar-error">{voteError}</p>
      {/if}
    </div>

    <div class="sidebar-divider"></div>

    <div class="sidebar-section">
      <p class="sidebar-heading">Stats</p>
      <div class="sidebar-stats">
        <div class="stat-card">
          <span>Total</span>
          <strong>{boardIdeas.length}</strong>
        </div>
        <div class="stat-card">
          <span>Named</span>
          <strong>{boardIdeas.filter((i) => !!i.userName).length}</strong>
        </div>
      </div>
    </div>
  </aside>

  <main
    bind:this={boardContainer}
    ondragover={handleDragOver}
    ondrop={handleDrop}
    class="brainstorm-canvas {draggedIdea ? 'dragging' : ''}"
    role="region"
    aria-label="Brainstorm board"
  >
    {#if boardIdeas.length === 0}
      <div class="empty-state">
        <div>
          <p class="empty-title">Brainstorm Board</p>
          <p class="empty-subtitle">Drag cards to group ideas</p>
        </div>
      </div>
    {:else}
      <div class="canvas-inner">
        {#each boardIdeas as idea (idea.id)}
          <div
            data-idea-id={idea.id}
            class="idea-wrapper {draggedIdea === idea.id ? 'is-dragging' : ''}"
            style="left: {idea.positionLeft}px; top: {idea.positionTop}px;"
            ondragstart={(e) => handleDragStart(idea.id, e)}
            role="none"
          >
            <IdeaCard
              {idea}
              isDragging={draggedIdea === idea.id}
              currentUserId={$currentRoomUser?.id}
              isVoting={votingIdeaId === idea.id}
              onVote={(voteType) => handleVote(idea.id, voteType)}
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

  .brainstorm-board {
    display: flex;
    gap: 18px;
    height: 100%;
    padding: 20px;
    background: #121212;
    color: #e5e7eb;
  }

  .brainstorm-sidebar {
    width: 280px;
    padding: 18px;
    border-radius: 16px;
    background: linear-gradient(180deg, #1b1b1b 0%, #161616 100%);
    border: 1px solid rgba(255, 255, 255, 0.08);
    box-shadow: 0 12px 28px rgba(0, 0, 0, 0.35);
    overflow-y: auto;
  }

  :global(.sidebar-section + .sidebar-section) {
    margin-top: 18px;
  }

  .sidebar-title {
    font-size: 18px;
    font-weight: 700;
    color: #f3f4f6;
    margin: 0 0 6px;
  }

  .sidebar-subtitle {
    font-size: 12px;
    color: #94a3b8;
    margin: 0;
  }

  .sidebar-card {
    margin-top: 16px;
    padding: 12px;
    border-radius: 12px;
    background: rgba(255, 255, 255, 0.04);
    border: 1px solid rgba(255, 255, 255, 0.08);
  }

  .sidebar-note {
    font-size: 12px;
    color: #cbd5f5;
    margin: 0;
    line-height: 1.6;
  }

  .sidebar-divider {
    height: 1px;
    margin: 18px 0;
    background: rgba(255, 255, 255, 0.08);
  }

  .sidebar-form {
    margin-top: 16px;
    display: grid;
    gap: 10px;
  }

  .sidebar-label {
    font-size: 12px;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: #94a3b8;
    font-weight: 600;
  }

  .sidebar-input {
    width: 100%;
    min-height: 72px;
    resize: vertical;
    border-radius: 12px;
    padding: 10px 12px;
    background: rgba(255, 255, 255, 0.04);
    border: 1px solid rgba(255, 255, 255, 0.12);
    color: #e2e8f0;
    font-size: 13px;
    line-height: 1.5;
  }

  .sidebar-input:focus-visible {
    outline: 2px solid rgba(96, 165, 250, 0.6);
    outline-offset: 2px;
  }

  .sidebar-input:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .sidebar-actions {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
  }

  .sidebar-btn {
    padding: 8px 14px;
    border-radius: 10px;
    background: #2563eb;
    color: #e5f0ff;
    border: 1px solid rgba(59, 130, 246, 0.6);
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
  }

  .sidebar-btn:hover:not(:disabled) {
    background: #3b82f6;
  }

  .sidebar-btn:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .sidebar-hint {
    font-size: 11px;
    color: #94a3b8;
  }

  .sidebar-error {
    font-size: 12px;
    color: #fca5a5;
    margin: 0;
  }

  .sidebar-heading {
    font-size: 12px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: #94a3b8;
    margin: 0 0 10px;
  }

  .sidebar-list {
    display: grid;
    gap: 6px;
    font-size: 13px;
    color: #cbd5e1;
  }

  .sidebar-stats {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 10px;
  }

  .stat-card {
    padding: 10px;
    border-radius: 12px;
    background: rgba(255, 255, 255, 0.04);
    border: 1px solid rgba(255, 255, 255, 0.08);
    display: grid;
    gap: 6px;
    font-size: 12px;
    color: #94a3b8;
  }

  .stat-card strong {
    font-size: 18px;
    color: #60a5fa;
  }

  .brainstorm-canvas {
    position: relative;
    flex: 1;
    border-radius: 18px;
    border: 1px solid rgba(255, 255, 255, 0.08);
    background-color: #151515;
    background-image: radial-gradient(rgba(255, 255, 255, 0.07) 1px, transparent 1px);
    background-size: 22px 22px;
    box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.02), 0 12px 28px rgba(0, 0, 0, 0.35);
    overflow: hidden;
    transition: border-color 0.2s ease, box-shadow 0.2s ease;
  }

  .brainstorm-canvas.dragging {
    border-color: rgba(96, 165, 250, 0.6);
    box-shadow: inset 0 0 0 1px rgba(96, 165, 250, 0.2), 0 18px 36px rgba(0, 0, 0, 0.45);
  }

  .canvas-inner {
    position: relative;
    width: 100%;
    height: 100%;
  }

  .idea-wrapper {
    position: absolute;
    width: 220px;
    transition: transform 0.2s ease, box-shadow 0.2s ease;
  }

  .idea-wrapper.is-dragging {
    z-index: 50;
  }

  .empty-state {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    color: #94a3b8;
  }

  .empty-title {
    font-size: 18px;
    font-weight: 600;
    color: #e2e8f0;
    margin: 0 0 6px;
    text-align: center;
  }

  .empty-subtitle {
    font-size: 13px;
    margin: 0;
    text-align: center;
  }
</style>
