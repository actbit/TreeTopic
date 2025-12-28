import { api } from '$lib/api/client';

/**
 * Public tenant information
 */
export interface PublicTenantInfo {
  identifier: string;
  name: string;
}

/**
 * Get all public tenants
 * Used for tenant selection on home page
 */
export async function getAllPublicTenants(): Promise<PublicTenantInfo[]> {
  try {
    console.log('Fetching tenants from /api/tenants/public');
    const response = await api.get<PublicTenantInfo[]>('/api/tenants/public');
    console.log('Tenants fetched:', response);
    return response;
  } catch (error) {
    console.error('Failed to fetch public tenants:', error);
    if (error instanceof Error) {
      console.error('Error message:', error.message);
    }
    return [];
  }
}

/**
 * Get public tenant info by identifier
 * Used to validate tenant exists before showing login page
 */
export async function getPublicTenantInfo(identifier: string): Promise<PublicTenantInfo | null> {
  try {
    const response = await api.get<PublicTenantInfo>(`/api/tenants/public/${identifier}`);
    return response;
  } catch (error) {
    console.error(`Failed to fetch tenant info for ${identifier}`, error);
    return null;
  }
}
