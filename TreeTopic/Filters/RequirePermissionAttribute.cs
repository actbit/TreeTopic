using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using TreeTopic.Models;

namespace TreeTopic.Filters;

/// <summary>
/// ASP.NET Core IdentityのPermissionをチェックするActionFilter
/// ApplicationRoleに関連する権限をチェックします
/// モデルバインド後に実行されます
///
/// 使用例:
/// [RequirePermission("user.manage")]
/// [RequirePermission("role.manage")]
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _permissionName;

    public RequirePermissionAttribute(string permissionName)
    {
        _permissionName = permissionName;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequirePermissionAttribute>>();
        var userManager = httpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

        // 認証済みユーザーを取得
        var user = httpContext.User;
        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // ユーザーIDを取得
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            logger.LogWarning("[RequirePermission] Invalid UserId claim: {UserIdClaim}", userIdClaim);
            context.Result = new UnauthorizedResult();
            return;
        }

        // ApplicationUserを取得
        var appUser = await userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            logger.LogWarning("[RequirePermission] ApplicationUser not found: UserId={UserId}", userId);
            context.Result = new UnauthorizedResult();
            return;
        }

        // ユーザーのロールを取得
        var roles = await userManager.GetRolesAsync(appUser);

        // ロールに関連するPermissionをチェック
        var hasPermission = await dbContext.Permissions
            .AnyAsync(p => roles.Contains(p.Role.Name) && p.Name == _permissionName, httpContext.RequestAborted);

        if (!hasPermission)
        {
            logger.LogWarning("[RequirePermission] Permission denied: UserId={UserId}, Permission={Permission}, Roles={Roles}",
                userId, _permissionName, string.Join(", ", roles));
            context.Result = new ForbidResult();
            return;
        }

        logger.LogDebug("[RequirePermission] Permission granted: UserId={UserId}, Permission={Permission}",
            userId, _permissionName);

        await next(); // アクションを実行
    }
}
