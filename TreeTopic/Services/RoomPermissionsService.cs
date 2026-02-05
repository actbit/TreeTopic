using Microsoft.EntityFrameworkCore;
using TreeTopic.Common;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Services;

/// <summary>
/// Room権限管理サービス
/// </summary>
public class RoomPermissionsService : BaseService, IRoomPermissionsService
{
    private readonly RoomRoleManager _roomRoleManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RoomPermissionsService> _logger;

    public RoomPermissionsService(
        RoomRoleManager roomRoleManager,
        ApplicationDbContext dbContext,
        ILogger<RoomPermissionsService> logger) : base(logger)
    {
        _roomRoleManager = roomRoleManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// RoomRoleに割り当てられている権限一覧を取得
    /// </summary>
    public async Task<Result<List<string>>> GetRolePermissionsAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // ロールの存在確認
            var role = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return Result<List<string>>.NotFound($"RoomRole '{roleName}' not found");
            }

            // 権限一覧を取得
            var permissions = await _roomRoleManager.GetPermissionNamesAsync(role.Id, cancellationToken);

            return Result<List<string>>.Success(permissions);
        }, nameof(GetRolePermissionsAsync));
    }

    /// <summary>
    /// RoomRoleに権限を割り当て
    /// </summary>
    public async Task<Result<RoomRolePermissionDto>> AddPermissionToRoleAsync(
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // ロールの存在確認
            var role = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return Result<RoomRolePermissionDto>.NotFound($"RoomRole '{roleName}' not found");
            }

            // 権限を追加
            var permission = await _roomRoleManager.AddPermissionAsync(
                role.Id,
                permissionName,
                cancellationToken);

            // DTOに変換
            var dto = new RoomRolePermissionDto(
                permission.Id,
                permission.RoomRoleId,
                roleName,
                permission.PermissionName);

            return Result<RoomRolePermissionDto>.Success(dto);
        }, nameof(AddPermissionToRoleAsync));
    }

    /// <summary>
    /// RoomRoleから権限を削除
    /// </summary>
    public async Task<Result> RemovePermissionFromRoleAsync(
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // ロールの存在確認
            var role = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return Result.NotFound($"RoomRole '{roleName}' not found");
            }

            // 権限を削除
            var permissions = await _roomRoleManager.GetPermissionNamesAsync(role.Id, cancellationToken);
            var targetPermission = permissions.FirstOrDefault(p => p == permissionName);

            if (targetPermission == null)
            {
                return Result.Success();
            }

            // PermissionHelperのメソッドを直接使用して削除
            var success = await PermissionHelper.RemoveWithTransactionAsync(
                _dbContext,
                _dbContext.RoomRolePermissions,
                async (ctx, ct) => await ctx.RoomRolePermissions
                    .FirstOrDefaultAsync(p => p.RoomRoleId == role.Id && p.PermissionName == permissionName, ct),
                _logger,
                $"Permission removed from RoomRole: {permissionName} -> {roleName}",
                $"Failed to remove permission from RoomRole: {permissionName} -> {roleName}",
                cancellationToken);

            return success ? Result.Success() : Result.NotFound("Permission not found");
        }, nameof(RemovePermissionFromRoleAsync));
    }

    /// <summary>
    /// RoomRoleの全権限をクリア
    /// </summary>
    public async Task<Result> ClearRolePermissionsAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // ロールの存在確認
            var role = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return Result.NotFound($"RoomRole '{roleName}' not found");
            }

            // 現在の権限をすべて取得
            var permissions = await _roomRoleManager.GetPermissionNamesAsync(role.Id, cancellationToken);

            // 一つずつ削除（トランザクションは各削除で管理）
            foreach (var permission in permissions)
            {
                await PermissionHelper.RemoveWithTransactionAsync(
                    _dbContext,
                    _dbContext.RoomRolePermissions,
                    async (ctx, ct) => await ctx.RoomRolePermissions
                        .FirstOrDefaultAsync(p => p.RoomRoleId == role.Id && p.PermissionName == permission, ct),
                    _logger,
                    $"Permission cleared from RoomRole: {permission} -> {roleName}",
                    $"Failed to clear permission from RoomRole: {permission} -> {roleName}",
                    cancellationToken);
            }

            _logger.LogInformation("All permissions cleared from RoomRole: {RoleName}", roleName);
            return Result.Success();
        }, nameof(ClearRolePermissionsAsync));
    }
}