using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models;

[MultiTenant]
public class RoomJoinUserPermission : BaseModel
{
    [ForeignKey(nameof(Room))]
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    [ForeignKey(nameof(ApplicationUser))]
    public Guid ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; } = null!;
}
