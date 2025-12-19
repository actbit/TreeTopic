using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class FileController : ControllerBase
{
    private readonly IFileManagementService _fileManagementService;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public FileController(
        IFileManagementService fileManagementService,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _fileManagementService = fileManagementService;
        _tenantAccessor = tenantAccessor;
    }

    private Guid CurrentTenantId => Guid.Parse(_tenantAccessor.MultiTenantContext?.TenantInfo?.Id ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.GetAllFilesAsync(CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("message/{messageId:guid}")]
    public async Task<IActionResult> GetByMessage(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.GetFilesByMessageAsync(messageId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> GetById(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.GetFileByIdAsync(fileId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFileRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _fileManagementService.CreateFileAsync(request, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{fileId:guid}")]
    public async Task<IActionResult> Update(Guid fileId, [FromBody] UpdateFileRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _fileManagementService.UpdateFileAsync(fileId, request, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{fileId:guid}")]
    public async Task<IActionResult> Delete(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.DeleteFileAsync(fileId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    private IActionResult HandleResult<T>(Common.Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, result.Data);

        return StatusCode(result.StatusCode, new { error = result.Error?.Message });
    }

    private IActionResult HandleResult(Common.Result result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode);

        return StatusCode(result.StatusCode, new { error = result.Error?.Message });
    }
}
