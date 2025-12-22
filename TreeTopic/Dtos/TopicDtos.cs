using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Attributes;

namespace TreeTopic.Dtos;

public class TopicDto : BaseDto
{
    [MaskedUUID]
    public Guid RoomId { get; set; }

    [MaskedUUID]
    public Guid? ParentId { get; set; }
}

public class CreateTopicRequest : BaseCreateRequest
{
    [Required]
    [MaskedUUID]
    public Guid RoomId { get; set; }

    [MaskedUUID]
    public Guid? ParentId { get; set; }
}

public class UpdateTopicRequest : BaseUpdateRequest
{
    [MaskedUUID]
    public Guid? ParentId { get; set; }
}
