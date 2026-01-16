using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.SignalR;
using MaskedUUID.AspNetCore.Services;
using TreeTopic.Hubs;
using Microsoft.EntityFrameworkCore;

namespace TreeTopic.Services;

public interface IRoomManagementService
{
    Task<Result<List<RoomDto>>> GetAllRoomsAsync(CancellationToken cancellationToken = default);
    Task<Result<RoomDto>> GetRoomByIdAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<Result<RoomDto>> CreateRoomAsync(CreateRoomRequest request, Guid createdUserId, CancellationToken cancellationToken = default);
    Task<Result<RoomDto>> UpdateRoomAsync(Guid roomId, UpdateRoomRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteRoomAsync(Guid roomId, CancellationToken cancellationToken = default);
}

public class RoomManagementService : BaseService, IRoomManagementService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly IHubContext<RoomTopicHub, IRoomTopicHubClient> _roomTopicHub;
    private readonly IMaskedUUIDService _maskedUuidService;

    public RoomManagementService(
        IRoomRepository roomRepository,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        IHubContext<RoomTopicHub, IRoomTopicHubClient> roomTopicHub,
        IMaskedUUIDService maskedUuidService,
        ILogger<RoomManagementService> logger) : base(logger)
    {
        _roomRepository = roomRepository;
        _tenantAccessor = tenantAccessor;
        _roomTopicHub = roomTopicHub;
        _maskedUuidService = maskedUuidService;
    }

    public async Task<Result<List<RoomDto>>> GetAllRoomsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var rooms = await _roomRepository.Query()
                .ToListAsync(cancellationToken);

            var dtos = rooms.Select(MapToDto).ToList();
            return Result<List<RoomDto>>.Success(dtos);
        }, nameof(GetAllRoomsAsync));
    }

    public async Task<Result<RoomDto>> GetRoomByIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken);

            if (room == null)
                return Result<RoomDto>.NotFound("Room not found");

            var dto = MapToDto(room);
            return Result<RoomDto>.Success(dto);
        }, nameof(GetRoomByIdAsync));
    }

    public async Task<Result<RoomDto>> CreateRoomAsync(
        CreateRoomRequest request,
        Guid createdUserId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = new Room
            {
                Name = request.Name,
                CreatedUserId = createdUserId
            };

            await _roomRepository.AddAsync(room, cancellationToken);
            await _roomRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(room);
            await BroadcastRoomCreatedAsync(dto);
            return Result<RoomDto>.Success(dto, 201);
        }, nameof(CreateRoomAsync));
    }

    public async Task<Result<RoomDto>> UpdateRoomAsync(
        Guid roomId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken);

            if (room == null)
                return Result<RoomDto>.NotFound("Room not found");

            if (!string.IsNullOrEmpty(request.Name))
                room.Name = request.Name;

            room.UpdatedAt = DateTime.UtcNow;
            _roomRepository.Update(room);
            await _roomRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(room);
            await BroadcastRoomUpdatedAsync(dto);
            return Result<RoomDto>.Success(dto);
        }, nameof(UpdateRoomAsync));
    }

    public async Task<Result> DeleteRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken);

            if (room == null)
                return Result.NotFound("Room not found");

            _roomRepository.Delete(room);
            await _roomRepository.SaveChangesAsync(cancellationToken);

            await BroadcastRoomDeletedAsync(room.Id);
            return Result.Success();
        }, nameof(DeleteRoomAsync));
    }

    private string ResolveTenantKey()
    {
        return RoomTopicHubGroups.ResolveTenantKey(_tenantAccessor.MultiTenantContext?.TenantInfo);
    }

    private RoomRealtimeDto MapToRealtime(RoomDto dto)
    {
        var id = (Guid)dto.Id;
        var createdUserId = (Guid)dto.CreatedUserId;

        return new RoomRealtimeDto(
            id == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous(id),
            dto.Name,
            createdUserId == Guid.Empty ? null : _maskedUuidService.EncodeSynchronous(createdUserId),
            dto.CreatedUserName,
            dto.CreatedAt,
            dto.UpdatedAt);
    }

    private Task BroadcastRoomCreatedAsync(RoomDto dto)
    {
        var groupName = RoomTopicHubGroups.Tenant(ResolveTenantKey());
        var payload = MapToRealtime(dto);
        Logger.LogInformation("[RoomTopicHub] Broadcast RoomCreated room={RoomId} group={Group}", dto.Id, groupName);
        return _roomTopicHub.Clients.Group(groupName).RoomCreated(payload);
    }

    private Task BroadcastRoomUpdatedAsync(RoomDto dto)
    {
        var groupName = RoomTopicHubGroups.Tenant(ResolveTenantKey());
        var payload = MapToRealtime(dto);
        Logger.LogInformation("[RoomTopicHub] Broadcast RoomUpdated room={RoomId} group={Group}", dto.Id, groupName);
        return _roomTopicHub.Clients.Group(groupName).RoomUpdated(payload);
    }

    private Task BroadcastRoomDeletedAsync(Guid roomId)
    {
        var groupName = RoomTopicHubGroups.Tenant(ResolveTenantKey());
        var payload = new RoomDeletedEvent(
            roomId == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous(roomId));
        Logger.LogInformation("[RoomTopicHub] Broadcast RoomDeleted room={RoomId} group={Group}", roomId, groupName);
        return _roomTopicHub.Clients.Group(groupName).RoomDeleted(payload);
    }

    private static RoomDto MapToDto(Room room)
    {
        return new RoomDto
        {
            Id = room.Id,
            Name = room.Name,
            CreatedUserId = room.CreatedUserId,
            CreatedUserName = room.CreatedUser?.UserName,
            CreatedAt = room.CreatedAt,
            UpdatedAt = room.UpdatedAt
        };
    }
}
