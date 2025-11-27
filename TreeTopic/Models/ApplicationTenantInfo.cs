using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Models
{
    public class ApplicationTenantInfo : ITenantInfo
    {
        [Key]
        public string? Id { get; set; } = Guid.NewGuid().ToString();
        public string? Identifier { get; set; }
        public string? Name { get; set; }
        public string? DbProvider { get; set; }
        public string? ConnectionString { get; set; }

        public string? OpenIdConnctAuthority { get; set; }
        public string? OpenIdConnecClientId { get; set; }
        public string? OpenIdConnecClientSecret { get; set; }

        // Role取得方法の設定
        // null: OIDCプロバイダーのデフォルトRole情報を使用
        // "Database": ApplicationDbContext から取得
        // その他: 指定したOIDCクレーム名から取得
        public string? RoleClaimName { get; set; }

        // UUIDv47Codec用の暗号化キー（テナント単位で管理）
        public ulong TenantObfuscationKeyK0 { get; set; }
        public ulong TenantObfuscationKeyK1 { get; set; }
    }
}
