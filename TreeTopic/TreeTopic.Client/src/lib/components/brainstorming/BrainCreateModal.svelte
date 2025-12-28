<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { currentRoom } from '$lib/stores/rooms';
  import { selectedTopic } from '$lib/stores/topics';
  import { api } from '$lib/api/client';
  import { isRequired } from '$lib/utils/validation';

  const modalId = 'brainstorm-create';
  let isOpen = $derived($activeModals.some((m) => m.id === modalId));

  let title = $state('');
  let description = $state('');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let titleError = $state<string | null>(null);
  let backgroundFile = $state<File | null>(null);
  let fileInput: HTMLInputElement | undefined = $state();

  async function handleCreate(e: Event) {
    e.preventDefault();

    titleError = null;
    error = null;

    if (!isRequired(title)) {
      titleError = 'Board title is required';
      return;
    }

    if (!$currentRoom) {
      error = 'Please select a room first';
      return;
    }

    isLoading = true;

    try {
      const formData = new FormData();
      formData.append('roomId', $currentRoom.id);
      formData.append('title', title.trim());
      formData.append('description', description.trim());
      if (backgroundFile) {
        formData.append('backgroundImage', backgroundFile);
      }

      // Create brainstorm board via API
      await api.post('/api/brainstorm', {
        roomId: $currentRoom.id,
        topicId: $selectedTopic?.id,
        title: title.trim(),
        description: description.trim(),
      });

      resetForm();
      ui.closeModal(modalId);
    } catch (err: any) {
      error = err.message || 'Failed to create brainstorm board';
    } finally {
      isLoading = false;
    }
  }

  function resetForm() {
    title = '';
    description = '';
    backgroundFile = null;
  }

  function handleClose() {
    ui.closeModal(modalId);
    resetForm();
  }

  function handleFileSelect(e: Event) {
    const input = e.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      if (file.type.startsWith('image/') || file.name.endsWith('.pdf')) {
        backgroundFile = file;
        error = null;
      } else {
        error = 'Please select an image or PDF file';
      }
    }
  }
</script>

<Modal {isOpen} title="Create Brainstorm Board" onClose={handleClose} size="medium">
  <form on:submit={handleCreate} class="space-y-4">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <Input
      label="Board Title"
      type="text"
      bind:value={title}
      placeholder="Enter board title"
      error={titleError}
      disabled={isLoading}
      required
    />

    <div class="flex flex-col gap-1">
      <label class="text-sm font-semibold text-text">Description</label>
      <textarea
        bind:value={description}
        placeholder="What is this brainstorm about? (optional)"
        disabled={isLoading}
        class="px-4 py-2 border border-border rounded-sm text-base bg-white transition-all
          placeholder:text-text-light
          focus:outline-none focus:border-primary
          disabled:bg-surface disabled:cursor-not-allowed disabled:opacity-60"
        rows="3"
      />
    </div>

    <div class="flex flex-col gap-2">
      <label class="text-sm font-semibold text-text">Background Image (Optional)</label>
      <input
        type="file"
        bind:this={fileInput}
        on:change={handleFileSelect}
        accept="image/*,.pdf"
        disabled={isLoading}
        class="hidden"
      />

      <button
        type="button"
        on:click={() => fileInput?.click()}
        disabled={isLoading}
        class="px-4 py-2 border-2 border-dashed border-border rounded-lg text-sm text-text-light hover:border-primary hover:text-primary transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
      >
        {backgroundFile ? `✓ ${backgroundFile.name}` : '📸 Choose background image'}
      </button>
    </div>

    <div class="flex gap-3 pt-4">
      <Button
        type="submit"
        variant="primary"
        size="base"
        fullWidth
        loading={isLoading}
        disabled={isLoading}
      >
        Create Board
      </Button>
      <Button
        type="button"
        variant="secondary"
        size="base"
        fullWidth
        disabled={isLoading}
        on:click={handleClose}
      >
        Cancel
      </Button>
    </div>
  </form>
</Modal>

<style>
  textarea {
    font-family: var(--font-family-base);
    resize: vertical;
  }
</style>
