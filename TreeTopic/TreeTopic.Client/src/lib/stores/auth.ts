import { writable, derived } from 'svelte/store';
import { api } from '$lib/api/client';

/**
 * User information interface
 */
export interface User {
  id: string;
  userName: string;
  email: string;
  displayName: string;
  iconUrl?: string;
  avatar?: string;
  roles: string[];
}

/**
 * Authentication context
 */
export interface AuthContext {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
}

/**
 * Initialize auth store
 */
interface AuthCheckResponse {
  isAuthenticated?: boolean;
}

interface AuthMeResponse {
  userId: string;
  userName: string;
  displayName?: string;
  iconUrl?: string;
  email: string;
  roles?: string[];
}

function createAuthStore() {
  const { subscribe, set, update } = writable<AuthContext>({
    user: null,
    isAuthenticated: false,
    isLoading: false,
    error: null,
  });

  return {
    subscribe,
    /**
     * Check if session exists (cookie-based)
     */
    async checkSession(tenant: string): Promise<boolean> {
      try {
        const response = await api.get<AuthCheckResponse>(`/${tenant}/auth/check`);
        return response?.isAuthenticated ?? false;
      } catch {
        return false;
      }
    },
    /**
     * Fetch current user info
     */
    async fetchCurrentUser(tenant: string): Promise<void> {
      try {
        const userData = await api.get<AuthMeResponse>(`/${tenant}/auth/me`);
        set({
          user: {
            id: userData.userId,
            userName: userData.userName,
            email: userData.email,
            displayName: userData.displayName ?? userData.userName,
            iconUrl: userData.iconUrl,
            roles: userData.roles || [],
          },
          isAuthenticated: true,
          isLoading: false,
          error: null,
        });
      } catch (error) {
        const message =
          error instanceof api.ApiError
            ? error.data?.message ?? error.message ?? 'Failed to fetch user info'
            : 'Failed to fetch user info';

        set({
          user: null,
          isAuthenticated: false,
          isLoading: false,
          error: message,
        });

        throw error;
      }
    },
    /**
     * Set user information after login
     */
    setUser: (user: User) => {
      update((state) => ({
        ...state,
        user,
        isAuthenticated: true,
        error: null,
      }));
    },
    /**
     * Logout and clear local state
     */
    logout: () => {
      set({
        user: null,
        isAuthenticated: false,
        isLoading: false,
        error: null,
      });
    },
    /**
     * Clear local state only (for session expiration)
     */
    clear: () => {
      set({
        user: null,
        isAuthenticated: false,
        isLoading: false,
        error: null,
      });
    },
    /**
     * Set loading state
     */
    setLoading: (isLoading: boolean) => {
      update((state) => ({ ...state, isLoading }));
    },
    /**
     * Set error
     */
    setError: (error: string | null) => {
      update((state) => ({ ...state, error }));
    },
    /**
     * Update user profile
     */
    updateUser: (updates: Partial<User>) => {
      update((state) => ({
        ...state,
        user: state.user ? { ...state.user, ...updates } : null,
      }));
    },
  };
}

export const auth = createAuthStore();

/**
 * Derived stores
 */
export const currentUser = derived(auth, ($auth) => $auth.user);
export const isAuthenticated = derived(auth, ($auth) => $auth.isAuthenticated);
export const isLoading = derived(auth, ($auth) => $auth.isLoading);
export const authError = derived(auth, ($auth) => $auth.error);
export const userRoles = derived(currentUser, ($user) => $user?.roles ?? []);
export const hasRole = (role: string) => derived(userRoles, ($roles) => $roles.includes(role));
