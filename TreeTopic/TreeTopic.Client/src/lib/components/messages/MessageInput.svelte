<script lang="ts">
  import Button from '../common/Button.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { selectedTopic } from '$lib/stores/topics';
  import { currentRoom } from '$lib/stores/rooms';
  import { addMessage } from '$lib/stores/messages';
  import { isRequired } from '$lib/utils/validation';
  import { api } from '$lib/api/client';

  let subject = $state('');
  let content = $state('');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let fileInput: HTMLInputElement | undefined = $state();

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
      const response = await api.post(`/${tenant}/api/Message`, {
        topicId: $selectedTopic.id,
        subject: subject.trim(),
        content: content.trim(),
      });

      addMessage(response);
      subject = '';
      content = '';
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to send message';
    } finally {
      isLoading = false;
    }
  }

  function handleFileSelect(e: Event) {
    const input = e.target as HTMLInputElement;
    const files = input.files;

    if (files && files.length > 0) {
      // File upload logic would go here
      // For now, just show a placeholder
      error = 'File upload feature coming soon';
    }
  }
</script>

<div class="panel-footer">
  <form on:submit={handleSubmit} class="spacing-sm">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
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
</style>
