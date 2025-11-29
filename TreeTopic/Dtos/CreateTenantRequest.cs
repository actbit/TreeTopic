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
    /// デフォルト: "postgres"
    /// </summary>
    [RegularExpression(@"(?i)^(postgres|postgresql|mysql)$", ErrorMessage = "DbProvider must be 'postgres', 'postgresql', or 'mysql'")]
    public string? DbProvider { get; set; }

    /// <summary>
    /// テナント用データベース接続文字列
    /// 未指定の場合、appsettings.json の "SharedApp" ConnectionString を使用
    /// （注: DB保存時に暗号化され5000文字まで可能）
    /// </summary>
    [StringLength(3000, MinimumLength = 10, ErrorMessage = "ConnectionString must be between 10 and 3000 characters")]
    public string? ConnectionString { get; set; }

    /// <summary>
    /// OIDC ロールクレーム名（オプション）
    /// テナント側でロール情報を取得する場合に設定
    /// 例: "roles", "groups"
    /// </summary>
    public string? RoleClaimName { get; set; }

    /// <summary>
    /// OpenID Connect メタデータアドレス（ディスカバリーエンドポイント）（オプション）
    /// テナント側で独自のOIDC プロバイダーを使用する場合に設定
    /// 例: http://localhost:8081/realms/master/.well-known/openid-configuration
    /// ここからすべてのエンドポイント情報が自動取得される
    /// </summary>
    public string? OpenIdConnectMetadataAddress { get; set; }

    /// <summary>
    /// OpenID Connect Authority（オプション、後方互換性のため）
    /// 未指定の場合、MetadataAddress から自動的にメタデータを取得
    /// Authority のみが指定された場合は、自動的に /.well-known/openid-configuration を構築
    /// 例: http://localhost:8081/realms/master
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
