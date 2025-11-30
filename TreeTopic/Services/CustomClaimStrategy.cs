using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.Security.Claims;

namespace TreeTopic.Services;

/// <summary>
/// カスタム Claim Strategy
/// OIDC コールバックパス（/auth/signin-oidc）では tenant 解決をスキップ
/// </summary>
public class CustomClaimStrategy : IMultiTenantStrategy
{
    private readonly string _claimType;
    private readonly ILogger<CustomClaimStrategy> _logger;

    public CustomClaimStrategy(string claimType, ILogger<CustomClaimStrategy> logger)
    {
        _claimType = claimType;
        _logger = logger;
    }

    public async Task<string?> GetIdentifierAsync(object context)
    {
        if (context is not HttpContext httpContext)
        {
            return null;
        }

        // OIDC コールバックパスではスキップ（tenant 解決を行わない）
        if (httpContext.Request.Path.StartsWithSegments("/auth/signin-oidc"))
        {
            _logger.LogInformation("[CustomClaimStrategy] Skipping tenant resolution for /auth/signin-oidc");
            return null;
        }

        // それ以外のパスでは claim から tenant を取得
        var tenantId = httpContext.User?.FindFirst(_claimType)?.Value;

        if (!string.IsNullOrEmpty(tenantId))
        {
            _logger.LogInformation("[CustomClaimStrategy] Tenant resolved from claim: {TenantId}", tenantId);
        }

        return tenantId;
    }
}
