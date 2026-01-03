using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Models
{
    public class ApplicationTenantInfo : TenantInfo
    {
        public ApplicationTenantInfo():base()
        {
            this.Id = Guid.CreateVersion7().ToString();
        }
        public ApplicationTenantInfo(string Id, string Identifier, string? Name = null) 
        {
            this.Id = Id;
            this.Identifier = Identifier;
            this.Name = Name ?? Identifier;
        }

        public ApplicationTenantInfo(string Identifier, string? Name = null)
            : this(Guid.CreateVersion7().ToString(), Identifier, Name ?? Identifier)
        {
        }


        [StringLength(50, MinimumLength = 1, ErrorMessage = "Identifier must be between 1 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Identifier can only contain alphanumeric characters, hyphens, and underscores")]
        public string? Identifier { get; set; }

        [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters")]
        public string? Name { get; set; }

        public ApplicationTenantDetail? Detail { get; set; }

        // リレーション：初期設定トークン
        public ICollection<SetupToken>? SetupTokens { get; set; }

        // Finbuckle.MultiTenant WithPerTenantAuthentication() 用のラッパープロパティ
        // Detail から OIDC 設定を公開することで、per-tenant 認証が正しく動作する
        public string? OpenIdConnectAuthority => Detail?.OpenIdConnectAuthority;
        public string? OpenIdConnectClientId => Detail?.OpenIdConnectClientId;
        public string? OpenIdConnectClientSecret => Detail?.OpenIdConnectClientSecret;
        public string? ChallengeScheme => !string.IsNullOrEmpty(OpenIdConnectAuthority) ? "oidc" : null;
    }
}

