using System;
using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class RoomUserDto
{
    public MaskedGuid Id { get; set; }
    public MaskedGuid ApplicationUserId { get; set; }
    public MaskedGuid RoomId { get; set; }
    public MaskedGuid RoomPermissionId { get; set; }
    public string? Name { get; set; }
    public bool UseMainName { get; set; }
    public string? DisplayName { get; set; }
    public string? IconUrl { get; set; }
    public bool UseMainIcon { get; set; }
}

public class CreateRoomUserRequest
{
    [Required]
    public MaskedGuid ApplicationUserId { get; set; }

    [Required]
    public MaskedGuid RoomPermissionId { get; set; }

    [StringLength(255)]
    public string? Name { get; set; }

    public bool? UseMainName { get; set; }
}

public class JoinRoomUserRequest
{
    [StringLength(255)]
    public string? Name { get; set; }

    public bool? UseMainName { get; set; }

    public bool? UseMainIcon { get; set; }
}
