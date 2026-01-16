<script lang="ts">
  import Modal from '$lib/components/common/Modal.svelte';
  import Button from '$lib/components/common/Button.svelte';
  import ErrorMessage from '$lib/components/common/ErrorMessage.svelte';
  import Input from '$lib/components/common/Input.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { currentRoom } from '$lib/stores/rooms';
  import { selectedTopic } from '$lib/stores/topics';
  import { shares, shareItems, denormalizeShareForAdd, loadShares } from '$lib/stores/shares';
  import { api } from '$lib/api/client';
  import { get } from 'svelte/store';

  const modalId = 'share-upload';
  let isOpen = $derived.by(() => $activeModals.some((m) => m.id === modalId));

  type Kind = 'document' | 'image' | 'brainstorm';
  let kind = $state<Kind>('document');
  let title = $state('');
  let fileInput: HTMLInputElement | undefined = $state();
  let file = $state<File | null>(null);
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let scope = $state<'room' | 'topic'>('topic');

  let fileTarget = $state<'new' | 'existing'>('new');
  let existingShareId = $state('');
  let updateExistingShare = $state(true);

  let boardTopicId = $state('');
  let roomTopics = $state<Array<{ id: string; title: string }>>([]);
  let roomTopicsLoading = $state(false);
  let roomTopicsError = $state<string | null>(null);
  let roomTopicsLoadToken = $state(0);
  let roomTopicsLastLoadKey = $state('');

  let brainstormBoards = $state<Array<{ id: string; title: string; topicId: string }>>([]);
  let brainstormSelectedBoardId = $state('');
  let brainstormBoardsLoading = $state(false);
  let brainstormBoardsError = $state<string | null>(null);
  let brainstormBoardsLoadToken = $state(0);
  let brainstormBoardsLastLoadKey = $state('');
  let newBoardTitle = $state('');
  let newBoardDescription = $state('');
  let newBoardError = $state<string | null>(null);
  let isCreatingBoard = $state(false);
  let wasOpen = $state(false);

  $effect(() => {
    if (isOpen && !wasOpen) {
      error = null;
      isLoading = false;
      file = null;
      title = '';
      fileTarget = 'new';
      existingShareId = '';
      updateExistingShare = true;
      boardTopicId = $selectedTopic?.id ?? '';
      roomTopics = [];
      roomTopicsLoading = false;
      roomTopicsError = null;
      roomTopicsLoadToken = 0;
      roomTopicsLastLoadKey = '';
      brainstormBoards = [];
      brainstormSelectedBoardId = '';
      brainstormBoardsLoading = false;
      brainstormBoardsError = null;
      brainstormBoardsLoadToken = 0;
      brainstormBoardsLastLoadKey = '';
      newBoardTitle = '';
      newBoardDescription = '';
      newBoardError = null;
      isCreatingBoard = false;
      scope = $selectedTopic ? 'topic' : 'room';
      kind = 'document';
    }
    wasOpen = isOpen;
  });

  $effect(() => {
    if (!isOpen) return;
    if (scope === 'topic' && !$selectedTopic) scope = 'room';
  });

  $effect(() => {
    if (!isOpen) return;
    if (kind !== 'brainstorm') return;

    const tenant = api.getCurrentTenant();
    const topicId = $selectedTopic?.id ?? boardTopicId ?? '';
    const key = `${tenant}:${topicId}`;
    if (key === brainstormBoardsLastLoadKey) return;
    brainstormBoardsLastLoadKey = key;

    void loadBrainstormBoards();
  });

  $effect(() => {
    if (!isOpen) return;
    if (kind !== 'brainstorm') return;
    if (!$currentRoom) return;

    const tenant = api.getCurrentTenant();
    const key = `${tenant}:${$currentRoom.id}`;
    if (key === roomTopicsLastLoadKey) return;
    roomTopicsLastLoadKey = key;

    void loadRoomTopics();
  });

  function handleClose() {
    ui.closeModal(modalId);
  }

  function handleFileSelect(e: Event) {
    const input = e.target as HTMLInputElement;
    file = input.files && input.files.length > 0 ? input.files[0] : null;
  }

  async function loadBrainstormBoards() {
    const token = ++brainstormBoardsLoadToken;
    brainstormBoardsError = null;
    brainstormBoardsLoading = true;

    try {
      const tenant = api.getCurrentTenant();
      const topicId = $selectedTopic?.id ?? boardTopicId ?? null;

      const raw = topicId
        ? await api.get<any[]>(`/${tenant}/api/Brainstorm/topic/${topicId}`)
        : await api.get<any[]>(`/${tenant}/api/Brainstorm`);

      const list = Array.isArray(raw) ? raw : [];
      const normalized = list
        .map((b) => ({
          id: b?.id ?? b?.Id ?? '',
          title: b?.title ?? b?.Title ?? b?.name ?? b?.Name ?? '(untitled)',
          topicId: b?.topicId ?? b?.TopicId ?? '',
        }))
        .filter((b) => b.id);

      if (token !== brainstormBoardsLoadToken) return;

      brainstormBoards = normalized;
      if (brainstormSelectedBoardId && !brainstormBoards.some((b) => b.id === brainstormSelectedBoardId)) {
        brainstormSelectedBoardId = '';
      }
    } catch (err: unknown) {
      if (token !== brainstormBoardsLoadToken) return;
      brainstormBoardsError = err instanceof Error ? err.message : 'Failed to load boards';
    } finally {
      if (token !== brainstormBoardsLoadToken) return;
      brainstormBoardsLoading = false;
    }
  }

  async function loadRoomTopics() {
    const token = ++roomTopicsLoadToken;
    roomTopicsError = null;
    roomTopicsLoading = true;

    if (!$currentRoom) {
      roomTopics = [];
      roomTopicsLoading = false;
      return;
    }

    try {
      const tenant = api.getCurrentTenant();
      const raw = await api.get<any[]>(`/${tenant}/api/Topic/room/${$currentRoom.id}`);
      const list = Array.isArray(raw) ? raw : [];

      const normalized = list
        .map((t) => ({
          id: t?.id ?? t?.Id ?? '',
          title: t?.title ?? t?.Title ?? '(untitled)',
        }))
        .filter((t) => t.id);

      if (token !== roomTopicsLoadToken) return;

      roomTopics = normalized;
      if (!$selectedTopic?.id && !boardTopicId && roomTopics.length > 0) {
        boardTopicId = roomTopics[0].id;
        brainstormBoardsLastLoadKey = '';
        void loadBrainstormBoards();
      }
    } catch (err: unknown) {
      if (token !== roomTopicsLoadToken) return;
      roomTopicsError = err instanceof Error ? err.message : 'Failed to load topics';
    } finally {
      if (token !== roomTopicsLoadToken) return;
      roomTopicsLoading = false;
    }
  }

  async function createBoard() {
    newBoardError = null;

    if (!$currentRoom) {
      newBoardError = 'Please select a room first';
      return;
    }

    if (!$selectedTopic?.id && !boardTopicId) {
      await loadRoomTopics();
    }

    const topicIdForBoard = $selectedTopic?.id ?? boardTopicId;
    if (!topicIdForBoard) {
      newBoardError = 'Please select a topic first (boards are created under a topic)';
      return;
    }

    if (!newBoardTitle.trim()) {
      newBoardError = 'Board title is required';
      return;
    }

    isCreatingBoard = true;
    try {
      const tenant = api.getCurrentTenant();
      const created = await api.post<any>(`/${tenant}/api/brainstorm`, {
        topicId: topicIdForBoard,
        title: newBoardTitle.trim(),
        description: newBoardDescription.trim(),
      });

      const id = created?.id ?? created?.Id ?? '';
      if (!id) {
        newBoardError = 'Board created but could not read its ID';
        return;
      }

      brainstormSelectedBoardId = id;
      if (!title.trim()) title = newBoardTitle.trim();

      newBoardTitle = '';
      newBoardDescription = '';

      await loadBrainstormBoards();
    } catch (err: unknown) {
      newBoardError = err instanceof Error ? err.message : 'Failed to create brainstorm board';
    } finally {
      isCreatingBoard = false;
    }
  }

  async function refreshSharesAfterMutation() {
    const tenant = api.getCurrentTenant();
    const roomId = get(currentRoom)?.id ?? '';
    if (!tenant || !roomId) return;
    const topicId = scope === 'topic' ? get(selectedTopic)?.id ?? null : null;
    await loadShares({ tenant, roomId, topicId });
  }

  async function handleUpload() {
    error = null;

    if (!$currentRoom) {
      error = 'Please select a room first';
      return;
    }

    const tenant = api.getCurrentTenant();
    const topicId = scope === 'topic' ? $selectedTopic?.id ?? null : null;

    isLoading = true;
    try {
      if (kind === 'brainstorm') {
        const boardId = brainstormSelectedBoardId;
        if (!boardId) {
          error = 'Board is required';
          return;
        }

        const payload = {
          roomId: $currentRoom.id,
          topicId,
          boardId,
          title: title.trim() || undefined,
        };

        const created = await api.post<any>(`/${tenant}/api/Share/room/${$currentRoom.id}/brainstorm`, payload);
        shares.addShare(denormalizeShareForAdd(created));
        await refreshSharesAfterMutation();
        ui.closeModal(modalId);
        return;
      }

      if (!file) {
        error = 'Please choose a file';
        return;
      }

      if (fileTarget === 'existing' && !existingShareId) {
        error = 'Please select an existing share';
        return;
      }

      const form = new FormData();
      form.append('file', file);
      if (topicId) form.append('topicId', topicId);
      form.append('kind', kind);
      if (title.trim()) form.append('title', title.trim());
      if (fileTarget === 'existing' && existingShareId) {
        form.append('shareId', existingShareId);
        form.append('updateShare', updateExistingShare ? 'true' : 'false');
      }

      const created = await api.post<any>(`/${tenant}/api/Share/room/${$currentRoom.id}`, form);
      shares.addShare(denormalizeShareForAdd(created));
      await refreshSharesAfterMutation();
      ui.closeModal(modalId);
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to upload';
    } finally {
      isLoading = false;
    }
  }
</script>

<Modal {isOpen} title="Add shared item" onClose={handleClose} size="medium">
  <div class="spacing-md">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <div class="share-kind-tabs" role="tablist" aria-label="Share type">
      <button
        type="button"
        role="tab"
        aria-selected={kind === 'document'}
        class="tab {kind === 'document' ? 'active' : ''}"
        onclick={(e) => {
          e.stopPropagation();
          kind = 'document';
        }}
      >
        Document
      </button>
      <button
        type="button"
        role="tab"
        aria-selected={kind === 'image'}
        class="tab {kind === 'image' ? 'active' : ''}"
        onclick={(e) => {
          e.stopPropagation();
          kind = 'image';
        }}
      >
        Image
      </button>
      <button
        type="button"
        role="tab"
        aria-selected={kind === 'brainstorm'}
        class="tab {kind === 'brainstorm' ? 'active' : ''}"
        onclick={(e) => {
          e.stopPropagation();
          kind = 'brainstorm';
        }}
      >
        Brainstorm
      </button>
    </div>

    <div class="share-scope">
      <div class="text-small text-light">Scope</div>
      <div class="share-scope-buttons">
        <button
          type="button"
          class="pill {scope === 'topic' ? 'active' : ''}"
          disabled={!$selectedTopic}
          onclick={() => (scope = 'topic')}
          title={!$selectedTopic ? 'Select a topic to share to a topic' : 'Share to current topic'}
        >
          Current topic
        </button>
        <button type="button" class="pill {scope === 'room' ? 'active' : ''}" onclick={() => (scope = 'room')}>
          Room
        </button>
      </div>
    </div>

    <Input label="Title (optional)" type="text" bind:value={title} placeholder="Display title" disabled={isLoading} />

    {#if kind === 'brainstorm'}
      <div class="spacing-sm">
        <div class="flex items-center justify-between">
          <label for="share-board-select" class="text-small text-light">Board</label>
          <div class="flex items-center gap-2">
            <Button
              type="button"
              variant="secondary"
              size="small"
              disabled={isLoading || brainstormBoardsLoading}
              onclick={loadBrainstormBoards}
            >
              Refresh
            </Button>
          </div>
        </div>

        {#if brainstormBoardsLoading}
          <div class="text-small text-light">Loading boards...</div>
        {:else if brainstormBoardsError}
          <div class="text-small text-light">
            {brainstormBoardsError}
            <button type="button" class="action-link" onclick={loadBrainstormBoards}>Retry</button>
          </div>
        {:else}
          <select id="share-board-select" class="select" bind:value={brainstormSelectedBoardId} disabled={isLoading}>
            <option value="">Select a board</option>
            {#each brainstormBoards as b (b.id)}
              <option value={b.id}>{b.title}</option>
            {/each}
          </select>
        {/if}

        <details class="details">
          <summary class="text-small text-light">Create a new board</summary>
          <div class="spacing-sm padding-top-sm">
            {#if newBoardError}
              <ErrorMessage message={newBoardError} onDismiss={() => (newBoardError = null)} />
            {/if}

            {#if !$selectedTopic}
              <div class="spacing-sm">
                <label for="share-topic-select" class="text-small text-light">Topic</label>
                {#if roomTopicsLoading}
                  <div class="text-small text-light">Loading topics...</div>
                {:else if roomTopicsError}
                  <div class="text-small text-light">{roomTopicsError}</div>
                {:else}
                  {#if roomTopics.length === 0}
                    <div class="text-small text-light">No topics in this room. Create a topic first.</div>
                    <Button
                      type="button"
                      variant="secondary"
                      size="small"
                      disabled={isLoading || isCreatingBoard}
                      onclick={() => ui.openModal({ id: 'topic-create', title: 'Create Topic', type: 'custom' })}
                    >
                      Create topic
                    </Button>
                  {/if}
                  <select
                    id="share-topic-select"
                    class="select"
                    bind:value={boardTopicId}
                    disabled={isLoading || isCreatingBoard || roomTopicsLoading}
                    onchange={() => {
                      brainstormBoardsLastLoadKey = '';
                      void loadBrainstormBoards();
                    }}
                  >
                    <option value="">Select a topic</option>
                    {#each roomTopics as t (t.id)}
                      <option value={t.id}>{t.title}</option>
                    {/each}
                  </select>
                {/if}
              </div>
            {/if}

            <Input
              label="Board title"
              type="text"
              bind:value={newBoardTitle}
              placeholder="Enter board title"
              disabled={isLoading || isCreatingBoard}
            />
            <Input
              label="Description (optional)"
              type="text"
              bind:value={newBoardDescription}
              placeholder="What is this brainstorm about?"
              disabled={isLoading || isCreatingBoard}
            />

            <div class="flex gap-2">
              <Button
                type="button"
                variant="secondary"
                size="small"
                loading={isCreatingBoard}
                disabled={isLoading || isCreatingBoard}
                onclick={createBoard}
              >
                Create board
              </Button>
              {#if brainstormSelectedBoardId}
                <Button
                  type="button"
                  variant="secondary"
                  size="small"
                  disabled={isLoading || isCreatingBoard}
                  onclick={() => {
                  if (brainstormSelectedBoardId && brainstormSelectedBoardId !== 'undefined' && brainstormSelectedBoardId !== 'null') {
                    window.open(`/${api.getCurrentTenant()}/brainstorm/${brainstormSelectedBoardId}`, '_blank');
                  }
                  }}
                >
                  Open
                </Button>
              {/if}
            </div>
            <div class="text-small text-light">Note: board creation requires a selected topic.</div>
          </div>
        </details>
      </div>
    {:else}
      <div class="spacing-sm">
        <label for="share-target-select" class="text-small text-light">Target</label>
        <select
          id="share-target-select"
          class="select"
          bind:value={fileTarget}
          disabled={isLoading}
          onchange={() => {
            if (fileTarget === 'new') {
              existingShareId = '';
              updateExistingShare = true;
            }
          }}
        >
          <option value="new">Create new shared item</option>
          <option value="existing">Use an existing shared item</option>
        </select>

        {#if fileTarget === 'existing'}
          <div class="spacing-sm padding-top-sm">
            <label for="share-existing-select" class="text-small text-light">Existing share</label>
            <select id="share-existing-select" class="select" bind:value={existingShareId} disabled={isLoading}>
              <option value="">Select...</option>
              {#each $shareItems.filter((x) => x.kind === kind) as item (item.id)}
                <option value={item.id}>{item.title || item.fileName || item.id}</option>
              {/each}
            </select>

            <label class="text-small text-light">
              <input type="checkbox" bind:checked={updateExistingShare} disabled={isLoading} />
              Update existing share to latest version
            </label>
            <div class="text-small text-light">
              If unchecked, a new share entry is created for this upload (the old one stays as-is).
            </div>
          </div>
        {/if}
      </div>

      <input
        type="file"
        bind:this={fileInput}
        onchange={handleFileSelect}
        accept={kind === 'image' ? 'image/*' : undefined}
        disabled={isLoading}
      />
      {#if file}
        <div class="text-small text-light">Selected: {file.name}</div>
      {/if}
    {/if}

    <div class="flex spacing-md padding-top-md">
      <Button
        type="button"
        variant="primary"
        size="base"
        loading={isLoading}
        disabled={isLoading || !$currentRoom}
        onclick={handleUpload}
      >
        Share
      </Button>
      <Button type="button" variant="secondary" size="base" disabled={isLoading} onclick={handleClose}>Cancel</Button>
    </div>
  </div>
</Modal>

<style>
  .share-kind-tabs {
    display: flex;
    gap: var(--spacing-xs);
    padding-bottom: var(--spacing-sm);
  }

  .tab {
    flex: 1;
    border: 1px solid var(--color-border);
    background: var(--color-surface);
    color: var(--color-text);
    padding: 8px 10px;
    border-radius: var(--border-radius-sm);
    cursor: pointer;
  }

  .tab.active {
    border-color: var(--color-primary);
    background: color-mix(in srgb, var(--color-primary) 8%, var(--color-surface));
  }

  .share-scope {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .share-scope-buttons {
    display: flex;
    gap: var(--spacing-xs);
  }

  .pill {
    border: 1px solid var(--color-border);
    background: transparent;
    color: var(--color-text-light);
    padding: 6px 10px;
    border-radius: 999px;
    cursor: pointer;
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

  .details summary {
    cursor: pointer;
    user-select: none;
  }

  .action-link {
    border: none;
    background: transparent;
    color: var(--color-primary);
    cursor: pointer;
    font-size: var(--font-size-xs);
    padding: 4px 6px;
  }

  .select {
    width: 100%;
    padding: 10px;
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    background: var(--color-background);
    color: var(--color-text);
  }
</style>
