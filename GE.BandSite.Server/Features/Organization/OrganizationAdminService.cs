using GE.BandSite.Database;
using GE.BandSite.Database.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace GE.BandSite.Server.Features.Organization;

public interface IOrganizationAdminService
{
    Task<IReadOnlyList<BandMemberProfile>> GetBandAsync(CancellationToken cancellationToken = default);
    Task AddOrUpdateBandMemberAsync(BandMemberProfile member, CancellationToken cancellationToken = default);
    Task DeleteBandMemberAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Testimonial>> GetTestimonialsAsync(CancellationToken cancellationToken = default);
    Task AddOrUpdateTestimonialAsync(Testimonial testimonial, CancellationToken cancellationToken = default);
    Task DeleteTestimonialAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventListing>> GetEventsAsync(CancellationToken cancellationToken = default);
    Task AddOrUpdateEventAsync(EventListing listing, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class OrganizationAdminService : IOrganizationAdminService
{
    private readonly IGeBandSiteDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILogger<OrganizationAdminService> _logger;

    public OrganizationAdminService(IGeBandSiteDbContext dbContext, IClock clock, ILogger<OrganizationAdminService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _logger = logger;
    }

    public Task<IReadOnlyList<BandMemberProfile>> GetBandAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.BandMembers
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<BandMemberProfile>)t.Result, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public async Task AddOrUpdateBandMemberAsync(BandMemberProfile member, CancellationToken cancellationToken = default)
    {
        if (member.Id == Guid.Empty)
        {
            member.Id = Guid.NewGuid();
            member.CreatedAt = _clock.GetCurrentInstant();
            await _dbContext.BandMembers.AddAsync(member, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var existing = await _dbContext.BandMembers.FirstOrDefaultAsync(x => x.Id == member.Id, cancellationToken).ConfigureAwait(false);
            if (existing == null)
            {
                throw new InvalidOperationException("Band member not found.");
            }

            existing.Name = member.Name;
            existing.Role = member.Role;
            existing.Spotlight = member.Spotlight;
            existing.DisplayOrder = member.DisplayOrder;
            existing.IsActive = member.IsActive;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteBandMemberAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.BandMembers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (existing == null)
        {
            return;
        }

        _dbContext.BandMembers.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Testimonial>> GetTestimonialsAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Testimonials
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Testimonial>)t.Result, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public async Task AddOrUpdateTestimonialAsync(Testimonial testimonial, CancellationToken cancellationToken = default)
    {
        if (testimonial.Id == Guid.Empty)
        {
            testimonial.Id = Guid.NewGuid();
            testimonial.CreatedAt = _clock.GetCurrentInstant();
            await _dbContext.Testimonials.AddAsync(testimonial, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var existing = await _dbContext.Testimonials.FirstOrDefaultAsync(x => x.Id == testimonial.Id, cancellationToken).ConfigureAwait(false);
            if (existing == null)
            {
                throw new InvalidOperationException("Testimonial not found.");
            }

            existing.Quote = testimonial.Quote;
            existing.Name = testimonial.Name;
            existing.Role = testimonial.Role;
            existing.DisplayOrder = testimonial.DisplayOrder;
            existing.IsPublished = testimonial.IsPublished;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteTestimonialAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Testimonials.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (existing == null)
        {
            return;
        }

        _dbContext.Testimonials.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<EventListing>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.EventListings
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.EventDate)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<EventListing>)t.Result, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public async Task AddOrUpdateEventAsync(EventListing listing, CancellationToken cancellationToken = default)
    {
        if (listing.Id == Guid.Empty)
        {
            listing.Id = Guid.NewGuid();
            listing.CreatedAt = _clock.GetCurrentInstant();
            await _dbContext.EventListings.AddAsync(listing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var existing = await _dbContext.EventListings.FirstOrDefaultAsync(x => x.Id == listing.Id, cancellationToken).ConfigureAwait(false);
            if (existing == null)
            {
                throw new InvalidOperationException("Event not found.");
            }

            existing.Title = listing.Title;
            existing.EventDate = listing.EventDate;
            existing.Location = listing.Location;
            existing.Description = listing.Description;
            existing.DisplayOrder = listing.DisplayOrder;
            existing.IsPublished = listing.IsPublished;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.EventListings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (existing == null)
        {
            return;
        }

        _dbContext.EventListings.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
