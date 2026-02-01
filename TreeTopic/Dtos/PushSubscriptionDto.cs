using System.Text.Json.Serialization;

namespace TreeTopic.Dtos;

public class PushSubscriptionDto
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("keys")]
    public PushSubscriptionKeys Keys { get; set; } = new();
}

public class PushSubscriptionKeys
{
    [JsonPropertyName("p256dh")]
    public string P256dh { get; set; } = string.Empty;

    [JsonPropertyName("auth")]
    public string Auth { get; set; } = string.Empty;
}

public class PushNotificationRequest
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Icon { get; set; }
    public string? Badge { get; set; }
    public string? Data { get; set; }
}
