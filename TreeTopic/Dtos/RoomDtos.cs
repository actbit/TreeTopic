using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Models;

namespace TreeTopic.Dtos;

public class RoomDto : BaseDto
{
    public new MaskedGuid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public RoomJoinPolicy JoinPolicy { get; set; } = RoomJoinPolicy.Public;

    public MaskedGuid CreatedUserId { get; set; }

    public string? CreatedUserName { get; set; }

    public bool IsJoined { get; set; }

    public bool CanJoin { get; set; }
}

public class CreateRoomRequest : BaseCreateRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public RoomJoinPolicy JoinPolicy { get; set; } = RoomJoinPolicy.Public;
}

public class UpdateRoomRequest : BaseUpdateRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public RoomJoinPolicy? JoinPolicy { get; set; }
}
