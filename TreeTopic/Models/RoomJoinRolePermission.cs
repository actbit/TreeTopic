using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models;

[MultiTenant]
public class RoomJoinRolePermission : BaseModel
{
    [ForeignKey(nameof(Room))]
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    [ForeignKey(nameof(Role))]
    public Guid RoleId { get; set; }
    public ApplicationRole Role { get; set; } = null!;
}
