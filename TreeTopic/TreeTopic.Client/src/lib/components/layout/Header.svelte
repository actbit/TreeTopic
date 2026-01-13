<script lang="ts">
  import { sidebarCollapsed } from '$lib/stores/ui';
  import { currentUser } from '$lib/stores/auth';
  import { currentRoomUser } from '$lib/stores/rooms';

  interface Props {
    onMenuToggle?: () => void;
    children?: any;
  }

  let { onMenuToggle, children }: Props = $props();
</script>

<header class="app-header">
  <div class="flex items-center header-left">
    {#if onMenuToggle}
      <button
        type="button"
        onclick={onMenuToggle}
        class="menu-toggle-button"
        aria-label={$sidebarCollapsed ? 'Open navigation menu' : 'Close navigation menu'}
        aria-expanded={!$sidebarCollapsed}
        title="Toggle navigation"
      >
        <span class="menu-toggle-icon" aria-hidden="true">
          <span></span>
          <span></span>
          <span></span>
        </span>
        <span class="sr-only">
          {$sidebarCollapsed ? 'Open sidebar navigation' : 'Close sidebar navigation'}
        </span>
      </button>
    {/if}

    <div class="flex items-center header-logo">
      <h1 class="text-large text-bold text-primary">TreeTopic</h1>
    </div>
  </div>

  <div class="flex items-center header-right">
    {#if children}
      <div class="flex items-center header-content">
        {@render children()}
      </div>
    {/if}

    {#if $currentUser}
      <div class="flex items-center header-user">
        {#if $currentRoomUser}
          {#if $currentRoomUser.iconUrl}
            <img
              src={$currentRoomUser.iconUrl}
              alt={$currentRoomUser.displayName}
              class="avatar avatar-md bg-primary"
            />
          {:else}
            <div class="avatar avatar-md bg-primary text-white">
              {$currentRoomUser.displayName?.charAt(0) ?? 'U'}
            </div>
          {/if}
          <span class="text-small text-bold">{$currentRoomUser.displayName}</span>
        {:else}
          {#if $currentUser.avatar}
            <img
              src={$currentUser.avatar}
              alt={$currentUser.displayName}
              class="avatar avatar-md bg-primary"
            />
          {:else}
            <div class="avatar avatar-md bg-primary text-white">
              {$currentUser.displayName?.charAt(0) ?? 'U'}
            </div>
          {/if}
          <span class="text-small text-bold">{$currentUser.displayName}</span>
        {/if}
      </div>
    {/if}
  </div>
</header>

<style>
  .app-header {
    height: 60px;
    position: sticky;
    top: 0;
    z-index: 40;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding-left: var(--spacing-xl);
    padding-right: var(--spacing-xl);
    background-color: var(--color-background);
    border-bottom: 1px solid var(--color-border);
  }

  .header-left {
    gap: var(--spacing-md);
  }

  .menu-toggle-button {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 44px;
    height: 44px;
    border-radius: var(--border-radius-full);
    border: 1px solid transparent;
    background-color: transparent;
    cursor: pointer;
    transition: background-color 0.2s ease, border-color 0.2s ease;
  }

  .menu-toggle-button:hover,
  .menu-toggle-button:focus-visible {
    background-color: var(--color-surface);
    border-color: var(--color-border);
  }

  .menu-toggle-icon {
    display: inline-flex;
    flex-direction: column;
    align-items: center;
    gap: 5px;
  }

  .menu-toggle-icon span {
    width: 22px;
    height: 2px;
    border-radius: 999px;
    background-color: var(--color-text);
    display: block;
  }

  .header-logo {
    gap: var(--spacing-sm);
  }

  .header-right {
    gap: var(--spacing-xl);
  }

  @media (max-width: 768px) {
    .app-header {
      padding-left: var(--spacing-md);
      padding-right: var(--spacing-md);
    }

    .header-right {
      gap: var(--spacing-sm);
    }
  }

  .header-content {
    gap: var(--spacing-md);
  }

  .header-user {
    gap: var(--spacing-sm);
    padding-left: var(--spacing-xl);
    border-left: 1px solid var(--color-border);
  }
</style>
