using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize(Roles = "Admin")]
public class PermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public PermissionsController(
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _context = context;
        _roleManager = roleManager;
        _tenantAccessor = tenantAccessor;
    }

    private string? CurrentTenantId => _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var query = _context.Permissions.Include(p => p.Role).AsQueryable();
        var tenantId = CurrentTenantId;
        if (!string.IsNullOrEmpty(tenantId))
        {
            query = query.Where(p => p.TenantId == tenantId);
        }

        var permissions = await query.ToListAsync(cancellationToken);

        var mapped = permissions.Select(PermissionToDto).ToList();
        return Ok(mapped);
    }

    [HttpGet("{permissionId:guid}")]
    public async Task<ActionResult> Get(Guid permissionId, CancellationToken cancellationToken)
    {
        var permission = await _context.Permissions
            .Include(p => p.Role)
            .FirstOrDefaultAsync(p => p.Id == permissionId, cancellationToken);

        if (permission == null)
        {
            return NotFound(new { message = $"Permission '{permissionId}' not found" });
        }

        return Ok(PermissionToDto(permission));
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] PermissionModificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var role = await _roleManager.FindByIdAsync(request.RoleId.ToString());
        if (role == null)
        {
            return NotFound(new { message = $"Role '{request.RoleId}' not found" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required" });
        }

        var permission = new Permission
        {
            Name = request.Name.Trim(),
            RoleId = request.RoleId,
            TenantId = CurrentTenantId ?? string.Empty
        };

        _context.Permissions.Add(permission);
        await _context.SaveChangesAsync();

        await _context.Entry(permission).Reference(p => p.Role).LoadAsync();
        return CreatedAtAction(nameof(Get), new { permissionId = permission.Id }, PermissionToDto(permission));
    }

    [HttpPut("{permissionId:guid}")]
    public async Task<ActionResult> Update(Guid permissionId, [FromBody] PermissionModificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var permission = await _context.Permissions.FindAsync(permissionId);
        if (permission == null)
        {
            return NotFound(new { message = $"Permission '{permissionId}' not found" });
        }

        var role = await _roleManager.FindByIdAsync(request.RoleId.ToString());
        if (role == null)
        {
            return NotFound(new { message = $"Role '{request.RoleId}' not found" });
        }

        permission.Name = request.Name.Trim();
        permission.RoleId = request.RoleId;
        await _context.SaveChangesAsync();
        await _context.Entry(permission).Reference(p => p.Role).LoadAsync();
        return Ok(PermissionToDto(permission));
    }

    private static PermissionDto PermissionToDto(Permission permission)
    {
        return new PermissionDto
        {
            Id = permission.Id,
            Name = permission.Name,
            RoleId = permission.RoleId,
            RoleName = permission.Role?.Name
        };
    }
}
