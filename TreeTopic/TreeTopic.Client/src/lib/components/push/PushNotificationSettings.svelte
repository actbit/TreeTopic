<script lang="ts">
  import { onMount } from 'svelte';
  import { push } from '$lib/stores/push';
  import { api, getCurrentTenant } from '$lib/api/client';
  import Button from '../common/Button.svelte';
  import ErrorMessage from '../common/ErrorMessage.svelte';

  let vapidPublicKey = $state<string | null>(null);
  let isLoading = $state(false);
  let error = $state<string | null>(null);

  onMount(async () => {
    await push.init();
    await loadVapidPublicKey();
  });

  async function loadVapidPublicKey() {
    try {
      const tenant = getCurrentTenant();
      if (!tenant) return;

      const response = await api.get<{ publicKey: string }>(`/${tenant}/api/Push/vapid-public-key`);
      vapidPublicKey = response.publicKey;
    } catch (err) {
      console.error('Failed to load VAPID public key:', err);
    }
  }

  async function handleSubscribe() {
    error = null;
    isLoading = true;

    try {
      if ($push.permission !== 'granted') {
        const granted = await push.requestPermission();
        if (!granted) {
          error = '通知許可が拒否されました';
          return;
        }
      }

      if (!vapidPublicKey) {
        error = 'VAPIDキーが見つかりません';
        return;
      }

      await push.subscribe(vapidPublicKey);
    } catch (err) {
      error = err instanceof Error ? err.message : '購読に失敗しました';
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
      error = err instanceof Error ? err.message : '購読解除に失敗しました';
    } finally {
      isLoading = false;
    }
  }
</script>

<div class="push-notification-settings">
  <h3 class="text-base text-bold margin-bottom-sm">プッシュ通知</h3>

  {#if error}
    <ErrorMessage message={error} onDismiss={() => (error = null)} />
  {/if}

  {#if !$push.supported}
    <p class="text-small text-light">このブラウザはプッシュ通知をサポートしていません</p>
  {:else if $push.subscribed}
    <div class="flex items-center gap-2">
      <span class="text-small">通知が有効です</span>
      <Button
        type="button"
        variant="secondary"
        size="small"
        onclick={handleUnsubscribe}
        disabled={isLoading}
      >
        {isLoading ? '処理中...' : '無効化'}
      </Button>
    </div>
  {:else}
    <div class="flex items-center gap-2">
      <span class="text-small text-light">通知が無効です</span>
      <Button
        type="button"
        variant="primary"
        size="small"
        onclick={handleSubscribe}
        disabled={isLoading}
      >
        {isLoading ? '処理中...' : '有効化'}
      </Button>
    </div>
  {/if}
</div>
