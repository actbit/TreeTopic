import { redirect } from '@sveltejs/kit';
import type { PageLoad } from './$types';
import { getPublicTenantInfo } from '$lib/api/tenants';

export const load: PageLoad = async ({ params }) => {
  const { tenant } = params;

  // Validate that the tenant exists
  const tenantInfo = await getPublicTenantInfo(tenant);

  if (!tenantInfo) {
    // Tenant not found, redirect to home for tenant selection
    throw redirect(303, '/');
  }

  return {
    tenant,
    tenantInfo,
  };
};
