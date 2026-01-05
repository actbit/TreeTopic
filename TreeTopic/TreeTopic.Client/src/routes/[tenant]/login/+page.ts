import type { PageLoad } from './$types';

export const load: PageLoad = async ({ params }) => {
  const { tenant } = params;

  console.log('Loading login page for tenant:', tenant);

  // Return basic tenant data without validation
  // The tenant was already selected, so we can trust it
  return {
    tenant,
    tenantInfo: { identifier: tenant, name: tenant },
  };
};
