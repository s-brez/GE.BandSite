using System.Collections.Generic;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public sealed class SesWebhookOptions
{
    public bool Enabled { get; set; } = true;

    public bool AutoConfirmSubscriptions { get; set; } = true;

    public bool RequireTopicValidation { get; set; } = true;

    public string? BounceTopicArn { get; set; }

    public string? ComplaintTopicArn { get; set; }

    public string? DeliveryTopicArn { get; set; }

    public List<string> AllowedTopicArns { get; set; } = new();
}
