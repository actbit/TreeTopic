using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// プッシュ購読管理サービス
/// </summary>
public class PushSubscriptionService : BaseService, IPushSubscriptionService
{
    private readonly IVapidService _vapidService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PushSubscriptionService> _logger;

    public PushSubscriptionService(
        IVapidService vapidService,
        ApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PushSubscriptionService> logger) : base(logger)
    {
        _vapidService = vapidService;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// VAPID公開キーを取得
    /// </summary>
    public async Task<Result<VapidPublicKeyDto>> GetVapidPublicKeyAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // グローバルなVAPIDキーを取得
            var (publicKey, _) = await _vapidService.GetOrCreateKeysAsync();
            return Result<VapidPublicKeyDto>.Success(new VapidPublicKeyDto(publicKey));
        }, nameof(GetVapidPublicKeyAsync));
    }

    /// <summary>
    /// ユーザーを購読
    /// </summary>
    public async Task<Result<PushSubscriptionDto>> SubscribeAsync(
        PushSubscriptionDto subscriptionDto,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Result<PushSubscriptionDto>.Unauthorized("User not authenticated");
            }

            // 既存の購読を確認（同じEndpointの場合は更新）
            var existing = await _dbContext.PushSubscriptions
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.Endpoint == subscriptionDto.Endpoint, cancellationToken);

            if (existing != null)
            {
                // 既存の購読を更新
                existing.P256dhKey = subscriptionDto.Keys.P256dh;
                existing.AuthKey = subscriptionDto.Keys.Auth;
                existing.LastUsedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("User {UserId} push subscription updated: {Endpoint}", userId, subscriptionDto.Endpoint);
                subscriptionDto.Updated = true;
                return Result<PushSubscriptionDto>.Success(subscriptionDto);
            }

            // 新規購読を追加
            var subscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Endpoint = subscriptionDto.Endpoint,
                P256dhKey = subscriptionDto.Keys.P256dh,
                AuthKey = subscriptionDto.Keys.Auth,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            };
            _dbContext.PushSubscriptions.Add(subscription);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("User {UserId} subscribed to push notifications: {Endpoint}", userId, subscriptionDto.Endpoint);
                subscriptionDto.Updated = false;
                return Result<PushSubscriptionDto>.Success(subscriptionDto);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
            {
                // ユニーク制約違反（競合状態で既に登録された場合）
                _logger.LogWarning("Push subscription already exists (race condition): {Endpoint}", subscriptionDto.Endpoint);
                subscriptionDto.Updated = false;
                subscriptionDto.Existed = true;
                return Result<PushSubscriptionDto>.Success(subscriptionDto);
            }
        }, nameof(SubscribeAsync));
    }

    /// <summary>
    /// ユーザーの購読を解除
    /// </summary>
    public async Task<Result> UnsubscribeAsync(
        PushSubscriptionDto subscriptionDto,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Result.Unauthorized("User not authenticated");
            }

            var subscription = await _dbContext.PushSubscriptions
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.Endpoint == subscriptionDto.Endpoint, cancellationToken);

            if (subscription != null)
            {
                _dbContext.PushSubscriptions.Remove(subscription);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("User {UserId} unsubscribed from push notifications", userId);
            return Result.Success();
        }, nameof(UnsubscribeAsync));
    }

    /// <summary>
    /// 購読ステータスを確認
    /// </summary>
    public async Task<Result<bool>> CheckSubscriptionStatusAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Result<bool>.Unauthorized("User not authenticated");
            }

            var subscription = await _dbContext.PushSubscriptions
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.Endpoint == endpoint, cancellationToken);

            return Result<bool>.Success(subscription != null);
        }, nameof(CheckSubscriptionStatusAsync));
    }

    /// <summary>
    /// 現在のユーザーIDを取得
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return Guid.Empty;

        var userIdValue = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdValue) || !Guid.TryParse(userIdValue, out var userId))
        {
            return Guid.Empty;
        }
        return userId;
    }
}