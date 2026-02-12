using TreeTopic.Models;

namespace TreeTopic.Hubs;

public static class RoomTopicHubGroups
{
    public static string ResolveTenantKey(ApplicationTenantInfo? tenantInfo)
    {
        return tenantInfo?.Identifier ?? "default";
    }

    public static string Tenant(string tenantKey)
    {
        return $"tenant:{tenantKey}";
    }

    public static string Room(string tenantKey, string maskedRoomId)
    {
        return $"tenant:{tenantKey}:room:{maskedRoomId}";
    }
}
