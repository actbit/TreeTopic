using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

public class UserSummaryDto
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public IList<string>? Roles { get; set; }
}

public class RoleAssignmentRequest
{
    [Required]
    public string? RoleName { get; set; }
}
