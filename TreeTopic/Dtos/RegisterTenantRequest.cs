using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

/// <summary>
/// テナント登録リクエスト
/// </summary>
public class RegisterTenantRequest
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
    public string? DbConnectionString { get; set; }

    /// <summary>
    /// OpenID Connect Role Claim Name（オプション）
    /// </summary>
    [StringLength(255, MinimumLength = 1)]
    public string? RoleClaimName { get; set; }

    /// <summary>
    /// OpenID Connect Authority URL
    /// </summary>
    [StringLength(2048)]
    public string? OpenIdConnectAuthority { get; set; }

    /// <summary>
    /// OpenID Connect Metadata Address
    /// </summary>
    [StringLength(2048)]
    public string? OpenIdConnectMetadataAddress { get; set; }

    /// <summary>
    /// OpenID Connect Client ID
    /// </summary>
    [StringLength(255)]
    public string? OpenIdConnectClientId { get; set; }

    /// <summary>
    /// OpenID Connect Client Secret
    /// </summary>
    [StringLength(500)]
    public string? OpenIdConnectClientSecret { get; set; }
}
