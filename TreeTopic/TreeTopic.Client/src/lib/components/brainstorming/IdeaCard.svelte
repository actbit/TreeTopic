<script lang="ts">
  import type { BrainIdea } from '$lib/stores/brainstorm';

  interface Props {
    idea: BrainIdea;
    isDragging?: boolean;
    currentUserId?: string | null;
    isVoting?: boolean;
    onVote?: (voteType: string) => void;
    onDragStart?: (e: DragEvent) => void;
    onDragEnd?: (e: DragEvent) => void;
    onDelete?: () => void;
  }

  let {
    idea,
    isDragging = false,
    currentUserId = null,
    isVoting = false,
    onVote,
    onDragStart,
    onDragEnd,
    onDelete,
  }: Props = $props();

  let isHovered = $state(false);

  function getMarkIcon(type: string): string {
    switch (type) {
      case 'circle':
        return '○';
      case 'square':
        return '□';
      case 'triangle':
        return '△';
      case 'cross':
        return '✕';
      default:
        return '○';
    }
  }

  function getMarkLabel(type: string): string {
    const labels: Record<string, string> = {
      circle: 'Agree',
      square: 'Consider',
      triangle: 'Priority',
      cross: 'Disagree',
    };
    return labels[type] || type;
  }

  function handleKeyDown(e: KeyboardEvent) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
    }
  }

  const voteCounts = $derived.by(() => {
    const counts: Record<string, number> = {
      circle: 0,
      square: 0,
      triangle: 0,
      cross: 0,
    };

    (idea.votes ?? []).forEach((vote) => {
      const key = (vote.voteType || '').toLowerCase();
      if (!key) return;
      if (!counts[key]) counts[key] = 0;
      counts[key] += vote.value ?? 1;
    });

    return counts;
  });

  const currentUserVote = $derived.by(() => {
    if (!currentUserId) return null;
    return (idea.votes ?? []).find((vote) => vote.applicationUserId === currentUserId) ?? null;
  });

  const currentUserVoteType = $derived.by(() => currentUserVote?.voteType?.toLowerCase() ?? null);

  function handleVoteClick(type: string) {
    if (!onVote || isVoting || !currentUserId) return;
    onVote(type);
  }
</script>

<article
  class="idea-card {isDragging ? 'dragging' : ''}"
  draggable={true}
  ondragstart={onDragStart}
  ondragend={onDragEnd}
  onmouseenter={() => (isHovered = true)}
  onmouseleave={() => (isHovered = false)}
  role="region"
  aria-label="Idea card: {idea.idea.substring(0, 50)}"
  tabindex="0"
  onkeydown={handleKeyDown}
>
  <div class="idea-card__header">
    <span class="idea-card__label">Idea</span>
    {#if idea.userName}
      <span class="idea-card__badge">{idea.userName}</span>
    {/if}
  </div>

  <p class="idea-card__text" style="white-space: pre-wrap; word-break: break-word;">
    {idea.idea}
  </p>

  <div class="idea-card__votes">
    {#each ['circle', 'square', 'triangle', 'cross'] as voteType}
      <button
        class="vote-pill vote-{voteType} {voteCounts[voteType] > 0 ? 'active' : ''} {currentUserVoteType === voteType ? 'selected' : ''}"
        title={`${getMarkLabel(voteType)} - ${voteCounts[voteType]} votes`}
        aria-label={`${getMarkLabel(voteType)} vote: ${voteCounts[voteType]}`}
        aria-pressed={currentUserVoteType === voteType}
        disabled={isVoting || !currentUserId || !onVote}
        onclick={() => handleVoteClick(voteType)}
        type="button"
      >
        <span class="vote-icon">{getMarkIcon(voteType)}</span>
        {#if voteCounts[voteType] > 0}
          <span class="vote-count">{voteCounts[voteType]}</span>
        {/if}
      </button>
    {/each}
  </div>

  {#if isHovered && onDelete}
    <div class="idea-card__actions">
      <button onclick={onDelete} class="idea-card__delete">Delete</button>
    </div>
  {/if}
</article>

<style>
  .idea-card {
    touch-action: none;
    cursor: grab;
    border-radius: 14px;
    background: linear-gradient(180deg, #1f1f1f 0%, #181818 100%);
    border: 1px solid rgba(255, 255, 255, 0.08);
    box-shadow: 0 10px 24px rgba(0, 0, 0, 0.4);
    padding: 12px 14px 14px;
    color: #e2e8f0;
    display: grid;
    gap: 10px;
  }

  .idea-card:focus-visible {
    outline: 2px solid rgba(96, 165, 250, 0.8);
    outline-offset: 2px;
  }

  .idea-card.dragging {
    opacity: 0.6;
    cursor: grabbing;
  }

  .idea-card__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    font-size: 11px;
    text-transform: uppercase;
    letter-spacing: 0.12em;
    color: #94a3b8;
  }

  .idea-card__label {
    font-weight: 600;
  }

  .idea-card__badge {
    padding: 2px 8px;
    border-radius: 999px;
    background: rgba(255, 255, 255, 0.08);
    border: 1px solid rgba(255, 255, 255, 0.12);
    font-size: 11px;
    color: #e2e8f0;
    letter-spacing: 0.02em;
  }

  .idea-card__text {
    font-size: 13px;
    line-height: 1.5;
    margin: 0;
  }

  .idea-card__votes {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
  }

  .vote-pill {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 10px;
    border-radius: 999px;
    font-size: 12px;
    border: 1px solid rgba(255, 255, 255, 0.12);
    background: rgba(15, 23, 42, 0.35);
    color: #cbd5e1;
    transition: all 0.2s ease;
    cursor: pointer;
  }

  .vote-pill.active {
    background: rgba(255, 255, 255, 0.08);
    color: #f8fafc;
    box-shadow: 0 6px 14px rgba(0, 0, 0, 0.3);
  }

  .vote-pill.selected {
    border-color: rgba(96, 165, 250, 0.7);
    box-shadow: 0 0 0 2px rgba(96, 165, 250, 0.2);
  }

  .vote-pill:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .vote-pill.vote-circle {
    border-color: rgba(56, 189, 248, 0.6);
    color: #7dd3fc;
  }

  .vote-pill.vote-square {
    border-color: rgba(167, 139, 250, 0.6);
    color: #c4b5fd;
  }

  .vote-pill.vote-triangle {
    border-color: rgba(245, 158, 11, 0.65);
    color: #fbbf24;
  }

  .vote-pill.vote-cross {
    border-color: rgba(248, 113, 113, 0.7);
    color: #fca5a5;
  }

  .vote-icon {
    font-size: 12px;
  }

  .vote-count {
    font-weight: 600;
  }

  .idea-card__actions {
    border-top: 1px solid rgba(255, 255, 255, 0.06);
    padding-top: 8px;
  }

  .idea-card__delete {
    width: 100%;
    padding: 6px 12px;
    border-radius: 10px;
    background: rgba(239, 68, 68, 0.2);
    border: 1px solid rgba(239, 68, 68, 0.35);
    color: #fecaca;
    font-size: 12px;
    cursor: pointer;
  }

  .idea-card__delete:hover {
    background: rgba(239, 68, 68, 0.35);
  }
</style>
