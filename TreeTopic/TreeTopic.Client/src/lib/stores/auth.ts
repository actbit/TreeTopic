import { writable, derived } from 'svelte/store';
import { api } from '$lib/api/client';
import { getCachedAuth, setCachedAuth, clearAuthCache } from '$lib/utils/authCache';

/**
 * ユーザー情報インターフェース
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
 * 認証コンテキスト
 */
export interface AuthContext {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
}

/**
 * 認証ストア初期化
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
     * セッションが存在するかチェック（Cookieベース）
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
     * 現在のユーザー情報を取得
     * キャッシュを使用してAPI呼び出しの冗長化を回避
     */
    async fetchCurrentUser(tenant: string): Promise<void> {
      try {
        // 最初にキャッシュをチェック
        const cached = getCachedAuth(tenant);
        if (cached) {
          set({
            user: {
              id: cached.userId as string,
              userName: cached.userName as string,
              email: cached.email as string,
              displayName: (cached.displayName as string | undefined) ?? (cached.userName as string),
              iconUrl: cached.iconUrl as string | undefined,
              roles: cached.roles as string[] || [],
            },
            isAuthenticated: true,
            isLoading: false,
            error: null,
          });
          return;
        }

        const userData = await api.get<AuthMeResponse>(`/${tenant}/auth/me`);
        // ユーザーデータをキャッシュ
        setCachedAuth(tenant, userData as unknown as Record<string, unknown>);

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
            ? (error.data as { message?: string } | undefined)?.message ?? error.message ?? 'Failed to fetch user info'
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
     * ログイン後にユーザー情報を設定
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
     * POST リクエストでログアウトを実行
     */
    logout: async (tenant: string) => {
      try {
        // POST リクエストでログアウト
        await api.post(`/${tenant}/auth/logout`, {});
      } catch (error) {
        // エラーが発生してもローカル状態はクリア
        console.error('Logout error:', error);
      } finally {
        // 常にローカル状態をクリア
        clearAuthCache();
        set({
          user: null,
          isAuthenticated: false,
          isLoading: false,
          error: null,
        });
      }
    },
    /**
     * ローカル状態のみクリア（セッション期限切れ用）
     */
    clear: () => {
      clearAuthCache();
      set({
        user: null,
        isAuthenticated: false,
        isLoading: false,
        error: null,
      });
    },
    setLoading: (isLoading: boolean) => {
      update((state) => ({ ...state, isLoading }));
    },
    setError: (error: string | null) => {
      update((state) => ({ ...state, error }));
    },
    updateUser: (updates: Partial<User>) => {
      update((state) => ({
        ...state,
        user: state.user ? { ...state.user, ...updates } : null,
      }));
    },
  };
}

export const auth = createAuthStore();

export const currentUser = derived(auth, ($auth) => $auth.user);
export const isAuthenticated = derived(auth, ($auth) => $auth.isAuthenticated);
export const isLoading = derived(auth, ($auth) => $auth.isLoading);
export const authError = derived(auth, ($auth) => $auth.error);
export const userRoles = derived(currentUser, ($user) => $user?.roles ?? []);
export const hasRole = (role: string) => derived(userRoles, ($roles) => $roles.includes(role));
