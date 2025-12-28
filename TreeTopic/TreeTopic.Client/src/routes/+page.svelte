<script lang="ts">
  import { goto } from '$app/navigation';
  import { onMount } from 'svelte';
  import { getAllPublicTenants } from '$lib/api/tenants';
  import type { PublicTenantInfo } from '$lib/api/tenants';

  let tenants: PublicTenantInfo[] = [];
  let selectedTenant: string | null = null;
  let isLoading = true;
  let error: string | null = null;

  onMount(async () => {
    try {
      console.log('Loading tenants...');
      tenants = await getAllPublicTenants();
      console.log('Tenants loaded:', tenants);
      isLoading = false;
      if (tenants.length === 0) {
        error = 'No tenants available';
        console.warn('No tenants returned from API');
      }
    } catch (err) {
      isLoading = false;
      error = 'Failed to load tenants';
      console.error('Error loading tenants:', err);
    }
  });

  function handleSelectTenant() {
    if (selectedTenant) {
      goto(`/${selectedTenant}/login`);
    }
  }
</script>

<svelte:head>
  <title>Select Workspace - TreeTopic</title>
</svelte:head>

<div class="min-h-screen bg-gradient-to-br from-primary to-secondary flex items-center justify-center p-4">
  <div class="max-w-md w-full">
    <div class="bg-white rounded-lg shadow-xl p-8">
      <div class="text-center mb-8">
        <h1 class="text-3xl font-bold text-primary mb-2">🌳 TreeTopic</h1>
        <p class="text-text-secondary">Select your workspace</p>
      </div>

      {#if isLoading}
        <div class="text-center py-8">
          <p class="text-text-light">Loading workspaces...</p>
        </div>
      {:else if error}
        <div class="text-center py-8">
          <p class="text-red-500">{error}</p>
        </div>
      {:else if tenants.length > 0}
        <div class="space-y-4">
          <label class="block">
            <span class="text-sm font-semibold text-text mb-2 block">Workspace</span>
            <select
              bind:value={selectedTenant}
              class="w-full px-4 py-2 border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary bg-white text-text"
            >
              <option value={null}>-- Select a workspace --</option>
              {#each tenants as tenant (tenant.identifier)}
                <option value={tenant.identifier}>{tenant.name}</option>
              {/each}
            </select>
          </label>

          <button
            on:click={handleSelectTenant}
            disabled={!selectedTenant}
            class="w-full px-4 py-2 bg-primary text-white font-semibold rounded-lg hover:bg-primary-dark disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
          >
            Continue
          </button>
        </div>
      {:else}
        <div class="text-center py-8">
          <p class="text-text-light">No workspaces available</p>
        </div>
      {/if}

      <div class="mt-8 pt-8 border-t border-border">
        <p class="text-center text-sm text-text-light">
          Secured by OIDC authentication
        </p>
      </div>
    </div>

    <div class="mt-8 text-center text-text-light text-sm">
      <p>&copy; 2025 TreeTopic. All rights reserved.</p>
    </div>
  </div>
</div>

<style>
  :global(.bg-gradient-to-br) {
    background: linear-gradient(135deg, var(--color-primary) 0%, var(--color-secondary) 100%);
  }
</style>
