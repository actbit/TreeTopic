<script lang="ts">
  import { onMount } from 'svelte';
  import { push, type PushState } from '$lib/stores/push';
  import Button from '../common/Button.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';

  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let pushState = $state<PushState>({
    supported: false,
    permission: 'default',
    subscribed: false,
    subscription: null
  });

  onMount(() => {
    let unsubscribe: (() => void) | undefined;

    (async () => {
      await push.init();

      // Subscribe to push store changes
      unsubscribe = push.subscribe((state) => {
        pushState = state;
      });
    })();

    return () => unsubscribe?.();
  });

  async function handleSubscribe() {
    error = null;
    isLoading = true;

    try {
      if (pushState.permission !== 'granted') {
        const granted = await push.requestPermission();
        if (!granted) {
          error = 'Notification permission denied';
          return;
        }
      }

      await push.subscribePush();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to subscribe';
    } finally {
      isLoading = false;
    }
  }

  async function handleUnsubscribe() {
    error = null;
    isLoading = true;

    try {
      await push.unsubscribe();
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to unsubscribe';
    } finally {
      isLoading = false;
    }
  }
</script>

<div class="push-notification-settings">
  <h3 class="text-base text-bold margin-bottom-sm">Push Notifications</h3>

  {#if error}
    <ErrorMessage message={error} onDismiss={() => (error = null)} />
  {/if}

  {#if !pushState.supported}
    <p class="text-small text-light">This browser does not support push notifications</p>
  {:else if pushState.subscribed}
    <div class="flex items-center gap-2">
      <span class="text-small">Notifications enabled</span>
      <Button
        type="button"
        variant="secondary"
        size="small"
        onclick={handleUnsubscribe}
        disabled={isLoading}
      >
        {isLoading ? 'Processing...' : 'Disable'}
      </Button>
    </div>
  {:else}
    <div class="flex items-center gap-2">
      <span class="text-small text-light">Notifications disabled</span>
      <Button
        type="button"
        variant="primary"
        size="small"
        onclick={handleSubscribe}
        disabled={isLoading}
      >
        {isLoading ? 'Processing...' : 'Enable'}
      </Button>
    </div>
  {/if}
</div>
