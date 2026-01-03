using System.Collections.Concurrent;
using Finbuckle.MultiTenant.Abstractions;
using MaskedUUID.AspNetCore.KeyProviders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeTopic;
using TreeTopic.Constants;
using TreeTopic.Models;

namespace TreeTopic.KeyProviders;

public class TreeTopicMaskedUUIDKeyProvider : IMaskedUUIDKeyProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TreeTopicMaskedUUIDKeyProvider> _logger;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _multiTenantContextAccessor;
    private const string CacheKeyPrefix = "MaskedUUID_Keys_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(7);
    private readonly ConcurrentDictionary<string, Guid> _tenantIdentifierCache = new();
    private readonly object _defaultTenantLock = new();
    private Guid? _defaultTenantId;

    public TreeTopicMaskedUUIDKeyProvider(
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory serviceScopeFactory,
        IMemoryCache cache,
        ILogger<TreeTopicMaskedUUIDKeyProvider> logger,
        IMultiTenantContextAccessor<ApplicationTenantInfo> multiTenantContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
        _cache = cache;
        _logger = logger;
        _multiTenantContextAccessor = multiTenantContextAccessor;
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
        {
            return GetFallbackTenantId();
        }

        var tenantInfo = _multiTenantContextAccessor.MultiTenantContext?.TenantInfo;
        if (tenantInfo != null && Guid.TryParse(tenantInfo.Id, out var infoGuid))
        {
            return infoGuid;
        }

        var identifier = ResolveTenantIdentifier(httpContext);
        if (!string.IsNullOrEmpty(identifier))
        {
            return ResolveTenantIdFromIdentifier(identifier);
        }

        return GetFallbackTenantId();
    }

    private string? ResolveTenantIdentifier(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User?.FindFirst(AuthenticationConstants.TenantClaimType)?.Value;
        if (!string.IsNullOrEmpty(tenantClaim))
        {
            return tenantClaim;
        }

        var routeTenant = httpContext.GetRouteValue(AuthenticationConstants.TenantClaimType)?.ToString();
        if (!string.IsNullOrEmpty(routeTenant))
        {
            return routeTenant;
        }

        if (httpContext.Items.TryGetValue(AuthenticationConstants.Cookie.TenantForCookieKey, out var tenantObj) &&
            tenantObj is string tenantForCookie &&
            !string.IsNullOrEmpty(tenantForCookie))
        {
            return tenantForCookie;
        }

        var queryTenant = httpContext.Request.Query[AuthenticationConstants.TenantClaimType].ToString();
        if (!string.IsNullOrEmpty(queryTenant))
        {
            return queryTenant;
        }

        return null;
    }

    private Guid ResolveTenantIdFromIdentifier(string tenantIdentifier)
    {
        var normalizedIdentifier = tenantIdentifier.Trim();
        if (_tenantIdentifierCache.TryGetValue(normalizedIdentifier, out var cached))
        {
            return cached;
        }

        if (Guid.TryParse(normalizedIdentifier, out var parsedGuid))
        {
            _tenantIdentifierCache.TryAdd(normalizedIdentifier, parsedGuid);
            return parsedGuid;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenantCatalogDbContext>();
        var tenantInfo = dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefault(t => t.Identifier == normalizedIdentifier || t.Id == normalizedIdentifier);

        if (tenantInfo == null)
        {
            _logger.LogWarning("Tenant identifier '{TenantIdentifier}' not found when resolving MaskedUUID keys", normalizedIdentifier);
            throw new InvalidOperationException($"Tenant '{normalizedIdentifier}' not found for MaskedUUID encoding");
        }

        if (!Guid.TryParse(tenantInfo.Id, out var tenantGuid))
        {
            throw new InvalidOperationException($"Tenant ID '{tenantInfo.Id}' is not a valid GUID");
        }

        _tenantIdentifierCache.TryAdd(normalizedIdentifier, tenantGuid);
        _tenantIdentifierCache.TryAdd(tenantInfo.Id, tenantGuid);

        if (!string.IsNullOrEmpty(tenantInfo.Identifier))
        {
            _tenantIdentifierCache.TryAdd(tenantInfo.Identifier, tenantGuid);
        }

        return tenantGuid;
    }

    private Guid GetFallbackTenantId()
    {
        if (_defaultTenantId.HasValue)
        {
            return _defaultTenantId.Value;
        }

        lock (_defaultTenantLock)
        {
            if (_defaultTenantId.HasValue)
            {
                return _defaultTenantId.Value;
            }

            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TenantCatalogDbContext>();
            var tenantInfo = dbContext.Tenants.AsNoTracking().FirstOrDefault();

            if (tenantInfo == null || string.IsNullOrEmpty(tenantInfo.Id))
            {
                throw new InvalidOperationException("No tenant configured for MaskedUUID encoding");
            }

            if (!Guid.TryParse(tenantInfo.Id, out var fallbackGuid))
            {
                throw new InvalidOperationException($"Configured tenant ID '{tenantInfo.Id}' is not a valid GUID");
            }

            _defaultTenantId = fallbackGuid;
            _tenantIdentifierCache.TryAdd(tenantInfo.Id, fallbackGuid);
            if (!string.IsNullOrEmpty(tenantInfo.Identifier))
            {
                _tenantIdentifierCache.TryAdd(tenantInfo.Identifier, fallbackGuid);
            }

            _logger.LogWarning("MaskedUUID fallback tenant used: {TenantIdentifier}", tenantInfo.Identifier ?? tenantInfo.Id);
            return fallbackGuid;
        }
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
