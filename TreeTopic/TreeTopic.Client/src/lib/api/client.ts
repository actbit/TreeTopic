/**
 * API Client Wrapper
 *
 * This wrapper provides:
 * - Authentication management
 * - Request/response interceptors
 * - Error handling
 * - Tenant context management
 *
 * Note: This integrates with OpenAPI auto-generated client from 'src/lib/api/generated'
 */

import { auth } from '$lib/stores/auth';
import type { User, AuthContext } from '$lib/stores/auth';
import { get as getStore } from 'svelte/store';
import { goto } from '$app/navigation';

/**
 * API Error
 */
export class ApiError extends Error {
  constructor(
    public status: number,
    public statusText: string,
    message: string,
    public data?: any
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/**
 * API Client configuration
 */
export interface ApiClientConfig {
  baseUrl: string;
  tenant: string;
  headers?: Record<string, string>;
}

/**
 * Initialize API client
 */
export function initializeApiClient(config: ApiClientConfig): void {
  // Store for later use in interceptors
  apiClientConfig = config;
}

/**
 * Global API client configuration
 */
let apiClientConfig: ApiClientConfig = {
  baseUrl: process.env.VITE_API_BASE_URL || '',
  tenant: '',
};

/**
 * Build headers for API request
 * Note: Cookie-based authentication is used (credentials: 'include')
 */
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

function redirectToTenantOidc(tenant: string): void {
  const returnUrl = buildReturnUrl(tenant);
  const encodedReturnUrl = encodeURIComponent(returnUrl);
  const loginUrl = `/${tenant}/auth/login?returnUrl=${encodedReturnUrl}`;

  if (typeof window !== 'undefined') {
    window.location.href = loginUrl;
  } else {
    goto(loginUrl);
  }
}

/**
 * Handle API response
 */
async function handleResponse<T>(response: Response): Promise<T> {
  const contentType = response.headers.get('content-type');
  let data: any;

  if (contentType?.includes('application/json')) {
    data = await response.json();
  } else {
    data = await response.text();
  }

  if (!response.ok) {
    const error = new ApiError(
      response.status,
      response.statusText,
      data?.error?.message || data?.message || 'An error occurred',
      data
    );

    // Handle 401 Unauthorized
    if (response.status === 401) {
      auth.clear();
      // Redirect to login page for current tenant
      const tenant = apiClientConfig.tenant || '';
      let path = '';

      if (response.url) {
        try {
          path = new URL(response.url).pathname;
        } catch {
          path = response.url;
        }
      }

      const isAuthStatusCheck = path.endsWith('/auth/me') || path.endsWith('/auth/check');
      const currentPath = typeof window !== 'undefined' ? window.location.pathname : '';
      const isOnLoginPage = tenant &&
        (currentPath === `/${tenant}/login` || currentPath === `/${tenant}/auth/login`);

      if (!isAuthStatusCheck && !isOnLoginPage) {
        if (tenant) {
          redirectToTenantOidc(tenant);
        } else {
          // If no tenant in context, redirect to home
          goto('/');
        }
      }
    }

    throw error;
  }

  return data as T;
}

/**
 * Make GET request
 */
export async function get<T>(
  path: string,
  options?: {
    headers?: Record<string, string>;
    params?: Record<string, any>;
  }
): Promise<T> {
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
    // Relative path
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

  return handleResponse<T>(response);
}

/**
 * Make POST request
 */
export async function post<T>(
  path: string,
  data?: any,
  options?: {
    headers?: Record<string, string>;
  }
): Promise<T> {
  const body = data instanceof FormData ? data : JSON.stringify(data);

  const headers = buildHeaders(options?.headers);
  // FormData should not include Content-Type header (browser will set it)
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

  return handleResponse<T>(response);
}

/**
 * Make PUT request
 */
export async function put<T>(
  path: string,
  data?: any,
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

  return handleResponse<T>(response);
}

/**
 * Make PATCH request
 */
export async function patch<T>(
  path: string,
  data?: any,
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

  return handleResponse<T>(response);
}

/**
 * Make DELETE request
 */
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
    return;
  }

  return handleResponse<T>(response);
}

/**
 * Configure API client with tenant
 */
export function configureApiClient(tenant: string): void {
  apiClientConfig.tenant = tenant;
}

/**
 * Get the current tenant from API client config
 */
export function getCurrentTenant(): string {
  return apiClientConfig.tenant;
}

/**
 * Set API base URL
 */
export function setApiBaseUrl(baseUrl: string): void {
  apiClientConfig.baseUrl = baseUrl;
}

/**
 * Get current API base URL
 */
export function getApiBaseUrl(): string {
  return apiClientConfig.baseUrl;
}

/**
 * Check API connectivity
 */
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

/**
 * Upload file with progress tracking
 */
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

/**
 * Retry API call with exponential backoff
 */
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

      // Don't retry on 4xx errors (except 429)
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

export default api;
