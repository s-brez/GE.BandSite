using System.Threading;
using System.Threading.Tasks;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public interface ISnsMessageValidator
{
    Task<bool> ValidateAsync(SnsMessageEnvelope envelope, CancellationToken cancellationToken = default);
}
