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

<div class="flex flex-col gap-2 p-4 bg-surface rounded-lg border border-border">
  <p class="text-sm font-semibold text-text mb-2">Voting Marks</p>

  <div class="flex flex-col gap-2">
    {#each marks as mark}
      <button
        on:click={() => onVote?.(mark.type)}
        class="flex items-center gap-3 p-2 rounded hover:bg-white transition-colors text-left"
      >
        <span class="text-2xl">{mark.icon}</span>
        <div class="flex-1">
          <p class="text-sm font-semibold text-text">{mark.label}</p>
        </div>
      </button>
    {/each}
  </div>
</div>
