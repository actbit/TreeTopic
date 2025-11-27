using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Identity;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class ApplicationRole : IdentityRole<Guid>
    {
        public ApplicationRole() : base()
        {
            Id = Guid.NewGuid();
        }
        public ApplicationRole(string roleName) : this()
        {
            Name = roleName;
        }
        public List<Permission> Authorities { get; set; } = new List<Permission>();
    }
}
