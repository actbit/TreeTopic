using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

public class RoomDto : BaseDto
{
    public string Name { get; set; } = string.Empty;
    public Guid CreatedUserId { get; set; }
    public string? CreatedUserName { get; set; }
}

public class CreateRoomRequest : BaseCreateRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}

public class UpdateRoomRequest : BaseUpdateRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; set; }
}
