using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class PermissionDto
{
    public MaskedGuid Id { get; set; }
    public string? Name { get; set; }
    public MaskedGuid RoleId { get; set; }
    public string? RoleName { get; set; }
}
