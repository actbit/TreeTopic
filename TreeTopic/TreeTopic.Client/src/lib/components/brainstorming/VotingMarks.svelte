<script lang="ts">
  interface Props {
    onVote?: (type: 'circle' | 'square' | 'triangle' | 'cross') => void;
  }

  let { onVote }: Props = $props();

  const marks = [
    { type: 'circle' as const, icon: '●', label: 'Yes / Agree', color: 'bg-green-100' },
    { type: 'square' as const, icon: '■', label: 'Maybe / Consider', color: 'bg-yellow-100' },
    { type: 'triangle' as const, icon: '▲', label: 'Priority', color: 'bg-blue-100' },
    { type: 'cross' as const, icon: '×', label: 'No / Disagree', color: 'bg-red-100' },
  ];
</script>

<div class="panel">
  <div class="panel-header">
    <h3 class="panel-title">Voting Marks</h3>
  </div>
  <div class="panel-body spacing-sm">
    {#each marks as mark}
      <button
        on:click={() => onVote?.(mark.type)}
        class="list-item clickable hoverable"
        style="display: flex; align-items: center; gap: var(--spacing-md); text-align: left;"
      >
        <span style="font-size: var(--font-size-2xl);">{mark.icon}</span>
        <div style="flex: 1;">
          <p class="text-small text-bold">{mark.label}</p>
        </div>
      </button>
    {/each}
  </div>
</div>
