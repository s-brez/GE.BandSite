using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GE.BandSite.Database.Organization;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public interface IEmailSuppressionService
{
    Task<bool> IsSuppressedAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, EmailSuppression>> GetSuppressionsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default);

    Task ApplySuppressionAsync(EmailSuppressionRequest request, CancellationToken cancellationToken = default);
}
