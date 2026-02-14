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

  configureApiClient(tenant);

  const cachedAuth = getCachedAuth(tenant);
  const shouldFetchAuth = !cachedAuth;

  return {
    tenant,
    shouldFetchAuth,
  };
};
