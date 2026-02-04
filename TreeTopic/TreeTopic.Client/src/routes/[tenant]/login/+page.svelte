<script lang="ts">
  import { onMount } from 'svelte';
  import { page } from '$app/stores';
  import type { PageData } from './$types';

  let { data }: { data: PageData } = $props();

  const { tenant } = data;

  let hasSetupToken = $state(false);
  let setupTokenValue = $state<string | null>(null);

  onMount(() => {
    const urlParams = new URLSearchParams(window.location.search);
    const tokenFromUrl = urlParams.get('setupToken');
    const returnUrlFromUrl = urlParams.get('returnUrl');

    // setupTokenを処理
    if (tokenFromUrl) {
      setupTokenValue = tokenFromUrl;
      sessionStorage.setItem(`setupToken_${tenant}`, tokenFromUrl);
      // トークンの作成日時を保存（8時間有効）
      sessionStorage.setItem(`setupTokenCreatedAt_${tenant}`, Date.now().toString());
      hasSetupToken = true;
    } else {
      // セッションストレージからsetupTokenを取得
      const storedToken = sessionStorage.getItem(`setupToken_${tenant}`);
      if (storedToken) {
        setupTokenValue = storedToken;
        hasSetupToken = true;
      }
    }

    // returnUrlを保存（URLパラメータにある場合）
    if (returnUrlFromUrl) {
      sessionStorage.setItem(`returnUrl_${tenant}`, returnUrlFromUrl);
    }
  });

  function handleOIDCLogin() {
    // 保存されたreturnUrlまたはURLパラメータからreturnUrlを取得
    const returnUrlFromStorage = sessionStorage.getItem(`returnUrl_${tenant}`);
    const returnUrlFromUrl = $page.url.searchParams.get('returnUrl');
    const returnUrl = returnUrlFromStorage ?? returnUrlFromUrl ?? `/${tenant}/`;

    const encodedReturnUrl = encodeURIComponent(returnUrl);
    const loginUrl = `/${tenant}/auth/login?returnUrl=${encodedReturnUrl}`;

    window.location.href = loginUrl;
  }

  function goToSetup() {
    window.location.href = `/${tenant}/setup`;
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
        <p>Sign in to continue.</p>
      </div>

      {#if hasSetupToken}
        <div class="setup-token-notice">
          <div class="notice-header">
            <svg class="notice-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="12" y1="16" x2="12.01" y2="16"></line>
              <polyline points="12 12 12 8"></polyline>
            </svg>
            <h3>Setup Required</h3>
          </div>
          <p class="notice-text">
            You have a setup token that will expire soon.
          </p>
          <div class="notice-actions">
            <button class="btn btn-primary" onclick={goToSetup}>
              Complete Setup
            </button>
            <p class="small-text">
              Or sign in normally if you already completed setup
            </p>
          </div>
        </div>
      {:else}
        <div class="form-section">
          <p class="text-center">
            Signing in to <strong>{tenant}</strong> workspace uses your organization's secure SSO.
          </p>

          <button class="sso-button" onclick={handleOIDCLogin}>
            Sign in with SSO
          </button>
        </div>
      {/if}

      <div class="footer-section">
        <p>Protected by secure OIDC authentication.</p>
        <div class="legal-links">
          <a href="/privacy" target="_blank" rel="noopener">Privacy Policy</a>
          <span class="separator">•</span>
          <a href="/terms" target="_blank" rel="noopener">Terms of Service</a>
        </div>
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
    background-color: #1a1a1a;
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

  .form-section {
    display: flex;
    flex-direction: column;
    gap: 24px;
  }

  .form-section p {
    text-align: center;
    font-size: var(--font-size-sm);
    color: var(--color-text-light);
  }

  .form-section p strong {
    color: var(--color-text);
    font-weight: 600;
  }

  .sso-button {
    width: 100%;
    padding: 14px 20px;
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
    font-weight: 600;
    border-radius: var(--border-radius-lg);
    border: none;
    cursor: pointer;
    font-size: var(--font-size-base);
    transition: all 0.2s ease;
  }

  .sso-button:hover {
    background-color: var(--color-primary-hover);
  }

  .sso-button:active {
    background-color: var(--color-primary-active);
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
    margin-bottom: 12px;
  }

  .legal-links {
    text-align: center;
    font-size: var(--font-size-sm);
  }

  .legal-links a {
    color: var(--color-primary);
    text-decoration: none;
    transition: color 0.2s ease;
  }

  .legal-links a:hover {
    color: var(--color-primary-hover);
    text-decoration: underline;
  }

  .separator {
    color: var(--color-text-light);
    margin: 0 8px;
  }

  .copyright {
    margin-top: 40px;
    text-align: center;
  }

  .copyright p {
    font-size: var(--font-size-sm);
    color: var(--color-text-light);
  }

  .text-center {
    text-align: center;
  }

  .setup-token-notice {
    background-color: #eff6ff;
    border: 1px solid #93c5fd;
    border-radius: var(--border-radius-lg);
    padding: 24px;
    text-align: center;
  }

  .notice-header {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 12px;
    margin-bottom: 16px;
  }

  .notice-icon {
    width: 24px;
    height: 24px;
    color: #2563eb;
    flex-shrink: 0;
  }

  .notice-header h3 {
    margin: 0;
    font-size: var(--font-size-lg);
    font-weight: 600;
    color: var(--color-text);
  }

  .notice-text {
    margin-bottom: 20px;
    color: var(--color-text);
  }

  .notice-text strong {
    color: #2563eb;
  }

  .notice-actions {
    display: flex;
    flex-direction: column;
    gap: 12px;
  }

  .btn {
    padding: 10px 16px;
    border: none;
    border-radius: var(--border-radius-lg);
    font-size: var(--font-size-sm);
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s ease;
  }

  .btn-primary {
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
  }

  .btn-primary:hover {
    background-color: var(--color-primary-hover);
  }

  .small-text {
    font-size: var(--font-size-xs);
    color: var(--color-text-light);
  }
</style>
