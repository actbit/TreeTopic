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
    Task<Result<List<RoomDto>>> GetAllRoomsAsync(
        Guid userId,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default);
    Task<Result<RoomDto>> GetRoomByIdAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<Result<RoomDto>> CreateRoomAsync(CreateRoomRequest request, Guid createdUserId, CancellationToken cancellationToken = default);
    Task<Result<RoomDto>> UpdateRoomAsync(Guid roomId, UpdateRoomRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteRoomAsync(Guid roomId, CancellationToken cancellationToken = default);
}

public class RoomManagementService : BaseService, IRoomManagementService
{
    private readonly IRoomRepository _roomRepository;
    private readonly ApplicationDbContext _dbContext;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly IHubContext<RoomTopicHub, IRoomTopicHubClient> _roomTopicHub;
    private readonly IMaskedUUIDService _maskedUuidService;

    public RoomManagementService(
        IRoomRepository roomRepository,
        ApplicationDbContext dbContext,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        IHubContext<RoomTopicHub, IRoomTopicHubClient> roomTopicHub,
        IMaskedUUIDService maskedUuidService,
        ILogger<RoomManagementService> logger) : base(logger)
    {
        _roomRepository = roomRepository;
        _dbContext = dbContext;
        _tenantAccessor = tenantAccessor;
        _roomTopicHub = roomTopicHub;
        _maskedUuidService = maskedUuidService;
    }

    public async Task<Result<List<RoomDto>>> GetAllRoomsAsync(
        Guid userId,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var rooms = await _roomRepository.Query()
                .ToListAsync(cancellationToken);

            var roomIds = rooms.Select(r => r.Id).ToList();

            var joinedRoomIds = await _roomRepository.Query()
                .SelectMany(r => r.RoomUsers)
                .Where(ru => ru.ApplicationUserId == userId && roomIds.Contains(ru.RoomId))
                .Select(ru => ru.RoomId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var joinedRoomSet = joinedRoomIds.ToHashSet();

            var normalizedRoleNames = roleNames
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var roleIdList = await _dbContext.Roles
                .AsNoTracking()
                .Where(r => r.Name != null && normalizedRoleNames.Contains(r.Name))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            var roleIdSet = roleIdList.ToHashSet();

            var allowedByUserRoomIds = await _dbContext.RoomJoinUserPermissions
                .AsNoTracking()
                .Where(p => p.ApplicationUserId == userId && roomIds.Contains(p.RoomId))
                .Select(p => p.RoomId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var allowedByRoleRoomIds = roleIdSet.Count == 0
                ? new List<Guid>()
                : await _dbContext.RoomJoinRolePermissions
                    .AsNoTracking()
                    .Where(p => roleIdSet.Contains(p.RoleId) && roomIds.Contains(p.RoomId))
                    .Select(p => p.RoomId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

            var canManageRooms = await _dbContext.Permissions
                .AsNoTracking()
                .Include(p => p.Role)
                .AnyAsync(
                    p => p.Role != null
                         && p.Role.Name != null
                         && normalizedRoleNames.Contains(p.Role.Name)
                         && (p.Name == Permissions.TenantPermissions.RoomManage
                             || p.Name == Permissions.TenantPermissions.RoomRead),
                    cancellationToken);

            var allowedByUserSet = allowedByUserRoomIds.ToHashSet();
            var allowedByRoleSet = allowedByRoleRoomIds.ToHashSet();

            var dtos = rooms.Select(room =>
            {
                var isJoined = joinedRoomSet.Contains(room.Id);
                var canJoin =
                    isJoined ||
                    canManageRooms ||
                    room.CreatedUserId == userId ||
                    room.JoinPolicy == RoomJoinPolicy.Public ||
                    allowedByUserSet.Contains(room.Id) ||
                    allowedByRoleSet.Contains(room.Id);

                return MapToDto(room, isJoined, canJoin);
            }).ToList();
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
                Description = request.Description,
                JoinPolicy = request.JoinPolicy,
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

            room.Description = request.Description;
            if (request.JoinPolicy.HasValue)
            {
                room.JoinPolicy = request.JoinPolicy.Value;
            }
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
        return new RoomRealtimeDto(
            dto.Id,
            dto.Name,
            dto.Description,
            (int)dto.JoinPolicy,
            dto.CreatedUserId,
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
        var payload = new RoomDeletedEvent(roomId);
        Logger.LogInformation("[RoomTopicHub] Broadcast RoomDeleted room={RoomId} group={Group}", roomId, groupName);
        return _roomTopicHub.Clients.Group(groupName).RoomDeleted(payload);
    }

    private static RoomDto MapToDto(Room room, bool isJoined = false, bool canJoin = true)
    {
        return new RoomDto
        {
            Id = room.Id,
            Name = room.Name,
            Description = room.Description,
            JoinPolicy = room.JoinPolicy,
            CreatedUserId = room.CreatedUserId,
            CreatedUserName = room.CreatedUser?.UserName,
            IsJoined = isJoined,
            CanJoin = canJoin,
            CreatedAt = room.CreatedAt,
            UpdatedAt = room.UpdatedAt
        };
    }
}
