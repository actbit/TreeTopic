<script lang="ts">
  import { goto } from '$app/navigation';
  import type { PublicTenantInfo } from '$lib/api/tenants';

  let { tenants = [], isLoading = false, onSelect }: {
    tenants: PublicTenantInfo[];
    isLoading?: boolean;
    onSelect: (tenant: PublicTenantInfo) => Promise<void>;
  } = $props();

  let isOpen = $state(false);
  let selectedTenant = $state<PublicTenantInfo | null>(null);
  let isNavigating = $state(false);

  async function selectTenant(tenant: PublicTenantInfo) {
    if (isNavigating) return;
    selectedTenant = tenant;
    isNavigating = true;
    try {
      await onSelect(tenant);
    } finally {
      isNavigating = false;
      isOpen = false;
    }
  }
</script>

<div class="workspace-selector-wrapper">
  <div class="relative w-full">
    <button
      onclick={() => (isOpen = !isOpen)}
      disabled={isLoading || tenants.length === 0}
      class="button button-primary w-full selector-button"
    >
      <span class="selector-label">
        {#if selectedTenant}
          {selectedTenant.name}
        {:else}
          {isLoading ? 'Loading...' : 'Select Workspace'}
        {/if}
      </span>
      <span class="dropdown-arrow {isOpen ? 'open' : ''}"
        >▼</span
      >
    </button>

    {#if isOpen && !isLoading}
      <div class="card selector-dropdown">
        <div class="selector-list">
          {#each tenants as tenant (tenant.identifier)}
            <button
              onclick={() => selectTenant(tenant)}
              disabled={isNavigating}
              class="selector-item {selectedTenant?.identifier === tenant.identifier ? 'selected' : ''}"
            >
              <div class="item-content">
                <div class="item-title">{tenant.name}</div>
                <div class="item-subtitle">@{tenant.identifier}</div>
              </div>
              {#if selectedTenant?.identifier === tenant.identifier && isNavigating}
                <span class="item-loading">⟳</span>
              {:else}
                <span class="item-icon">→</span>
              {/if}
            </button>
          {/each}
        </div>
      </div>
    {/if}
  </div>
</div>

<style>
  .workspace-selector-wrapper {
    width: 100%;
  }

  .relative {
    position: relative;
  }

  .w-full {
    width: 100%;
  }

  .selector-button {
    display: flex;
    align-items: center;
    justify-content: space-between;
    text-align: left;
  }

  .selector-label {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .dropdown-arrow {
    margin-left: var(--spacing-md);
    transition: transform var(--transition-fast);
    flex-shrink: 0;
  }

  .dropdown-arrow.open {
    transform: rotate(180deg);
  }

  .selector-dropdown {
    position: absolute;
    top: calc(100% + var(--spacing-sm));
    left: 0;
    right: 0;
    z-index: 50;
    max-height: 400px;
    overflow-y: auto;
    padding: 0;
  }

  .selector-list {
    display: flex;
    flex-direction: column;
    gap: 0;
  }

  .selector-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    width: 100%;
    padding: var(--spacing-md);
    background: transparent;
    border: none;
    border-bottom: 1px solid var(--color-border);
    cursor: pointer;
    transition: all var(--transition-fast);
    text-align: left;
  }

  .selector-item:last-child {
    border-bottom: none;
  }

  .selector-item:hover:not(:disabled) {
    background-color: color-mix(in srgb, var(--color-primary) 5%, var(--color-background));
  }

  .selector-item.selected {
    background-color: color-mix(in srgb, var(--color-primary) 10%, var(--color-background));
    border-left: 3px solid var(--color-primary);
    padding-left: calc(var(--spacing-md) - 3px);
  }

  .selector-item:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .item-content {
    flex: 1;
  }

  .item-title {
    font-size: var(--font-size-base);
    font-weight: 600;
    color: var(--color-text);
    margin: 0;
  }

  .item-subtitle {
    font-size: var(--font-size-sm);
    color: var(--color-text-light);
    margin: 4px 0 0 0;
  }

  .item-icon,
  .item-loading {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 24px;
    height: 24px;
    margin-left: var(--spacing-md);
    color: var(--color-primary);
    flex-shrink: 0;
  }

  .item-loading {
    animation: spin 1s linear infinite;
  }

  @keyframes spin {
    from {
      transform: rotate(0deg);
    }
    to {
      transform: rotate(360deg);
    }
  }
</style>
