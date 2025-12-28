import { redirect } from '@sveltejs/kit';
import type { LayoutLoad } from './$types';
import { auth } from '$lib/stores/auth';
import { configureApiClient } from '$lib/api/client';

export const load: LayoutLoad = async ({ params, depends, url }) => {
  depends('app:auth');

  const { tenant } = params;

  if (!tenant) {
    throw redirect(303, '/');
  }

  // Configure API client with current tenant
  configureApiClient(tenant);

  // Skip auth check for login page (handled by +page.ts in login route)
  if (url.pathname === `/${tenant}/login`) {
    return { tenant };
  }

  // Check if user has an active session
  const hasSession = await auth.checkSession(tenant);

  if (!hasSession) {
    // No active session, redirect to login page for this tenant
    throw redirect(303, `/${tenant}/login`);
  }

  // Fetch current user information
  await auth.fetchCurrentUser(tenant);

  return {
    tenant,
  };
};
