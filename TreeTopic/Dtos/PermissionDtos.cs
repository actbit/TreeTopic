using System;
using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

public class PermissionDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public Guid RoleId { get; set; }
    public string? RoleName { get; set; }
}

public class PermissionModificationRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid RoleId { get; set; }
}
