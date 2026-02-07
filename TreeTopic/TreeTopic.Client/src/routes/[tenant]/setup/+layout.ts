import { redirect } from '@sveltejs/kit';
import type { LayoutLoad } from './$types';

export const load: LayoutLoad = async ({ params }) => {
  const { tenant } = params;

  // This will be checked on the client side
  // as sessionStorage is only available in the browser
  return { tenant };
};
