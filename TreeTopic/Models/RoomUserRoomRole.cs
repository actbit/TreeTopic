using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models;

/// <summary>
/// RoomUser と RoomRole の多対多関係を管理する中間テーブル
/// </summary>
[MultiTenant]
public class RoomUserRoomRole : BaseModel
{
    /// <summary>
    /// RoomUser ID
    /// </summary>
    [ForeignKey(nameof(RoomUser))]
    public Guid RoomUserId { get; set; }
    public RoomUser RoomUser { get; set; } = null!;

    /// <summary>
    /// RoomRole ID
    /// </summary>
    [ForeignKey(nameof(RoomRole))]
    public Guid RoomRoleId { get; set; }
    public RoomRole RoomRole { get; set; } = null!;
}
