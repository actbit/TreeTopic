<script lang="ts">
  import type { PageData } from './$types';
  import AuthShell from '$lib/components/layout/AuthShell.svelte';

  export let data: PageData;

  const { tenant } = data;

  function handleOIDCLogin() {
    const returnUrl = `/${tenant}/`;
    const encodedReturnUrl = encodeURIComponent(returnUrl);
    const loginUrl = `/${tenant}/auth/login?returnUrl=${encodedReturnUrl}`;

    window.location.href = loginUrl;
  }
</script>

<svelte:head>
  <title>Sign In - TreeTopic</title>
</svelte:head>

<AuthShell
  title="TreeTopic"
  subtitle="Collaborative discussion platform"
  description="Welcome back. Sign in to continue."
  footerText="Protected by secure OIDC authentication."
>
  <p class="text-center text-light">
    Signing in to <strong>{tenant}</strong> workspace uses your organization's secure SSO.
  </p>

  <button class="button button-primary button-large" on:click={handleOIDCLogin}>
    Sign in with SSO
  </button>

  <span slot="footer">&copy; 2025 TreeTopic. Protected by secure OIDC authentication.</span>
</AuthShell>
