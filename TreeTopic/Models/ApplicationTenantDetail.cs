using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Models
{
    public class ApplicationTenantDetail
    {
        [Key]
        public string TenantId { get; set; } = null!;

        public ApplicationTenantInfo? Tenant { get; set; }

        [StringLength(20)]
        public string? DbProvider { get; set; }

        [StringLength(200)]
        public string? TenantEncryptionKey { get; set; }

        [StringLength(5000)]
        public string? ConnectionString { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "MetadataAddress must be a valid URL")]
        public string? OpenIdConnectMetadataAddress { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "Authority must be a valid URL")]
        public string? OpenIdConnectAuthority { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "AuthorizationEndpoint must be a valid URL")]
        public string? OpenIdConnectAuthorizationEndpoint { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "TokenEndpoint must be a valid URL")]
        public string? OpenIdConnectTokenEndpoint { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "JwksUri must be a valid URL")]
        public string? OpenIdConnectJwksUri { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "EndSessionEndpoint must be a valid URL")]
        public string? OpenIdConnectEndSessionEndpoint { get; set; }

        [StringLength(500, MinimumLength = 1, ErrorMessage = "ClientId must be between 1 and 500 characters")]
        public string? OpenIdConnectClientId { get; set; }

        [StringLength(1000)]
        public string? OpenIdConnectClientSecret { get; set; }

        public string? RoleClaimName { get; set; }

        public ulong TenantObfuscationKeyK0 { get; set; }
        public ulong TenantObfuscationKeyK1 { get; set; }
    }

    public static class ApplicationTenantDetailExtensions
    {
        /// <summary>
        /// OIDC設定が有効かどうかを判定
        /// MetadataAddress または Authority、および ClientId が設定されている場合に true
        /// </summary>
        public static bool HasOidcSettings(this ApplicationTenantDetail? detail)
        {
            if (detail == null) return false;

            var hasMetadataOrAuthority =
                !string.IsNullOrWhiteSpace(detail.OpenIdConnectMetadataAddress) ||
                !string.IsNullOrWhiteSpace(detail.OpenIdConnectAuthority);

            var hasClientId = !string.IsNullOrWhiteSpace(detail.OpenIdConnectClientId);

            return hasMetadataOrAuthority && hasClientId;
        }

        /// <summary>
        /// OIDCロール同期が有効かどうかを判定
        /// OIDC設定があり、かつ RoleClaimName が設定されている場合に true
        /// </summary>
        public static bool HasOidcRoleSync(this ApplicationTenantDetail? detail)
        {
            if (!detail.HasOidcSettings()) return false;
            return !string.IsNullOrWhiteSpace(detail.RoleClaimName);
        }

        /// <summary>
        /// ユーザー作成が許可されているかどうかを判定
        /// OIDC設定がある場合はユーザー作成を禁止
        /// </summary>
        public static bool CanCreateUsers(this ApplicationTenantDetail? detail)
        {
            return !detail.HasOidcSettings();
        }

        /// <summary>
        /// ユーザーへのロール割り当てが許可されているかどうかを判定
        /// OIDCロール同期が有効な場合はユーザーへのロール割り当てを禁止
        /// </summary>
        public static bool CanAssignRolesToUsers(this ApplicationTenantDetail? detail)
        {
            return !detail.HasOidcRoleSync();
        }
    }
}
