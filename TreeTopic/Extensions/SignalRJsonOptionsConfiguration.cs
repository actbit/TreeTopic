using MaskedUUID.AspNetCore.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace TreeTopic.Extensions;

/// <summary>
/// SignalR JSONシリアライゼーションでMaskedGuidコンバータを使用するように設定
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
