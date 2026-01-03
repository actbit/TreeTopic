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

<div class="login-container">
  <div class="login-card-wrapper">
    <div class="login-card">
      <div class="logo-section">
        <h1>TreeTopic</h1>
        <p>Collaborative discussion platform</p>
      </div>

      <div class="welcome-section">
        <h2>Welcome back</h2>
        <p>Sign in to continue to your workspace</p>
      </div>

      <button on:click={handleOIDCLogin} class="login-button">
        Sign in with SSO
      </button>

      <div class="footer-section">
        <p>Protected by secure OIDC authentication</p>
      </div>
    </div>

    <div class="copyright">
      <p>&copy; 2025 TreeTopic. All rights reserved.</p>
    </div>
  </div>
</div>

<style>
  .login-container {
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: var(--spacing-lg);
    background-color: #f9fafb;
  }

  .login-card-wrapper {
    width: 100%;
    max-width: 400px;
  }

  .login-card {
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

  .login-button {
    width: 100%;
    padding: 12px 20px;
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
    font-weight: 600;
    border-radius: var(--border-radius-lg);
    border: none;
    cursor: pointer;
    font-size: var(--font-size-sm);
    transition: background-color 0.2s ease;
  }

  .login-button:hover {
    background-color: var(--color-primary-hover);
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
