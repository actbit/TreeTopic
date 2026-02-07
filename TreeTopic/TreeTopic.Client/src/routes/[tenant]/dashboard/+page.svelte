<script lang="ts">
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';

  const tenant = $page.params.tenant;

  onMount(() => {
    // ダッシュボードがまだ実装されていないので、代わりにメインページにリダイレクト
    goto(`/${tenant}/`);
  });
</script>

<svelte:head>
  <title>Dashboard - TreeTopic</title>
</svelte:head>

<div class="dashboard-container">
  <div class="loading-message">
    <h1>Dashboard</h1>
    <p>Redirecting to main page...</p>
  </div>
</div>

<style>
  .dashboard-container {
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    background-color: var(--color-background);
  }

  .loading-message {
    text-align: center;
    color: var(--color-text);
  }

  .loading-message h1 {
    font-size: 2rem;
    margin-bottom: 1rem;
  }
</style>