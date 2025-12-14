using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

/// <summary>
/// Data Transfer Object for returning role information
/// </summary>
public class RoleDto
{
    /// <summary>
    /// Unique identifier of the role
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the role
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Request DTO for creating a new role
/// </summary>
public class RoleCreationRequest
{
    /// <summary>
    /// Name of the role to create
    /// </summary>
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string Name { get; set; } = string.Empty;
}
