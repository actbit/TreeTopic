using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Permissions;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// 権限スキャン API
/// </summary>
[ApiController]
[Route("{tenant}/api/permissions/scan")]
[Authorize]
public class PermissionsScanController : ControllerBase
{
    private readonly PermissionScanService _scanService;
    private readonly ILogger<PermissionsScanController> _logger;

    public PermissionsScanController(
        PermissionScanService scanService,
        ILogger<PermissionsScanController> logger)
    {
        _scanService = scanService;
        _logger = logger;
    }

    /// <summary>
    /// すべての権限を取得
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<PermissionRequirement>> GetAll()
    {
        return Ok(_scanService.Permissions);
    }

    /// <summary>
    /// スコープ別に権限を取得
    /// </summary>
    [HttpGet("by-scope")]
    public ActionResult<Dictionary<PermissionScope, List<PermissionRequirement>>> GetByScope()
    {
        return Ok(_scanService.GetPermissionsByScope());
    }

    /// <summary>
    /// 指定されたスコープの権限のみ取得
    /// </summary>
    [HttpGet("scope/{scope}")]
    public ActionResult<IEnumerable<PermissionRequirement>> GetByScope(PermissionScope scope)
    {
        return Ok(_scanService.GetPermissionsByScope(scope));
    }

    /// <summary>
    /// 権限の数を取得
    /// </summary>
    [HttpGet("count")]
    public ActionResult<int> GetCount()
    {
        return Ok(_scanService.GetPermissionCount());
    }

    /// <summary>
    /// スキャンを再実行
    /// </summary>
    [HttpPost("rescan")]
    public IActionResult Rescan()
    {
        _scanService.Scan();
        _logger.LogInformation("Permission scan completed. Total permissions: {Count}", _scanService.GetPermissionCount());
        return Ok(new { count = _scanService.GetPermissionCount() });
    }
}
