using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

/// <summary>
/// トピックの基本情報（最小限のフィールド）
/// </summary>
public class TopicBasicDto
{
    public MaskedGuid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public MaskedGuid? ParentId { get; set; }
}

/// <summary>
/// トピックツリー表示用（hasChildrenと未読数付き）
/// </summary>
public class TopicTreeDto : TopicBasicDto
{
    public MaskedGuid RoomId { get; set; }
    public bool HasChildren { get; set; }
    public int UnreadCount { get; set; }
}

/// <summary>
/// トピック詳細情報（編集・管理用）
/// </summary>
public class TopicDetailDto : TopicBasicDto
{
    public MaskedGuid RoomId { get; set; }
    public MaskedGuid? SourceMessageId { get; set; }
    public string? Description { get; set; }
    public bool HasChildren { get; set; }
    public int UnreadCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 互換性のために残す古いDTO（新規コードでは使用禁止）
/// TODO: 移行完了後に削除
/// </summary>
[Obsolete("Use TopicBasicDto, TopicTreeDto, or TopicDetailDto instead")]
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
