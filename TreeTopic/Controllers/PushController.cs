using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class PushController : BaseController
{
    private readonly IPushSubscriptionService _service;

    public PushController(IPushSubscriptionService service)
    {
        _service = service;
    }

    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVapidPublicKey()
    {
        var result = await _service.GetVapidPublicKeyAsync();
        if (result.IsSuccess)
        {
            return Ok(new { publicKey = result.Data.PublicKey });
        }
        return StatusCode(500, new { error = "Push notifications not configured" });
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto subscriptionDto)
    {
        if (subscriptionDto == null || subscriptionDto.Keys == null)
        {
            return BadRequest(new { error = "Invalid subscription data" });
        }

        if (string.IsNullOrWhiteSpace(subscriptionDto.Endpoint))
        {
            return BadRequest(new { error = "Endpoint is required" });
        }

        if (string.IsNullOrWhiteSpace(subscriptionDto.Keys.P256dh) || string.IsNullOrWhiteSpace(subscriptionDto.Keys.Auth))
        {
            return BadRequest(new { error = "Keys are required" });
        }

        var result = await _service.SubscribeAsync(subscriptionDto);
        if (result.IsSuccess)
        {
            var sub = result.Data;
            if (sub.Existed.HasValue && sub.Existed.Value)
            {
                return Ok(new { success = true, updated = false, existed = true });
            }
            return Ok(new { success = true, updated = sub.Updated });
        }
        return result.Error?.Message.Contains("unauthorized") == true
            ? Unauthorized(new { error = result.Error.Message })
            : StatusCode(500, new { error = "Failed to subscribe" });
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        // UnsubscribeRequestからPushSubscriptionDtoを作成
        var subscriptionDto = new PushSubscriptionDto
        {
            Endpoint = request.Endpoint,
            Keys = new PushSubscriptionKeys { P256dh = string.Empty, Auth = string.Empty }
        };

        var result = await _service.UnsubscribeAsync(subscriptionDto);
        if (result.IsSuccess)
        {
            return Ok(new { success = true });
        }
        return result.Error?.Message.Contains("unauthorized") == true
            ? Unauthorized(new { error = result.Error.Message })
            : StatusCode(500, new { error = "Failed to unsubscribe" });
    }

    [HttpGet("subscription-status")]
    public async Task<IActionResult> GetSubscriptionStatus([FromQuery] string endpoint)
    {
        var result = await _service.CheckSubscriptionStatusAsync(endpoint);
        if (result.IsSuccess)
        {
            return Ok(new { exists = result.Data });
        }
        return result.Error?.Message.Contains("unauthorized") == true
            ? Unauthorized(new { error = result.Error.Message })
            : StatusCode(500, new { error = "Failed to check subscription status" });
    }
}

public class UnsubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
}
