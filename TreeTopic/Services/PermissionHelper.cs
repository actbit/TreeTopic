using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TreeTopic.Services;

/// <summary>
/// 権限管理の共通処理を提供するヘルパークラス
/// </summary>
public static class PermissionHelper
{
    /// <summary>
    /// トランザクション付きでエンティティを追加（重複チェック付き）
    /// </summary>
    /// <typeparam name="TEntity">エンティティ型</typeparam>
    /// <typeparam name="TContext">DbContext型</typeparam>
    /// <param name="context">DbContext</param>
    /// <param name="dbSet">対象のDbSet</param>
    /// <param name="entity">追加するエンティティ</param>
    /// <param name="duplicateCheck">重複チェック関数</param>
    /// <param name="logger">ロガー</param>
    /// <param name="successMessage">成功時のログメッセージ</param>
    /// <param name="errorMessage">エラー時のログメッセージ</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>追加されたエンティティ、または既存のエンティティ</returns>
    public static async Task<TEntity> AddWithTransactionAsync<TEntity, TContext>(
        TContext context,
        DbSet<TEntity> dbSet,
        TEntity entity,
        Func<TContext, CancellationToken, Task<TEntity?>> duplicateCheck,
        ILogger logger,
        string successMessage,
        string errorMessage,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TContext : DbContext
    {
        // 既にトランザクション内の場合はトランザクションを開始しない
        var hasExistingTransaction = context.Database.CurrentTransaction != null;

        var transaction = hasExistingTransaction
            ? null
            : await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 重複チェック
            var existing = await duplicateCheck(context, cancellationToken);
            if (existing != null)
            {
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("Duplicate found, returning existing entity: {EntityType}", typeof(TEntity).Name);
                return existing;
            }

            dbSet.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(successMessage);
            return entity;
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, errorMessage);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// トランザクション付きでエンティティを削除
    /// </summary>
    /// <typeparam name="TEntity">エンティティ型</typeparam>
    /// <typeparam name="TContext">DbContext型</typeparam>
    /// <param name="context">DbContext</param>
    /// <param name="dbSet">対象のDbSet</param>
    /// <param name="findEntity">エンティティ検索関数</param>
    /// <param name="logger">ロガー</param>
    /// <param name="successMessage">成功時のログメッセージ</param>
    /// <param name="errorMessage">エラー時のログメッセージ</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>削除成功時はtrue、エンティティが存在しない場合はfalse</returns>
    public static async Task<bool> RemoveWithTransactionAsync<TEntity, TContext>(
        TContext context,
        DbSet<TEntity> dbSet,
        Func<TContext, CancellationToken, Task<TEntity?>> findEntity,
        ILogger logger,
        string successMessage,
        string errorMessage,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TContext : DbContext
    {
        var entity = await findEntity(context, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        var hasExistingTransaction = context.Database.CurrentTransaction != null;
        var transaction = hasExistingTransaction
            ? null
            : await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbSet.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(successMessage);
            return true;
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, errorMessage);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// 汎用的な権限取得処理（ロール権限 + 個別権限）
    /// </summary>
    /// <typeparam name="TRolePermission">ロール権限エンティティ型</typeparam>
    /// <typeparam name="TUserPermission">個別権限エンティティ型</typeparam>
    /// <param name="rolePermissionsQuery">ロール権限クエリ</param>
    /// <param name="userPermissionsQuery">個別権限クエリ</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>権限名のセット</returns>
    public static async Task<HashSet<string>> GetPermissionsAsync<TRolePermission, TUserPermission>(
        IQueryable<TRolePermission> rolePermissionsQuery,
        IQueryable<TUserPermission> userPermissionsQuery,
        CancellationToken cancellationToken = default)
        where TRolePermission : class
        where TUserPermission : class
    {
        var permissions = new HashSet<string>();

        // 1. ロールから権限を取得
        var rolePermissions = await rolePermissionsQuery.ToListAsync(cancellationToken);
        foreach (var perm in rolePermissions)
        {
            // リフレクションでNameプロパティを取得
            var nameProperty = typeof(TRolePermission).GetProperty("Name") 
                ?? typeof(TRolePermission).GetProperty("PermissionName");
            if (nameProperty != null)
            {
                var name = nameProperty.GetValue(perm)?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    permissions.Add(name);
                }
            }
        }

        // 2. 個別ユーザー権限を追加
        var userPermissions = await userPermissionsQuery.ToListAsync(cancellationToken);
        foreach (var perm in userPermissions)
        {
            // リフレクションでNameプロパティを取得
            var nameProperty = typeof(TUserPermission).GetProperty("Name") 
                ?? typeof(TUserPermission).GetProperty("PermissionName");
            if (nameProperty != null)
            {
                var name = nameProperty.GetValue(perm)?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    permissions.Add(name);
                }
            }
        }

        return permissions;
    }

    /// <summary>
    /// 汎用的な権限確認処理（ロール権限または個別権限のいずれか）
    /// </summary>
    /// <typeparam name="TRolePermission">ロール権限エンティティ型</typeparam>
    /// <typeparam name="TUserPermission">個別権限エンティティ型</typeparam>
    /// <param name="rolePermissionQuery">ロール権限確認クエリ</param>
    /// <param name="userPermissionQuery">個別権限確認クエリ</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>権限を持つ場合はtrue</returns>
    public static async Task<bool> HasPermissionAsync<TRolePermission, TUserPermission>(
        IQueryable<TRolePermission> rolePermissionQuery,
        IQueryable<TUserPermission> userPermissionQuery,
        CancellationToken cancellationToken = default)
        where TRolePermission : class
        where TUserPermission : class
    {
        // 1. ロール権限を確認
        var hasRolePermission = await rolePermissionQuery.AnyAsync(cancellationToken);
        if (hasRolePermission)
        {
            return true;
        }

        // 2. 個別ユーザー権限を確認
        var hasUserPermission = await userPermissionQuery.AnyAsync(cancellationToken);
        return hasUserPermission;
    }
}
