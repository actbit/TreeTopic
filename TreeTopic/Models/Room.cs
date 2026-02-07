using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class Room : BaseModel
    {
        public string Name { get; set; }

        public string? Description { get; set; }

        public RoomJoinPolicy JoinPolicy { get; set; } = RoomJoinPolicy.Public;

        [ForeignKey(nameof(CreatedUser))]
        public Guid CreatedUserId { get; set; }
        public ApplicationUser CreatedUser { get; set; }

        public List<Topic> Topics { get; set; } = new List<Topic>();
        public List<RoomUser> RoomUsers { get; set; } = new List<RoomUser>();
        public List<RoomJoinUserPermission> JoinUserPermissions { get; set; } = new();
        public List<RoomJoinRolePermission> JoinRolePermissions { get; set; } = new();
    }
}




