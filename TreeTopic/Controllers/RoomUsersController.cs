using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize(Roles = "Admin")]
public class RoomUsersController : ControllerBase
{
    private readonly IRoomUserRepository _roomUserRepository;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public RoomUsersController(
        IRoomUserRepository roomUserRepository,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _roomUserRepository = roomUserRepository;
        _tenantAccessor = tenantAccessor;
    }

    private string? CurrentTenantId => _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;

    [HttpGet("room/{roomId:guid}")]
    public async Task<IActionResult> ListByRoom(Guid roomId, CancellationToken cancellationToken)
    {
        var entities = await _roomUserRepository.GetByRoomIdAsync(roomId, cancellationToken);
        var dtos = entities.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> ListByUser(Guid userId, CancellationToken cancellationToken)
    {
        var entities = await _roomUserRepository.GetByUserIdAsync(userId, cancellationToken);
        var dtos = entities.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpPost("room/{roomId:guid}")]
    public async Task<IActionResult> Create(Guid roomId, [FromBody] CreateRoomUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var toCreate = new RoomUser
        {
            ApplicationUserId = request.ApplicationUserId,
            RoomId = roomId
        };

        await _roomUserRepository.AddAsync(toCreate);
        await _roomUserRepository.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = toCreate.Id }, MapToDto(toCreate));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _roomUserRepository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _roomUserRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return NotFound();
        }

        _roomUserRepository.Delete(entity);
        await _roomUserRepository.SaveChangesAsync();
        return NoContent();
    }

    private static RoomUserDto MapToDto(RoomUser entity)
    {
        return new RoomUserDto
        {
            Id = entity.Id,
            ApplicationUserId = entity.ApplicationUserId,
            RoomId = entity.RoomId
        };
    }
}
