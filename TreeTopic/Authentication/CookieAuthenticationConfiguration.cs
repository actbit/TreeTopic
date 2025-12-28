using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace TreeTopic.Authentication;

/// <summary>
/// Configures CookieAuthenticationOptions with TenantAwareCookieManager using constructor injection.
/// This avoids calling BuildServiceProvider() during configuration.
/// </summary>
internal class CookieAuthenticationConfiguration : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly IServiceProvider _serviceProvider;

    public CookieAuthenticationConfiguration(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name != CookieAuthenticationDefaults.AuthenticationScheme)
            return;

        var httpContextAccessor = _serviceProvider.GetRequiredService<IHttpContextAccessor>();
        var logger = _serviceProvider.GetRequiredService<ILogger<TenantAwareCookieManager>>();
        options.CookieManager = new TenantAwareCookieManager(httpContextAccessor, logger);
    }
}
