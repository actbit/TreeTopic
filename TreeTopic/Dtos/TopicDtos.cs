using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class TopicDto : BaseDto
{
    public MaskedGuid RoomId { get; set; }

    public MaskedGuid? ParentId { get; set; }
}

public class CreateTopicRequest : BaseCreateRequest
{
    [Required]
    public MaskedGuid RoomId { get; set; }

    public MaskedGuid? ParentId { get; set; }
}

public class UpdateTopicRequest : BaseUpdateRequest
{
    public MaskedGuid? ParentId { get; set; }
}
