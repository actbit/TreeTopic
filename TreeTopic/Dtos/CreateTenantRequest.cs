using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

/// <summary>
/// テナント作成リクエスト
/// </summary>
public class CreateTenantRequest
{
    /// <summary>
    /// テナント識別子（外部公開用、URLに使用可能）
    /// 例: "acme-corp", "tenant-001"
    /// </summary>
    [Required(ErrorMessage = "Identifier is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Identifier must be between 3 and 50 characters")]
    [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Identifier can only contain lowercase letters, numbers, and hyphens")]
    public required string Identifier { get; set; }

    /// <summary>
    /// テナント表示名
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters")]
    public required string Name { get; set; }

    /// <summary>
    /// データベースプロバイダー
    /// "postgres", "postgresql" または "mysql" (大文字・小文字対応)
    /// </summary>
    [Required(ErrorMessage = "DbProvider is required")]
    [RegularExpression(@"(?i)^(postgres|postgresql|mysql)$", ErrorMessage = "DbProvider must be 'postgres', 'postgresql', or 'mysql'")]
    public required string DbProvider { get; set; }

    /// <summary>
    /// テナント用データベース接続文字列
    /// （注: DB保存時に暗号化され4000文字まで可能）
    /// </summary>
    [Required(ErrorMessage = "ConnectionString is required")]
    [StringLength(3000, MinimumLength = 10, ErrorMessage = "ConnectionString must be between 10 and 3000 characters")]
    public required string ConnectionString { get; set; }

    /// <summary>
    /// OIDC ロールクレーム名（オプション）
    /// テナント側でロール情報を取得する場合に設定
    /// 例: "roles", "groups"
    /// </summary>
    public string? RoleClaimName { get; set; }

    /// <summary>
    /// OpenID Connect プロバイダーの Authority（オプション）
    /// テナント側で独自のOIDC プロバイダーを使用する場合に設定
    /// </summary>
    public string? OpenIdConnectAuthority { get; set; }

    /// <summary>
    /// OpenID Connect Client ID（オプション）
    /// </summary>
    public string? OpenIdConnectClientId { get; set; }

    /// <summary>
    /// OpenID Connect Client Secret（オプション）
    /// サーバーサイドアプリケーション用の秘密鍵
    /// </summary>
    [StringLength(500, ErrorMessage = "OpenIdConnectClientSecret must not exceed 500 characters")]
    public string? OpenIdConnectClientSecret { get; set; }
}
