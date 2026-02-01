using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models;

/// <summary>
/// RoomUserが特定トピックに対して持つ個別権限
/// ロール権限に追加される（または上書き）
/// </summary>
[MultiTenant]
public class TopicUserPermission : BaseModel
{
    /// <summary>
    /// 関連するトピックID
    /// </summary>
    [ForeignKey(nameof(Topic))]
    public Guid TopicId { get; set; }
    public Topic Topic { get; set; } = null!;

    /// <summary>
    /// 関連するルームユーザーID
    /// </summary>
    [ForeignKey(nameof(RoomUser))]
    public Guid RoomUserId { get; set; }
    public RoomUser RoomUser { get; set; } = null!;

    /// <summary>
    /// 権限名（例: "topic.read", "topic.write", "custom.permission"）
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
