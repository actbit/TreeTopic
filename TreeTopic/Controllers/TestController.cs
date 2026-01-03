using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;

    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { message = "Test controller is working", timestamp = DateTime.UtcNow });
    }

    [HttpGet("maskeduuid/{id}")]
    public IActionResult TestMaskedUUID([FromRoute] MaskedGuid id)
    {
        _logger.LogInformation("Received masked UUID, decoded to GUID: {Id}", id);
        return Ok(new
        {
            decodedId = id,
            maskedId = id,  // JSON converter will automatically mask this
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("maskeduuid/echo")]
    public IActionResult EchoMaskedUUID([FromBody] TestRequest request)
    {
        _logger.LogInformation("Received GUID in request body: {Id}", request.Id);
        return Ok(new
        {
            receivedRawGuid = request.Id,
            returnedMaskedGuid = request.Id,  // JSON converter will automatically mask this
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("maskeduuid/encode")]
    public IActionResult EncodeMaskedUUID([FromBody] EncodeRequest request)
    {
        var decodedGuid = (Guid)request.RawGuid;
        _logger.LogInformation("Received raw GUID: {Id}", decodedGuid);
        return Ok(new EncodeResponse
        {
            RawGuid = request.RawGuid,
            MaskedGuid = request.RawGuid,  // JSON converter will automatically mask this
            Timestamp = DateTime.UtcNow
        });
    }

    public class TestRequest
    {
        public MaskedGuid Id { get; set; }
    }

    public class EncodeRequest
    {
        public MaskedGuid RawGuid { get; set; }
    }

    public class EncodeResponse
    {
        public MaskedGuid RawGuid { get; set; }

        public MaskedGuid MaskedGuid { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
