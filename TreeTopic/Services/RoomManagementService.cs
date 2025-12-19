using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Microsoft.EntityFrameworkCore;

namespace TreeTopic.Services;

public interface IRoomManagementService
{
    Task<Result<List<RoomDto>>> GetAllRoomsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<RoomDto>> GetRoomByIdAsync(Guid roomId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<RoomDto>> CreateRoomAsync(CreateRoomRequest request, Guid createdUserId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<RoomDto>> UpdateRoomAsync(Guid roomId, UpdateRoomRequest request, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> DeleteRoomAsync(Guid roomId, Guid tenantId, CancellationToken cancellationToken = default);
}

public class RoomManagementService : BaseService, IRoomManagementService
{
    private readonly IRoomRepository _roomRepository;

    public RoomManagementService(
        IRoomRepository roomRepository,
        ILogger<RoomManagementService> logger) : base(logger)
    {
        _roomRepository = roomRepository;
    }

    public async Task<Result<List<RoomDto>>> GetAllRoomsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var rooms = await _roomRepository.Query()
                .Where(r => r.TenantId == tenantId.ToString())
                .ToListAsync(cancellationToken);

            var dtos = rooms.Select(MapToDto).ToList();
            return Result<List<RoomDto>>.Success(dtos);
        }, nameof(GetAllRoomsAsync));
    }

    public async Task<Result<RoomDto>> GetRoomByIdAsync(Guid roomId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken);

            if (room == null || room.TenantId != tenantId.ToString())
                return Result<RoomDto>.NotFound("Room not found");

            var dto = MapToDto(room);
            return Result<RoomDto>.Success(dto);
        }, nameof(GetRoomByIdAsync));
    }

    public async Task<Result<RoomDto>> CreateRoomAsync(
        CreateRoomRequest request,
        Guid createdUserId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = new Room
            {
                Name = request.Name,
                CreatedUserId = createdUserId,
                TenantId = tenantId.ToString()
            };

            await _roomRepository.AddAsync(room, cancellationToken);
            await _roomRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(room);
            return Result<RoomDto>.Success(dto, 201);
        }, nameof(CreateRoomAsync));
    }

    public async Task<Result<RoomDto>> UpdateRoomAsync(
        Guid roomId,
        UpdateRoomRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken);

            if (room == null || room.TenantId != tenantId.ToString())
                return Result<RoomDto>.NotFound("Room not found");

            if (!string.IsNullOrEmpty(request.Name))
                room.Name = request.Name;

            room.UpdatedAt = DateTime.UtcNow;
            _roomRepository.Update(room);
            await _roomRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(room);
            return Result<RoomDto>.Success(dto);
        }, nameof(UpdateRoomAsync));
    }

    public async Task<Result> DeleteRoomAsync(Guid roomId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken);

            if (room == null || room.TenantId != tenantId.ToString())
                return Result.NotFound("Room not found");

            _roomRepository.Delete(room);
            await _roomRepository.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }, nameof(DeleteRoomAsync));
    }

    private static RoomDto MapToDto(Room room)
    {
        return new RoomDto
        {
            Id = room.Id,
            TenantId = Guid.Parse(room.TenantId),
            Name = room.Name,
            CreatedUserId = room.CreatedUserId,
            CreatedUserName = room.CreatedUser?.UserName,
            CreatedAt = room.CreatedAt,
            UpdatedAt = room.UpdatedAt
        };
    }
}
