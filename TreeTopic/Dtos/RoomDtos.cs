using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class RoomDto : BaseDto
{
    public new MaskedGuid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public MaskedGuid CreatedUserId { get; set; }

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
