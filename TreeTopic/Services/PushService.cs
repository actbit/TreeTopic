using System.Text;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using TreeTopic.Dtos;

namespace TreeTopic.Services;

public interface IPushService
{
    Task SendNotificationAsync(PushSubscriptionDto subscription, PushNotificationRequest notification);
    Task SendNotificationAsync(PushSubscriptionDto subscription, string title, string? body = null);
}

/// <summary>
/// 購読が無効であることを示す例外
/// </summary>
public class SubscriptionExpiredException : Exception
{
    public string Endpoint { get; }

    public SubscriptionExpiredException(string endpoint)
        : base($"Push subscription has expired: {endpoint}")
    {
        Endpoint = endpoint;
    }
}

public class PushService : IPushService
{
    private readonly PushServiceClient _pushClient;
    private readonly IVapidService _vapidService;
    private readonly ILogger<PushService> _logger;

    public PushService(
        HttpClient httpClient,
        IVapidService vapidService,
        ILogger<PushService> logger)
    {
        _pushClient = new PushServiceClient(httpClient);
        _vapidService = vapidService;
        _logger = logger;
    }

    public async Task SendNotificationAsync(PushSubscriptionDto subscription, PushNotificationRequest notification)
    {
        try
        {
            // グローバルなVAPIDキーを取得
            var (publicKey, privateKey) = await _vapidService.GetOrCreateKeysAsync();

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
        catch (Lib.Net.Http.WebPush.PushServiceClientException ex)
        {
            // 購読が無効（410 Goneまたは403 Forbidden）の場合
            // 410: 購読が期限切れ
            // 403: VAPIDキーが無効（キーが再生成された場合など）
            bool isGone = ex.Message?.Contains("Gone") == true || ex.ToString().Contains("Gone") == true;
            bool isForbidden = ex.Message?.Contains("Forbidden") == true || ex.ToString().Contains("Forbidden") == true;

            if (isGone || isForbidden)
            {
                _logger.LogWarning("Push subscription is invalid ({Status}): {Endpoint}", isGone ? "Gone" : "Forbidden", subscription.Endpoint);
                throw new SubscriptionExpiredException(subscription.Endpoint);
            }
            _logger.LogError(ex, "Failed to send push notification: {Title}", notification.Title);
            throw;
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
