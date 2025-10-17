using System;
using NodaTime;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public sealed record EmailSuppressionRequest(
    string Email,
    string Reason,
    string? ReasonDetail,
    Guid? FeedbackEventId,
    Instant SuppressedAt);
