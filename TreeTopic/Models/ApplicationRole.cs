using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class ApplicationRole : IdentityRole<Guid>
    {
        public ApplicationRole() : base()
        {
            Id = Guid.CreateVersion7();
        }
        public ApplicationRole(string roleName) : this()
        {
            Name = roleName;
        }
        public List<Permission> Authorities { get; set; } = new List<Permission>();
    }
}




