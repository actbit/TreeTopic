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

    // BAN状態
    public bool IsBanned { get; set; }
    public string? BannedAt { get; set; }
    public string? BannedBy { get; set; }
    public string? BanReason { get; set; }
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

/// <summary>
/// ユーザー作成リクエスト (OIDC default用)
/// </summary>
public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    public string? Email { get; set; }
}

/// <summary>
/// ユーザー作成レスポンス
/// </summary>
public class CreateUserResponse
{
    public MaskedGuid Id { get; set; }
    public string? Email { get; set; }
    public string? UserName { get; set; }
}

/// <summary>
/// Banリクエスト
/// </summary>
public class BanUserRequest
{
    [Required]
    public string? Reason { get; set; }
}

/// <summary>
/// Ban解除レスポンス
/// </summary>
public class BanUserResponse
{
    public bool IsBanned { get; set; }
    public string? BannedAt { get; set; }
    public string? BannedBy { get; set; }
    public string? Reason { get; set; }
}
