<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/stores';
  import { onMount } from 'svelte';
  import type { PageData } from './$types';
  import AuthShell from '$lib/components/layout/AuthShell.svelte';

  export let data: PageData;

  const tenants = data.tenants ?? [];
  const loadError = data.error;

  let selectedTenant = '';
  let isNavigating = false;

  onMount(() => {
    const url = new URL($page.url.toString());
    if (url.searchParams.has('room')) {
      url.searchParams.delete('room');
      goto(url, { replaceState: true, keepFocus: true, noScroll: true });
    }
  });

  async function handleSelectTenant() {
    if (!selectedTenant || isNavigating) return;
    isNavigating = true;
    await goto(`/${selectedTenant}/login`, {
      keepFocus: true,
      noScroll: true,
    });
    isNavigating = false;
  }
</script>

<svelte:head>
  <title>Select Workspace - TreeTopic</title>
</svelte:head>

<AuthShell
  title="TreeTopic"
  subtitle="Collaborative discussion platform"
  description="Choose your workspace to continue."
>
  {#if loadError}
    <div class="message message-error">{loadError}</div>
  {/if}

  {#if tenants.length === 0}
    <div class="message message-info text-center">
      {#if !loadError}
        No workspaces available right now.
      {:else}
        Unable to load workspaces.
      {/if}
    </div>
  {:else}
    <label class="form-group">
      <span class="form-label">Workspace</span>
      <select class="form-input" bind:value={selectedTenant}>
        <option value="">-- Select a workspace --</option>
        {#each tenants as tenant (tenant.identifier)}
          <option value={tenant.identifier}>{tenant.name}</option>
        {/each}
      </select>
    </label>
  {/if}

  <div class="auth-card__actions">
    <button
      type="button"
      class="button button-primary"
      on:click={handleSelectTenant}
      disabled={!selectedTenant || tenants.length === 0 || isNavigating}
    >
      Continue
    </button>
  </div>

  <span slot="footer">&copy; 2025 TreeTopic. Secured by OIDC authentication.</span>
</AuthShell>
