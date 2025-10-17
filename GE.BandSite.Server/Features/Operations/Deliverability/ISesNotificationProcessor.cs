using System;
using System.Threading;
using System.Threading.Tasks;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public interface ISesNotificationProcessor
{
    Task ProcessAsync(SnsMessageEnvelope envelope, SesNotificationMessage notification, string rawMessage, CancellationToken cancellationToken = default);
}
