using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models;

/// <summary>
/// RoomRoleが特定トピックに対して持つ権限を定義
/// ロールテンプレートとして機能する
/// </summary>
[MultiTenant]
public class TopicRolePermission : BaseModel
{
    /// <summary>
    /// 関連するトピックID
    /// </summary>
    [ForeignKey(nameof(Topic))]
    public Guid TopicId { get; set; }
    public Topic Topic { get; set; } = null!;

    /// <summary>
    /// 関連するルームロールID
    /// </summary>
    [ForeignKey(nameof(RoomRole))]
    public Guid RoomRoleId { get; set; }
    public RoomRole RoomRole { get; set; } = null!;

    /// <summary>
    /// 権限名（例: "topic.read", "topic.write", "custom.permission"）
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
