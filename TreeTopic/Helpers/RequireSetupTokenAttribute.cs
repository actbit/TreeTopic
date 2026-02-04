using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TreeTopic.Services;
using Microsoft.EntityFrameworkCore;

namespace TreeTopic.Helpers;

/// <summary>
/// SetupTokenが要求されるAPIエンドポイントに適用する属性
/// AuthorizationヘッダーのBearerトークンを検証する
/// </summary>
public class RequireSetupTokenAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _tenantParamName = "tenant";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var tenant = GetTenantFromContext(context);
        if (string.IsNullOrEmpty(tenant))
        {
            context.Result = new BadRequestObjectResult(new { message = "Tenant is required" });
            return;
        }

        // SetupTokenをAuthorizationヘッダーから取得
        var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Result = new BadRequestObjectResult(new { message = "Bearer token required" });
            return;
        }

        var setupToken = authHeader.Substring("Bearer ".Length).Trim();

        // SetupToken検証サービスを取得
        var setupTokenValidator = context.HttpContext.RequestServices
            .GetRequiredService<SetupTokenValidationService>();

        // テナント識別子からテナントIDを解決
        var tenantDb = context.HttpContext.RequestServices.GetRequiredService<TenantCatalogDbContext>();
        var tenantInfo = await tenantDb.Tenants
            .FirstOrDefaultAsync(t => t.Identifier == tenant);

        if (tenantInfo == null)
        {
            context.Result = new BadRequestObjectResult(new { message = "Tenant not found" });
            return;
        }

        var tenantId = tenantInfo.Id;

        // トークンを検証（テナントIDを使用）
        var isValid = await setupTokenValidator.ValidateSetupTokenAsync(tenantId, setupToken);
        if (!isValid)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid or expired setup token" });
            return;
        }

        // 検証済みのトークンをコントローラーで使用できるようHttpContextに保存
        context.HttpContext.Items["ValidatedSetupToken"] = setupToken;
        context.HttpContext.Items["ValidatedTenantId"] = tenantId;
    }

    private string? GetTenantFromContext(AuthorizationFilterContext context)
    {
        // ルートパラメータからテナントを取得
        if (context.RouteData.Values.ContainsKey(_tenantParamName))
        {
            return context.RouteData.Values[_tenantParamName]?.ToString();
        }

        // クエリパラメータからテナントを取得
        if (context.HttpContext.Request.Query.ContainsKey(_tenantParamName))
        {
            return context.HttpContext.Request.Query[_tenantParamName];
        }

        return null;
    }
}