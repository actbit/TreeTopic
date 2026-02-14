using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class RoomUser : BaseModel
    {
        [ForeignKey(nameof(ApplicationUser))]
        public Guid ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; } = null!;

        [ForeignKey(nameof(Room))]
        public Guid RoomId { get; set; }
        public Room Room { get; set; } = null!;

        public string? Name { get; set; }

        public bool UseMainName { get; set; }

        public string? IconFileName { get; set; }

        public bool UseMainIcon { get; set; }

        public List<RoomPermission> RoomPermission { get; set; } = new List<RoomPermission>();

        /// <summary>
        /// このユーザーのトピック権限設定
        /// </summary>
        public List<TopicUserPermission> TopicUserPermissions { get; set; } = new();

        /// <summary>
        /// RoomUserとRoomRoleの多対多関係
        /// </summary>
        public List<RoomUserRoomRole> RoomUserRoomRoles { get; set; } = new();
    }
}
