using GE.BandSite.Database;
using GE.BandSite.Server.Features.Organization.Models;
using Microsoft.EntityFrameworkCore;

namespace GE.BandSite.Server.Features.Organization;

public interface IOrganizationContentService
{
    Task<IReadOnlyList<BandMemberModel>> GetActiveBandMembersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TestimonialModel>> GetPublishedTestimonialsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventListingModel>> GetPublishedEventsAsync(CancellationToken cancellationToken = default);
}

public sealed class OrganizationContentService : IOrganizationContentService
{
    private readonly IGeBandSiteDbContext _dbContext;

    public OrganizationContentService(IGeBandSiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<BandMemberModel>> GetActiveBandMembersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.BandMembers
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new BandMemberModel(x.Name, x.Role, x.Spotlight ?? string.Empty))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TestimonialModel>> GetPublishedTestimonialsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Testimonials
            .Where(x => x.IsPublished)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new TestimonialModel(x.Quote, x.Name, x.Role))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EventListingModel>> GetPublishedEventsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.EventListings
            .Where(x => x.IsPublished)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.EventDate)
            .Select(x => new EventListingModel(x.Title, x.EventDate, x.Location, x.Description))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
