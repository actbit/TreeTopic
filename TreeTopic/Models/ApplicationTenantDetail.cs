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
        public string? OpenIdConnectMetadataAddress { get; set; }

        [StringLength(500)]
        public string? OpenIdConnectAuthority { get; set; }

        [StringLength(500)]
        public string? OpenIdConnectAuthorizationEndpoint { get; set; }

        [StringLength(500)]
        public string? OpenIdConnectTokenEndpoint { get; set; }

        [StringLength(500)]
        public string? OpenIdConnectJwksUri { get; set; }

        [StringLength(500)]
        public string? OpenIdConnectEndSessionEndpoint { get; set; }

        [StringLength(500)]
        public string? OpenIdConnecClientId { get; set; }

        [StringLength(1000)]
        public string? OpenIdConnecClientSecret { get; set; }

        public string? RoleClaimName { get; set; }

        public ulong TenantObfuscationKeyK0 { get; set; }
        public ulong TenantObfuscationKeyK1 { get; set; }
    }
}
