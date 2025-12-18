using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class Room : BaseModel
    {
        public string Name { get; set; }

        [ForeignKey(nameof(CreatedUser))]
        public Guid CreatedUserId { get; set; }
        public ApplicationUser CreatedUser { get; set; }

        public List<Topic> Topics { get; set; } = new List<Topic>();
        public List<RoomUser> RoomUsers { get; set; } = new List<RoomUser>();
    }
}
