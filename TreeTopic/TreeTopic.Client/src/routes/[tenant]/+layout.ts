import { redirect } from '@sveltejs/kit';
import type { LayoutLoad } from './$types';
import { configureApiClient } from '$lib/api/client';
import { auth } from '$lib/stores/auth';
import { getCachedAuth } from '$lib/utils/authCache';

export const load: LayoutLoad = async ({ params, depends, url }) => {
  depends('app:auth');

  const { tenant } = params;

  if (!tenant) {
    throw redirect(303, '/');
  }

  // Configure API client with current tenant
  configureApiClient(tenant);

  // Check authentication from cache first to avoid redundant API calls
  // If cache is valid, we can skip the auth check for now
  const cachedAuth = getCachedAuth(tenant);
  const shouldFetchAuth = !cachedAuth;

  return {
    tenant,
    shouldFetchAuth,
  };
};
