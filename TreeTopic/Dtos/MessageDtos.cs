using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class MessageDto : BaseDto
{
    public MaskedGuid TopicId { get; set; }

    public MaskedGuid RoomUserId { get; set; }

    public string? UserName { get; set; }

    public string? UserAvatar { get; set; }

    public string Header { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public MaskedGuid? ReplyId { get; set; }

    public List<MessageDto>? Replies { get; set; }

    public List<FileDto>? Files { get; set; }

    public MaskedGuid? ChildTopicId { get; set; }
    public string? ChildTopicTitle { get; set; }
}

public class CreateMessageRequest : BaseCreateRequest
{
    [Required]
    public MaskedGuid TopicId { get; set; }

    [StringLength(500)]
    public string? Header { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    public MaskedGuid? ReplyId { get; set; }

    // ファイルアップロード用
    public List<IFormFile>? Files { get; set; }

    public CreateChildTopicRequest? ChildTopic { get; set; }
}

public class UpdateMessageRequest : BaseUpdateRequest
{
    [StringLength(500)]
    public string? Header { get; set; }

    public string? Body { get; set; }
}

public class CreateChildTopicRequest : BaseCreateRequest
{
    public MaskedGuid? ParentId { get; set; }

    [Required]
    [MinLength(2)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<MaskedGuid>? SelectedMessageIds { get; set; }

    /// <summary>
    /// 親トピックの権限を継承するかどうか（デフォルト: true）
    /// </summary>
    public bool InheritPermissions { get; set; } = true;
}

public class MoveMessagesRequest : BaseCreateRequest
{
    [Required]
    public MaskedGuid SourceTopicId { get; set; }

    [Required]
    public MaskedGuid TargetTopicId { get; set; }

    [Required]
    public MaskedGuid AnchorMessageId { get; set; }

    public bool IncludeAnchorMessage { get; set; } = false;
}
