using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class TopicDto : BaseDto
{
    public MaskedGuid RoomId { get; set; }

    public MaskedGuid? ParentId { get; set; }

    public MaskedGuid? SourceMessageId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool HasChildren { get; set; }

    /// <summary>
    /// 子トピックのIDリスト
    /// </summary>
    public List<MaskedGuid> ChildIds { get; set; } = new();

    /// <summary>
    /// 未読メッセージ数
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// 最後に読んだメッセージID
    /// </summary>
    public MaskedGuid? LastReadMessageId { get; set; }
}

public class CreateTopicRequest : BaseCreateRequest
{
    [Required]
    public MaskedGuid RoomId { get; set; }

    public MaskedGuid? ParentId { get; set; }

    [Required]
    [MinLength(2)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public MaskedGuid? SourceMessageId { get; set; }
}

public class UpdateTopicRequest : BaseUpdateRequest
{
    public MaskedGuid? ParentId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }
}

public enum TopicDeleteStrategy
{
    Cascade = 0,
    ReparentToParent = 1
}

/// <summary>
/// 未読数などの統計情報を含むトピックDTO
/// N+1問題を解決するために使用
/// </summary>
public class TopicWithStatsDto : TopicDto
{
    /// <summary>
    /// トピック内の全メッセージ数
    /// </summary>
    public int TotalMessageCount { get; set; }

    /// <summary>
    /// 最終更新日時（メッセージの最新更新日時）
    /// </summary>
    public DateTime? LastMessageUpdatedAt { get; set; }

    /// <summary>
    /// 最終アクセス日時（UserTopicのLastAccessAt）
    /// </summary>
    public DateTime? LastAccessAt { get; set; }

    /// <summary>
    /// このトピックがアクセス可能かどうか
    /// </summary>
    public bool? IsAccessible { get; set; }
}
