<script lang="ts">
  import { ui } from '$lib/stores';
  import { currentUser } from '$lib/stores/auth';

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
        on:click={onMenuToggle}
        class="button clickable menu-toggle-button"
        aria-label="Toggle menu"
        title="Toggle sidebar"
      >
        ☰
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
        {#if $currentUser.avatar}
          <img
            src={$currentUser.avatar}
            alt={$currentUser.displayName}
            class="user-avatar"
          />
        {:else}
          <div class="flex items-center justify-center user-avatar-placeholder bg-primary">
            {$currentUser.displayName?.charAt(0) ?? 'U'}
          </div>
        {/if}
        <span class="text-small text-bold">{$currentUser.displayName}</span>
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
    padding: var(--spacing-sm);
    background-color: transparent;
    border: none;
  }

  .menu-toggle-button:hover {
    background-color: var(--color-surface);
  }

  .header-logo {
    gap: var(--spacing-sm);
  }

  .header-right {
    gap: var(--spacing-xl);
  }

  .header-content {
    gap: var(--spacing-md);
  }

  .header-user {
    gap: var(--spacing-sm);
    padding-left: var(--spacing-xl);
    border-left: 1px solid var(--color-border);
  }

  .user-avatar,
  .user-avatar-placeholder {
    width: 32px;
    height: 32px;
    border-radius: var(--border-radius-full);
  }

  .user-avatar {
    background-color: var(--color-surface);
  }

  .user-avatar-placeholder {
    color: var(--color-white);
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-bold);
  }
</style>
