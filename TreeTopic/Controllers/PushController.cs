using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;
using System.Security.Claims;
using MaskedUUID.AspNetCore.Types;
using Finbuckle.MultiTenant.Abstractions;
using TreeTopic;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly IVapidService _vapidService;
    private readonly IMultiTenantContextAccessor _multiTenantContextAccessor;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PushController> _logger;

    public PushController(
        IVapidService vapidService,
        IMultiTenantContextAccessor multiTenantContextAccessor,
        ApplicationDbContext dbContext,
        ILogger<PushController> logger)
    {
        _vapidService = vapidService;
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _dbContext = dbContext;
        _logger = logger;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    [HttpGet("vapid-public-key")]
    public async Task<IActionResult> GetVapidPublicKey()
    {
        try
        {
            var tenantId = _multiTenantContextAccessor.MultiTenantContext?.TenantInfo?.Id
                ?? throw new InvalidOperationException("No tenant context available");

            var (publicKey, _) = await _vapidService.GetOrCreateKeysAsync(tenantId);
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

            // 既存の購読を確認（同じEndpointの場合は更新）
            var existing = await _dbContext.PushSubscriptions
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.Endpoint == subscriptionDto.Endpoint);

            if (existing != null)
            {
                // 既存の購読を更新
                existing.P256dhKey = subscriptionDto.Keys.P256dh;
                existing.AuthKey = subscriptionDto.Keys.Auth;
                existing.LastUsedAt = DateTime.UtcNow;
            }
            else
            {
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
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} subscribed to push notifications: {Endpoint}", userId, subscriptionDto.Endpoint);
            return Ok(new { success = true });
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

            var subscription = await _dbContext.PushSubscriptions
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.Endpoint == request.Endpoint);

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
}

public class UnsubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
}
