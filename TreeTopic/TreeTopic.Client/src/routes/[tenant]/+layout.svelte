<script lang="ts">
  import { isAuthenticated } from '$lib/stores/auth';
</script>

{#if $isAuthenticated}
  <slot />
{:else}
  <div class="flex items-center justify-center h-screen bg-gradient-to-br from-primary to-secondary">
    <div class="text-center text-white">
      <h1 class="text-4xl font-bold mb-4">TreeTopic</h1>
      <p>Loading...</p>
    </div>
  </div>
{/if}
