<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/stores';
  import { onMount } from 'svelte';
  import { getAllPublicTenants } from '$lib/api/tenants';
  import type { PublicTenantInfo } from '$lib/api/tenants';

  let tenants: PublicTenantInfo[] = [];
  let selectedTenant: string | null = null;
  let isLoading = true;
  let error: string | null = null;

  onMount(async () => {
    const url = new URL($page.url.toString());
    if (url.searchParams.has('room')) {
      url.searchParams.delete('room');
      await goto(url, { replaceState: true, keepFocus: true, noScroll: true });
    }
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

  async function handleSelectTenant() {
    if (selectedTenant) {
      try {
        console.log('Navigating to:', `/${selectedTenant}/login`);
        await goto(`/${selectedTenant}/login`);
      } catch (err) {
        console.error('Navigation error:', err);
        error = 'Failed to navigate to login page';
      }
    }
  }
</script>

<svelte:head>
  <title>Select Workspace - TreeTopic</title>
</svelte:head>

<div class="workspace-container">
  <div class="workspace-card-wrapper">
    <div class="workspace-card">
      <div class="logo-section">
        <h1>TreeTopic</h1>
        <p>Collaborative discussion platform</p>
      </div>

      <div class="welcome-section">
        <h2>Select workspace</h2>
        <p>Choose your workspace to continue</p>
      </div>

      {#if isLoading}
        <div class="status-message">
          <p>Loading workspaces...</p>
        </div>
      {:else if error}
        <div class="status-message error">
          <p>{error}</p>
        </div>
      {:else if tenants.length > 0}
        <div class="form-section">
          <label>
            <span class="label-text">Workspace</span>
            <select bind:value={selectedTenant}>
              <option value={null}>-- Select a workspace --</option>
              {#each tenants as tenant (tenant.identifier)}
                <option value={tenant.identifier}>{tenant.name}</option>
              {/each}
            </select>
          </label>

          <button
            on:click={handleSelectTenant}
            disabled={!selectedTenant}
            class="continue-button"
          >
            Continue
          </button>
        </div>
      {:else}
        <div class="status-message">
          <p>No workspaces available</p>
        </div>
      {/if}

      <div class="footer-section">
        <p>Secured by OIDC authentication</p>
      </div>
    </div>

    <div class="copyright">
      <p>&copy; 2025 TreeTopic. All rights reserved.</p>
    </div>
  </div>
</div>

<style>
  .workspace-container {
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: var(--spacing-lg);
    background-color: #1a1a1a;
  }

  .workspace-card-wrapper {
    width: 100%;
    max-width: 400px;
  }

  .workspace-card {
    background-color: var(--color-background);
    border-radius: var(--border-radius-lg);
    border: 1px solid var(--color-border);
    box-shadow: var(--shadow-lg);
    padding: 64px;
  }

  .logo-section {
    text-align: center;
    margin-bottom: 64px;
  }

  .logo-section h1 {
    font-size: var(--font-size-2xl);
    font-weight: 700;
    color: var(--color-primary);
    margin-bottom: 24px;
  }

  .logo-section p {
    font-size: var(--font-size-base);
    color: var(--color-text-light);
  }

  .welcome-section {
    text-align: center;
    margin-bottom: 48px;
  }

  .welcome-section h2 {
    font-size: var(--font-size-xl);
    font-weight: 600;
    color: var(--color-text);
    margin-bottom: 20px;
  }

  .welcome-section p {
    font-size: var(--font-size-base);
    color: var(--color-text-light);
  }

  .status-message {
    text-align: center;
    padding: 40px 0;
  }

  .status-message p {
    font-size: var(--font-size-base);
    color: var(--color-text-light);
  }

  .status-message.error p {
    color: var(--color-error);
  }

  .form-section {
    display: flex;
    flex-direction: column;
    gap: 24px;
  }

  .form-section label {
    display: block;
  }

  .label-text {
    display: block;
    font-size: var(--font-size-sm);
    font-weight: 600;
    color: var(--color-text);
    margin-bottom: 16px;
  }

  select {
    width: 100%;
    padding: 12px 16px;
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-lg);
    background-color: var(--color-background);
    color: var(--color-text);
    font-size: var(--font-size-sm);
    transition: all 0.2s ease;
  }

  select:focus {
    outline: none;
    border-color: var(--color-primary);
    box-shadow: 0 0 0 3px rgba(74, 144, 226, 0.1);
  }

  .continue-button {
    width: 100%;
    padding: 12px 20px;
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
    font-weight: 600;
    border-radius: var(--border-radius-lg);
    border: none;
    cursor: pointer;
    font-size: var(--font-size-sm);
    transition: all 0.2s ease;
  }

  .continue-button:hover:not(:disabled) {
    background-color: var(--color-primary-hover);
  }

  .continue-button:disabled {
    background-color: #d1d5db;
    cursor: not-allowed;
    opacity: 0.6;
  }

  .footer-section {
    margin-top: 48px;
    padding-top: 40px;
    border-top: 1px solid var(--color-border);
  }

  .footer-section p {
    text-align: center;
    font-size: var(--font-size-sm);
    color: var(--color-text-light);
  }

  .copyright {
    margin-top: 40px;
    text-align: center;
  }

  .copyright p {
    font-size: var(--font-size-sm);
    color: var(--color-text-light);
  }
</style>
