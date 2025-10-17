using System;
using System.Threading;
using System.Threading.Tasks;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public interface IDeliverabilityReportService
{
    Task<DeliverabilityDashboardModel> GetDashboardAsync(int suppressionCount, int eventCount, CancellationToken cancellationToken = default);

    Task<bool> ReleaseSuppressionAsync(Guid suppressionId, string? releaseNote, CancellationToken cancellationToken = default);
}
