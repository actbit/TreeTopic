using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Permissions;

namespace TreeTopic.Controllers;

/// <summary>
/// テナント管理コントローラ
/// </summary>
[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class TenantController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public TenantController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>
    /// 現在のユーザーのテナント権限一覧を取得
    /// </summary>
    [HttpGet("my/permissions")]
    public async Task<IActionResult> GetMyPermissions(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        // ユーザーのロールを取得
        var roles = await _userManager.GetRolesAsync(user);

        // 各ロールの権限を収集
        var permissions = new List<string>();
        foreach (var roleName in roles)
        {
            var role = await _db.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
            if (role != null)
            {
                var rolePermissions = await _db.Permissions
                    .AsNoTracking()
                    .Where(p => p.RoleId == role.Id)
                    .Select(p => p.Name)
                    .ToListAsync(cancellationToken);
                permissions.AddRange(rolePermissions);
            }
        }

        return Ok(new
        {
            permissions = permissions.Distinct().ToList()
        });
    }
}
