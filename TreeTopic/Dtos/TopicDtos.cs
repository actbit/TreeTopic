using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

public class TopicDto : BaseDto
{
    public Guid RoomId { get; set; }
    public Guid? ParentId { get; set; }
}

public class CreateTopicRequest : BaseCreateRequest
{
    [Required]
    public Guid RoomId { get; set; }

    public Guid? ParentId { get; set; }
}

public class UpdateTopicRequest : BaseUpdateRequest
{
    public Guid? ParentId { get; set; }
}
