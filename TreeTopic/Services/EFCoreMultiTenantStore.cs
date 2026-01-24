using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public EFCoreMultiTenantStore(
        TenantCatalogDbContext dbContext,
        ILogger<EFCoreMultiTenantStore> logger,
        IMemoryCache cache)
    {
        _dbContext = dbContext;
        _logger = logger;
        _cache = cache;
    }

    public Task<bool> AddAsync(ApplicationTenantInfo tenantInfo)
    {
        throw new NotImplementedException("Use TenantManagementService.CreateTenantAsync instead");
    }

    public Task<bool> UpdateAsync(ApplicationTenantInfo tenantInfo)
    {
        throw new NotImplementedException("Use API endpoints to update tenant");
    }

    public Task<bool> RemoveAsync(string identifier)
    {
        throw new NotImplementedException("Use TenantManagementService.DeleteTenantAsync instead");
    }

    public Task<ApplicationTenantInfo?> GetByIdentifierAsync(string identifier)
    {
        return TryGetAsync(identifier);
    }

    public Task<ApplicationTenantInfo?> GetAsync(string id)
    {
        return TryGetByIdAsync(id);
    }

    /// <summary>
    /// Identifier からテナント情報を取得
    /// </summary>
    public async Task<ApplicationTenantInfo?> TryGetAsync(string identifier)
    {
        var cacheKey = $"tenant:id:{identifier}";

        // キャッシュをチェック
        if (_cache.TryGetValue<ApplicationTenantInfo?>(cacheKey, out var cached))
        {
            _logger.LogDebug("Tenant found in cache: {Identifier}", identifier);
            return cached;
        }

        try
        {
            var tenant = await _dbContext.Tenants
                .Include(t => t.Detail)
                .FirstOrDefaultAsync(t => t.Identifier == identifier);

            if (tenant == null)
            {
                _logger.LogDebug("Tenant not found: {Identifier}", identifier);
                return null;
            }

            // キャッシュに保存（5分間）
            _cache.Set(cacheKey, tenant, CacheExpiration);
            _logger.LogDebug("Tenant cached: {Identifier}", identifier);

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
        var cacheKey = $"tenant:id-guid:{id}";

        // キャッシュをチェック
        if (_cache.TryGetValue<ApplicationTenantInfo?>(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var tenant = await _dbContext.Tenants
                .Include(t => t.Detail)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tenant != null)
            {
                // キャッシュに保存（5分間）
                _cache.Set(cacheKey, tenant, CacheExpiration);
            }

            return tenant;
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
            return await _dbContext.Tenants
                .Include(t => t.Detail)
                .ToListAsync();
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
                .Include(t => t.Detail)
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




