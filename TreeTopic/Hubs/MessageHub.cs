using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Routing;
using MaskedUUID.AspNetCore.Types;
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
    private readonly IRealtimeAccessService _realtimeAccessService;
    private readonly ILogger<MessageHub> _logger;

    public MessageHub(
        IRealtimeAccessService realtimeAccessService,
        ILogger<MessageHub> logger)
    {
        _realtimeAccessService = realtimeAccessService;
        _logger = logger;
    }

    public async Task JoinTopic(MaskedGuid topicId)
    {
        try
        {
            if (!await _realtimeAccessService.CanJoinTopicAsync(topicId, Context.User, Context.ConnectionAborted))
            {
                _logger.LogWarning("[MessageHub] JoinTopic denied connection={ConnectionId} topic={TopicId} user={UserId}",
                    Context.ConnectionId, topicId, Context.UserIdentifier);
                return;
            }

            var tenantInfo = Context.GetHttpContext()?.GetMultiTenantContext<ApplicationTenantInfo>()?.TenantInfo;
            var tenant = MessageHubGroups.ResolveTenantKey(tenantInfo);
            var groupName = MessageHubGroups.Topic(tenant, topicId.ToString());
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

    public async Task LeaveTopic(MaskedGuid topicId)
    {
        try
        {
            var tenantInfo = Context.GetHttpContext()?.GetMultiTenantContext<ApplicationTenantInfo>()?.TenantInfo;
            var tenant = MessageHubGroups.ResolveTenantKey(tenantInfo);
            var groupName = MessageHubGroups.Topic(tenant, topicId.ToString());
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _logger.LogInformation("[MessageHub] Client disconnected: {ConnectionId}, Exception: {Exception}",
                Context.ConnectionId, exception?.Message ?? "None");

            // 注: 接続が閉じるとSignalRが自動的にグループをクリーンアップする
            // カスタムグループ管理を使用している場合を除き、手動クリーンアップは不要
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageHub] Error in OnDisconnectedAsync: {Message}", ex.Message);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
