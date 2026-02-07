using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models;

[MultiTenant]
public class RoomRole : BaseModel
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public List<RoomRolePermission> Permissions { get; set; } = new();

    /// <summary>
    /// このロールのトピック権限設定
    /// </summary>
    public List<TopicRolePermission> TopicRolePermissions { get; set; } = new();

    /// <summary>
    /// RoomUserとの多対多関係
    /// </summary>
    public List<RoomUserRoomRole> RoomUserRoomRoles { get; set; } = new();
}

