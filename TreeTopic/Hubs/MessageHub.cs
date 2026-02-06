using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Routing;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;

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
    private readonly IRealtimeAccessService _realtimeAccessService;
    private readonly ILogger<MessageHub> _logger;

    public MessageHub(
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        IRealtimeAccessService realtimeAccessService,
        ILogger<MessageHub> logger)
    {
        _tenantAccessor = tenantAccessor;
        _realtimeAccessService = realtimeAccessService;
        _logger = logger;
    }

    public async Task JoinTopic(string topicId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(topicId))
                return;

            if (!await _realtimeAccessService.CanJoinTopicAsync(topicId, Context.User, Context.ConnectionAborted))
            {
                _logger.LogWarning("[MessageHub] JoinTopic denied connection={ConnectionId} topic={TopicId} user={UserId}",
                    Context.ConnectionId, topicId, Context.UserIdentifier);
                return;
            }

            var groupName = MessageHubGroups.Topic(string.Empty, topicId);
            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                _logger.LogInformation("[MessageHub] JoinTopic connection={ConnectionId} topic={Topic} group={Group}", Context.ConnectionId, topicId, groupName);
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageHub] Error in JoinTopic: {Message}", ex.Message);
        }
    }

    public async Task LeaveTopic(string topicId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(topicId))
                return;

            var groupName = MessageHubGroups.Topic(string.Empty, topicId);
            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                _logger.LogInformation("[MessageHub] LeaveTopic connection={ConnectionId} topic={Topic} group={Group}", Context.ConnectionId, topicId, groupName);
            }
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageHub] Error in LeaveTopic: {Message}", ex.Message);
        }
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _logger.LogInformation("[MessageHub] Client disconnected: {ConnectionId}, Exception: {Exception}",
                Context.ConnectionId, exception?.Message ?? "None");

            // Note: Groups are automatically cleaned up by SignalR when a connection closes
            // No manual cleanup needed unless using custom group management
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageHub] Error in OnDisconnectedAsync: {Message}", ex.Message);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
