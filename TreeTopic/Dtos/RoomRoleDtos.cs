using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class RoomRoleDto
{
    public MaskedGuid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class CreateRoomRoleRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public List<string> Permissions { get; set; } = new();
}

public class UpdateRoomRoleRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public List<string> Permissions { get; set; } = new();
}

public class RoomRoleWithUsersDto : RoomRoleDto
{
    public int UserCount { get; set; }
}
