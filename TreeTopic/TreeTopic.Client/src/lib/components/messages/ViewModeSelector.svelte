<script lang="ts">
  import { ui } from '$lib/stores/ui';

  type ViewMode = 'default' | 'timeline' | 'user' | 'document' | 'image' | 'search' | 'topic';

  const viewModes: Array<{
    id: ViewMode;
    label: string;
    description: string;
  }> = [
    { id: 'default', label: 'Messages', description: 'Standard message list' },
    { id: 'timeline', label: 'Timeline', description: 'Chronological view' },
    { id: 'user', label: 'By User', description: 'Grouped by user' },
    { id: 'document', label: 'By Document', description: 'By attached files' },
    { id: 'image', label: 'Images', description: 'Image gallery' },
    { id: 'topic', label: 'By Topic', description: 'Grouped by topic' },
    { id: 'search', label: 'Search', description: 'Search messages' },
  ];

  let currentMode = $derived.by(() => {
    return $ui.viewMode || 'default';
  });

  function changeMode(mode: ViewMode) {
    ui.setViewMode(mode);
  }
</script>

<div class="tabs">
  {#each viewModes as mode}
    <button
      onclick={() => changeMode(mode.id)}
      class="tab {currentMode === mode.id ? 'tab-active' : ''}"
      title={mode.description}
    >
      <span class="text-small text-bold">{mode.label}</span>
    </button>
  {/each}
</div>
