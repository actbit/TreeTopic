using System;
using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

public class RoomUserDto
{
    public Guid Id { get; set; }
    public Guid ApplicationUserId { get; set; }
    public Guid RoomId { get; set; }
    public Guid RoomPermissionId { get; set; }
}

public class CreateRoomUserRequest
{
    [Required]
    public Guid ApplicationUserId { get; set; }

    [Required]
    public Guid RoomPermissionId { get; set; }
}
