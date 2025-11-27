using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class RoomPermission : BaseModel
    {
        [ForeignKey(nameof(RoomUser))]
        public Guid RoomUserId { get; set; }
        public RoomUser RoomUser { get; set; }
    }
}
