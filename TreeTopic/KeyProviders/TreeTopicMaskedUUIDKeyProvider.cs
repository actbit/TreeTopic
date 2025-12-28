using MaskedUUID.AspNetCore.KeyProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeTopic.Models;

namespace TreeTopic.KeyProviders;

public class TreeTopicMaskedUUIDKeyProvider : IMaskedUUIDKeyProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TreeTopicMaskedUUIDKeyProvider> _logger;
    private const string CacheKeyPrefix = "MaskedUUID_Keys_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(7);

    public TreeTopicMaskedUUIDKeyProvider(
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory serviceScopeFactory,
        IMemoryCache cache,
        ILogger<TreeTopicMaskedUUIDKeyProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(ulong K0, ulong K1)> GetKeysAsync()
    {
        var tenantId = GetCurrentTenantId();
        var cacheKey = $"{CacheKeyPrefix}{tenantId}";

        // Check cache first
        if (_cache.TryGetValue(cacheKey, out (ulong k0, ulong k1) cachedKeys))
        {
            _logger.LogDebug("MaskedUUID keys found in cache for tenant {TenantId}", tenantId);
            return cachedKeys;
        }

        // Fetch from DB
        var keys = await FetchKeysFromDatabaseAsync(tenantId);

        // Cache the keys
        _cache.Set(cacheKey, keys, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        });

        _logger.LogDebug("MaskedUUID keys fetched from DB and cached for tenant {TenantId}", tenantId);
        return keys;
    }

    public (ulong K0, ulong K1) GetKeysSynchronous()
    {
        var tenantId = GetCurrentTenantId();
        var cacheKey = $"{CacheKeyPrefix}{tenantId}";

        // Check cache first
        if (_cache.TryGetValue(cacheKey, out (ulong k0, ulong k1) cachedKeys))
        {
            _logger.LogDebug("MaskedUUID keys found in cache (sync) for tenant {TenantId}", tenantId);
            return cachedKeys;
        }

        // Fetch from DB (sync wrapper)
        var keys = FetchKeysFromDatabaseSync(tenantId);

        // Cache the keys
        _cache.Set(cacheKey, keys, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        });

        _logger.LogDebug("MaskedUUID keys fetched from DB and cached (sync) for tenant {TenantId}", tenantId);
        return keys;
    }

    private Guid GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            throw new InvalidOperationException("HttpContext is not available");

        var tenantId = httpContext.GetRouteValue("tenant")?.ToString();
        if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var parsedTenantId))
            throw new InvalidOperationException("Valid tenant ID not found in route");

        return parsedTenantId;
    }

    private async Task<(ulong K0, ulong K1)> FetchKeysFromDatabaseAsync(Guid tenantId)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenantCatalogDbContext>();

        var tenantInfo = await dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Id == tenantId.ToString());

        if (tenantInfo == null)
        {
            _logger.LogWarning("Tenant {TenantId} not found in database", tenantId);
            throw new InvalidOperationException($"Tenant {tenantId} not found");
        }

        if (tenantInfo.Detail == null ||
            tenantInfo.Detail.TenantObfuscationKeyK0 == 0 ||
            tenantInfo.Detail.TenantObfuscationKeyK1 == 0)
        {
            _logger.LogWarning("MaskedUUID keys not configured for tenant {TenantId}", tenantId);
            throw new InvalidOperationException($"MaskedUUID keys not configured for tenant {tenantId}");
        }

        _logger.LogDebug("MaskedUUID keys loaded from database for tenant {TenantId}", tenantId);
        return (tenantInfo.Detail.TenantObfuscationKeyK0, tenantInfo.Detail.TenantObfuscationKeyK1);
    }

    private (ulong K0, ulong K1) FetchKeysFromDatabaseSync(Guid tenantId)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenantCatalogDbContext>();

        var tenantInfo = dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefault(t => t.Id == tenantId.ToString());

        if (tenantInfo == null)
        {
            _logger.LogWarning("Tenant {TenantId} not found in database", tenantId);
            throw new InvalidOperationException($"Tenant {tenantId} not found");
        }

        if (tenantInfo.Detail == null ||
            tenantInfo.Detail.TenantObfuscationKeyK0 == 0 ||
            tenantInfo.Detail.TenantObfuscationKeyK1 == 0)
        {
            _logger.LogWarning("MaskedUUID keys not configured for tenant {TenantId}", tenantId);
            throw new InvalidOperationException($"MaskedUUID keys not configured for tenant {tenantId}");
        }

        _logger.LogDebug("MaskedUUID keys loaded from database (sync) for tenant {TenantId}", tenantId);
        return (tenantInfo.Detail.TenantObfuscationKeyK0, tenantInfo.Detail.TenantObfuscationKeyK1);
    }
}
