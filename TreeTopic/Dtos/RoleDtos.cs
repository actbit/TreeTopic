using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

public class RoleDto
{
    public Guid Id { get; set; }

    public string? Name { get; set; }
}

public class RoleCreationRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string Name { get; set; } = string.Empty;
}
