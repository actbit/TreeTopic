using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class TopicDto : BaseDto
{
    public MaskedGuid RoomId { get; set; }

    public MaskedGuid? ParentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
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
}

public class UpdateTopicRequest : BaseUpdateRequest
{
    public MaskedGuid? ParentId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }
}
