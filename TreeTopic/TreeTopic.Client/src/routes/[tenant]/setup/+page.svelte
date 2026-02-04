<script lang="ts">
  import type { PageData } from './$types';
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import { page } from '$app/stores';
  import { api, createRoleWithSetupToken, getRolesWithSetupToken, getTenantDetail, invalidateSetupToken, getUsersWithSetupToken, assignUserRoleWithSetupToken, removeUserRoleWithSetupToken } from '$lib/api/client';

  // 型定義
  interface TenantDetail {
    identifier: string;
    name: string;
    roleClaimName?: string;
  }

  interface Role {
    id: string;
    name: string;
    permissions?: string[];
  }

  interface User {
    id: string;
    userName: string;
    email?: string;
    roles?: string[];
  }

  const tenant = $page.params.tenant;

  let setupToken: string | null = null;
  let isLoading = $state(false);
  let error = $state<string | null>(null);

  // Role management state
  let roles = $state<any[]>([]);
  let newRoleName = $state('');
  let availablePermissions = $state<string[]>([]);
  let selectedPermissions = $state<Record<string, boolean>>({});

  // User role assignment state
  let currentUser = $state<User | null>(null);
  let selectedRoleForUser = $state<string>('');

  // Token expiry
  let tokenExpiryTime = $state<Date | null>(null);
  let timeRemaining = $state<string>('');

  // Tenant detail
  let tenantDetail = $state<TenantDetail | null>(null);
  let canManageRoles = $state(false);

  onMount(async () => {
    // Check for setup token in sessionStorage
    setupToken = sessionStorage.getItem(`setupToken_${tenant}`);

    if (!setupToken) {
      // No setup token, redirect to main page
      await goto(`/${tenant}/`);
      return;
    }

    // Load initial data
    await loadRoles();
    await loadAvailablePermissions();
    await loadTenantDetail();
    await loadCurrentUser();

    // Set token expiry based on creation time (8 hours from creation)
    const tokenCreatedAt = sessionStorage.getItem(`setupTokenCreatedAt_${tenant}`);
    if (tokenCreatedAt) {
      tokenExpiryTime = new Date(parseInt(tokenCreatedAt) + 480 * 60 * 1000);
    } else {
      // Fallback: if no creation time stored, assume now and save it
      const now = Date.now();
      sessionStorage.setItem(`setupTokenCreatedAt_${tenant}`, now.toString());
      tokenExpiryTime = new Date(now + 480 * 60 * 1000);
    }

    // Update time remaining every second
    setInterval(() => {
      if (tokenExpiryTime) {
        const diff = tokenExpiryTime.getTime() - Date.now();
        if (diff > 0) {
          const hours = Math.floor(diff / (1000 * 60 * 60));
          const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
          const seconds = Math.floor((diff % (1000 * 60)) / 1000);
          timeRemaining = `${hours}h ${minutes}m ${seconds}s`;
        } else {
          timeRemaining = 'Expired';
        }
      }
    }, 1000);
  });


  async function loadTenantDetail() {
    try {
      const response = await getTenantDetail(tenant!);
      tenantDetail = response as TenantDetail;
      // Role management is only allowed when OIDC role claim name is null/empty
      canManageRoles = !tenantDetail.roleClaimName || tenantDetail.roleClaimName.trim() === '';
    } catch (err) {
      console.error('Error loading tenant detail:', err);
      // Default to allowing role management if there's an error
      canManageRoles = true;
    }
  }

  async function loadRoles() {
    try {
      if (!setupToken) {
        roles = [];
        return;
      }
      const response = await getRolesWithSetupToken(tenant!, setupToken);
      roles = response as Role[];

      // パーミッションのチェック状態を復元
      selectedPermissions = {};
      for (const role of roles) {
        if (role.permissions) {
          for (const perm of role.permissions) {
            selectedPermissions[`${role.name}_${perm}`] = true;
          }
        }
      }
    } catch (err) {
      console.error('Error loading roles:', err);
      roles = [];
    }
  }

  async function loadAvailablePermissions() {
    try {
      // This would call an endpoint to get available identity permissions
      // For now, use common permissions
      availablePermissions = [
        'UserRead',
        'UserManage',
        'RoleRead',
        'RoleManage',
        'TenantRead',
        'TenantManage'
      ];
    } catch (err) {
      console.error('Error loading permissions:', err);
    }
  }

  async function loadCurrentUser() {
    try {
      if (!setupToken) {
        currentUser = null;
        return;
      }
      const response = await getUsersWithSetupToken(tenant!, setupToken) as User[];
      if (response && response.length > 0) {
        currentUser = response[0];
      }
    } catch (err) {
      console.error('Error loading current user:', err);
      currentUser = null;
    }
  }

  async function assignRoleToUser() {
    if (!currentUser || !selectedRoleForUser || !setupToken) {
      error = 'Please select a role';
      return;
    }

    try {
      isLoading = true;
      error = null;

      const updatedUser = await assignUserRoleWithSetupToken(tenant!, currentUser.id, selectedRoleForUser, setupToken!);

      // Update current user with the response
      currentUser = updatedUser as User;

      // Clear selection
      selectedRoleForUser = '';
    } catch (err: any) {
      error = err.message || 'Failed to assign role';
    } finally {
      isLoading = false;
    }
  }

  async function removeRoleFromUser(roleName: string) {
    if (!currentUser || !setupToken) return;

    try {
      isLoading = true;
      error = null;

      const updatedUser = await removeUserRoleWithSetupToken(tenant!, currentUser.id, roleName, setupToken!);

      // Update current user with the response
      currentUser = updatedUser as User;
    } catch (err: any) {
      error = err.message || 'Failed to remove role';
    } finally {
      isLoading = false;
    }
  }

  async function createRole() {
    if (!newRoleName.trim() || !setupToken) {
      error = 'Role name is required';
      return;
    }

    try {
      isLoading = true;
      error = null;

      const response = await createRoleWithSetupToken(tenant!, setupToken!, newRoleName.trim());

      // Assuming response structure matches RoleDto { Id: string, Name: string }
      roles = [...roles, response as Role];
      newRoleName = '';
    } catch (err: any) {
      error = err.message || 'Failed to create role';
    } finally {
      isLoading = false;
    }
  }

  async function deleteRole(roleName: string) {
    if (!setupToken || !confirm(`Delete role "${roleName}"?`)) return;

    try {
      isLoading = true;
      error = null;

      await api.del(`/${tenant}/api/setup/rolesetup/${roleName}`, {
        headers: { 'Authorization': `Bearer ${setupToken}` }
      });

      roles = roles.filter(r => r.name !== roleName);
    } catch (err: any) {
      error = err.message || 'Failed to delete role';
    } finally {
      isLoading = false;
    }
  }

  async function addPermissionToRole(roleName: string, permissionName: string) {
    if (!setupToken) return;

    try {
      isLoading = true;
      error = null;

      await api.post(`/${tenant}/api/setup/rolesetup/permissions/add`, {
        roleName,
        permissionName
      }, {
        headers: { 'Authorization': `Bearer ${setupToken}` }
      });

      selectedPermissions[`${roleName}_${permissionName}`] = true;
    } catch (err: any) {
      error = err.message || 'Failed to add permission';
    } finally {
      isLoading = false;
    }
  }

  let isSetupComplete = $state(false);
  let redirectToDashboard = $state(false);

  async function completeSetup() {
    if (!setupToken) return;

    try {
      isLoading = true;
      error = null;

      await invalidateSetupToken(tenant!, setupToken!);

      sessionStorage.removeItem(`setupToken_${tenant}`);

      // Show completion message briefly before redirect
      isSetupComplete = true;

      // Redirect after a short delay to show completion
      setTimeout(() => {
        redirectToDashboard = true;
      }, 1500);
    } catch (err: any) {
      error = err.message || 'Failed to complete setup';
      isLoading = false;
    }
  }

  $effect(() => {
    if (redirectToDashboard) {
      goto(`/${tenant}/dashboard`);
    }
  });
</script>

<svelte:head>
  <title>Setup Your Workspace - TreeTopic</title>
</svelte:head>

{#if isSetupComplete}
  <div class="setup-container">
    <div class="setup-card-wrapper">
      <div class="setup-card">
        <div class="logo-section">
          <h1>TreeTopic</h1>
          <p>Collaborative discussion platform</p>
        </div>

        <div class="success-message">
          <div class="success-icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
              <polyline points="20 6 9 17 4 12"></polyline>
            </svg>
          </div>
          <h2>Setup Complete!</h2>
          <p>Redirecting to your workspace...</p>
        </div>

        <div class="footer-section">
          <p>Protected by setup token authentication.</p>
        </div>
      </div>

      <div class="copyright">
        <p>&copy; 2025 TreeTopic. All rights reserved.</p>
      </div>
    </div>
  </div>
{:else}
  <div class="setup-container">
    <div class="setup-card-wrapper">
      <div class="setup-card">
        <div class="logo-section">
          <h1>TreeTopic</h1>
          <p>Collaborative discussion platform</p>
        </div>

        <div class="welcome-section">
          <h2>Setup Your Workspace</h2>
          <p>Token expires in: <span class="expiry-time">{timeRemaining}</span></p>
        </div>

        {#if error}
          <div class="error-banner">
            <span>{error}</span>
            <button onclick={() => error = null} class="close-btn">×</button>
          </div>
        {/if}

        {#if !canManageRoles}
          <div class="info-banner">
            <p>
              Role management is handled through your OIDC provider.
              Roles are automatically synced from the <strong>{tenantDetail?.roleClaimName || 'role'}</strong> claim.
            </p>
          </div>
        {/if}

        {#if canManageRoles}
          <div class="form-section">
            <div class="form-group">
              <label for="role-name">Role Name</label>
              <input
                id="role-name"
                type="text"
                bind:value={newRoleName}
                placeholder="e.g., Administrator, Editor, Viewer"
                disabled={isLoading}
                class:input-error={!!error}
              />
            </div>

            <button
              onclick={createRole}
              disabled={isLoading || !newRoleName.trim()}
              class="submit-button"
            >
              {#if isLoading}
                <span class="loading-spinner"></span>
              {:else}
                Create Role
              {/if}
            </button>

            {#if roles.length > 0}
              <div class="roles-section">
                <h3>Roles</h3>
                {#each roles as role (role.id)}
                  <div class="role-item">
                    <div class="role-header">
                      <span class="role-name">{role.name}</span>
                      <button
                        onclick={() => deleteRole(role.name)}
                        disabled={isLoading}
                        class="delete-button"
                      >
                        Delete
                      </button>
                    </div>
                    <div class="role-permissions">
                      <p class="permissions-label">Assign permissions:</p>
                      <div class="permissions-list">
                        {#each availablePermissions as permission (permission)}
                          <label class="permission-checkbox">
                            <input
                              type="checkbox"
                              checked={selectedPermissions[`${role.name}_${permission}`] || false}
                              onchange={(e: Event) => {
                                const target = e.target as HTMLInputElement;
                                if (target.checked) {
                                  addPermissionToRole(role.name, permission);
                                }
                              }}
                              disabled={isLoading}
                            />
                            <span>{permission}</span>
                          </label>
                        {/each}
                      </div>
                    </div>
                  </div>
                {/each}
              </div>
            {:else}
              <p class="empty-message">No roles created yet. Create your first role above.</p>
            {/if}
          </div>
        {/if}

        {#if currentUser && canManageRoles}
          <div class="user-role-section">
            <h3>Assign Role to Yourself</h3>
            <p class="user-info">Current user: <strong>{currentUser.userName}</strong></p>

            <div class="current-roles-section">
              <p class="current-roles-label">Your current roles:</p>
              {#if currentUser.roles && currentUser.roles.length > 0}
                <div class="user-roles-list">
                  {#each currentUser.roles as userRole (userRole)}
                    <span class="user-role-badge-with-remove">
                      <span class="user-role-badge">{userRole}</span>
                      <button
                        onclick={() => removeRoleFromUser(userRole)}
                        disabled={isLoading}
                        class="role-remove-button"
                        title="Remove role"
                      >
                        ×
                      </button>
                    </span>
                  {/each}
                </div>
              {:else}
                <p class="no-roles-message">No roles assigned yet.</p>
              {/if}
            </div>

            <div class="role-assignment">
              <select
                bind:value={selectedRoleForUser}
                disabled={isLoading}
                class="role-select"
              >
                <option value="">Select a role...</option>
                {#each roles.filter(role => !currentUser.roles?.includes(role.name)) as role (role.id)}
                  <option value={role.name}>{role.name}</option>
                {/each}
              </select>
              <button
                onclick={assignRoleToUser}
                disabled={isLoading || !selectedRoleForUser}
                class="assign-button"
              >
                {#if isLoading}
                  <span class="loading-spinner"></span>
                {:else}
                  Assign Role
                {/if}
              </button>
            </div>
          </div>
        {/if}

        <div class="footer-section">
          <button
            onclick={completeSetup}
            disabled={isLoading}
            class="complete-button"
          >
            {#if isLoading}
              <span class="loading-spinner"></span>
            {:else}
              Complete Setup
            {/if}
          </button>
        </div>
      </div>

      <div class="copyright">
        <p>&copy; 2025 TreeTopic. All rights reserved.</p>
      </div>
    </div>
  </div>
{/if}

<style>
  .setup-container {
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: var(--spacing-lg);
    background-color: #1a1a1a;
  }

  .setup-card-wrapper {
    width: 100%;
    max-width: 500px;
  }

  .setup-card {
    background-color: #2a2a2a;
    border-radius: var(--border-radius-lg);
    border: 1px solid #404040;
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

  .expiry-time {
    font-family: 'Courier New', monospace;
    font-weight: 600;
    color: var(--color-text);
  }

  .error-banner {
    margin-bottom: 24px;
    padding: 12px 16px;
    background-color: var(--color-error-light, #fee2e2);
    border: 1px solid var(--color-error-border, var(--color-error));
    border-radius: var(--border-radius-lg);
    color: var(--color-error);
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 12px;
    font-size: var(--font-size-sm);
  }

  .close-btn {
    background: none;
    border: none;
    color: inherit;
    font-size: 20px;
    cursor: pointer;
    padding: 0;
    line-height: 1;
  }

  .info-banner {
    margin-bottom: 24px;
    padding: 16px;
    background-color: rgba(59, 130, 246, 0.1);
    border: 1px solid rgba(59, 130, 246, 0.3);
    border-radius: var(--border-radius-lg);
    color: var(--color-text);
    font-size: var(--font-size-sm);
  }

  .info-banner p {
    margin: 0;
    line-height: 1.6;
  }

  .form-section {
    display: flex;
    flex-direction: column;
    gap: 24px;
  }

  .form-group label {
    display: block;
    font-size: var(--font-size-sm);
    font-weight: 600;
    color: var(--color-text);
    margin-bottom: 8px;
  }

  .form-group input {
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

  .form-group input:focus {
    outline: none;
    border-color: var(--color-primary);
    box-shadow: 0 0 0 3px rgba(74, 144, 226, 0.1);
  }

  .form-group input:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .form-group input.input-error {
    border-color: var(--color-error);
    background-color: var(--color-error-light);
  }

  .submit-button {
    width: 100%;
    padding: 12px 20px;
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
    border-radius: var(--border-radius-lg);
    border: none;
    cursor: pointer;
    font-size: var(--font-size-sm);
    font-weight: 600;
    transition: all 0.2s ease;
  }

  .submit-button:hover:not(:disabled) {
    background-color: var(--color-primary-hover);
  }

  .submit-button:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .roles-section {
    display: flex;
    flex-direction: column;
    gap: 16px;
    margin-top: 16px;
  }

  .roles-section h3 {
    font-size: var(--font-size-base);
    font-weight: 600;
    color: var(--color-text);
    margin: 0;
  }

  .role-item {
    background-color: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: var(--border-radius-lg);
    padding: 16px;
  }

  .role-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
  }

  .role-name {
    font-weight: 600;
    color: var(--color-text);
  }

  .delete-button {
    padding: 6px 12px;
    background-color: var(--color-error);
    color: white;
    border-radius: var(--border-radius-lg);
    border: none;
    cursor: pointer;
    font-size: var(--font-size-xs);
    font-weight: 600;
    transition: all 0.2s ease;
  }

  .delete-button:hover:not(:disabled) {
    background-color: var(--color-error-hover, #dc2626);
  }

  .delete-button:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .role-permissions {
    margin-top: 12px;
  }

  .permissions-label {
    font-size: var(--font-size-xs);
    color: var(--color-text-light);
    margin: 0 0 8px 0;
  }

  .permissions-list {
    display: flex;
    flex-wrap: wrap;
    gap: 12px;
  }

  .permission-checkbox {
    display: flex;
    align-items: center;
    gap: 8px;
    cursor: pointer;
    font-size: var(--font-size-xs);
    color: var(--color-text);
  }

  .permission-checkbox input {
    width: 16px;
    height: 16px;
    cursor: pointer;
    accent-color: var(--color-primary);
  }

  .permission-checkbox input:disabled {
    cursor: not-allowed;
  }

  .empty-message {
    text-align: center;
    padding: 24px;
    color: var(--color-text-light);
    font-size: var(--font-size-sm);
    margin: 0;
  }

  .user-role-section {
    margin-top: 24px;
    padding: 16px;
    background-color: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: var(--border-radius-lg);
  }

  .user-role-section h3 {
    font-size: var(--font-size-base);
    font-weight: 600;
    color: var(--color-text);
    margin: 0 0 12px 0;
  }

  .user-info {
    font-size: var(--font-size-sm);
    color: var(--color-text-light);
    margin: 0 0 8px 0;
  }

  .current-roles-section {
    margin-bottom: 16px;
  }

  .current-roles-label {
    font-size: var(--font-size-sm);
    font-weight: 600;
    color: var(--color-text);
    margin: 0 0 8px 0;
  }

  .user-roles-list {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
  }

  .user-role-badge {
    display: inline-block;
    padding: 4px 12px;
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
    border-radius: var(--border-radius-md);
    font-size: var(--font-size-xs);
    font-weight: 500;
  }

  .no-roles-message {
    font-size: var(--font-size-sm);
    color: var(--color-text-light);
    margin: 0;
    font-style: italic;
  }

  .role-assignment {
    display: flex;
    gap: 12px;
    align-items: center;
  }

  .role-select {
    flex: 1;
    padding: 10px 12px;
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-lg);
    background-color: var(--color-background);
    color: var(--color-text);
    font-size: var(--font-size-sm);
    font-family: inherit;
    cursor: pointer;
  }

  .role-select:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .assign-button {
    padding: 10px 20px;
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
    border-radius: var(--border-radius-lg);
    border: none;
    cursor: pointer;
    font-size: var(--font-size-sm);
    font-weight: 600;
    transition: all 0.2s ease;
    white-space: nowrap;
  }

  .assign-button:hover:not(:disabled) {
    background-color: var(--color-primary-hover);
  }

  .assign-button:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .user-role-badge-with-remove {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 6px 4px 12px;
    background-color: var(--color-primary);
    border-radius: var(--border-radius-md);
  }

  .role-remove-button {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 18px;
    height: 18px;
    padding: 0;
    background-color: rgba(255, 255, 255, 0.2);
    color: white;
    border-radius: 50%;
    border: none;
    cursor: pointer;
    font-size: 16px;
    line-height: 1;
    transition: all 0.2s ease;
  }

  .role-remove-button:hover:not(:disabled) {
    background-color: rgba(255, 255, 255, 0.3);
  }

  .role-remove-button:disabled {
    opacity: 0.5;
    cursor: not-allowed;
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

  .complete-button {
    width: 100%;
    padding: 14px 20px;
    background-color: var(--color-primary);
    color: var(--color-text-inverse);
    border-radius: var(--border-radius-lg);
    border: none;
    cursor: pointer;
    font-size: var(--font-size-base);
    font-weight: 600;
    transition: all 0.2s ease;
  }

  .complete-button:hover:not(:disabled) {
    background-color: var(--color-primary-hover);
  }

  .complete-button:disabled {
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

  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }

  .copyright {
    margin-top: 40px;
    text-align: center;
  }

  .copyright p {
    font-size: var(--font-size-sm);
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
    background-color: #10b981;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    animation: scaleIn 0.3s ease-out;
  }

  .success-icon svg {
    width: 32px;
    height: 32px;
    color: white;
  }

  .success-message h2 {
    margin: 0 0 12px;
    font-size: 24px;
    font-weight: 600;
    color: var(--color-text);
  }

  .success-message p {
    margin: 0;
    font-size: 16px;
    color: var(--color-text-light);
  }

  @keyframes scaleIn {
    from {
      transform: scale(0);
      opacity: 0;
    }
    to {
      transform: scale(1);
      opacity: 1;
    }
  }
</style>
