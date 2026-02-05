using Microsoft.EntityFrameworkCore;
using TreeTopic.Common;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Services;

/// <summary>
/// Topic権限管理サービス
/// </summary>
public class TopicPermissionsService : BaseService, ITopicPermissionsService
{
    private readonly TopicPermissionManager _topicPermissionManager;
    private readonly RoomUserManager _roomUserManager;
    private readonly RoomRoleManager _roomRoleManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<TopicPermissionsService> _logger;

    public TopicPermissionsService(
        TopicPermissionManager topicPermissionManager,
        RoomUserManager roomUserManager,
        RoomRoleManager roomRoleManager,
        ApplicationDbContext dbContext,
        ILogger<TopicPermissionsService> logger) : base(logger)
    {
        _topicPermissionManager = topicPermissionManager;
        _roomUserManager = roomUserManager;
        _roomRoleManager = roomRoleManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Topicのユーザー権限一覧を取得
    /// </summary>
    public async Task<Result<List<TopicUserPermissionDto>>> GetTopicUserPermissionsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var permissions = await _topicPermissionManager.GetTopicUserPermissionsAsync(topicId, cancellationToken);

            var dtos = permissions.Select(p => new TopicUserPermissionDto(
                p.Id,
                p.TopicId,
                p.RoomUserId,
                p.RoomUser?.ApplicationUser?.UserName,
                RoomUserNameHelper.ResolveDisplayName(p.RoomUser),
                p.Name)).ToList();

            return Result<List<TopicUserPermissionDto>>.Success(dtos);
        }, nameof(GetTopicUserPermissionsAsync));
    }

    /// <summary>
    /// 特定ユーザーのTopic権限を取得
    /// </summary>
    public async Task<Result<List<string>>> GetUserTopicPermissionsAsync(
        Guid topicId,
        Guid roomUserId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // RoomUserがTopicのルームに所属しているか検証
            var roomUser = await _roomUserManager.FindByIdAsync(roomUserId, cancellationToken);
            if (roomUser == null)
            {
                return Result<List<string>>.NotFound("RoomUser not found");
            }

            var topic = await _dbContext.Topics
                .FirstOrDefaultAsync(t => t.Id == topicId, cancellationToken);
            if (topic == null)
            {
                return Result<List<string>>.NotFound("Topic not found");
            }

            if (roomUser.RoomId != topic.RoomId)
            {
                return Result<List<string>>.BadRequest("RoomUser does not belong to topic's room");
            }

            var permissions = await _topicPermissionManager.GetUserPermissionsAsync(topicId, roomUserId, cancellationToken);
            var permissionNames = permissions.Select(p => p.Name).ToList();

            return Result<List<string>>.Success(permissionNames);
        }, nameof(GetUserTopicPermissionsAsync));
    }

    /// <summary>
    /// ユーザーにTopic権限を割り当て
    /// </summary>
    public async Task<Result<TopicUserPermissionDto>> AddPermissionToUserAsync(
        Guid topicId,
        Guid roomUserId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // RoomUserがTopicのルームに所属しているか検証
            var roomUser = await _roomUserManager.FindByIdAsync(roomUserId, cancellationToken);
            if (roomUser == null)
            {
                return Result<TopicUserPermissionDto>.NotFound("RoomUser not found");
            }

            var topic = await _dbContext.Topics
                .FirstOrDefaultAsync(t => t.Id == topicId, cancellationToken);
            if (topic == null)
            {
                return Result<TopicUserPermissionDto>.NotFound("Topic not found");
            }

            if (roomUser.RoomId != topic.RoomId)
            {
                return Result<TopicUserPermissionDto>.BadRequest("RoomUser does not belong to topic's room");
            }

            // 権限を追加
            var permission = await _topicPermissionManager.AddUserPermissionAsync(topicId, roomUserId, permissionName, cancellationToken);

            // DTOに変換
            var dto = new TopicUserPermissionDto(
                permission.Id,
                permission.TopicId,
                permission.RoomUserId,
                permission.RoomUser?.ApplicationUser?.UserName,
                RoomUserNameHelper.ResolveDisplayName(permission.RoomUser),
                permission.Name);

            return Result<TopicUserPermissionDto>.Success(dto);
        }, nameof(AddPermissionToUserAsync));
    }

    /// <summary>
    /// ユーザーからTopic権限を削除
    /// </summary>
    public async Task<Result> RemovePermissionFromUserAsync(
        Guid topicId,
        Guid roomUserId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // RoomUserがTopicのルームに所属しているか検証
            var roomUser = await _roomUserManager.FindByIdAsync(roomUserId, cancellationToken);
            if (roomUser == null)
            {
                return Result.NotFound("RoomUser not found");
            }

            var topic = await _dbContext.Topics
                .FirstOrDefaultAsync(t => t.Id == topicId, cancellationToken);
            if (topic == null)
            {
                return Result.NotFound("Topic not found");
            }

            if (roomUser.RoomId != topic.RoomId)
            {
                return Result.BadRequest("RoomUser does not belong to topic's room");
            }

            // 権限を削除
            var success = await _topicPermissionManager.RemoveUserPermissionAsync(topicId, roomUserId, permissionName, cancellationToken);

            return success ? Result.Success() : Result.NotFound("Permission not found");
        }, nameof(RemovePermissionFromUserAsync));
    }

    /// <summary>
    /// ユーザーのTopic権限をクリア
    /// </summary>
    public async Task<Result> ClearUserPermissionsAsync(
        Guid topicId,
        Guid roomUserId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // RoomUserがTopicのルームに所属しているか検証
            var roomUser = await _roomUserManager.FindByIdAsync(roomUserId, cancellationToken);
            if (roomUser == null)
            {
                return Result.NotFound("RoomUser not found");
            }

            var topic = await _dbContext.Topics
                .FirstOrDefaultAsync(t => t.Id == topicId, cancellationToken);
            if (topic == null)
            {
                return Result.NotFound("Topic not found");
            }

            if (roomUser.RoomId != topic.RoomId)
            {
                return Result.BadRequest("RoomUser does not belong to topic's room");
            }

            // 権限をクリア
            await _topicPermissionManager.ClearUserPermissionsAsync(topicId, roomUserId, cancellationToken);

            return Result.Success();
        }, nameof(ClearUserPermissionsAsync));
    }

    /// <summary>
    /// TopicのRoomRole権限一覧を取得
    /// </summary>
    public async Task<Result<List<TopicRolePermissionDto>>> GetTopicRolePermissionsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var permissions = await _topicPermissionManager.GetTopicRolePermissionsAsync(topicId, cancellationToken);

            var dtos = permissions.Select(p => new TopicRolePermissionDto(
                p.Id,
                p.TopicId,
                p.RoomRoleId,
                p.RoomRole?.Name,
                p.RoomRole?.Description,
                p.Name)).ToList();

            return Result<List<TopicRolePermissionDto>>.Success(dtos);
        }, nameof(GetTopicRolePermissionsAsync));
    }

    /// <summary>
    /// TopicにRoomRole権限を割り当て
    /// </summary>
    public async Task<Result<TopicRolePermissionDto>> AddTopicRolePermissionAsync(
        Guid topicId,
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // RoomRoleの存在確認
            var roomRole = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (roomRole == null)
            {
                return Result<TopicRolePermissionDto>.NotFound($"RoomRole '{roleName}' not found");
            }

            // 権限を追加
            var permission = await _topicPermissionManager.AddRolePermissionAsync(topicId, roomRole.Id, permissionName, cancellationToken);

            // DTOに変換
            var dto = new TopicRolePermissionDto(
                permission.Id,
                permission.TopicId,
                permission.RoomRoleId,
                permission.RoomRole?.Name,
                permission.RoomRole?.Description,
                permission.Name);

            return Result<TopicRolePermissionDto>.Success(dto);
        }, nameof(AddTopicRolePermissionAsync));
    }

    /// <summary>
    /// TopicからRoomRole権限を削除
    /// </summary>
    public async Task<Result> RemoveTopicRolePermissionAsync(
        Guid topicId,
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // RoomRoleの存在確認
            var roomRole = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (roomRole == null)
            {
                return Result.NotFound($"RoomRole '{roleName}' not found");
            }

            // 権限を削除
            var success = await _topicPermissionManager.RemoveRolePermissionAsync(topicId, roomRole.Id, permissionName, cancellationToken);

            return success ? Result.Success() : Result.NotFound("Permission not found");
        }, nameof(RemoveTopicRolePermissionAsync));
    }

    /// <summary>
    /// Topicの全RoomRole権限をクリア
    /// </summary>
    public async Task<Result> ClearRolePermissionsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // トピックに割り当てられているロール権限をすべて取得
            var permissions = await _topicPermissionManager.GetTopicRolePermissionsAsync(topicId, cancellationToken);

            if (!permissions.Any())
            {
                return Result.Success();
            }

            // 各ロール権限を個別に削除（TopicPermissionManagerにはClearRolePermissionsがないので個別削除）
            foreach (var permission in permissions)
            {
                await _topicPermissionManager.RemoveRolePermissionAsync(
                    topicId,
                    permission.RoomRoleId,
                    permission.Name,
                    cancellationToken);
            }

            _logger.LogInformation("All Role permissions cleared from Topic: {TopicId}", topicId);
            return Result.Success();
        }, nameof(ClearRolePermissionsAsync));
    }
}