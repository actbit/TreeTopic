using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class UserSummaryDto
{
    public MaskedGuid Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? IconUrl { get; set; }
    public IList<string>? Roles { get; set; }
}

public class RoleAssignmentRequest
{
    [Required]
    public string? RoleName { get; set; }
}

public class UpdateUserRequest
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string? DisplayName { get; set; }
}

/// <summary>
/// ユーザー設定用のDTO - フロントエンドに必要最小限の情報のみ
/// </summary>
public class ApplicationUserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? IconFileName { get; set; }
    public string? IconUrl { get; set; }
}
