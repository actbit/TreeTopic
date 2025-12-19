using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Identity;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? DisplayName { get; set; }
        public string? Sub { get; set; }
        public string? TenantId { get; set; }
        public ApplicationUser() : base()
        {
            Id = Guid.NewGuid();

        }
        public ApplicationUser(string userName) : this()
        {
            UserName = userName;
            DisplayName = userName;
        }

        public List<Room> Rooms { get; set; } = new List<Room>();
        public List<Message> Messages { get; set; } = new List<Message>();
        public List<RoomUser> RoomUsers { get; set; } = new List<RoomUser>();
        public List<BrainIdea> BrainIdeas { get; set; } = new List<BrainIdea>();

    }
}
