using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class RoomUser : BaseModel
    {
        [ForeignKey(nameof(ApplicationUser))]
        public Guid ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }

        [ForeignKey(nameof(Room))]
        public Guid RoomId { get; set; }
        public Room Room { get; set; }

        [ForeignKey(nameof(RoomPermission))]
        public Guid RoomPermissonId { get; set; }
        public RoomPermission RoomPermission { get; set; }
    }
}
