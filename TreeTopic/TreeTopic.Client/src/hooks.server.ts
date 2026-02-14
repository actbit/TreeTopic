import type { Handle } from '@sveltejs/kit';
import { redirect } from '@sveltejs/kit';

export const handle: Handle = async ({ event, resolve }) => {
  // 静的ファイルやAPIエンドポイントはスキップ
  if (event.url.pathname.startsWith('/_') || event.url.pathname.startsWith('/api/')) {
    return resolve(event);
  }

  // ログインページとsetupページはスキップ（setupはsetupTokenベース認証）
  if (event.url.pathname === '/login' ||
      event.url.pathname.match(/^\/[^/]+\/auth\/login/) ||
      event.url.pathname.match(/^\/[^/]+\/setup/)) {
    return resolve(event);
  }

  // HTMLリクエストのみで認証チェックを実行
  const acceptHeader = event.request.headers.get('accept');
  if (acceptHeader?.includes('text/html')) {
    // 現在のシステムではCookieベースの認証を使用。
    // 固定名(TreeTopic.Cookie)、テナント別Cookie名(TreeTopic.Cookie_<tenant>.Tenant)に対応する。
    const hasTenantScopedCookie = event.cookies
      .getAll()
      .some((cookie) =>
        cookie.name.startsWith('TreeTopic.Cookie_') &&
        cookie.name.endsWith('.Tenant') &&
        Boolean(cookie.value)
      );

    const hasAuthCookie =
      Boolean(event.cookies.get('TreeTopic.Cookie')) ||
      Boolean(event.cookies.get('AuthSession')) ||
      hasTenantScopedCookie;

    if (!hasAuthCookie) {
      // 未認証の場合はリダイレクト
      const pathSegments = event.url.pathname.split('/').filter(Boolean);
      const tenant = pathSegments[0] || null;

      let redirectUrl = '/login';
      if (tenant) {
        redirectUrl = `/${tenant}/auth/login`;
      }

      // リターンURLを設定
      if (event.url.pathname !== '/' && event.url.pathname !== '/login') {
        redirectUrl += `?returnUrl=${encodeURIComponent(event.url.pathname)}`;
      }

      throw redirect(302, redirectUrl);
    }
  }

  return resolve(event);
};
