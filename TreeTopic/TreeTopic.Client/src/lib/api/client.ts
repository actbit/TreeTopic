import { auth } from '$lib/stores/auth';
import type { User, AuthContext } from '$lib/stores/auth';
import { get as getStore } from 'svelte/store';
import { goto } from '$app/navigation';
import * as cacheManager from './cache';
import { activeModals, ui } from '$lib/stores/ui';

export class ApiError extends Error {
  constructor(
    public status: number,
    public statusText: string,
    message: string,
    public data?: unknown
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export interface ApiClientConfig {
  baseUrl: string;
  tenant: string;
  headers?: Record<string, string>;
}

export function initializeApiClient(config: ApiClientConfig): void {
  apiClientConfig = config;
}

const DEFAULT_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string) ?? '';

let apiClientConfig: ApiClientConfig = {
  baseUrl: DEFAULT_BASE_URL,
  tenant: '',
};

function buildHeaders(customHeaders?: Record<string, string>): Record<string, string> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    Accept: 'application/json',
    ...customHeaders,
  };

  return headers;
}

function buildReturnUrl(tenant: string): string {
  if (typeof window !== 'undefined') {
    const { pathname, search, hash } = window.location;
    return `${pathname}${search}${hash}`;
  }

  return tenant ? `/${tenant}/` : '/';
}

function inferTenantFromPathname(pathname: string): string {
  const parts = pathname.split('/').filter(Boolean);
  return parts[0] ?? '';
}

let isRedirectingToLogin = false;
const forbiddenModalId = 'forbidden-access';

function redirectToTenantOidc(tenant: string): void {
  if (!tenant) return;
  if (isRedirectingToLogin) return;
  isRedirectingToLogin = true;

  const returnUrl = buildReturnUrl(tenant);
  const encodedReturnUrl = encodeURIComponent(returnUrl);
  const loginUrl = `/${tenant}/auth/login?returnUrl=${encodedReturnUrl}`;

  if (typeof window !== 'undefined') {
    window.location.href = loginUrl;
  } else {
    goto(loginUrl);
  }
}

function showForbiddenModal(data?: unknown): void {
  if (typeof window === 'undefined') return;

  const modals = getStore(activeModals);
  if (modals.some((modal) => modal.id === forbiddenModalId)) {
    return;
  }

  const message =
    (data as { message?: string } | undefined)?.message ??
    'You do not have permission to perform this action.';

  ui.openModal({
    id: forbiddenModalId,
    title: 'Access Denied',
    type: 'custom',
    data: { message },
  });
}

async function handleResponse<T>(response: Response): Promise<T> {
  const contentType = response.headers.get('content-type');
  let data: unknown;

  if (contentType?.includes('application/json')) {
    data = await response.json();
  } else {
    data = await response.text();
  }

  if (!response.ok) {
    const error = new ApiError(
      response.status,
      response.statusText,
      (data as { error?: { message?: string }; message?: string })?.error?.message
        || (data as { message?: string })?.message
        || 'An error occurred',
      data
    );

    let requestPath = '';
    if (response.url) {
      try {
        requestPath = new URL(response.url).pathname;
      } catch {
        requestPath = response.url;
      }
    }

    const currentPath = typeof window !== 'undefined' ? window.location.pathname : '';
    const tenantFromConfig = apiClientConfig.tenant || '';
    const tenant =
      tenantFromConfig ||
      (typeof window !== 'undefined' ? inferTenantFromPathname(currentPath) : '');

    const isAuthMeRequest = requestPath.toLowerCase().endsWith('/auth/me');
    const isSetupRequest = requestPath.includes('/api/setup/');
    const isPublicTenantRegistrationRequest =
      requestPath === '/api/tenants/register' ||
      requestPath === '/api/tenants/captcha';
    const isOnLoginPage =
      Boolean(tenant) &&
      (currentPath === `/${tenant}/login` || currentPath === `/${tenant}/auth/login`);

    if (
      !isOnLoginPage &&
      !isSetupRequest &&
      !isPublicTenantRegistrationRequest &&
      (response.status === 401 || (response.status === 404 && isAuthMeRequest))
    ) {
      // Clear all caches on authentication error
      auth.clear();
      cacheManager.clear();

      if (tenant) {
        redirectToTenantOidc(tenant);
      } else {
        // If no tenant in context, redirect to home
        goto('/');
      }
    }

    if (response.status === 403) {
      showForbiddenModal(data);
    }

    throw error;
  }

  return data as T;
}

export async function get<T>(
  path: string,
  options?: {
    headers?: Record<string, string>;
    params?: Record<string, any>;
    cache?: boolean; // false to bypass cache
  }
): Promise<T> {
  // Generate cache key
  const cacheKey = cacheManager.generateCacheKey('GET', path, options?.params);

  // Check cache first (unless explicitly bypassed)
  if (options?.cache !== false) {
    const cached = cacheManager.get<T>(cacheKey);
    if (cached !== null) {
      return cached;
    }
  }

  let url: string;

  if (apiClientConfig.baseUrl) {
    const urlObj = new URL(`${apiClientConfig.baseUrl}${path}`);
    if (options?.params) {
      Object.entries(options.params).forEach(([key, value]) => {
        if (value !== null && value !== undefined) {
          urlObj.searchParams.append(key, String(value));
        }
      });
    }
    url = urlObj.toString();
  } else {
    // 相対パス
    url = path;
    if (options?.params) {
      const params = new URLSearchParams();
      Object.entries(options.params).forEach(([key, value]) => {
        if (value !== null && value !== undefined) {
          params.append(key, String(value));
        }
      });
      const queryString = params.toString();
      if (queryString) {
        url += (url.includes('?') ? '&' : '?') + queryString;
      }
    }
  }

  const response = await fetch(url, {
    method: 'GET',
    headers: buildHeaders(options?.headers),
    credentials: 'include',
  });

  const result = await handleResponse<T>(response);

  // 成功時のみキャッシュ
  if (response.ok && result !== null && options?.cache !== false) {
    cacheManager.set(cacheKey, result);
  }

  return result;
}

export async function post<T>(
  path: string,
  data?: FormData | object,
  options?: {
    headers?: Record<string, string>;
  }
): Promise<T> {
  const body = data instanceof FormData ? data : JSON.stringify(data);

  const headers = buildHeaders(options?.headers);
  // FormDataの場合はContent-Typeヘッダーを削除（ブラウザが自動設定）
  if (data instanceof FormData) {
    delete headers['Content-Type'];
  }

  const url = apiClientConfig.baseUrl ? `${apiClientConfig.baseUrl}${path}` : path;

  const response = await fetch(url, {
    method: 'POST',
    headers,
    body,
    credentials: 'include',
  });

  const result = await handleResponse<T>(response);

  if (response.ok) {
    cacheManager.invalidateByResource('POST', path);
  }

  return result;
}

export async function put<T>(
  path: string,
  data?: object,
  options?: {
    headers?: Record<string, string>;
  }
): Promise<T> {
  const body = JSON.stringify(data);
  const url = apiClientConfig.baseUrl ? `${apiClientConfig.baseUrl}${path}` : path;

  const response = await fetch(url, {
    method: 'PUT',
    headers: buildHeaders(options?.headers),
    body,
    credentials: 'include',
  });

  const result = await handleResponse<T>(response);

  if (response.ok) {
    cacheManager.invalidateByResource('PUT', path);
  }

  return result;
}

export async function patch<T>(
  path: string,
  data?: object,
  options?: {
    headers?: Record<string, string>;
  }
): Promise<T> {
  const body = JSON.stringify(data);
  const url = apiClientConfig.baseUrl ? `${apiClientConfig.baseUrl}${path}` : path;

  const response = await fetch(url, {
    method: 'PATCH',
    headers: buildHeaders(options?.headers),
    body,
    credentials: 'include',
  });

  const result = await handleResponse<T>(response);

  if (response.ok) {
    cacheManager.invalidateByResource('PATCH', path);
  }

  return result;
}

export async function del<T>(
  path: string,
  options?: {
    headers?: Record<string, string>;
  }
): Promise<T | void> {
  const url = apiClientConfig.baseUrl ? `${apiClientConfig.baseUrl}${path}` : path;

  const response = await fetch(url, {
    method: 'DELETE',
    headers: buildHeaders(options?.headers),
    credentials: 'include',
  });

  if (response.status === 204) {
    cacheManager.invalidateByResource('DELETE', path);
    return;
  }

  const result = await handleResponse<T>(response);

  if (response.ok) {
    cacheManager.invalidateByResource('DELETE', path);
  }

  return result;
}

export function configureApiClient(tenant: string): void {
  apiClientConfig.tenant = tenant;
}

export function getCurrentTenant(): string {
  return apiClientConfig.tenant;
}

export function setApiBaseUrl(baseUrl: string): void {
  apiClientConfig.baseUrl = baseUrl;
}

export function getApiBaseUrl(): string {
  return apiClientConfig.baseUrl;
}

export async function checkApiHealth(): Promise<boolean> {
  try {
    const url = apiClientConfig.baseUrl ? `${apiClientConfig.baseUrl}/health` : '/health';
    const response = await fetch(url, {
      method: 'GET',
      credentials: 'include',
    });
    return response.ok;
  } catch {
    return false;
  }
}

export async function uploadFile(
  path: string,
  file: File,
  onProgress?: (progress: number) => void
): Promise<any> {
  return new Promise((resolve, reject) => {
    const formData = new FormData();
    formData.append('file', file);

    const xhr = new XMLHttpRequest();

    if (onProgress) {
      xhr.upload.addEventListener('progress', (event) => {
        if (event.lengthComputable) {
          const progress = (event.loaded / event.total) * 100;
          onProgress(progress);
        }
      });
    }

    xhr.addEventListener('load', () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          const response = JSON.parse(xhr.responseText);
          resolve(response);
        } catch {
          resolve(xhr.responseText);
        }
      } else if (xhr.status === 401) {
        auth.clear();
        const tenant =
          apiClientConfig.tenant ||
          (typeof window !== 'undefined' ? inferTenantFromPathname(window.location.pathname) : '');
        const currentPath = typeof window !== 'undefined' ? window.location.pathname : '';
        const isOnLoginPage = tenant &&
          (currentPath === `/${tenant}/login` || currentPath === `/${tenant}/auth/login`);
        if (tenant && !isOnLoginPage) redirectToTenantOidc(tenant);
        reject(new ApiError(xhr.status, xhr.statusText, 'Unauthorized'));
      } else if (xhr.status === 403) {
        showForbiddenModal();
        reject(new ApiError(xhr.status, xhr.statusText, 'Forbidden'));
      } else {
        reject(
          new ApiError(
            xhr.status,
            xhr.statusText,
            'Upload failed'
          )
        );
      }
    });

    xhr.addEventListener('error', () => {
      reject(new ApiError(0, 'Network Error', 'Failed to upload file'));
    });

    xhr.addEventListener('abort', () => {
      reject(new ApiError(0, 'Aborted', 'Upload was cancelled'));
    });

    const headers = buildHeaders();
    Object.entries(headers).forEach(([key, value]) => {
      if (key !== 'Content-Type') {
        xhr.setRequestHeader(key, value);
      }
    });

    const url = apiClientConfig.baseUrl ? `${apiClientConfig.baseUrl}${path}` : path;
    xhr.open('POST', url);
    xhr.withCredentials = true;
    xhr.send(formData);
  });
}

export async function retryWithBackoff<T>(
  fn: () => Promise<T>,
  maxRetries: number = 3,
  delay: number = 1000
): Promise<T> {
  let lastError: Error | null = null;

  for (let i = 0; i < maxRetries; i++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error as Error;

      // 4xxエラーはリトライしない（429を除く）
      if (error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 429) {
        throw error;
      }

      if (i < maxRetries - 1) {
        await new Promise((resolve) => setTimeout(resolve, delay * Math.pow(2, i)));
      }
    }
  }

  throw lastError || new Error('Max retries exceeded');
}

export const api = {
  get,
  post,
  put,
  patch,
  delete: del,
  configureApiClient,
  getCurrentTenant,
  setApiBaseUrl,
  getApiBaseUrl,
  checkApiHealth,
  uploadFile,
  retryWithBackoff,
  ApiError,
};

export async function getRolesWithSetupToken(tenant: string, setupToken: string) {
  return api.get(`/${tenant}/api/setup/rolesetup`, {
    headers: { 'Authorization': `Bearer ${setupToken}` }
  });
}

export async function createRoleWithSetupToken(
  tenant: string,
  setupToken: string,
  roleName: string
) {
  return api.post(`/${tenant}/api/setup/rolesetup/create`, {
    name: roleName
  }, {
    headers: { 'Authorization': `Bearer ${setupToken}` }
  });
}

export async function addPermissionWithSetupToken(
  tenant: string,
  setupToken: string,
  roleName: string,
  permissionName: string
) {
  return api.post(`/${tenant}/api/setup/rolesetup/permissions/add`, {
    roleName,
    permissionName
  }, {
    headers: { 'Authorization': `Bearer ${setupToken}` }
  });
}

export async function getTenantDetail(tenant: string) {
  return api.get(`/${tenant}/api/tenant/detail`);
}

export async function createUserWithSetupToken(tenant: string, email: string, setupToken: string) {
  return api.post(`/${tenant}/api/setup/defaultuser`, { email }, {
    headers: { 'Authorization': `Bearer ${setupToken}` }
  });
}

export async function assignUserRoleWithSetupToken(
  tenant: string,
  userId: string,
  roleName: string,
  setupToken: string
) {
  if (!userId || userId === 'undefined' || userId === 'null') {
    throw new Error('Invalid userId for setup role assignment');
  }
  return api.post(
    `/${tenant}/api/setup/users/${userId}/roles`,
    { roleName },
    { headers: { 'Authorization': `Bearer ${setupToken}` } }
  );
}

export async function removeUserRoleWithSetupToken(
  tenant: string,
  userId: string,
  roleName: string,
  setupToken: string
) {
  if (!userId || userId === 'undefined' || userId === 'null') {
    throw new Error('Invalid userId for setup role removal');
  }
  const url = apiClientConfig.baseUrl ? `${apiClientConfig.baseUrl}/${tenant}/api/setup/users/${userId}/roles` : `/${tenant}/api/setup/users/${userId}/roles`;

  const response = await fetch(url, {
    method: 'DELETE',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${setupToken}`
    },
    body: JSON.stringify({ roleName }),
    credentials: 'include',
  });

  return handleResponse(response);
}

export async function invalidateSetupToken(tenant: string, setupToken: string) {
  return api.post(`/${tenant}/api/setup/token/invalidate`, {}, {
    headers: { 'Authorization': `Bearer ${setupToken}` }
  });
}

export async function getCurrentUser(tenant: string) {
  return api.get(`/${tenant}/auth/me`);
}

export async function checkUserPermissions(tenant: string) {
  return api.get(`/${tenant}/auth/me/permissions`);
}

export async function createUser(tenant: string, email: string) {
  return api.post(`/${tenant}/api/users`, { email });
}

export async function banUser(tenant: string, userId: string, reason: string) {
  return api.post(`/${tenant}/api/users/${userId}/ban`, { reason });
}

export async function unbanUser(tenant: string, userId: string) {
  return api.delete(`/${tenant}/api/users/${userId}/ban`);
}

export async function assignUserRole(tenant: string, userId: string, roleName: string) {
  return api.post(`/${tenant}/api/users/${userId}/roles`, { roleName });
}

export async function removeUserRole(tenant: string, userId: string, roleName: string) {
  const url = apiClientConfig.baseUrl ? `${apiClientConfig.baseUrl}/${tenant}/api/users/${userId}/roles` : `/${tenant}/api/users/${userId}/roles`;

  const response = await fetch(url, {
    method: 'DELETE',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json'
    },
    body: JSON.stringify({ roleName }),
    credentials: 'include',
  });

  return handleResponse(response);
}

export { removeUserRole as deleteUserRole };

export async function getRoomUserCandidates(tenant: string, roomId: string, search?: string) {
  const params = search ? { search } : undefined;
  return api.get(`/${tenant}/api/room/${roomId}/users/candidates`, { params });
}

export default api;
