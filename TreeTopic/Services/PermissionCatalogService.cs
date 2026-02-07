namespace TreeTopic.Services;

public sealed record PermissionCatalogItem(string Name, string Scope, bool IsUsed);

public class PermissionCatalogService
{
    private readonly PermissionScanService _permissionScanService;

    public PermissionCatalogService(PermissionScanService permissionScanService)
    {
        _permissionScanService = permissionScanService;
    }

    public IReadOnlyList<PermissionCatalogItem> GetTenantPermissions()
        => BuildCatalog("tenant");

    public IReadOnlyList<PermissionCatalogItem> GetRoomPermissions()
        => BuildCatalog("room");

    public IReadOnlyList<PermissionCatalogItem> GetTopicPermissions()
        => BuildCatalog("topic");

    public object GetAllByCategory()
    {
        return new
        {
            tenant = GetTenantPermissions(),
            topic = GetTopicPermissions(),
            room = GetRoomPermissions()
        };
    }

    private IReadOnlyList<PermissionCatalogItem> BuildCatalog(string scope)
    {
        return _permissionScanService.Permissions
            .Where(p => p.Name.StartsWith(scope + ".", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p =>
                new PermissionCatalogItem(
                    p,
                    scope,
                    true))
            .ToList();
    }
}
