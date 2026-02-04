using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;
using System.Security.Claims;
using MaskedUUID.AspNetCore.Types;
using Npgsql;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly IVapidService _vapidService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PushController> _logger;

    public PushController(
        IVapidService vapidService,
        ApplicationDbContext dbContext,
        ILogger<PushController> logger)
    {
        _vapidService = vapidService;
        _dbContext = dbContext;
        _logger = logger;
    }

    private Guid? CurrentUserId
    {
        get
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdValue) || !Guid.TryParse(userIdValue, out var userId))
            {
                return null;
            }
            return userId;
        }
    }

    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVapidPublicKey()
    {
        try
        {
            // グローバルなVAPIDキーを取得
            var (publicKey, _) = await _vapidService.GetOrCreateKeysAsync();
            return Ok(new { publicKey });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "VAPID keys not configured");
            return StatusCode(500, new { error = "Push notifications not configured" });
        }
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto subscriptionDto)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null || userId == Guid.Empty)
            {
                _logger.LogWarning("Subscribe called without valid user ID");
                return Unauthorized(new { error = "User not authenticated" });
            }

            // 既存の購読を確認（同じEndpointの場合は更新）
            var existing = await _dbContext.PushSubscriptions
                .FirstOrDefaultAsync(ps => ps.UserId == userId.Value && ps.Endpoint == subscriptionDto.Endpoint);

            if (existing != null)
            {
                // 既存の購読を更新
                existing.P256dhKey = subscriptionDto.Keys.P256dh;
                existing.AuthKey = subscriptionDto.Keys.Auth;
                existing.LastUsedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("User {UserId} push subscription updated: {Endpoint}", userId, subscriptionDto.Endpoint);
                return Ok(new { success = true, updated = true });
            }

            // 新規購読を追加
            var subscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Endpoint = subscriptionDto.Endpoint,
                P256dhKey = subscriptionDto.Keys.P256dh,
                AuthKey = subscriptionDto.Keys.Auth,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            };
            _dbContext.PushSubscriptions.Add(subscription);

            try
            {
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("User {UserId} subscribed to push notifications: {Endpoint}", userId, subscriptionDto.Endpoint);
                return Ok(new { success = true, updated = false });
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                // ユニーク制約違反（競合状態で既に登録された場合）
                _logger.LogWarning("Push subscription already exists (race condition): {Endpoint}", subscriptionDto.Endpoint);
                return Ok(new { success = true, updated = false, existed = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to push notifications");
            return StatusCode(500, new { error = "Failed to subscribe" });
        }
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null || userId == Guid.Empty)
            {
                _logger.LogWarning("Unsubscribe called without valid user ID");
                return Unauthorized(new { error = "User not authenticated" });
            }

            var subscription = await _dbContext.PushSubscriptions
                .FirstOrDefaultAsync(ps => ps.UserId == userId.Value && ps.Endpoint == request.Endpoint);

            if (subscription != null)
            {
                _dbContext.PushSubscriptions.Remove(subscription);
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("User {UserId} unsubscribed from push notifications", userId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from push notifications");
            return StatusCode(500, new { error = "Failed to unsubscribe" });
        }
    }

    [HttpGet("subscription-status")]
    public async Task<IActionResult> GetSubscriptionStatus([FromQuery] string endpoint)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null || userId == Guid.Empty)
            {
                _logger.LogWarning("GetSubscriptionStatus called without valid user ID");
                return Unauthorized(new { error = "User not authenticated" });
            }

            var subscription = await _dbContext.PushSubscriptions
                .FirstOrDefaultAsync(ps => ps.UserId == userId.Value && ps.Endpoint == endpoint);

            return Ok(new { exists = subscription != null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check subscription status");
            return StatusCode(500, new { error = "Failed to check subscription status" });
        }
    }
}

public class UnsubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
}
