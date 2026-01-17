using System;
using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class RoomUserDto
{
    public MaskedGuid Id { get; set; }
    public MaskedGuid ApplicationUserId { get; set; }
    public MaskedGuid RoomId { get; set; }
    /// <summary>
    /// Display name (already resolved based on UseMainName setting)
    /// </summary>
    public string? DisplayName { get; set; }
    /// <summary>
    /// Icon URL (already resolved based on UseMainIcon setting)
    /// </summary>
    public string? IconUrl { get; set; }
    /// <summary>
    /// Whether to use the main account's name
    /// </summary>
    public bool UseMainName { get; set; }
    /// <summary>
    /// Whether to use the main account's icon
    /// </summary>
    public bool UseMainIcon { get; set; }
}

public class CreateRoomUserRequest
{
    [Required]
    public MaskedGuid ApplicationUserId { get; set; }

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

public class UpdateRoomUserRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? DisplayName { get; set; }

    public bool? UseMainName { get; set; }

    public bool? UseMainIcon { get; set; }
}
