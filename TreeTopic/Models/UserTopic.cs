using Finbuckle.MultiTenant;

namespace TreeTopic.Models;

/// <summary>
/// ユーザーとトピックの関係（未読管理、権限管理）
/// </summary>
[MultiTenant]
public class UserTopic : BaseModel
{
    /// <summary>
    /// ユーザーID（RoomUserのIdではなくApplicationUserIdを使用）
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// トピックID
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// 最後に読んだメッセージID
    /// </summary>
    public Guid? LastReadMessageId { get; set; }

    /// <summary>
    /// 最後にアクセスした日時
    /// </summary>
    public DateTime? LastAccessAt { get; set; }

    /// <summary>
    /// トピックへのアクセス権限（nullの場合はRoomの権限に依存）
    /// </summary>
    public bool? IsAccessible { get; set; }

    // ナビゲーションプロパティ
    public Topic? Topic { get; set; }
}
