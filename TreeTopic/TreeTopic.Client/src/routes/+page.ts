import type { PageLoad } from './$types';
import { normalizeTenantInfo } from '$lib/api/tenants';
import type { PublicTenantInfo } from '$lib/api/tenants';

type LandingLoadData = {
  tenants: PublicTenantInfo[];
  error: string | null;
};

function parseErrorPayload(payload: any): string {
  if (!payload) {
    return 'Failed to load workspaces';
  }

  if (typeof payload === 'string') {
    return payload;
  }

  if (typeof payload === 'object') {
    return (
      (payload.error?.message as string | undefined) ??
      (payload.message as string | undefined) ??
      'Failed to load workspaces'
    );
  }

  return 'Failed to load workspaces';
}

export const load: PageLoad<LandingLoadData> = async ({ fetch }) => {
  try {
    const response = await fetch('/api/tenants/public', {
      headers: {
        Accept: 'application/json',
      },
    });

    let payload: any;

    try {
      payload = await response.json();
    } catch {
      payload = null;
    }

    const tenants = Array.isArray(payload)
      ? payload
          .map((raw: any) => normalizeTenantInfo(raw))
          .filter((tenant) => tenant.identifier)
      : [];

    if (!response.ok) {
      return {
        tenants,
        error: parseErrorPayload(payload),
      };
    }

    return {
      tenants,
      error: null,
    };
  } catch (error) {
    const message =
      error instanceof Error ? error.message : 'Failed to load workspaces';
    return {
      tenants: [],
      error: message,
    };
  }
};
