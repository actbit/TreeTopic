using System;
using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class PermissionDto
{
    public MaskedGuid Id { get; set; }
    public string? Name { get; set; }
    public MaskedGuid RoleId { get; set; }
    public string? RoleName { get; set; }
}

public class PermissionModificationRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public MaskedGuid RoleId { get; set; }
}
