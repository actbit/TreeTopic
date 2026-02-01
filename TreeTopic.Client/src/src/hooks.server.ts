import type { Handle } from '@sveltejs/kit';
import { redirect } '@sveltejs/kit';

export const handle: Handle = async ({ event, resolve }) => {
  // 静的ファイルやAPIエンドポイントはスキップ
  if (event.url.pathname.startsWith('/_') || event.url.pathname.startsWith('/api/')) {
    return resolve(event);
  }

  // ログインページはスキップ
  if (event.url.pathname === '/login' || event.url.pathname.match(/^\/[^/]+\/auth\/login/)) {
    return resolve(event);
  }

  // HTMLリクエストのみで認証チェックを実行
  if (event.request.headers.get('accept')?.includes('text/html')) {
    // セッションCookieの存在で認証状態を判断
    // 実際の認証チェックはサーバーサイドで行う
    const hasSession = event.request.headers.get('cookie')?.includes('AuthSession=') || false;

    if (!hasSession) {
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