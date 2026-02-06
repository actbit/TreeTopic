<script lang="ts">
  import { api } from '$lib/api/client';

  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let success = $state(false);
  let createdTenant = $state<{ identifier: string; name: string; setupToken?: string } | null>(null);
  let errors = $state<Record<string, boolean>>({
    identifier: false,
    name: false,
    dbConnectionString: false,
    roleClaimName: false,
    metadataAddress: false,
    openIdConnectAuthority: false,
    clientId: false,
    clientSecret: false
  });

  // フォームフィールド
  let identifier = $state('');
  let name = $state('');
  let dbProvider = $state('postgres');
  let useCustomConnection = $state(false);
  let dbConnectionString = $state('');

  // OIDC設定
  let useOidc = $state(false);
  let openIdConnectAuthority = $state('');
  let roleClaimName = $state('');
  let metadataAddress = $state('');
  let clientId = $state('');
  let clientSecret = $state('');

  $effect(() => {
    // MySQLを選ぶと自動的にカスタム接続文字列を有効にする
    if (dbProvider === 'mysql') {
      useCustomConnection = true;
    } else if (dbProvider === 'postgres') {
      useCustomConnection = false;
    }
  });

  $effect(() => {
    // カスタム文字列を使用しないときはPostgreSQLに設定
    if (!useCustomConnection) {
      dbProvider = 'postgres';
    }
  });

  async function handleSubmit() {
    // エラーをリセット
    errors = {
      identifier: false,
      name: false,
      dbConnectionString: false,
      roleClaimName: false,
      metadataAddress: false,
      clientId: false,
      clientSecret: false
    };

    if (!identifier.trim()) {
      errors.identifier = true;
      return;
    } else {
      // 識別子のバリデーション
      const identifierRegex = /^[a-z0-9-]+$/;
      if (!identifierRegex.test(identifier)) {
        error = 'Identifier must contain only lowercase letters, numbers, and hyphens (3-50 characters)';
        return;
      }

      if (identifier.length < 3 || identifier.length > 50) {
        error = 'Identifier must be between 3 and 50 characters';
        return;
      }
    }

    if (!name.trim()) {
      errors.name = true;
      return;
    }

    // カスタム接続文字列が有効な場合のバリデーション
    if (useCustomConnection && !dbConnectionString.trim()) {
      error = 'Connection string is required when using custom connection';
      errors.dbConnectionString = true;
      return;
    }

    // OIDC設定が有効な場合のバリデーション
    if (useOidc) {
      if (!openIdConnectAuthority.trim()) {
        error = 'Authority URL is required for OIDC authentication';
        errors.openIdConnectAuthority = true;
        return;
      }
      if (!metadataAddress.trim()) {
        error = 'Metadata address is required for OIDC authentication';
        errors.metadataAddress = true;
        return;
      }
      if (!clientId.trim()) {
        error = 'Client ID is required for OIDC authentication';
        errors.clientId = true;
        return;
      }
      if (!clientSecret.trim()) {
        error = 'Client Secret is required for OIDC authentication';
        errors.clientSecret = true;
        return;
      }
    }

    try {
      isLoading = true;
      error = null;
      success = false;
      createdTenant = null;

      const request: {
        identifier: string;
        name: string;
        dbProvider: string;
        dbConnectionString?: string;
        openIdConnectAuthority?: string;
        roleClaimName?: string;
        openIdConnectMetadataAddress?: string;
        openIdConnectClientId?: string;
        openIdConnectClientSecret?: string;
      } = {
        identifier: identifier.trim(),
        name: name.trim(),
        dbProvider: dbProvider
      };

      // カスタム接続文字列を追加
      if (useCustomConnection && dbConnectionString.trim()) {
        request.dbConnectionString = dbConnectionString.trim();
      }

      // OIDC設定を追加
      if (useOidc) {
        if (openIdConnectAuthority) request.openIdConnectAuthority = openIdConnectAuthority.trim();
        if (roleClaimName) request.roleClaimName = roleClaimName.trim();
        if (metadataAddress) request.openIdConnectMetadataAddress = metadataAddress.trim();
        if (clientId) request.openIdConnectClientId = clientId.trim();
        if (clientSecret) request.openIdConnectClientSecret = clientSecret.trim();
      }

      const response = await api.post<{ identifier: string; name: string; setupToken?: string }>('/api/tenants/register', request);
      createdTenant = response;

      // 成功
      success = true;
      identifier = '';
      name = '';
      dbProvider = 'postgres';
      useCustomConnection = false;
      dbConnectionString = '';
      useOidc = false;
      roleClaimName = '';
      metadataAddress = '';
      clientId = '';
      clientSecret = '';
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to create workspace';
    } finally {
      isLoading = false;
    }
  }

  function resetForm() {
    identifier = '';
    name = '';
    dbProvider = 'postgres';
    useCustomConnection = false;
    dbConnectionString = '';
    useOidc = false;
    openIdConnectAuthority = '';
    roleClaimName = '';
    metadataAddress = '';
    clientId = '';
    clientSecret = '';
    error = null;
    success = false;
    createdTenant = null;
  }

  function handleSSOLogin() {
    console.log('[handleSSOLogin] createdTenant:', createdTenant);
    if (createdTenant?.identifier && createdTenant?.setupToken) {
      // setupTokenをsessionStorageに保存
      sessionStorage.setItem(`setupToken_${createdTenant.identifier}`, createdTenant.setupToken);
      const returnUrl = encodeURIComponent(`/${createdTenant.identifier}/setup`);
      const loginUrl = `/${createdTenant.identifier}/auth/login?returnUrl=${returnUrl}`;
      console.log('[handleSSOLogin] Redirecting to:', loginUrl);
      window.location.href = loginUrl;
    } else {
      console.error('[handleSSOLogin] Missing identifier or setupToken', {
        identifier: createdTenant?.identifier,
        setupToken: createdTenant?.setupToken
      });
      alert('認証情報が不足しています。管理者にお問い合わせください。');
    }
  }

  function goToTenant() {
    if (createdTenant?.identifier) {
      window.location.href = `/${createdTenant.identifier}/dashboard`;
    }
  }
</script>

<svelte:head>
  <title>Create Workspace - TreeTopic</title>
</svelte:head>

<div class="workspace-container">
  <div class="workspace-card-wrapper">
    <div class="workspace-card">
        {#if success}
        <!-- ログイン画面 -->
        <div class="logo-section">
          <h1>TreeTopic</h1>
          <p>Collaborative discussion platform</p>
        </div>

        <div class="login-section">
          <h2>Welcome to {createdTenant?.name || identifier}</h2>
          <p>Your workspace has been created successfully.</p>

          {#if createdTenant?.setupToken}
            <div class="setup-token-notice">
              <div class="notice-icon">
                              </div>
              <h3>First-time Setup Required</h3>
              <p>Please sign in to set up your workspace and configure your role.</p>
            </div>
          {/if}

          <button
            type="button"
            onclick={handleSSOLogin}
            disabled={isLoading}
            class="sso-button"
          >
            {#if isLoading}
              <span class="loading-spinner"></span>
            {:else}
              Sign in with SSO
            {/if}
          </button>
        </div>

      {:else if error}
        <!-- エラー画面 -->
        <div class="logo-section">
          <h1>TreeTopic</h1>
        </div>

        <div class="error-message">
          <div class="error-icon">
            <svg class="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>
          <h2 class="text-xl font-bold text-text mb-2">An error occurred</h2>
          <p class="text-text-light mb-6">{error}</p>
          <button
            onclick={resetForm}
            class="submit-button"
            style="width: 100%;"
          >
            Back to form
          </button>
        </div>

      {:else}
        <!-- 入力フォーム -->
        <div class="logo-section">
          <h1>TreeTopic</h1>
        </div>

        <div class="welcome-section">
          <h2>Create new workspace</h2>
          <p>Create an isolated environment for your organization or team</p>
        </div>

        <form onsubmit={(e) => { e.preventDefault(); handleSubmit(); }} class="form-section">
          <!-- 識別子 -->
          <label>
            <span class="label-text">Identifier *</span>
            <input
              type="text"
              bind:value={identifier}
              placeholder="e.g. acme-corp"
              disabled={isLoading}
              class:input-error={errors.identifier}
            />
            <p class="input-helper">
              Lowercase letters, numbers, and hyphens only (3-50 characters)
            </p>
          </label>

          <!-- 表示名 -->
          <label>
            <span class="label-text">Display name *</span>
            <input
              type="text"
              bind:value={name}
              placeholder="e.g. ACME Corporation"
              disabled={isLoading}
              class:input-error={errors.name}
            />
          </label>

          <!-- データベース -->
          <label>
            <span class="label-text">Database</span>
            <select
              bind:value={dbProvider}
              disabled={isLoading}
            >
              <option value="postgres">PostgreSQL</option>
              <option value="mysql">MySQL</option>
            </select>
            <p class="input-helper">
              Select the database provider for your workspace
            </p>
          </label>

          <!-- カスタム接続文字列 -->
          <label class="checkbox-label">
            <input
              type="checkbox"
              bind:checked={useCustomConnection}
              disabled={isLoading}
            />
            <span>Use custom connection string</span>
          </label>

          {#if useCustomConnection}
            <label>
              <span class="label-text">Connection string *</span>
              <input
                type="text"
                bind:value={dbConnectionString}
                placeholder={dbProvider === 'postgres' ? 'Host=localhost;Port=5432;Username=user;Password=password;Database=mydb' : 'Server=localhost;Port=3306;Uid=user;Pwd=password;Database=mydb;'}
                disabled={isLoading}
                class:input-error={errors.dbConnectionString}
              />
              <p class="input-helper">
                Enter a custom database connection string
              </p>
            </label>
          {/if}

          <!-- OIDC設定 -->
          <div class="section-divider">
            <label class="checkbox-label">
              <input
                type="checkbox"
                bind:checked={useOidc}
                disabled={isLoading}
              />
              <span>Use custom OpenID Connect authentication</span>
            </label>
          </div>

          {#if useOidc}
            <label>
              <span class="label-text">Role claim name</span>
              <input
                type="text"
                bind:value={roleClaimName}
                placeholder="e.g. roles, groups (optional)"
                disabled={isLoading}
                class:input-error={errors.roleClaimName}
              />
              <p class="input-helper">
                Optional: Set this if your OIDC provider uses a custom role claim name.
              </p>
            </label>

            <label>
              <span class="label-text">Authority *</span>
              <input
                type="url"
                bind:value={openIdConnectAuthority}
                placeholder="https://example.com"
                disabled={isLoading}
                class:input-error={errors.openIdConnectAuthority}
              />
              <p class="input-helper">
                The base URL of your OIDC provider
              </p>
            </label>

            <label>
              <span class="label-text">Metadata address *</span>
              <input
                type="url"
                bind:value={metadataAddress}
                placeholder="https://example.com/.well-known/openid-configuration"
                disabled={isLoading}
                class:input-error={errors.metadataAddress}
              />
            </label>

            <label>
              <span class="label-text">Client ID *</span>
              <input
                type="text"
                bind:value={clientId}
                placeholder="Client ID"
                disabled={isLoading}
                class:input-error={errors.clientId}
              />
            </label>

            <label>
              <span class="label-text">Client Secret *</span>
              <input
                type="password"
                bind:value={clientSecret}
                placeholder="Client Secret"
                disabled={isLoading}
                class:input-error={errors.clientSecret}
              />
            </label>
          {/if}

          <!-- ボタン -->
          <div class="button-group">
            <a href="/" class="cancel-button">
              Cancel
            </a>
            <button
              type="submit"
              disabled={isLoading}
              class="submit-button"
            >
              {#if isLoading}
                <span class="loading-spinner"></span>
              {/if}
              Create workspace
            </button>
          </div>
        </form>
      {/if}

      <div class="footer-section">
        <p>Create a new workspace to get started</p>
      </div>
    </div>

    <div class="copyright">
      <p>&copy; 2025 TreeTopic. All rights reserved.</p>
    </div>
  </div>
</div>

<style>
  .workspace-container {
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: var(--spacing-lg);
    background-color: #1a1a1a;
  }

  .workspace-card-wrapper {
    width: 100%;
    max-width: 500px;
  }

  .workspace-card {
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

  .success-message {
    text-align: center;
    padding: 40px 0;
  }

  .success-icon {
    width: 64px;
    height: 64px;
    margin: 0 auto 24px;
    background-color: var(--color-success-light);
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .error-message {
    text-align: center;
    padding: 40px 0;
  }

  .error-icon {
    width: 64px;
    height: 64px;
    margin: 0 auto 24px;
    background-color: var(--color-error-light);
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .form-section {
    display: flex;
    flex-direction: column;
    gap: 24px;
  }

  .form-section label {
    display: block;
  }

  .label-text {
    display: block;
    font-size: var(--font-size-sm);
    font-weight: 600;
    color: var(--color-text);
    margin-bottom: 8px;
  }

  input,
  select {
    width: 100%;
    padding: 12px 16px;
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-lg);
    background-color: var(--color-background);
    color: var(--color-text);
    font-size: var(--font-size-sm);
    transition: all 0.2s ease;
    font-family: inherit;
  }

  input:focus,
  select:focus {
    outline: none;
    border-color: var(--color-primary);
    box-shadow: 0 0 0 3px rgba(74, 144, 226, 0.1);
  }

  input:disabled,
  select:disabled {
    background-color: var(--color-background-disabled);
    opacity: 0.6;
    cursor: not-allowed;
  }

  input.input-error,
  select.input-error {
    border-color: var(--color-error);
    background-color: var(--color-error-light);
  }

  .checkbox-label {
    display: flex;
    align-items: center;
    gap: 8px;
    cursor: pointer;
    font-size: var(--font-size-sm);
    color: var(--color-text);
  }

  input[type="checkbox"] {
    width: 16px;
    height: 16px;
    cursor: pointer;
    accent-color: var(--color-primary);
  }

  .input-helper {
    font-size: var(--font-size-xs);
    color: var(--color-text-light);
    margin-top: 8px;
  }

  .section-divider {
    border-top: 1px solid var(--color-border);
    padding-top: 24px;
    margin-top: 24px;
  }

  .section-title {
    font-size: var(--font-size-base);
    font-weight: 600;
    color: var(--color-text);
    margin-bottom: 16px;
  }

  .button-group {
    display: flex;
    gap: 16px;
    margin-top: 32px;
  }

  .cancel-button,
  .submit-button {
    flex: 1;
    padding: 12px 20px;
    border-radius: var(--border-radius-lg);
    border: none;
    cursor: pointer;
    font-size: var(--font-size-sm);
    font-weight: 600;
    transition: all 0.2s ease;
  }

  .cancel-button {
    background-color: var(--color-background);
    color: var(--color-text);
    border: 1px solid var(--color-border);
  }

  .cancel-button:hover {
    background-color: var(--color-background-hover);
  }

  .submit-button {
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
  }

  .submit-button:hover:not(:disabled) {
    background-color: var(--color-primary-hover);
  }

  .submit-button:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .loading-spinner {
    display: inline-block;
    width: 16px;
    height: 16px;
    border: 2px solid white;
    border-top-color: transparent;
    border-radius: 50%;
    animation: spin 1s linear infinite;
  }

  .footer-section {
    margin-top: 48px;
    padding-top: 40px;
    border-top: 1px solid var(--color-border);
    text-align: center;
  }

  .footer-section p {
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

  .setup-token {
    background-color: var(--color-surface);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-lg);
    padding: 16px;
    margin: 16px 0;
    word-break: break-all;
    font-family: monospace;
    font-size: var(--font-size-xs);
  }

  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }
</style>
