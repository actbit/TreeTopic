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
    } catch (err: any) {
      error = err.message || 'Failed to send message';
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

<div class="border-t border-border bg-white p-4">
  <form on:submit={handleSubmit} class="space-y-3">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <input
      type="text"
      bind:value={subject}
      placeholder="Message subject (optional)"
      disabled={isLoading}
      class="w-full px-3 py-2 border border-border rounded-sm text-sm bg-white transition-all
        placeholder:text-text-light
        focus:outline-none focus:border-primary
        disabled:bg-surface disabled:cursor-not-allowed disabled:opacity-60"
    />

    <textarea
      bind:value={content}
      placeholder="Type your message here..."
      disabled={isLoading}
      class="w-full px-3 py-2 border border-border rounded-sm text-sm bg-white transition-all
        placeholder:text-text-light
        focus:outline-none focus:border-primary
        disabled:bg-surface disabled:cursor-not-allowed disabled:opacity-60
        resize-none"
      rows="3"
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
        class="p-2 text-text-light hover:text-primary hover:bg-surface rounded transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
        title="Attach file"
      >
        📎
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
      <p class="text-xs text-text-light text-center">
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
