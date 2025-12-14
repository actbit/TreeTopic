using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// ユーザー管理サービス
/// ユーザー取得・ロール管理を統括
/// </summary>
public class UserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<UserManagementService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <summary>
    /// すべてのユーザーをロール情報と共に取得
    /// </summary>
    public async Task<(bool Success, List<UserSummaryDto>? Users, string? ErrorMessage)> GetAllUsersAsync()
    {
        try
        {
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var userWithRoles = await Task.WhenAll(users.Select(async user =>
            {
                var roles = await _userManager.GetRolesAsync(user);
                return (user, roles);
            }));

            var summaries = userWithRoles.Select(tuple => UserToDto(tuple.user, tuple.roles)).ToList();
            return (true, summaries, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users");
            return (false, null, "An error occurred while retrieving users");
        }
    }

    /// <summary>
    /// ユーザーをIDで取得（ロール情報含む）
    /// </summary>
    public async Task<(bool Success, UserSummaryDto? User, string? ErrorMessage)> GetUserByIdAsync(Guid userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return (false, null, $"User '{userId}' not found");
            }

            var dto = await BuildUserDtoAsync(user);
            return (true, dto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", userId);
            return (false, null, "An error occurred while retrieving the user");
        }
    }

    /// <summary>
    /// ユーザーにロールを追加
    /// </summary>
    public async Task<(bool Success, UserSummaryDto? User, string? ErrorMessage)> AddRoleToUserAsync(
        Guid userId, RoleAssignmentRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return (false, null, $"User '{userId}' not found");
            }

            if (string.IsNullOrWhiteSpace(request.RoleName))
            {
                return (false, null, "RoleName is required");
            }

            var roleName = request.RoleName.Trim();
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                _logger.LogWarning("Role {RoleName} does not exist", roleName);
                return (false, null, $"Role '{roleName}' does not exist");
            }

            // ユーザーが既にこのロールを持っているかチェック
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Contains(roleName))
            {
                _logger.LogWarning("User {UserId} already has role {RoleName}", userId, roleName);
                var dto = await BuildUserDtoAsync(user);
                return (true, dto, null); // 既に持っている場合は成功を返す
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to add role {RoleName} to user {UserId}: {Errors}",
                    roleName, userId, errors);
                return (false, null, $"Failed to add role: {errors}");
            }

            _logger.LogInformation("Role {RoleName} added to user {UserId}", roleName, userId);
            var updatedDto = await BuildUserDtoAsync(user);
            return (true, updatedDto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding role to user {UserId}", userId);
            return (false, null, "An error occurred while adding the role");
        }
    }

    /// <summary>
    /// ユーザーからロールを削除
    /// </summary>
    public async Task<(bool Success, UserSummaryDto? User, string? ErrorMessage)> RemoveRoleFromUserAsync(
        Guid userId, RoleAssignmentRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return (false, null, $"User '{userId}' not found");
            }

            if (string.IsNullOrWhiteSpace(request.RoleName))
            {
                return (false, null, "RoleName is required");
            }

            var roleName = request.RoleName.Trim();
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                _logger.LogWarning("Role {RoleName} does not exist", roleName);
                return (false, null, $"Role '{roleName}' does not exist");
            }

            // ユーザーがこのロールを持っているかチェック
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(roleName))
            {
                _logger.LogWarning("User {UserId} does not have role {RoleName}", userId, roleName);
                var dto = await BuildUserDtoAsync(user);
                return (true, dto, null); // 持っていない場合は成功を返す
            }

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to remove role {RoleName} from user {UserId}: {Errors}",
                    roleName, userId, errors);
                return (false, null, $"Failed to remove role: {errors}");
            }

            _logger.LogInformation("Role {RoleName} removed from user {UserId}", roleName, userId);
            var updatedDto = await BuildUserDtoAsync(user);
            return (true, updatedDto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role from user {UserId}", userId);
            return (false, null, "An error occurred while removing the role");
        }
    }

    /// <summary>
    /// ユーザーオブジェクトからDtoを非同期で構築
    /// </summary>
    private async Task<UserSummaryDto> BuildUserDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return UserToDto(user, roles);
    }

    /// <summary>
    /// ユーザーオブジェクトからDtoを構築
    /// </summary>
    private static UserSummaryDto UserToDto(ApplicationUser user, IList<string> roles)
    {
        return new UserSummaryDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Roles = roles
        };
    }
}
