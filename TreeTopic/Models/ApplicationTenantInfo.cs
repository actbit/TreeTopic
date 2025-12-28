using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Models
{
    public class ApplicationTenantInfo : TenantInfo
    {
        public ApplicationTenantInfo():base()
        {
            this.Id = Guid.NewGuid().ToString();
        }
        public ApplicationTenantInfo(string Id, string Identifier, string? Name = null) 
        {
            this.Id = Id;
            this.Identifier = Identifier;
            this.Name = Name ?? Identifier;
        }

        public ApplicationTenantInfo(string Identifier, string? Name = null)
            : this(Guid.NewGuid().ToString(), Identifier, Name ?? Identifier)
        {
        }

        [Key]
        public string? Id { get; set; } = Guid.NewGuid().ToString();

        [StringLength(50)]
        public string? Identifier { get; set; }

        [StringLength(255)]
        public string? Name { get; set; }

        public ApplicationTenantDetail? Detail { get; set; }

        // リレーション：初期設定トークン
        public ICollection<SetupToken>? SetupTokens { get; set; }
    }
}

