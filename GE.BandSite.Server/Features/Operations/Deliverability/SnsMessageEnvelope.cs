using Newtonsoft.Json;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

[JsonObject(MemberSerialization.OptIn)]
public sealed class SnsMessageEnvelope
{
    [JsonProperty("Type")]
    public string? Type { get; set; }

    [JsonProperty("MessageId")]
    public string? MessageId { get; set; }

    [JsonProperty("TopicArn")]
    public string? TopicArn { get; set; }

    [JsonProperty("Subject")]
    public string? Subject { get; set; }

    [JsonProperty("Message")]
    public string? Message { get; set; }

    [JsonProperty("Timestamp")]
    public string? Timestamp { get; set; }

    [JsonProperty("SignatureVersion")]
    public string? SignatureVersion { get; set; }

    [JsonProperty("Signature")]
    public string? Signature { get; set; }

    [JsonProperty("SigningCertURL")]
    public string? SigningCertUrl { get; set; }

    [JsonProperty("UnsubscribeURL")]
    public string? UnsubscribeUrl { get; set; }

    [JsonProperty("SubscribeURL")]
    public string? SubscribeUrl { get; set; }

    [JsonProperty("Token")]
    public string? Token { get; set; }
}
