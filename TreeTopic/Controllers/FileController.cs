using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Services;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class FileController : ControllerBase
{
    private readonly IFileManagementService _fileManagementService;

    public FileController(
        IFileManagementService fileManagementService)
    {
        _fileManagementService = fileManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.GetAllFilesAsync(cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("message/{messageId:guid}")]
    public async Task<IActionResult> GetByMessage(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.GetFilesByMessageAsync(messageId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> GetById(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.GetFileByIdAsync(fileId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFileRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _fileManagementService.CreateFileAsync(request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{fileId:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid fileId, [FromBody] UpdateFileRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _fileManagementService.UpdateFileAsync(fileId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid fileId, CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.DeleteFileAsync(fileId, cancellationToken);
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




