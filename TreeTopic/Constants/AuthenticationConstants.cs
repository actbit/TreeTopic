namespace TreeTopic.Constants;

/// <summary>
/// 認証関連の定数をまとめたクラス
/// マジックストリングを一箇所で管理し、重複を防ぐ
/// </summary>
public static class AuthenticationConstants
{
    /// <summary>
    /// テナント識別用の Claim タイプ
    /// </summary>
    public const string TenantClaimType = "tenant";

    /// <summary>
    /// OIDC スキーム名
    /// </summary>
    public const string OidcSchemeName = "oidc";

    /// <summary>
    /// Cookie 認証スキーム名
    /// </summary>
    public const string CookieSchemeName = "Cookies";

    /// <summary>
    /// Cookie 認証のデフォルトスキーム名
    /// </summary>
    public const string DefaultAuthenticationScheme = "Cookies";

    /// <summary>
    /// 認証パス関連
    /// </summary>
    public static class Paths
    {
        /// <summary>
        /// ログインページパス
        /// </summary>
        public const string LoginPath = "/auth/login";

        /// <summary>
        /// ログアウトパス
        /// </summary>
        public const string LogoutPath = "/auth/logout";

        /// <summary>
        /// OIDC サインイン（コールバック）パス
        /// </summary>
        public const string OidcCallbackPath = "/auth/signin-oidc";

        /// <summary>
        /// API エンドポイントプレフィックス
        /// </summary>
        public const string ApiPrefix = "/api";

        /// <summary>
        /// パスが API パスかどうかを判定
        /// </summary>
        public static bool IsApiPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (path.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase))
                return true;

            var trimmed = path.Trim('/');
            if (trimmed.Length == 0)
                return false;

            var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length >= 2 && string.Equals(segments[1], "api", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 静的ファイル検出用パターン
    /// </summary>
    public static class StaticFilePaths
    {
        /// <summary>
        /// SvelteKit アセット （_app フォルダ）
        /// </summary>
        public const string SvelteKitAssets = "/_app";

        /// <summary>
        /// CSS ファイルディレクトリ
        /// </summary>
        public const string CssDirectory = "/css";

        /// <summary>
        /// JavaScript ファイルディレクトリ
        /// </summary>
        public const string JavaScriptDirectory = "/js";

        /// <summary>
        /// 画像ファイルディレクトリ
        /// </summary>
        public const string ImagesDirectory = "/img";

        /// <summary>
        /// フォントファイルディレクトリ
        /// </summary>
        public const string FontsDirectory = "/fonts";

        /// <summary>
        /// CSS ファイル拡張子
        /// </summary>
        public const string CssFileExtension = ".css";

        /// <summary>
        /// JavaScript ファイル拡張子
        /// </summary>
        public const string JavaScriptFileExtension = ".js";

        /// <summary>
        /// PNG 画像ファイル拡張子
        /// </summary>
        public const string PngFileExtension = ".png";

        /// <summary>
        /// JPEG 画像ファイル拡張子
        /// </summary>
        public const string JpegFileExtension = ".jpg";

        /// <summary>
        /// Favicon ファイル拡張子
        /// </summary>
        public const string IcoFileExtension = ".ico";

        /// <summary>
        /// WOFF フォント拡張子
        /// </summary>
        public const string WoffFileExtension = ".woff";

        /// <summary>
        /// WOFF2 フォント拡張子
        /// </summary>
        public const string Woff2FileExtension = ".woff2";

        /// <summary>
        /// 静的ファイルかどうかを判定するメソッド
        /// </summary>
        public static bool IsStaticFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // ディレクトリチェック
            if (path.StartsWith(SvelteKitAssets) ||
                path.StartsWith(CssDirectory) ||
                path.StartsWith(JavaScriptDirectory) ||
                path.StartsWith(ImagesDirectory) ||
                path.StartsWith(FontsDirectory))
                return true;

            // ファイル拡張子チェック
            if (path.EndsWith(CssFileExtension) ||
                path.EndsWith(JavaScriptFileExtension) ||
                path.EndsWith(PngFileExtension) ||
                path.EndsWith(JpegFileExtension) ||
                path.EndsWith(IcoFileExtension) ||
                path.EndsWith(WoffFileExtension) ||
                path.EndsWith(Woff2FileExtension))
                return true;

            return false;
        }
    }

    /// <summary>
    /// Cookie 関連の定数
    /// </summary>
    public static class Cookie
    {
        /// <summary>
        /// Cookie 認証のデフォルト有効期限（時間）
        /// </summary>
        public const int ExpirationHours = 8;

        /// <summary>
        /// Cookie パス（全テナント共通）
        /// </summary>
        public const string CookiePath = "/";

        /// <summary>
        /// テナント別 Cookie 名の区切り文字
        /// </summary>
        public const string TenantCookieNameSeparator = "_";

        /// <summary>
        /// テナント別 Cookie 名の末尾サフィックス
        /// </summary>
        public const string TenantCookieSuffix = ".Tenant";

        /// <summary>
        /// HttpContext.Items に一時保存するテナント情報キー
        /// OnSigningIn イベント内で使用
        /// </summary>
        public const string TenantForCookieKey = "tenant_for_cookie";
    }

    /// <summary>
    /// CORS ポリシー名
    /// </summary>
    public static class CorsPolicy
    {
        /// <summary>
        /// 開発環境用 CORS ポリシー名
        /// </summary>
        public const string Development = "development";

        /// <summary>
        /// 本番環境用 CORS ポリシー名
        /// </summary>
        public const string Production = "production";
    }
}
