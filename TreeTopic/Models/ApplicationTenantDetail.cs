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
}
