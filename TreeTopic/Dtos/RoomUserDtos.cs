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
}

public class CreateRoomUserRequest
{
    [Required]
    public MaskedGuid ApplicationUserId { get; set; }

    [Required]
    public MaskedGuid RoomPermissionId { get; set; }
}
