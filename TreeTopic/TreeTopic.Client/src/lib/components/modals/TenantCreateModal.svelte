<script lang="ts">
  import Modal from '../common/Modal.svelte';
  import { api } from '$lib/api/client';
  import { ui, activeModals } from '$lib/stores/ui';

  const modalId = 'tenant-create';
  let modal = $derived.by(() => $activeModals.find((m) => m.id === modalId) ?? null);
  let isOpen = $derived.by(() => modal !== null);

  let isLoading = $state(false);
  let error = $state<string | null>(null);
  let showSetupToken = $state(false);
  let setupToken = $state<string | null>(null);
  let setupTokenCopied = $state(false);
  let errors = $state<Record<string, boolean>>({
    identifier: false,
    name: false,
    dbConnectionString: false,
    roleClaimName: false,
    metadataAddress: false,
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
    if (isOpen) {
      // リセット
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
    }
  });

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
      metadataAddress: false,
      openIdConnectAuthority: false,
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

      const request: any = {
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
        if (roleClaimName) request.roleClaimName = roleClaimName.trim();
        if (openIdConnectAuthority) request.openIdConnectAuthority = openIdConnectAuthority.trim();
        if (metadataAddress) request.openIdConnectMetadataAddress = metadataAddress.trim();
        if (clientId) request.openIdConnectClientId = clientId.trim();
        if (clientSecret) request.openIdConnectClientSecret = clientSecret.trim();
      }

      const response = await api.post<any>('/api/tenant/register', request);

      // セットアップトークンを保存して表示
      if (response.setupToken) {
        setupToken = response.setupToken;
        sessionStorage.setItem(`setupToken_${identifier}`, response.setupToken);
        showSetupToken = true;
      }
    } catch (err: any) {
      error = err.message || 'Failed to create workspace';
    } finally {
      isLoading = false;
    }
  }

  function handleClose() {
    ui.closeModal(modalId);
  }

  function copyToken() {
    if (setupToken) {
      navigator.clipboard.writeText(setupToken).then(() => {
        setupTokenCopied = true;
        setTimeout(() => {
          setupTokenCopied = false;
        }, 2000);
      });
    }
  }

  function continueToLogin() {
    ui.closeModal(modalId);
    window.location.href = `/${identifier}/login`;
  }
</script>

<Modal {isOpen} title={showSetupToken ? "Setup Token Generated" : "Create new workspace"} onClose={handleClose} size="large" closeButton={!isLoading && !showSetupToken}>
  {#if showSetupToken}
    <div class="setup-token-container">
      <div class="setup-token-content">
        <div class="setup-token-header">
          <svg class="check-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="20 6 9 17 4 12"></polyline>
          </svg>
          <h2>Workspace created successfully!</h2>
        </div>

        <p class="setup-token-description">
          Your workspace has been created. Save this setup token securely. You'll need it to configure your workspace roles and users. The token expires in 1 hour.
        </p>

        <div class="setup-token-box">
          <code class="token-text">{setupToken}</code>
          <button
            type="button"
            onclick={copyToken}
            class="copy-button"
            disabled={isLoading}
          >
            {setupTokenCopied ? '✓ Copied' : 'Copy'}
          </button>
        </div>

        <div class="setup-token-warning">
          <svg class="warning-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="8" x2="12" y2="12"></line>
            <line x1="12" y1="16" x2="12.01" y2="16"></line>
          </svg>
          <p>Keep this token safe. You'll need it to access the setup page after login.</p>
        </div>
      </div>

      <div class="setup-token-footer">
        <button
          type="button"
          onclick={continueToLogin}
          disabled={isLoading}
          class="modal-submit-button"
        >
          Continue to Login
        </button>
      </div>
    </div>
  {:else}
    <form onsubmit={(e) => { e.preventDefault(); handleSubmit(); }} class="modal-form">
      {#if error}
        <div class="modal-error">
          <span>{error}</span>
        </div>
      {/if}

      <div class="modal-content">
      <!-- 識別子 -->
      <label>
        <span class="modal-label">Identifier *</span>
        <input
          type="text"
          bind:value={identifier}
          placeholder="e.g. acme-corp"
          disabled={isLoading}
          class:input-error={errors.identifier}
        />
        <p class="modal-helper">
          Lowercase letters, numbers, and hyphens only (3-50 characters)
        </p>
      </label>

      <!-- 表示名 -->
      <label>
        <span class="modal-label">Display name *</span>
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
        <span class="modal-label">Database</span>
        <select
          bind:value={dbProvider}
          disabled={isLoading}
        >
          <option value="postgres">PostgreSQL</option>
          <option value="mysql">MySQL</option>
        </select>
        <p class="modal-helper">
          Select the database provider for your workspace
        </p>
      </label>

      <!-- カスタム接続文字列 -->
      <label class="modal-checkbox-label">
        <input
          type="checkbox"
          bind:checked={useCustomConnection}
          disabled={isLoading}
        />
        <span>Use custom connection string</span>
      </label>

      {#if useCustomConnection}
        <label>
          <span class="modal-label">Connection string *</span>
          <input
            type="text"
            bind:value={dbConnectionString}
            placeholder={dbProvider === 'postgres' ? 'Host=localhost;Port=5432;Username=user;Password=password;Database=mydb' : 'Server=localhost;Port=3306;Uid=user;Pwd=password;Database=mydb;'}
            disabled={isLoading}
            class:input-error={errors.dbConnectionString}
          />
          <p class="modal-helper">
            Enter a custom database connection string
          </p>
        </label>
      {/if}

      <!-- OIDC設定 -->
      <div class="modal-divider">
        <label class="modal-checkbox-label">
          <input
            type="checkbox"
            bind:checked={useOidc}
            disabled={isLoading}
          />
          <span>Use custom OpenID Connect authentication</span>
        </label>
        <p class="modal-helper">
          Enable if your organization uses an OIDC provider other than the default.
          Supports Keycloak, Azure AD, Google, and other OpenID Connect providers.
        </p>
      </div>

      {#if useOidc}
        <label>
          <span class="modal-label">Role claim name</span>
          <input
            type="text"
            bind:value={roleClaimName}
            placeholder="e.g. roles, groups (optional)"
            disabled={isLoading}
            class:input-error={errors.roleClaimName}
          />
          <p class="modal-helper">
            Optional: Set this if your OIDC provider uses a custom role claim name.
            <br><br>
            <strong>When set:</strong> Roles are automatically assigned from your OIDC provider
            using this claim name during login. Manual role assignment is disabled.
            <br><br>
            <strong>When not set:</strong> Roles are managed manually through the setup interface.
            No automatic role assignment from OIDC claims.
            <br><br>
            <strong>Common examples:</strong>
            <br>- Keycloak: "roles" or "realm_access.roles"
            <br>- Azure AD: "roles"
            <br>- Google: "roles" (from custom claims)
          </p>
        </label>

        <label>
          <span class="modal-label">Metadata address *</span>
          <input
            type="url"
            bind:value={metadataAddress}
            placeholder="https://example.com/.well-known/openid-configuration"
            disabled={isLoading}
            class:input-error={errors.metadataAddress}
          />
        </label>

        <label>
          <span class="modal-label">Authority *</span>
          <input
            type="url"
            bind:value={openIdConnectAuthority}
            placeholder="https://example.com"
            disabled={isLoading}
            class:input-error={errors.openIdConnectAuthority}
          />
          <p class="modal-helper">
            The base URL of your OIDC provider
          </p>
        </label>

        <label>
          <span class="modal-label">Client ID *</span>
          <input
            type="text"
            bind:value={clientId}
            placeholder="Client ID"
            disabled={isLoading}
            class:input-error={errors.clientId}
          />
        </label>

        <label>
          <span class="modal-label">Client Secret *</span>
          <input
            type="password"
            bind:value={clientSecret}
            placeholder="Client Secret"
            disabled={isLoading}
            class:input-error={errors.clientSecret}
          />
        </label>
      {/if}
    </div>

    <!-- Footer -->
    <div class="modal-footer">
      <button
        type="button"
        onclick={handleClose}
        disabled={isLoading}
        class="modal-cancel-button"
      >
        Cancel
      </button>
      <button
        type="submit"
        disabled={isLoading}
        class="modal-submit-button"
      >
        {#if isLoading}
          <span class="modal-spinner"></span>
        {/if}
        Create workspace
      </button>
    </div>
    </form>
  {/if}
</Modal>

<style>
  .modal-form {
    display: flex;
    flex-direction: column;
    height: 100%;
    gap: 0;
  }

  .modal-error {
    background-color: var(--color-error-light);
    border-bottom: 1px solid var(--color-border);
    padding: 12px 16px;
    color: var(--color-error);
    font-size: var(--font-size-sm);
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .modal-content {
    flex: 1;
    overflow-y: auto;
    padding: 20px 16px;
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  .modal-content label {
    display: block;
  }

  .modal-label {
    display: block;
    font-size: var(--font-size-sm);
    font-weight: 600;
    color: var(--color-text);
    margin-bottom: 8px;
  }

  .modal-content input,
  .modal-content select {
    width: 100%;
    padding: 10px 12px;
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-lg);
    background-color: var(--color-background);
    color: var(--color-text);
    font-size: var(--font-size-sm);
    transition: all 0.2s ease;
    font-family: inherit;
  }

  .modal-content input:focus,
  .modal-content select:focus {
    outline: none;
    border-color: var(--color-primary);
    box-shadow: 0 0 0 3px rgba(74, 144, 226, 0.1);
  }

  .modal-content input:disabled,
  .modal-content select:disabled {
    background-color: var(--color-background-disabled);
    opacity: 0.6;
    cursor: not-allowed;
  }

  .modal-content input.input-error,
  .modal-content select.input-error {
    border-color: var(--color-error);
    background-color: var(--color-error-light);
  }

  .modal-helper {
    font-size: var(--font-size-xs);
    color: var(--color-text-light);
    margin-top: 6px;
  }

  .modal-checkbox-label {
    display: flex;
    align-items: center;
    gap: 8px;
    cursor: pointer;
    font-size: var(--font-size-sm);
    color: var(--color-text);
  }

  .modal-content input[type="checkbox"] {
    width: 16px;
    height: 16px;
    cursor: pointer;
    accent-color: var(--color-primary);
  }

  .modal-divider {
    border-top: 1px solid var(--color-border);
    padding-top: 16px;
    margin-top: 16px;
  }

  .modal-footer {
    border-top: 1px solid var(--color-border);
    padding: 12px 16px;
    background-color: var(--color-background);
    display: flex;
    justify-content: flex-end;
    gap: 12px;
  }

  .modal-cancel-button,
  .modal-submit-button {
    padding: 10px 16px;
    border-radius: var(--border-radius-lg);
    border: none;
    cursor: pointer;
    font-size: var(--font-size-sm);
    font-weight: 600;
    transition: all 0.2s ease;
  }

  .modal-cancel-button {
    background-color: var(--color-background);
    color: var(--color-text);
    border: 1px solid var(--color-border);
  }

  .modal-cancel-button:hover {
    background-color: var(--color-background-hover);
  }

  .modal-cancel-button:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .modal-submit-button {
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .modal-submit-button:hover:not(:disabled) {
    background-color: var(--color-primary-hover);
  }

  .modal-submit-button:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .modal-spinner {
    display: inline-block;
    width: 14px;
    height: 14px;
    border: 2px solid white;
    border-top-color: transparent;
    border-radius: 50%;
    animation: spin 1s linear infinite;
  }

  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }

  .setup-token-container {
    display: flex;
    flex-direction: column;
    height: 100%;
    gap: 0;
  }

  .setup-token-content {
    flex: 1;
    overflow-y: auto;
    padding: 40px 32px;
    display: flex;
    flex-direction: column;
    gap: 24px;
  }

  .setup-token-header {
    display: flex;
    align-items: center;
    gap: 16px;
  }

  .check-icon {
    width: 32px;
    height: 32px;
    color: var(--color-success, #10b981);
    flex-shrink: 0;
  }

  .setup-token-header h2 {
    margin: 0;
    font-size: var(--font-size-lg, 18px);
    font-weight: 600;
    color: var(--color-text);
  }

  .setup-token-description {
    margin: 0;
    font-size: var(--font-size-sm, 14px);
    color: var(--color-text-light);
    line-height: 1.6;
  }

  .setup-token-box {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 16px;
    background-color: var(--color-background-secondary, #f5f5f5);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-lg, 8px);
  }

  .token-text {
    flex: 1;
    font-family: 'Courier New', monospace;
    font-size: var(--font-size-xs, 12px);
    color: var(--color-text);
    word-break: break-all;
    margin: 0;
  }

  .copy-button {
    padding: 8px 16px;
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
    border: none;
    border-radius: var(--border-radius-lg);
    font-size: var(--font-size-sm);
    font-weight: 600;
    cursor: pointer;
    white-space: nowrap;
    transition: all 0.2s ease;
    flex-shrink: 0;
  }

  .copy-button:hover:not(:disabled) {
    background-color: var(--color-primary-hover);
  }

  .copy-button:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .setup-token-warning {
    display: flex;
    align-items: flex-start;
    gap: 12px;
    padding: 12px 16px;
    background-color: var(--color-warning-light, #fef3c7);
    border: 1px solid var(--color-warning-border, #fcd34d);
    border-radius: var(--border-radius-lg);
  }

  .warning-icon {
    width: 20px;
    height: 20px;
    color: var(--color-warning, #f59e0b);
    flex-shrink: 0;
    margin-top: 2px;
  }

  .setup-token-warning p {
    margin: 0;
    font-size: var(--font-size-sm);
    color: var(--color-warning-text, #92400e);
    line-height: 1.5;
  }

  .setup-token-footer {
    border-top: 1px solid var(--color-border);
    padding: 12px 16px;
    background-color: var(--color-background);
    display: flex;
    justify-content: flex-end;
    gap: 12px;
  }
</style>
