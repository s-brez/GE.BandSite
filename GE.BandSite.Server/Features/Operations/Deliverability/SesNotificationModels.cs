using System.Collections.Generic;
using Newtonsoft.Json;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

[JsonObject(MemberSerialization.OptIn)]
public sealed class SesNotificationMessage
{
    [JsonProperty("notificationType")]
    public string? NotificationType { get; set; }

    [JsonProperty("mail")]
    public SesMailObject? Mail { get; set; }

    [JsonProperty("bounce")]
    public SesBounceObject? Bounce { get; set; }

    [JsonProperty("complaint")]
    public SesComplaintObject? Complaint { get; set; }

    [JsonProperty("delivery")]
    public SesDeliveryObject? Delivery { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class SesMailObject
{
    [JsonProperty("timestamp")]
    public string? Timestamp { get; set; }

    [JsonProperty("source")]
    public string? Source { get; set; }

    [JsonProperty("sourceArn")]
    public string? SourceArn { get; set; }

    [JsonProperty("messageId")]
    public string? MessageId { get; set; }

    [JsonProperty("destination")]
    public List<string>? Destination { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class SesBounceObject
{
    [JsonProperty("bounceType")]
    public string? BounceType { get; set; }

    [JsonProperty("bounceSubType")]
    public string? BounceSubType { get; set; }

    [JsonProperty("feedbackId")]
    public string? FeedbackId { get; set; }

    [JsonProperty("timestamp")]
    public string? Timestamp { get; set; }

    [JsonProperty("remoteMtaIp")]
    public string? RemoteMtaIp { get; set; }

    [JsonProperty("bouncedRecipients")]
    public List<SesBouncedRecipient>? BouncedRecipients { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class SesComplaintObject
{
    [JsonProperty("complaintFeedbackType")]
    public string? ComplaintFeedbackType { get; set; }

    [JsonProperty("complaintSubType")]
    public string? ComplaintSubType { get; set; }

    [JsonProperty("feedbackId")]
    public string? FeedbackId { get; set; }

    [JsonProperty("timestamp")]
    public string? Timestamp { get; set; }

    [JsonProperty("complainedRecipients")]
    public List<SesComplainedRecipient>? ComplainedRecipients { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class SesDeliveryObject
{
    [JsonProperty("timestamp")]
    public string? Timestamp { get; set; }

    [JsonProperty("processingTimeMillis")]
    public long? ProcessingTimeMillis { get; set; }

    [JsonProperty("recipients")]
    public List<string>? Recipients { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class SesBouncedRecipient
{
    [JsonProperty("emailAddress")]
    public string? EmailAddress { get; set; }

    [JsonProperty("action")]
    public string? Action { get; set; }

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("diagnosticCode")]
    public string? DiagnosticCode { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class SesComplainedRecipient
{
    [JsonProperty("emailAddress")]
    public string? EmailAddress { get; set; }
}
