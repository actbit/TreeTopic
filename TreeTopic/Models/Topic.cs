using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class Topic : BaseModel
    {
        [ForeignKey(nameof(Room))]
        public Guid RoomId { get; set; }
        public Room Room { get; set; }

        [ForeignKey(nameof(Parent))]
        public Guid ParentId { get; set; }
        public Topic Parent { get;set; }

    }
}
