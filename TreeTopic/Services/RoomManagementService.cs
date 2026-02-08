using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using TreeTopic.Permissions;
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
    private readonly IRoomUserRepository _roomUserRepository;
    private readonly RoomUserManager _roomUserManager;

    public RoomManagementService(
        IRoomRepository roomRepository,
        ApplicationDbContext dbContext,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        IHubContext<RoomTopicHub, IRoomTopicHubClient> roomTopicHub,
        IMaskedUUIDService maskedUuidService,
        IRoomUserRepository roomUserRepository,
        RoomUserManager roomUserManager,
        ILogger<RoomManagementService> logger) : base(logger)
    {
        _roomRepository = roomRepository;
        _dbContext = dbContext;
        _tenantAccessor = tenantAccessor;
        _roomTopicHub = roomTopicHub;
        _maskedUuidService = maskedUuidService;
        _roomUserRepository = roomUserRepository;
        _roomUserManager = roomUserManager;
    }

    public async Task<Result<List<RoomDto>>> GetAllRoomsAsync(
        Guid userId,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var normalizedRoleNames = roleNames
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // クエリ1: Roomsと参加情報をまとめて取得
            var roomsWithJoinInfo = await _roomRepository.Query()
                .GroupJoin(
                    _dbContext.RoomUsers.Where(ru => ru.ApplicationUserId == userId),
                    room => room.Id,
                    ru => ru.RoomId,
                    (room, roomUsers) => new { room, roomUsers })
                .SelectMany(
                    x => x.roomUsers.DefaultIfEmpty(),
                    (x, ru) => new { x.room, IsJoined = ru != null })
                .ToListAsync(cancellationToken);

            // クエリ2: Role権限チェック（RoomRead/RoomManageを持っているか）
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

            // クエリ3: RoomJoinPermissionsをまとめて取得（ユーザー直接 + Role経由）
            var roleIds = normalizedRoleNames.Count == 0
                ? new List<Guid>()
                : await _dbContext.Roles
                    .AsNoTracking()
                    .Where(r => r.Name != null && normalizedRoleNames.Contains(r.Name))
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);

            var allowedRoomIds = roleIds.Any()
                ? await _dbContext.RoomJoinUserPermissions
                    .AsNoTracking()
                    .Where(p => p.ApplicationUserId == userId)
                    .Select(p => p.RoomId)
                    .Union(
                        _dbContext.RoomJoinRolePermissions
                            .AsNoTracking()
                            .Where(p => roleIds.Contains(p.RoleId))
                            .Select(p => p.RoomId)
                    )
                    .ToHashSetAsync(cancellationToken)
                : await _dbContext.RoomJoinUserPermissions
                    .AsNoTracking()
                    .Where(p => p.ApplicationUserId == userId)
                    .Select(p => p.RoomId)
                    .ToHashSetAsync(cancellationToken);

            // DTOに変換
            var dtos = roomsWithJoinInfo.Select(x =>
            {
                var canJoin =
                    x.IsJoined ||
                    canManageRooms ||
                    x.room.CreatedUserId == userId ||
                    x.room.JoinPolicy == RoomJoinPolicy.Public ||
                    allowedRoomIds.Contains(x.room.Id);

                return MapToDto(x.room, x.IsJoined, canJoin);
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
            // トランザクションを開始してデータ整合性を確保
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
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

                // Room作成者のRoomUserを作成して管理者権限を付与
                var roomUser = new RoomUser
                {
                    Id = Guid.CreateVersion7(),
                    ApplicationUserId = createdUserId,
                    RoomId = room.Id,
                    Name = RoomUserNameHelper.DefaultUserToken,
                    UseMainName = true,
                    UseMainIcon = true
                };

                await _roomUserRepository.AddAsync(roomUser, cancellationToken);
                await _roomUserRepository.SaveChangesAsync(cancellationToken);

                // 管理者権限を付与
                var adminPermissions = new[]
                {
                    RoomPermissions.Manage,
                    RoomPermissions.ManageUsers,
                    RoomPermissions.ManageRoles,
                    RoomPermissions.Delete,
                    RoomPermissions.TopicManage,
                    RoomPermissions.TopicWrite,
                    RoomPermissions.TopicRead,
                    RoomPermissions.Write,
                    RoomPermissions.Read
                };

                foreach (var permissionName in adminPermissions)
                {
                    await _roomUserManager.AddPermissionAsync(roomUser, permissionName, cancellationToken);
                }

                Logger.LogInformation(
                    "Admin permissions granted to RoomUser {RoomUserId} for room {RoomId}",
                    roomUser.Id, room.Id);

                // トランザクションをコミット（ブロードキャスト前に確定）
                await transaction.CommitAsync(cancellationToken);

                var dto = MapToDto(room);
                await BroadcastRoomCreatedAsync(dto);
                return Result<RoomDto>.Success(dto, 201);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Logger.LogError(ex, "Failed to create room. Transaction rolled back.");
                throw;
            }
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
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken);

                if (room == null)
                    return Result.NotFound("Room not found");

                // 削除前にRoomの関連データをログに記録（監査用）
                var roomUserCount = await _dbContext.RoomUsers
                    .CountAsync(ru => ru.RoomId == roomId, cancellationToken);
                var topicCount = await _dbContext.Topics
                    .CountAsync(t => t.RoomId == roomId, cancellationToken);

                Logger.LogInformation(
                    "Deleting room {RoomId} with {RoomUserCount} room users and {TopicCount} topics",
                    roomId, roomUserCount, topicCount);

                _roomRepository.Delete(room);
                await _roomRepository.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                // トランザクション外でブロードキャスト
                await BroadcastRoomDeletedAsync(room.Id);
                return Result.Success();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Logger.LogError(ex, "Failed to delete room {RoomId}. Transaction rolled back.", roomId);
                throw;
            }
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
