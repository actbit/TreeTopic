import { api } from '$lib/api/client';

/**
 * Raw tenant info from API (supports both camelCase and PascalCase)
 */
export interface RawTenantInfo {
  identifier?: string;
  Identifier?: string;
  name?: string;
  Name?: string;
  [key: string]: unknown;
}

/**
 * Public tenant information
 */
export interface PublicTenantInfo {
  identifier: string;
  name: string;
}

let publicTenantsInFlight: Promise<PublicTenantInfo[]> | null = null;

export function normalizeTenantInfo(raw: RawTenantInfo): PublicTenantInfo {
  const identifier = raw?.identifier ?? raw?.Identifier ?? '';
  const name = raw?.name ?? raw?.Name ?? identifier;
  return { identifier, name };
}

/**
 * Get all public tenants
 * Used for tenant selection on home page
 */
export async function getAllPublicTenants(): Promise<PublicTenantInfo[]> {
  if (publicTenantsInFlight) {
    console.log('Reusing in-flight tenants request');
    return publicTenantsInFlight;
  }

  publicTenantsInFlight = (async () => {
    try {
      console.log('Fetching tenants from /api/tenants/public');
      const response = await api.get<RawTenantInfo[]>('/api/tenants/public');
      const tenants = Array.isArray(response)
        ? response.map(normalizeTenantInfo).filter(t => t.identifier)
        : [];
      console.log('Tenants fetched:', tenants);
      return tenants;
    } catch (error) {
      console.error('Failed to fetch public tenants:', error);
      if (error instanceof Error) {
        console.error('Error message:', error.message);
      }
      return [];
    } finally {
      publicTenantsInFlight = null;
    }
  })();

  return publicTenantsInFlight;
}

/**
 * Get public tenant info by identifier
 * Used to validate tenant exists before showing login page
 */
export async function getPublicTenantInfo(identifier: string): Promise<PublicTenantInfo | null> {
  try {
    const response = await api.get<RawTenantInfo>(`/api/tenants/public/${identifier}`);
    const tenant = normalizeTenantInfo(response);
    return tenant.identifier ? tenant : null;
  } catch (error) {
    console.error(`Failed to fetch tenant info for ${identifier}`, error);
    return null;
  }
}
