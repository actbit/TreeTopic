using System.Text.Json.Serialization;

namespace TreeTopic.Models.OpenIdConnect
{
    /// <summary>
    /// OpenID Connect ディスカバリーメタデータ
    /// .well-known/openid-configuration から取得
    /// </summary>
    public class OpenIdConnectMetadata
    {
        [JsonPropertyName("issuer")]
        public string? Issuer { get; set; }

        [JsonPropertyName("authorization_endpoint")]
        public string? AuthorizationEndpoint { get; set; }

        [JsonPropertyName("token_endpoint")]
        public string? TokenEndpoint { get; set; }

        [JsonPropertyName("jwks_uri")]
        public string? JwksUri { get; set; }

        [JsonPropertyName("end_session_endpoint")]
        public string? EndSessionEndpoint { get; set; }
    }
}
