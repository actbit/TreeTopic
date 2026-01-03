import { redirect } from '@sveltejs/kit';
import type { LayoutLoad } from './$types';
import { configureApiClient } from '$lib/api/client';

export const load: LayoutLoad = async ({ params, depends, url }) => {
  depends('app:auth');

  const { tenant } = params;

  if (!tenant) {
    throw redirect(303, '/');
  }

  // Configure API client with current tenant
  configureApiClient(tenant);

  return {
    tenant,
  };
};
