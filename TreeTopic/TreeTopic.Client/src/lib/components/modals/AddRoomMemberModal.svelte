<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { ui, activeModals } from '$lib/stores/ui';
  import { api } from '$lib/api/client';

  const modalId = 'add-room-member';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);
  let tenant = $derived.by(() => modal?.data?.tenant ?? '');
  let roomId = $derived.by(() => modal?.data?.roomId ?? '');

  let search = $state('');
  let userCandidates = $state<Array<{ id: string; userName?: string; displayName?: string; email?: string }>>([]);
  let selectedCandidateId = $state('');
  let isLoading = $state(false);
  let isSearching = $state(false);
  let error = $state<string | null>(null);

  let searchTimeout: ReturnType<typeof setTimeout> | null = null;

  $effect(() => {
    if (isOpen) {
      search = '';
      userCandidates = [];
      selectedCandidateId = '';
      error = null;
    }
  });

  async function searchUserCandidates() {
    if (search.length < 2) {
      userCandidates = [];
      return;
    }

    try {
      isSearching = true;
      error = null;
      const candidates = await api.get<any>(
        `/${tenant}/api/rooms/${roomId}/users/candidates`,
        { params: { search } }
      );
      userCandidates = candidates;
    } catch (err) {
      console.error('Failed to load user candidates:', err);
      error = err instanceof Error ? err.message : 'Failed to search users';
    } finally {
      isSearching = false;
    }
  }

  function handleSearchChange(value: string) {
    search = value;
    if (searchTimeout) clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
      searchUserCandidates();
    }, 300);
  }

  async function addUserToRoom() {
    if (!selectedCandidateId) return;

    try {
      isLoading = true;
      error = null;
      await api.post(`/${tenant}/api/roomusers/room/${roomId}`, {
        applicationUserId: selectedCandidateId
      });

      // Close modal and trigger refresh
      ui.closeModal(modalId);
      // Emit event for parent to refresh
      if (modal?.data?.onSuccess && typeof modal.data.onSuccess === 'function') {
        (modal.data.onSuccess as () => void)();
      }
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to add member';
    } finally {
      isLoading = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
  }

  function getCandidateDisplayName(candidate: { displayName?: string; userName?: string }): string {
    return candidate.displayName || candidate.userName || 'Unknown';
  }
</script>

<Modal {isOpen} title="Add Room Member" onClose={handleClose} size="medium" closeButton={!isLoading}>
  <div class="flex flex-col h-full bg-white">
    <!-- Error message -->
    {#if error}
      <div class="p-4 bg-red-50 border-b border-red-200 text-red-800 text-sm flex justify-between items-center">
        <span>{error}</span>
        <button onclick={() => (error = null)} class="underline hover:no-underline">Close</button>
      </div>
    {/if}

    <!-- Content -->
    <div class="flex-1 overflow-auto p-6 space-y-6">
      <!-- Search section -->
      <div class="space-y-2">
        <label class="block text-sm font-medium text-text">Search Users</label>
        <input
          type="text"
          bind:value={search}
          oninput={(e) => handleSearchChange(e.currentTarget.value)}
          placeholder="Search by name or email..."
          disabled={isLoading || isSearching}
          class="w-full px-3 py-2 border border-border rounded focus:outline-none focus:border-primary disabled:opacity-50"
          autofocus
        />
        <p class="text-xs text-text-light">Enter at least 2 characters to search</p>
      </div>

      {#if isSearching}
        <div class="text-center py-8">
          <div class="inline-block w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          <p class="mt-2 text-sm text-text-light">Searching...</p>
        </div>
      {:else if search.length >= 2 && userCandidates.length > 0}
        <div class="space-y-2">
          <label class="block text-sm font-medium text-text">Select a User</label>
          <div class="border border-border rounded-lg overflow-hidden">
            <div class="divide-y divide-border">
              {#each userCandidates as candidate}
                <button
                  onclick={() => selectedCandidateId = candidate.id}
                  class="w-full text-left p-4 hover:bg-surface transition-colors flex items-center justify-between {selectedCandidateId === candidate.id
                    ? 'bg-primary bg-opacity-5'
                    : ''}"
                  disabled={isLoading}
                >
                  <div>
                    <p class="font-medium text-text">{getCandidateDisplayName(candidate)}</p>
                    <p class="text-sm text-text-light">@{candidate.userName}</p>
                  </div>
                  {#if selectedCandidateId === candidate.id}
                    <div class="w-6 h-6 flex items-center justify-center bg-primary text-white rounded-full text-sm">
                      ✓
                    </div>
                  {/if}
                </button>
              {:else}
                <p class="p-4 text-center text-text-light text-sm">No users found</p>
              {/each}
            </div>
          </div>
        </div>
      {:else if search.length >= 2 && userCandidates.length === 0}
        <div class="border border-border rounded-lg p-8 text-center text-text-light">
          <p>No users found matching "{search}"</p>
        </div>
      {:else if search.length > 0 && search.length < 2}
        <p class="text-sm text-text-light text-center">Please enter at least 2 characters</p>
      {/if}
    </div>

    <!-- Actions -->
    <div class="p-4 border-t border-border flex gap-2 justify-end">
      <button
        onclick={addUserToRoom}
        disabled={!selectedCandidateId || isLoading}
        class="px-4 py-2 bg-primary text-white rounded hover:bg-opacity-90 transition-colors text-sm font-medium disabled:opacity-50"
      >
        {isLoading ? 'Adding...' : 'Add Member'}
      </button>
      <button
        onclick={handleClose}
        disabled={isLoading}
        class="px-4 py-2 bg-surface border border-border rounded hover:bg-opacity-80 transition-colors text-sm font-medium"
      >
        Cancel
      </button>
    </div>
  </div>
</Modal>

<style>
  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }

  :global(.animate-spin) {
    animation: spin 1s linear infinite;
  }
</style>
