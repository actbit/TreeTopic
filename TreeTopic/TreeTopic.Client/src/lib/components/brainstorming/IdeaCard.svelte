<script lang="ts">
  import { ui } from '$lib/stores/ui';
  import type { Idea } from '$lib/types/ui';

  interface Props {
    idea: Idea;
    isDragging?: boolean;
    onDragStart?: (e: DragEvent) => void;
    onDragEnd?: (e: DragEvent) => void;
    onDelete?: () => void;
    onEdit?: () => void;
  }

  let { idea, isDragging = false, onDragStart, onDragEnd, onDelete, onEdit }: Props = $props();

  let isHovered = $state(false);
  let isEditing = $state(false);
  let editedText = $state(idea.text);

  function handleEdit() {
    isEditing = true;
    editedText = idea.text;
  }

  function handleSave() {
    if (editedText.trim()) {
      idea.text = editedText.trim();
      isEditing = false;
    }
  }

  function handleCancel() {
    isEditing = false;
    editedText = idea.text;
  }

  function toggleVoteMark(type: 'circle' | 'square' | 'triangle' | 'cross') {
    const existingMark = idea.marks.find((m) => m.type === type);
    if (existingMark) {
      idea.marks = idea.marks.filter((m) => m !== existingMark);
    } else {
      idea.marks = [...idea.marks, { type, userId: 'current-user' }];
    }
  }

  function getMarkIcon(type: string): string {
    switch (type) {
      case 'circle':
        return '●';
      case 'square':
        return '■';
      case 'triangle':
        return '▲';
      case 'cross':
        return '×';
      default:
        return '●';
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
      handleEdit();
    }
  }

  const voteCounts = $derived.by(() => {
    const counts: Record<string, number> = {
      circle: 0,
      square: 0,
      triangle: 0,
      cross: 0,
    };

    idea.marks.forEach((mark) => {
      counts[mark.type]++;
    });

    return counts;
  });
</script>

<article
  class="card hoverable draggable-card {isDragging ? 'dragging' : ''}"
  draggable={true}
  on:dragstart={onDragStart}
  on:dragend={onDragEnd}
  on:mouseenter={() => (isHovered = true)}
  on:mouseleave={() => (isHovered = false)}
  role="region"
  aria-label="Idea card: {idea.text.substring(0, 50)}"
  tabindex="0"
  on:keydown={handleKeyDown}
>
  {#if isEditing}
    <div class="spacing-sm">
      <textarea
        value={editedText}
        on:input={(e) => (editedText = (e.target as HTMLTextAreaElement).value)}
        class="form-input w-full text-small"
        rows="2"
        style="resize: none;"
        autofocus
      />
      <div class="flex spacing-sm">
        <button
          on:click={handleSave}
          class="button button-primary button-small"
          style="flex: 1;"
        >
          Save
        </button>
        <button
          on:click={handleCancel}
          class="button button-secondary button-small"
          style="flex: 1;"
        >
          Cancel
        </button>
      </div>
    </div>
  {:else}
    <div class="spacing-sm">
      <p class="text-small" style="white-space: pre-wrap; word-break: break-word;">{idea.text}</p>

      {#if idea.userName && !idea.isAnonymous}
        <p class="text-small text-light">By {idea.userName}</p>
      {/if}

      <div class="flex flex-wrap spacing-xs">
        {#each ['circle', 'square', 'triangle', 'cross'] as voteType}
          <button
            on:click={() => toggleVoteMark(voteType)}
            class="badge clickable {voteCounts[voteType] > 0 ? 'badge-primary' : 'badge-secondary'}"
            title={`${getMarkLabel(voteType)} - ${voteCounts[voteType]} votes`}
            aria-label={`${getMarkLabel(voteType)} vote: ${voteCounts[voteType]}`}
            aria-pressed={voteCounts[voteType] > 0}
          >
            {getMarkIcon(voteType)} {voteCounts[voteType] > 0 ? voteCounts[voteType] : ''}
          </button>
        {/each}
      </div>

      {#if isHovered && !isEditing}
        <div class="divider margin-top-sm margin-bottom-sm"></div>
        <div class="flex spacing-xs">
          <button
            on:click={handleEdit}
            class="button button-primary button-small"
            style="flex: 1;"
          >
            Edit
          </button>
          <button
            on:click={onDelete}
            class="button button-danger button-small"
            style="flex: 1;"
          >
            Delete
          </button>
        </div>
      {/if}
    </div>
  {/if}
</article>

<style>
  .draggable-card {
    touch-action: none;
    cursor: move;
    border: 2px solid var(--color-primary);
  }

  .draggable-card.dragging {
    opacity: 0.5;
  }
</style>
