using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TreeTopic.Controllers;

    /// <summary>
    /// コントローラーの基底クラス
    /// </summary>
    public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// エンティティの存在確認
    /// </summary>
    /// <typeparam name="TEntity">エンティティ型</typeparam>
    /// <param name="dbSet">DbSet</param>
    /// <param name="id">ID</param>
    /// <param name="entityName">エンティティ名</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    protected async Task<(TEntity? Entity, IActionResult? ErrorResult)> FindEntityOrNotFoundAsync<TEntity>(
        DbSet<TEntity> dbSet,
        Guid id,
        string entityName,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var entity = await dbSet.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
        {
            return (null, NotFound(new { message = $"{entityName} not found" }));
        }
        return (entity, null);
    }

    /// <summary>
    /// 複数エンティティの存在確認
    /// </summary>
    /// <typeparam name="TEntity1">エンティティ1型</typeparam>
    /// <typeparam name="TEntity2">エンティティ2型</typeparam>
    /// <param name="dbSet1">DbSet1</param>
    /// <param name="id1">ID1</param>
    /// <param name="entityName1">エンティティ名1</param>
    /// <param name="dbSet2">DbSet2</param>
    /// <param name="id2">ID2</param>
    /// <param name="entityName2">エンティティ名2</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    protected async Task<(TEntity1? Entity1, TEntity2? Entity2, IActionResult? ErrorResult)> FindEntitiesOrNotFoundAsync<TEntity1, TEntity2>(
        DbSet<TEntity1> dbSet1,
        Guid id1,
        string entityName1,
        DbSet<TEntity2> dbSet2,
        Guid id2,
        string entityName2,
        CancellationToken cancellationToken = default)
        where TEntity1 : class
        where TEntity2 : class
    {
        var entity1 = await dbSet1.FindAsync(new object[] { id1 }, cancellationToken);
        if (entity1 == null)
        {
            return (null, null, NotFound(new { message = $"{entityName1} not found" }));
        }

        var entity2 = await dbSet2.FindAsync(new object[] { id2 }, cancellationToken);
        if (entity2 == null)
        {
            return (null, null, NotFound(new { message = $"{entityName2} not found" }));
        }

        return (entity1, entity2, null);
    }

    /// <summary>
    /// 親子関係の検証
    /// </summary>
    /// <typeparam name="TParent">親型</typeparam>
    /// <typeparam name="TChild">子型</typeparam>
    /// <param name="parent">親</param>
    /// <param name="child">子</param>
    /// <param name="parentIdSelector">親ID取得関数</param>
    /// <param name="childParentIdSelector">子の親ID取得関数</param>
    /// <param name="parentName">親名</param>
    /// <param name="childName">子名</param>
    protected IActionResult? ValidateParentChildRelationship<TParent, TChild>(
        TParent parent,
        TChild child,
        Func<TParent, Guid> parentIdSelector,
        Func<TChild, Guid> childParentIdSelector,
        string parentName,
        string childName)
    {
        var parentId = parentIdSelector(parent);
        var childParentId = childParentIdSelector(child);

        if (childParentId != parentId)
        {
            return BadRequest(new { message = $"{childName} does not belong to the {parentName}" });
        }

        return null;
    }

    /// <summary>
    /// 既存レコードの重複チェック
    /// </summary>
    /// <typeparam name="TEntity">エンティティ型</typeparam>
    /// <param name="query">クエリ</param>
    /// <param name="entityName">エンティティ名</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    protected async Task<IActionResult?> CheckDuplicateAsync<TEntity>(
        IQueryable<TEntity> query,
        string entityName,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var exists = await query.AnyAsync(cancellationToken);
        if (exists)
        {
            return Conflict(new { message = $"{entityName} already exists" });
        }
        return null;
    }
}
