import { writable, derived } from 'svelte/store';

export interface Tenant {
  id: string;
  identifier: string;
  name: string;
  description?: string;
  logo?: string;
  settings?: {
    theme?: 'light' | 'dark' | 'auto';
    language?: string;
    timezone?: string;
    [key: string]: unknown;
  };
}

export interface TenantState {
  currentTenant: Tenant | null;
  tenants: Tenant[];
  isLoading: boolean;
  error: string | null;
}

function createTenantStore() {
  const { subscribe, set, update } = writable<TenantState>({
    currentTenant: null,
    tenants: [],
    isLoading: false,
    error: null,
  });

  return {
    subscribe,
    setCurrentTenant: (tenant: Tenant) => {
      update((state) => ({
        ...state,
        currentTenant: tenant,
        error: null,
      }));
      localStorage.setItem('current_tenant', tenant.identifier);
    },
    setTenants: (tenants: Tenant[]) => {
      update((state) => ({
        ...state,
        tenants,
        error: null,
      }));
    },
    addTenant: (tenant: Tenant) => {
      update((state) => ({
        ...state,
        tenants: [...state.tenants, tenant],
      }));
    },
    updateTenant: (tenantId: string, updates: Partial<Tenant>) => {
      update((state) => ({
        ...state,
        tenants: state.tenants.map((t) =>
          t.id === tenantId ? { ...t, ...updates } : t
        ),
        currentTenant:
          state.currentTenant?.id === tenantId
            ? { ...state.currentTenant, ...updates }
            : state.currentTenant,
      }));
    },
    removeTenant: (tenantId: string) => {
      update((state) => ({
        ...state,
        tenants: state.tenants.filter((t) => t.id !== tenantId),
        currentTenant:
          state.currentTenant?.id === tenantId ? null : state.currentTenant,
      }));
    },
    setLoading: (isLoading: boolean) => {
      update((state) => ({ ...state, isLoading }));
    },
    setError: (error: string | null) => {
      update((state) => ({ ...state, error }));
    },
    updateSettings: (settings: Tenant['settings']) => {
      update((state) => ({
        ...state,
        currentTenant: state.currentTenant
          ? { ...state.currentTenant, settings: { ...state.currentTenant.settings, ...settings } }
          : null,
      }));
    },
    clear: () => {
      set({
        currentTenant: null,
        tenants: [],
        isLoading: false,
        error: null,
      });
      localStorage.removeItem('current_tenant');
    },
  };
}

export const tenant = createTenantStore();

export const currentTenant = derived(tenant, ($tenant) => $tenant.currentTenant);
export const tenantList = derived(tenant, ($tenant) => $tenant.tenants);
export const tenantLoading = derived(tenant, ($tenant) => $tenant.isLoading);
export const tenantError = derived(tenant, ($tenant) => $tenant.error);

export const getTenantByIdentifier = (identifier: string) =>
  derived(tenantList, ($tenants) =>
    $tenants.find((t) => t.identifier === identifier)
  );

export const isTenantMember = derived(
  [currentTenant],
  ([$current]) => $current !== null
);
