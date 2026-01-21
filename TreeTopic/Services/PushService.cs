using System.Text;
using System.Text.Json;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using TreeTopic.Dtos;

namespace TreeTopic.Services;

public interface IPushService
{
    Task SendNotificationAsync(PushSubscriptionDto subscription, PushNotificationRequest notification);
    Task SendNotificationAsync(PushSubscriptionDto subscription, string title, string? body = null);
}

public class PushService : IPushService
{
    private readonly PushServiceClient _pushClient;
    private readonly IVapidService _vapidService;
    private readonly IMultiTenantContextAccessor _multiTenantContextAccessor;
    private readonly ILogger<PushService> _logger;

    public PushService(
        HttpClient httpClient,
        IVapidService vapidService,
        IMultiTenantContextAccessor multiTenantContextAccessor,
        ILogger<PushService> logger)
    {
        _pushClient = new PushServiceClient(httpClient);
        _vapidService = vapidService;
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _logger = logger;
    }

    public async Task SendNotificationAsync(PushSubscriptionDto subscription, PushNotificationRequest notification)
    {
        try
        {
            // 現在のテナントIDを取得
            var tenantId = _multiTenantContextAccessor.MultiTenantContext?.TenantInfo?.Id
                ?? throw new InvalidOperationException("No tenant context available");

            // VAPIDキーを取得
            var (publicKey, privateKey) = await _vapidService.GetOrCreateKeysAsync(tenantId);

            // VAPID認証情報を設定
            var vapidAuth = new VapidAuthentication(publicKey, privateKey)
            {
                Subject = "mailto:admin@treetopic.com"
            };

            // PushSubscriptionを作成
            var pushSubscription = new PushSubscription
            {
                Endpoint = subscription.Endpoint,
                Keys = new Dictionary<string, string>
                {
                    { "p256dh", subscription.Keys.P256dh },
                    { "auth", subscription.Keys.Auth }
                }
            };

            // ペイロードを作成
            var payload = new
            {
                title = notification.Title,
                body = notification.Body ?? "",
                icon = notification.Icon ?? "/pwa-192x192.png"
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // PushMessageを作成
            var pushMessage = new PushMessage(content)
            {
                TimeToLive = 2419200 // 28日
            };

            // プッシュ通知を送信
            await _pushClient.RequestPushMessageDeliveryAsync(pushSubscription, pushMessage, vapidAuth);

            _logger.LogInformation("Push notification sent successfully: {Title}", notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification: {Title}", notification.Title);
            throw;
        }
    }

    public Task SendNotificationAsync(PushSubscriptionDto subscription, string title, string? body = null)
    {
        return SendNotificationAsync(subscription, new PushNotificationRequest
        {
            Title = title,
            Body = body
        });
    }
}
