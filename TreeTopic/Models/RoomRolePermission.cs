using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models;

[MultiTenant]
public class RoomRolePermission : BaseModel
{
    [ForeignKey(nameof(RoomRole))]
    public Guid RoomRoleId { get; set; }
    public RoomRole RoomRole { get; set; } = null!;

    public string PermissionName { get; set; } = string.Empty;
}
