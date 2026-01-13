<script lang="ts">
  import ShareUploadModal from './ShareUploadModal.svelte';
  import Button from '$lib/components/common/Button.svelte';
  import ContextMenu, { type ContextMenuItem } from '$lib/components/common/ContextMenu.svelte';
  import LoadingSpinner from '$lib/components/common/LoadingSpinner.svelte';
  import { ui } from '$lib/stores/ui';
  import { currentRoom } from '$lib/stores/rooms';
  import { selectedTopic } from '$lib/stores/topics';
  import { api } from '$lib/api/client';
  import { shareItems, sharesLoading, sharesError, shares, loadShares } from '$lib/stores/shares';

  interface Props {
    variant?: 'panel' | 'content';
  }

  let { variant = 'panel' }: Props = $props();

  let filter = $state<'topic' | 'room'>('topic');
  let menuX = $state(0);
  let menuY = $state(0);
  let menuItems = $state<ContextMenuItem[] | null>(null);

  $effect(() => {
    if (!$selectedTopic && filter === 'topic') filter = 'room';
  });

  $effect(() => {
    if (!$currentRoom) return;
    const tenant = api.getCurrentTenant();
    const topicId = filter === 'topic' ? $selectedTopic?.id ?? null : null;
    void loadShares({ tenant, roomId: $currentRoom.id, topicId });
  });

  let filtered = $derived.by(() => {
    if (filter === 'topic' && $selectedTopic) {
      return $shareItems.filter((s) => s.topicId === $selectedTopic.id);
    }
    return $shareItems;
  });

  let grouped = $derived.by(() => {
    const documents = [];
    const images = [];
    const brainstorms = [];

    for (const s of filtered) {
      if (s.kind === 'image') images.push(s);
      else if (s.kind === 'brainstorm') brainstorms.push(s);
      else documents.push(s);
    }

    return { documents, images, brainstorms };
  });

  function openShareModal() {
    ui.openModal({ id: 'share-upload', title: 'Add shared item', type: 'custom' });
  }

  async function deleteShare(shareId: string) {
    if (!$currentRoom) return;
    if (!confirm('Delete this shared item?')) return;
    try {
      const tenant = api.getCurrentTenant();
      await api.delete(`/${tenant}/api/Share/room/${$currentRoom.id}/${shareId}`);
      shares.removeShare(shareId);
    } catch (err) {
      console.error(err);
    }
  }

  function openBrain(url: string, boardId: string | null | undefined) {
    if (!boardId || boardId === 'undefined' || boardId === 'null') {
      ui.addNotification({ type: 'error', message: 'Brainstorm board ID is missing' });
      return;
    }
    window.open(url || `/${api.getCurrentTenant()}/brainstorm/${boardId}`, '_blank');
  }

  function openDoc(url: string) {
    if (!url) return;
    window.open(url, '_blank');
  }

  function formatWhen(value: any): string {
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return date.toLocaleString(undefined, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  async function copyLink(url: string) {
    if (typeof window === 'undefined') return;
    if (!url) return;
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(url);
      } else {
        const textarea = document.createElement('textarea');
        textarea.value = url;
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        document.body.appendChild(textarea);
        textarea.select();
        document.execCommand('copy');
        document.body.removeChild(textarea);
      }
      ui.addNotification({ type: 'success', message: 'Link copied' });
    } catch {
      ui.addNotification({ type: 'error', message: 'Failed to copy link' });
    }
  }

  function buildBrainstormLink(url: string | null | undefined, boardId: string | null | undefined) {
    if (url) return url;
    if (!boardId || boardId === 'undefined' || boardId === 'null') {
      ui.addNotification({ type: 'error', message: 'Brainstorm board ID is missing' });
      return '';
    }
    return `/${api.getCurrentTenant()}/brainstorm/${boardId}`;
  }

  function openMenuForShare(s: any, e: MouseEvent) {
    e.preventDefault();
    e.stopPropagation();

    menuX = e.clientX;
    menuY = e.clientY;

    const items: ContextMenuItem[] = [];
    if (s.kind === 'image') {
      items.push({ id: 'open', label: 'Open', icon: '?', action: () => openDoc(s.url) });
      items.push({ id: 'copy', label: 'Copy link', icon: '?', action: () => copyLink(s.url) });
      items.push({ id: 'div', label: '', divider: true, action: () => {} });
      items.push({ id: 'delete', label: 'Delete', icon: '??', isDangerous: true, action: () => deleteShare(s.id) });
    } else if (s.kind === 'brainstorm') {
      items.push({
        id: 'open',
        label: 'Open',
        icon: '?',
        action: () => openBrain(s.url, s.boardId),
      });
      items.push({
        id: 'copy',
        label: 'Copy link',
        icon: '?',
        action: () => copyLink(buildBrainstormLink(s.url, s.boardId)),
      });
      items.push({ id: 'div', label: '', divider: true, action: () => {} });
      items.push({ id: 'delete', label: 'Delete', icon: '??', isDangerous: true, action: () => deleteShare(s.id) });
    } else {
      items.push({ id: 'open', label: 'Open', icon: '?', action: () => openDoc(s.url) });
      items.push({ id: 'copy', label: 'Copy link', icon: '?', action: () => copyLink(s.url) });
      items.push({ id: 'div', label: '', divider: true, action: () => {} });
      items.push({ id: 'delete', label: 'Delete', icon: '??', isDangerous: true, action: () => deleteShare(s.id) });
    }

    menuItems = items;
  }

  function closeMenu() {
    menuItems = null;
  }
</script>

{#if variant === 'panel'}
  <div class="panel h-full">
    <div class="panel-header">
      <div class="header-row">
        <div class="min-w-0">
          <h3 class="panel-title">Shared</h3>
          <span class="text-small text-light">{filtered.length} items</span>
        </div>
        <Button variant="primary" size="small" icon="+" onclick={openShareModal} ariaLabel="Add" />
      </div>
    </div>

    <div class="panel-body overflow-y-auto">
      <div class="filter-row">
        <button
          type="button"
          class="pill {filter === 'topic' ? 'active' : ''}"
          disabled={!$selectedTopic}
          onclick={() => (filter = 'topic')}
          title={!$selectedTopic ? 'Select a topic to filter' : 'Show current topic shares'}
        >
          Topic
        </button>
        <button type="button" class="pill {filter === 'room' ? 'active' : ''}" onclick={() => (filter = 'room')}>
          Room
        </button>
      </div>

      {@render content()}
    </div>
  </div>
{:else}
  <div class="content-wrap">
    <div class="toolbar">
      <div class="text-small text-light">{filtered.length} items</div>
      <div class="flex items-center gap-2">
        <div class="segmented">
          <button
            type="button"
            class="pill {filter === 'topic' ? 'active' : ''}"
            disabled={!$selectedTopic}
            onclick={() => (filter = 'topic')}
            title={!$selectedTopic ? 'Select a topic to filter' : 'Show current topic shares'}
          >
            Topic
          </button>
          <button type="button" class="pill {filter === 'room' ? 'active' : ''}" onclick={() => (filter = 'room')}>
            Room
          </button>
        </div>
        <Button variant="primary" size="small" icon="+" onclick={openShareModal} ariaLabel="Add" />
      </div>
    </div>

    <div class="content-body">
      {@render content()}
    </div>
  </div>
{/if}

{#snippet content()}
  <div class="content-inner">
    {#if $sharesLoading}
      <div class="flex items-center justify-center h-full">
        <LoadingSpinner message="Loading..." />
      </div>
    {:else if $sharesError}
      <div class="message message-error">{$sharesError}</div>
    {:else if filtered.length === 0}
      <div class="empty-state">
        <p class="text-small text-light">No shared items yet</p>
        <Button variant="primary" size="small" icon="+" onclick={openShareModal}>Add item</Button>
      </div>
    {:else}
      {#if grouped.documents.length > 0}
        <div class="group">
          <div class="group-title">Documents</div>
          <div class="group-list">
            {#each grouped.documents as s (s.id)}
              <div class="item">
                <button type="button" class="item-main" onclick={() => openDoc(s.url)}>
                  <div class="min-w-0">
                    <div class="text-small text-bold truncate">{s.title || s.fileName}</div>
                    <div class="text-small text-light truncate">{s.fileName}</div>
                  </div>
                </button>
                <button type="button" class="menu-button" title="More" onclick={(e) => openMenuForShare(s, e)}>
                  ?
                </button>
              </div>
            {/each}
          </div>
        </div>
      {/if}

      {#if grouped.images.length > 0}
        <div class="group">
          <div class="group-title">Images</div>
          <div class="image-grid">
            {#each grouped.images as s (s.id)}
              <div class="image-card">
                <a href={s.url} target="_blank" rel="noreferrer" title={s.fileName}>
                  <img src={s.url} alt={s.fileName} loading="lazy" />
                </a>
                <div class="image-meta">
                  <div class="text-small truncate">{s.title || s.fileName}</div>
                  <button type="button" class="menu-button" title="More" onclick={(e) => openMenuForShare(s, e)}>
                    ?
                  </button>
                </div>
              </div>
            {/each}
          </div>
        </div>
      {/if}

      {#if grouped.brainstorms.length > 0}
        <div class="group">
          <div class="group-title">Brainstorm</div>
          <div class="group-list">
            {#each grouped.brainstorms as s (s.id)}
              <div class="item">
                <button type="button" class="item-main" onclick={() => openBrain(s.url, s.boardId)}>
                  <div class="min-w-0">
                    <div class="text-small text-bold truncate">{s.title}</div>
                    <div class="text-small text-light truncate">
                      {s.createdByName || 'Unknown'}{formatWhen(s.createdAt) ? ` ・ ${formatWhen(s.createdAt)}` : ''}
                    </div>
                  </div>
                </button>
                <button type="button" class="menu-button" title="More" onclick={(e) => openMenuForShare(s, e)}>
                  ?
                </button>
              </div>
            {/each}
          </div>
        </div>
      {/if}
    {/if}
  </div>
{/snippet}

{#if menuItems}
  <ContextMenu items={menuItems} x={menuX} y={menuY} onClose={closeMenu} />
{/if}

<ShareUploadModal />

<style>
  .header-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    width: 100%;
  }

  .filter-row {
    display: flex;
    gap: var(--spacing-xs);
    padding: var(--spacing-sm);
    border-bottom: 1px solid var(--color-border);
  }

  .content-wrap {
    display: flex;
    flex-direction: column;
    height: 100%;
  }

  .toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    padding: 10px 12px;
    border-bottom: 1px solid var(--color-border);
    background: color-mix(in srgb, var(--color-background) 86%, var(--color-surface));
  }

  .segmented {
    display: flex;
    gap: var(--spacing-xs);
  }

  .content-body {
    overflow: auto;
    height: 100%;
  }

  .pill {
    border: 1px solid var(--color-border);
    background: transparent;
    color: var(--color-text-light);
    padding: 6px 10px;
    border-radius: 999px;
    cursor: pointer;
    font-size: var(--font-size-xs);
  }

  .pill.active {
    border-color: var(--color-primary);
    color: var(--color-text);
    background: color-mix(in srgb, var(--color-primary) 8%, var(--color-background));
  }

  .pill:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .group {
    padding: var(--spacing-sm);
  }

  .group-title {
    font-size: var(--font-size-xs);
    text-transform: uppercase;
    color: var(--color-text-light);
    margin-bottom: var(--spacing-xs);
    letter-spacing: 0.06em;
  }

  .group-list {
    display: flex;
    flex-direction: column;
    gap: var(--spacing-xs);
  }

  .item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--spacing-sm);
    padding: 10px;
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    background: var(--color-surface);
    text-align: left;
  }

  .item-main {
    border: none;
    background: transparent;
    color: inherit;
    padding: 0;
    width: 100%;
    cursor: pointer;
    text-align: left;
  }

  .item:hover {
    background: var(--color-surface-hover);
  }

  .menu-button {
    border: 1px solid var(--color-border);
    background: var(--color-background);
    color: var(--color-text);
    border-radius: 10px;
    width: 34px;
    height: 28px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    flex-shrink: 0;
  }

  .menu-button:hover {
    background: var(--color-surface-hover);
  }

  .image-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: var(--spacing-xs);
  }

  .image-card {
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    overflow: hidden;
    background: var(--color-surface);
  }

  .image-card img {
    width: 100%;
    height: 110px;
    object-fit: cover;
    display: block;
  }

  .image-meta {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--spacing-xs);
    padding: 8px;
  }

  .empty-state {
    display: flex;
    flex-direction: column;
    gap: 10px;
    align-items: center;
    justify-content: center;
    padding: 18px 12px;
    text-align: center;
  }

  .truncate {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
