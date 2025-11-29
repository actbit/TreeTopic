using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Models
{
    public class ApplicationTenantInfo : ITenantInfo
    {
        [Key]
        public string? Id { get; set; } = Guid.NewGuid().ToString();

        [StringLength(50)]
        public string? Identifier { get; set; }

        [StringLength(255)]
        public string? Name { get; set; }

        [StringLength(20)]
        public string? DbProvider { get; set; }

        // テナント用暗号化キー（マスターキーで暗号化済み）
        // 形式: nonce:ciphertext:tag (Base64)
        [StringLength(200)]
        public string? TenantEncryptionKey { get; set; }

        // ConnectionString は テナント用キーで暗号化
        // (元データ 3000 文字 → Base64 エンコード後 ~4000 文字 + overhead)
        [StringLength(5000)]
        public string? ConnectionString { get; set; }

        // OpenID Connect メタデータアドレス（ディスカバリーエンドポイント、テナント登録時に入力）
        // 例: http://localhost:8081/realms/master/.well-known/openid-configuration
        [StringLength(500)]
        public string? OpenIdConnectMetadataAddress { get; set; }

        // OpenID Connect 発行者（メタデータから自動取得）
        // 例: http://localhost:8081/realms/master
        [StringLength(500)]
        public string? OpenIdConnectAuthority { get; set; }

        // OpenID Connect 認可エンドポイント（メタデータから自動取得）
        // 例: http://localhost:8081/realms/master/protocol/openid-connect/auth
        [StringLength(500)]
        public string? OpenIdConnectAuthorizationEndpoint { get; set; }

        // OpenID Connect トークンエンドポイント（メタデータから自動取得）
        // 例: http://localhost:8081/realms/master/protocol/openid-connect/token
        [StringLength(500)]
        public string? OpenIdConnectTokenEndpoint { get; set; }

        // OpenID Connect JSON Web Key Set URI（メタデータから自動取得）
        // ID Token の署名検証に使用
        [StringLength(500)]
        public string? OpenIdConnectJwksUri { get; set; }

        // OpenID Connect エンドセッションエンドポイント（メタデータから自動取得）
        // ログアウト時にプロバイダー側のセッション終了に使用
        // 例: http://localhost:8081/realms/master/protocol/openid-connect/logout
        [StringLength(500)]
        public string? OpenIdConnectEndSessionEndpoint { get; set; }

        [StringLength(500)]
        public string? OpenIdConnecClientId { get; set; }

        // OpenIdConnecClientSecret も暗号化後の base64 データ（最大 1000 文字）
        [StringLength(1000)]
        public string? OpenIdConnecClientSecret { get; set; }

        // Role取得方法の設定
        // null: OIDCプロバイダーのデフォルトRole情報を使用
        // "Database": ApplicationDbContext から取得
        // その他: 指定したOIDCクレーム名から取得
        public string? RoleClaimName { get; set; }

        // UUIDv47Codec用の暗号化キー（テナント単位で管理）
        public ulong TenantObfuscationKeyK0 { get; set; }
        public ulong TenantObfuscationKeyK1 { get; set; }

        // リレーション：初期設定トークン
        public virtual ICollection<SetupToken>? SetupTokens { get; set; }
    }
}
