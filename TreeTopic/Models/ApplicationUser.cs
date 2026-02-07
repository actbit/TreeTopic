using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Identity;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? DisplayName { get; set; }
        public string? IconFileName { get; set; }
        public string? Sub { get; set; }
        public string? TenantId { get; set; }

        // Ban related properties
        public bool IsBanned { get; set; }
        public DateTime? BannedAt { get; set; }
        public string? BannedBy { get; set; }
        public string? BanReason { get; set; }

        public ApplicationUser() : base()
        {
            Id = Guid.CreateVersion7();
        }
        public ApplicationUser(string userName) : this()
        {
            UserName = userName;
            DisplayName = userName;
        }

        public List<Room> Rooms { get; set; } = new List<Room>();
        public List<RoomUser> RoomUsers { get; set; } = new List<RoomUser>();

    }
}




