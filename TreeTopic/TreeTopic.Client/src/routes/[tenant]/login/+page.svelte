<script lang="ts">
  import type { PageData } from './$types';

  export let data: PageData;

  const { tenant } = data;

  console.log('Login page loaded with tenant:', tenant);

  function handleOIDCLogin() {
    // Redirect to backend OIDC login endpoint with returnUrl
    // Backend will handle the OIDC flow, authenticate, and redirect back
    const returnUrl = `/${tenant}/`;
    const encodedReturnUrl = encodeURIComponent(returnUrl);
    const loginUrl = `/${tenant}/auth/login?returnUrl=${encodedReturnUrl}`;

    console.log('Initiating OIDC login:');
    console.log('  Tenant:', tenant);
    console.log('  Return URL:', returnUrl);
    console.log('  Encoded Return URL:', encodedReturnUrl);
    console.log('  Login URL:', loginUrl);

    window.location.href = loginUrl;
  }
</script>

<svelte:head>
  <title>Sign In - TreeTopic</title>
</svelte:head>

<div class="min-h-screen bg-gradient-to-br from-primary to-secondary flex items-center justify-center p-4">
  <div class="max-w-md">
    <div class="bg-white rounded-lg shadow-xl p-8">
      <div class="text-center mb-8">
        <h1 class="text-3xl font-bold text-primary mb-2">🌳 TreeTopic</h1>
        <p class="text-text-secondary">Collaborative discussion platform</p>
      </div>

      <div class="space-y-4">
        <button
          on:click={handleOIDCLogin}
          class="w-full px-4 py-2 bg-primary text-white font-semibold rounded-lg hover:bg-primary-dark transition-colors"
        >
          Sign in with SSO
        </button>
      </div>

      <div class="mt-8 pt-8 border-t border-border">
        <p class="text-center text-sm text-text-light">
          Protected by secure OIDC authentication
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
