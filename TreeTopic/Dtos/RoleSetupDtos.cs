using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

public class SetupTokenRequest
{
    [Required(ErrorMessage = "SetupToken is required")]
    public string SetupToken { get; set; } = string.Empty;
}

public class SetupRoleCreationRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public class SetupRoleDeletionRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string RoleName { get; set; } = string.Empty;
}

public class SetupPermissionRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string RoleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Permission name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Permission name must be between 1 and 256 characters")]
    public string PermissionName { get; set; } = string.Empty;
}

public class SetupPermissionDeletionRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string RoleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Permission name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Permission name must be between 1 and 256 characters")]
    public string PermissionName { get; set; } = string.Empty;
}

public class SetupDefaultRoleRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Default role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Default role name must be between 1 and 256 characters")]
    public string DefaultRoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> DefaultPermissions { get; set; } = new List<string>();
}

public class RoleSetupCompletionRequest : SetupTokenRequest
{
}

public class RoleSetupCompletionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int RolesCreated { get; set; }
    public int PermissionsAdded { get; set; }
    public string? DefaultRoleName { get; set; }
}
