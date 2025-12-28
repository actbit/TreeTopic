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

<div
  class="bg-white border-2 border-primary rounded-lg p-3 shadow-md hover:shadow-lg transition-all {isDragging
    ? 'opacity-50'
    : ''}"
  style="touch-action: none; cursor: move;"
  draggable={true}
  on:dragstart={onDragStart}
  on:dragend={onDragEnd}
  on:mouseenter={() => (isHovered = true)}
  on:mouseleave={() => (isHovered = false)}
  role="button"
  tabindex="0"
>
  {#if isEditing}
    <div class="space-y-2">
      <textarea
        value={editedText}
        on:input={(e) => (editedText = (e.target as HTMLTextAreaElement).value)}
        class="w-full p-2 border border-primary rounded text-sm bg-white focus:outline-none focus:border-primary-hover resize-none"
        rows="2"
        autofocus
      />
      <div class="flex gap-2">
        <button
          on:click={handleSave}
          class="flex-1 px-2 py-1 bg-primary text-white text-xs rounded hover:bg-primary-hover transition-colors"
        >
          Save
        </button>
        <button
          on:click={handleCancel}
          class="flex-1 px-2 py-1 bg-surface text-text text-xs rounded hover:bg-white transition-colors border border-border"
        >
          Cancel
        </button>
      </div>
    </div>
  {:else}
    <div class="space-y-2">
      <p class="text-sm text-text whitespace-pre-wrap break-words">{idea.text}</p>

      {#if idea.userName && !idea.isAnonymous}
        <p class="text-xs text-text-light">By {idea.userName}</p>
      {/if}

      <div class="flex flex-wrap gap-1">
        {#each ['circle', 'square', 'triangle', 'cross'] as voteType}
          <button
            on:click={() => toggleVoteMark(voteType)}
            class="px-2 py-1 text-xs rounded transition-colors {voteCounts[voteType] > 0
              ? 'bg-primary text-white'
              : 'bg-surface text-text hover:bg-white border border-border'}"
            title={`${voteType} votes: ${voteCounts[voteType]}`}
          >
            {getMarkIcon(voteType)} {voteCounts[voteType] > 0 ? voteCounts[voteType] : ''}
          </button>
        {/each}
      </div>

      {#if isHovered && !isEditing}
        <div class="flex gap-1 pt-1 border-t border-border">
          <button
            on:click={handleEdit}
            class="flex-1 px-2 py-1 text-xs bg-primary text-white rounded hover:bg-primary-hover transition-colors"
          >
            Edit
          </button>
          <button
            on:click={onDelete}
            class="flex-1 px-2 py-1 text-xs bg-danger text-white rounded hover:bg-opacity-80 transition-colors"
          >
            Delete
          </button>
        </div>
      {/if}
    </div>
  {/if}
</div>

<style>
  div {
    touch-action: none;
  }
</style>
