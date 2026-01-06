<script lang="ts">
  import Button from '../common/Button.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { selectedTopic } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import { addMessage, cancelReply, replyTarget } from '$lib/stores/messages';
  import { isRequired } from '$lib/utils/validation';
  import { api } from '$lib/api/client';

  let subject = $state('');
  let content = $state('');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let fileInput: HTMLInputElement | undefined = $state();
  let selectedFiles = $state<File[]>([]);

  function normalizeMessage(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    return {
      id,
      topicId: raw?.topicId ?? raw?.TopicId ?? '',
      userId: raw?.applicationUserId ?? raw?.ApplicationUserId ?? raw?.userId ?? raw?.UserId ?? '',
      userName: raw?.userName ?? raw?.UserName ?? '',
      userDisplayName: raw?.userDisplayName ?? raw?.UserDisplayName ?? raw?.userName ?? raw?.UserName ?? '',
      userAvatar: raw?.userAvatar ?? raw?.UserAvatar ?? undefined,
      subject: raw?.subject ?? raw?.Subject ?? raw?.header ?? raw?.Header ?? '',
      content: raw?.content ?? raw?.Content ?? raw?.body ?? raw?.Body ?? '',
      replyToId: raw?.replyToId ?? raw?.ReplyToId ?? raw?.replyId ?? raw?.ReplyId ?? undefined,
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : undefined,
      attachments: [],
      isOwner: false,
      canEdit: false,
      canDelete: false,
      reactions: [],
      readBy: [],
      sortOrder: raw?.sortOrder ?? raw?.SortOrder ?? undefined,
    };
  }

  async function handleSubmit(e: Event) {
    e.preventDefault();

    error = null;

    if (!$currentRoom || !$selectedTopic) {
      error = 'Please select a room and topic first';
      return;
    }

    if (!isRequired(content)) {
      error = 'Message content cannot be empty';
      return;
    }

    isLoading = true;

    try {
      const tenant = api.getCurrentTenant();

      const trimmedContent = content.trim();
      const trimmedSubject = subject.trim();
      const header = trimmedSubject || trimmedContent.split('\n')[0]?.slice(0, 500) || 'Message';

      const form = new FormData();
      form.append('TopicId', $selectedTopic.id);
      form.append('Header', header);
      form.append('Body', trimmedContent);
      if ($replyTarget) {
        form.append('ReplyId', $replyTarget.id);
      }

      for (const file of selectedFiles) {
        form.append('Files', file);
      }

      const response = await api.post(`/${tenant}/api/Message`, form);
      addMessage({
        ...normalizeMessage(response),
        subject: trimmedSubject,
        content: trimmedContent,
      });
      subject = '';
      content = '';
      cancelReply();
      selectedFiles = [];
      if (fileInput) fileInput.value = '';
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to send message';
    } finally {
      isLoading = false;
    }
  }

  function handleFileSelect(e: Event) {
    const input = e.target as HTMLInputElement;
    selectedFiles = input.files ? Array.from(input.files) : [];
  }
</script>

<div class="panel-footer">
  <form on:submit={handleSubmit} class="spacing-sm flex flex-col w-full">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    {#if $replyTarget}
      <div class="replying-to">
        <div class="replying-to__bar"></div>
        <div class="replying-to__content">
          <div class="flex items-center gap-2">
            <span class="text-small text-light">Replying to</span>
            <span class="text-small text-bold">{$replyTarget.userDisplayName || $replyTarget.userName}</span>
            <button type="button" class="replying-to__cancel" on:click={() => cancelReply()} title="Cancel reply">×</button>
          </div>
          <div class="text-small text-light replying-to__text">
            {$replyTarget.subject || $replyTarget.content}
          </div>
        </div>
      </div>
    {/if}

    <input
      type="text"
      bind:value={subject}
      placeholder="Message subject (optional)"
      disabled={isLoading}
      class="form-input w-full text-small"
    />

    <textarea
      bind:value={content}
      placeholder="Type your message here..."
      disabled={isLoading}
      class="form-input w-full text-small"
      rows="3"
      style="resize: none;"
    />

    <div class="flex items-center gap-2">
      <input
        type="file"
        bind:this={fileInput}
        on:change={handleFileSelect}
        multiple
        disabled={isLoading}
        class="hidden"
      />

      <button
        type="button"
        on:click={() => fileInput?.click()}
        disabled={isLoading}
        class="button button-secondary button-small"
        title="Attach file"
      >
        Attach
      </button>

      <div class="flex-1"></div>

      <Button
        type="submit"
        variant="primary"
        size="small"
        loading={isLoading}
        disabled={isLoading || !$selectedTopic}
      >
        Send
      </Button>
    </div>

    {#if !$selectedTopic}
      <p class="text-small text-light text-center">
        Select a topic to send messages
      </p>
    {/if}
  </form>
</div>

<style>
  textarea {
    font-family: var(--font-family-base);
  }

  .replying-to {
    display: flex;
    gap: var(--spacing-sm);
    padding: var(--spacing-xs) var(--spacing-sm);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    background-color: var(--color-surface);
  }

  .replying-to__bar {
    width: 3px;
    border-radius: 2px;
    background-color: var(--color-primary);
    flex-shrink: 0;
  }

  .replying-to__content {
    min-width: 0;
    flex: 1;
  }

  .replying-to__text {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .replying-to__cancel {
    margin-left: auto;
    border: none;
    background: transparent;
    color: var(--color-text-light);
    cursor: pointer;
    font-size: var(--font-size-lg);
    line-height: 1;
  }

  .replying-to__cancel:hover {
    color: var(--color-primary);
  }
</style>
