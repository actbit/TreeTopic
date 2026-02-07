<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import Button from '../common/Button.svelte';
  import Input from '../common/Input.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { messageList, updateMessage } from '$lib/stores/messages';
  import { isRequired } from '$lib/utils/validation';
  import { api } from '$lib/api/client';
  import { getMessageAnchorId } from '$lib/utils/messageAnchor';

  const modalId = 'message-edit';

  let modalConfig = $derived.by(
    () => $activeModals.find((m) => m.id === modalId) ?? null
  );
  let isOpen = $derived.by(() => modalConfig !== null);
  let messageId = $derived.by(() => modalConfig?.data?.messageId as string | undefined);

  let message = $derived.by(() => {
    if (!messageId) return null;
    return $messageList.find((m) => m.id === messageId) ?? null;
  });

  let subject = $state('');
  let content = $state('');
  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let contentError = $state<string | null>(null);

  $effect(() => {
    if (!isOpen) return;
    if (!message) return;
    subject = message.subject ?? '';
    content = message.content ?? '';
    error = null;
    contentError = null;
  });

  async function handleSave(e: Event) {
    e.preventDefault();

    error = null;
    contentError = null;

    if (!messageId || !message) {
      error = 'Message not found';
      return;
    }

    if (!isRequired(content)) {
      contentError = 'Message content cannot be empty';
      return;
    }

    isLoading = true;

    try {
      const tenant = api.getCurrentTenant();
      const trimmedSubject = subject.trim();
      const trimmedContent = content.trim();

      await api.put(`/${tenant}/api/message/${messageId}`, {
        header: trimmedSubject || undefined,
        body: trimmedContent,
      });

      updateMessage(messageId, {
        subject: trimmedSubject,
        content: trimmedContent,
        updatedAt: new Date(),
      });

      ui.closeModal(modalId);
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to update message';
    } finally {
      isLoading = false;
    }
  }

  async function copyMessageUrl() {
    if (typeof window === 'undefined') return;
    if (!messageId) return;

    const url = new URL(window.location.href);
    url.hash = getMessageAnchorId(messageId);
    const link = url.toString();

    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(link);
      } else {
        const textarea = document.createElement('textarea');
        textarea.value = link;
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        document.body.appendChild(textarea);
        textarea.select();
        document.execCommand('copy');
        document.body.removeChild(textarea);
      }

      window.history.replaceState(null, '', link);
      ui.addNotification({ type: 'success', message: 'Message URL copied' });
    } catch {
      ui.addNotification({ type: 'error', message: 'Failed to copy message URL' });
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
  }
</script>

<Modal {isOpen} title="Edit Message" onClose={handleClose} size="medium">
  <form onsubmit={handleSave} class="spacing-md">
    {#if error}
      <ErrorMessage message={error} onDismiss={() => (error = null)} />
    {/if}

    <Input
      label="Subject (optional)"
      type="text"
      bind:value={subject}
      placeholder="Enter subject (optional)"
      disabled={isLoading}
    />

    <div class="form-group">
      <label for="message-edit-content" class="form-label">Content</label>
      <textarea
        id="message-edit-content"
        bind:value={content}
        placeholder="Type your message..."
        disabled={isLoading}
        class="form-input"
        style="resize: vertical; min-height: 120px;"
      ></textarea>
      {#if contentError}
        <p class="form-error">{contentError}</p>
      {/if}
    </div>

    <div class="flex spacing-md padding-top-md">
      <Button
        type="button"
        variant="secondary"
        size="base"
        disabled={isLoading || !messageId}
        onclick={copyMessageUrl}
      >
        Copy URL
      </Button>
      <Button
        type="submit"
        variant="primary"
        size="base"
        loading={isLoading}
        disabled={isLoading}
      >
        Save
      </Button>
      <Button
        type="button"
        variant="secondary"
        size="base"
        disabled={isLoading}
        onclick={handleClose}
      >
        Cancel
      </Button>
    </div>
  </form>
</Modal>
