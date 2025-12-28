<script lang="ts">
  import { ui } from '$lib/stores/ui';

  type ViewMode = 'default' | 'timeline' | 'user' | 'document' | 'image' | 'search' | 'topic';

  const viewModes: Array<{
    id: ViewMode;
    label: string;
    icon: string;
    description: string;
  }> = [
    { id: 'default', label: 'Messages', icon: '💬', description: 'Standard message list' },
    { id: 'timeline', label: 'Timeline', icon: '📅', description: 'Chronological view' },
    { id: 'user', label: 'By User', icon: '👤', description: 'Grouped by user' },
    { id: 'document', label: 'By Document', icon: '📄', description: 'By attached files' },
    { id: 'image', label: 'Images', icon: '🖼️', description: 'Image gallery' },
    { id: 'topic', label: 'By Topic', icon: '📌', description: 'Grouped by topic' },
    { id: 'search', label: 'Search', icon: '🔍', description: 'Search messages' },
  ];

  let currentMode = $derived.by(() => {
    return $ui.viewMode || 'default';
  });

  function changeMode(mode: ViewMode) {
    ui.setViewMode(mode);
  }
</script>

<div class="flex flex-wrap gap-2">
  {#each viewModes as mode}
    <button
      on:click={() => changeMode(mode.id)}
      class="flex flex-col items-center gap-1 px-3 py-2 rounded transition-all {currentMode === mode.id
        ? 'bg-primary text-white shadow-md'
        : 'bg-surface text-text hover:bg-white border border-border'}"
      title={mode.description}
    >
      <span class="text-lg">{mode.icon}</span>
      <span class="text-xs font-semibold">{mode.label}</span>
    </button>
  {/each}
</div>
