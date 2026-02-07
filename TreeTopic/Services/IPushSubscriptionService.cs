using TreeTopic.Common;
using TreeTopic.Dtos;

namespace TreeTopic.Services;

/// <summary>
/// プッシュ購読管理サービスインターフェース
/// </summary>
public interface IPushSubscriptionService
{
    /// <summary>
    /// VAPID公開キーを取得
    /// </summary>
    Task<Result<VapidPublicKeyDto>> GetVapidPublicKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザーを購読
    /// </summary>
    Task<Result<PushSubscriptionDto>> SubscribeAsync(
        PushSubscriptionDto subscriptionDto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザーの購読を解除
    /// </summary>
    Task<Result> UnsubscribeAsync(
        PushSubscriptionDto subscriptionDto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 購読ステータスを確認
    /// </summary>
    Task<Result<bool>> CheckSubscriptionStatusAsync(
        string endpoint,
        CancellationToken cancellationToken = default);
}