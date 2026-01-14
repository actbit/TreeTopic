using MaskedUUID.AspNetCore.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace TreeTopic.Extensions;

/// <summary>
/// Configure SignalR JSON serialization to use MaskedGuid converter.
/// </summary>
internal class SignalRJsonOptionsConfiguration : IConfigureOptions<JsonHubProtocolOptions>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SignalRJsonOptionsConfiguration(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Configure(JsonHubProtocolOptions options)
    {
        options.PayloadSerializerOptions.Converters.Add(new MaskedGuidConverter(_httpContextAccessor));
    }
}
