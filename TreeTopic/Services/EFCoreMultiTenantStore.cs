using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// EF Core を使用したマルチテナント Store
/// TenantCatalogDbContext から tenant 情報を取得
/// </summary>
public class EFCoreMultiTenantStore : IMultiTenantStore<ApplicationTenantInfo>
{
    private readonly TenantCatalogDbContext _dbContext;
    private readonly ILogger<EFCoreMultiTenantStore> _logger;

    public EFCoreMultiTenantStore(
        TenantCatalogDbContext dbContext,
        ILogger<EFCoreMultiTenantStore> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Identifier からテナント情報を取得
    /// </summary>
    public async Task<ApplicationTenantInfo?> TryGetAsync(string identifier)
    {
        try
        {
            var tenant = await _dbContext.Tenants
                .FirstOrDefaultAsync(t => t.Identifier == identifier);

            if (tenant == null)
            {
                _logger.LogDebug("Tenant not found: {Identifier}", identifier);
            }

            return tenant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant by identifier: {Identifier}", identifier);
            throw;
        }
    }

    /// <summary>
    /// Identifier からテナント情報を取得（非同期）
    /// </summary>
    public async Task<ApplicationTenantInfo?> TryGetByIdentifierAsync(string identifier)
    {
        return await TryGetAsync(identifier);
    }

    /// <summary>
    /// Id からテナント情報を取得
    /// </summary>
    public async Task<ApplicationTenantInfo?> TryGetByIdAsync(string id)
    {
        try
        {
            return await _dbContext.Tenants
                .FirstOrDefaultAsync(t => t.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant by id: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// すべてのテナント情報を取得
    /// </summary>
    public async Task<IEnumerable<ApplicationTenantInfo>> GetAllAsync()
    {
        try
        {
            return await _dbContext.Tenants.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tenants");
            throw;
        }
    }

    /// <summary>
    /// すべてのテナント情報を取得（ページング対応）
    /// </summary>
    public async Task<IEnumerable<ApplicationTenantInfo>> GetAllAsync(int pageNumber, int pageSize)
    {
        try
        {
            return await _dbContext.Tenants
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenants with pagination");
            throw;
        }
    }

    /// <summary>
    /// テナント情報を追加（実装不要、API で処理）
    /// </summary>
    public Task<bool> TryAddAsync(ApplicationTenantInfo tenantInfo)
    {
        throw new NotImplementedException("Use TenantManagementService.CreateTenantAsync instead");
    }

    /// <summary>
    /// テナント情報を更新（実装不要、API で処理）
    /// </summary>
    public Task<bool> TryUpdateAsync(ApplicationTenantInfo tenantInfo)
    {
        throw new NotImplementedException("Use API endpoints to update tenant");
    }

    /// <summary>
    /// テナント情報を削除（実装不要、API で処理）
    /// </summary>
    public Task<bool> TryRemoveAsync(string identifier)
    {
        throw new NotImplementedException("Use TenantManagementService.DeleteTenantAsync instead");
    }
}
