using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class MessageDto : BaseDto
{
    public MaskedGuid TopicId { get; set; }

    public MaskedGuid ApplicationUserId { get; set; }

    public string? UserName { get; set; }

    public string Header { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public MaskedGuid? ReplyId { get; set; }

    public List<MessageDto>? Replies { get; set; }

    public List<FileDto>? Files { get; set; }
}

public class CreateMessageRequest : BaseCreateRequest
{
    [Required]
    public MaskedGuid TopicId { get; set; }

    [Required]
    [StringLength(500)]
    public string Header { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public MaskedGuid? ReplyId { get; set; }

    // ファイルアップロード用
    public List<IFormFile>? Files { get; set; }
}

public class UpdateMessageRequest : BaseUpdateRequest
{
    [StringLength(500)]
    public string? Header { get; set; }

    public string? Body { get; set; }
}
