import { api } from '$lib/api/client';

export interface RawTenantInfo {
  identifier?: string;
  Identifier?: string;
  name?: string;
  Name?: string;
  [key: string]: unknown;
}

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

export async function getAllPublicTenants(): Promise<PublicTenantInfo[]> {
  if (publicTenantsInFlight) {
    return publicTenantsInFlight;
  }

  publicTenantsInFlight = (async () => {
    try {
      const response = await api.get<RawTenantInfo[]>('/api/tenants/public');
      const tenants = Array.isArray(response)
        ? response.map(normalizeTenantInfo).filter(t => t.identifier)
        : [];
      return tenants;
    } catch (error) {
      return [];
    } finally {
      publicTenantsInFlight = null;
    }
  })();

  return publicTenantsInFlight;
}

export async function getPublicTenantInfo(identifier: string): Promise<PublicTenantInfo | null> {
  try {
    const response = await api.get<RawTenantInfo>(`/api/tenants/public/${identifier}`);
    const tenant = normalizeTenantInfo(response);
    return tenant.identifier ? tenant : null;
  } catch (error) {
    return null;
  }
}
