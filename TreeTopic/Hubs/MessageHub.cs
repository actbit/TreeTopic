using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Routing;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Hubs;

public interface IMessageHubClient
{
    Task MessageCreated(MessageRealtimeDto message);
    Task MessageUpdated(MessageRealtimeDto message);
    Task MessageDeleted(MessageDeletedEvent payload);
}

[Authorize]
public class MessageHub : Hub<IMessageHubClient>
{
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public MessageHub(IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _tenantAccessor = tenantAccessor;
    }

    public Task JoinTopic(string topicId)
    {
        if (string.IsNullOrWhiteSpace(topicId))
            return Task.CompletedTask;

        var groupName = MessageHubGroups.Topic(string.Empty, topicId);
        var logger = Context.GetHttpContext()?.RequestServices.GetService<ILogger<MessageHub>>();
        logger?.LogInformation("[MessageHub] JoinTopic connection={ConnectionId} topic={Topic} group={Group}", Context.ConnectionId, topicId, groupName);
        return Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public Task LeaveTopic(string topicId)
    {
        if (string.IsNullOrWhiteSpace(topicId))
            return Task.CompletedTask;

        var groupName = MessageHubGroups.Topic(string.Empty, topicId);
        var logger = Context.GetHttpContext()?.RequestServices.GetService<ILogger<MessageHub>>();
        logger?.LogInformation("[MessageHub] LeaveTopic connection={ConnectionId} topic={Topic} group={Group}", Context.ConnectionId, topicId, groupName);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    private string ResolveTenantKey()
    {
        var tenantFromRoute = Context.GetHttpContext()?.GetRouteValue("tenant")?.ToString();
        if (!string.IsNullOrWhiteSpace(tenantFromRoute))
        {
            return tenantFromRoute;
        }

        return MessageHubGroups.ResolveTenantKey(_tenantAccessor.MultiTenantContext?.TenantInfo);
    }
}
