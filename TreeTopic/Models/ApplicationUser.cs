using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Identity;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? DisplayName { get; set; }
        public ApplicationUser() : base()
        {
            Id = Guid.NewGuid();

        }
        public ApplicationUser(string userName) : this()
        {
            UserName = userName;
            DisplayName = userName;
        }
    }
}
