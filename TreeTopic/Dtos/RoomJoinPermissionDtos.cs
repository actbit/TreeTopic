using MaskedUUID.AspNetCore.Types;
using TreeTopic.Models;

namespace TreeTopic.Dtos;

public class RoomJoinPermissionsResponse
{
    public RoomJoinPolicy JoinPolicy { get; set; }
    public List<RoomJoinAllowedUserDto> Users { get; set; } = new();
    public List<RoomJoinAllowedRoleDto> Roles { get; set; } = new();
}

public class RoomJoinAllowedUserDto
{
    public MaskedGuid UserId { get; set; }
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
}

public class RoomJoinAllowedRoleDto
{
    public MaskedGuid RoleId { get; set; }
    public string? RoleName { get; set; }
}

public class RoomJoinAvailableUsersResponse
{
    public List<RoomJoinAllowedUserDto> Users { get; set; } = new();
}

public class RoomJoinAvailableRolesResponse
{
    public List<RoomJoinAllowedRoleDto> Roles { get; set; } = new();
}

public class UpdateRoomJoinPolicyRequest
{
    public RoomJoinPolicy JoinPolicy { get; set; }
}

public class AddRoomJoinAllowedUserRequest
{
    public MaskedGuid UserId { get; set; }
}

public class AddRoomJoinAllowedRoleRequest
{
    public MaskedGuid RoleId { get; set; }
}
