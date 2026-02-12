using TreeTopic.Models;

namespace TreeTopic.Hubs;

public static class MessageHubGroups
{
    public static string ResolveTenantKey(ApplicationTenantInfo? tenantInfo)
    {
        return tenantInfo?.Identifier ?? "default";
    }

    public static string Topic(string tenantKey, string maskedTopicId)
    {
        return $"tenant:{tenantKey}:topic:{maskedTopicId}";
    }
}
