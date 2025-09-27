using GE.BandSite.Database;
using GE.BandSite.Database.Organization;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace GE.BandSite.Server.Features.Organization;

public static class OrganizationSeedData
{
    public static async Task EnsureSeedDataAsync(GeBandSiteDbContext dbContext, IClock clock, CancellationToken cancellationToken = default)
    {
        var timestamp = clock.GetCurrentInstant();

        if (!await dbContext.Testimonials.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            await dbContext.Testimonials.AddRangeAsync(new[]
            {
                new Testimonial
                {
                    Id = Guid.NewGuid(),
                    Quote = "They kept our 500-person corporate gala buzzing until we literally turned the lights on. The custom walk-on stings for award winners were a huge hit.",
                    Name = "Amelia Grant",
                    Role = "Global Events Director, LumenTech",
                    DisplayOrder = 0,
                    IsPublished = true,
                    CreatedAt = timestamp
                },
                new Testimonial
                {
                    Id = Guid.NewGuid(),
                    Quote = "Gilbert crafted a first-dance medley that moved us to tears and then immediately flipped into a packed dance floor. The night was pure magic.",
                    Name = "Natalia & Aaron",
                    Role = "Lake Como Destination Wedding",
                    DisplayOrder = 1,
                    IsPublished = true,
                    CreatedAt = timestamp
                },
                new Testimonial
                {
                    Id = Guid.NewGuid(),
                    Quote = "We needed a band that could arrive, understand our AV quickly, and deliver with zero drama. They exceeded every expectation and left guests chanting for one more song.",
                    Name = "Darius Chen",
                    Role = "Private 50th Celebration, Singapore",
                    DisplayOrder = 2,
                    IsPublished = true,
                    CreatedAt = timestamp
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        if (!await dbContext.EventListings.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            await dbContext.EventListings.AddRangeAsync(new[]
            {
                new EventListing
                {
                    Id = Guid.NewGuid(),
                    Title = "Skyline Jazz Weekend",
                    EventDate = new LocalDate(timestamp.InUtc().Year, 7, 12),
                    Location = "Chicago, IL",
                    Description = "Two-night residency at the Skyline Pavilion featuring guest vocalists and late-night jam sessions.",
                    IsPublished = true,
                    DisplayOrder = 0,
                    CreatedAt = timestamp
                },
                new EventListing
                {
                    Id = Guid.NewGuid(),
                    Title = "Azure Roof Club",
                    EventDate = new LocalDate(timestamp.InUtc().Year, 8, 23),
                    Location = "Singapore",
                    Description = "Sunset rooftop show with special string arrangements for a limited guest list.",
                    IsPublished = true,
                    DisplayOrder = 1,
                    CreatedAt = timestamp
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        if (!await dbContext.BandMembers.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            await dbContext.BandMembers.AddRangeAsync(new[]
            {
                new BandMemberProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Gilbert Ernest",
                    Role = "Bandleader & Piano",
                    Spotlight = "Signature stride piano and charismatic emceeing keep the room energized.",
                    DisplayOrder = 0,
                    IsActive = true,
                    CreatedAt = timestamp
                },
                new BandMemberProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Sienna Flores",
                    Role = "Lead Vocals",
                    Spotlight = "Velvet crooner by day, high-octane belter by night with bilingual standards.",
                    DisplayOrder = 1,
                    IsActive = true,
                    CreatedAt = timestamp
                },
                new BandMemberProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Marcus Reed",
                    Role = "Trumpet",
                    Spotlight = "Brings brassy flair with bebop runs and soaring solo work.",
                    DisplayOrder = 2,
                    IsActive = true,
                    CreatedAt = timestamp
                },
                new BandMemberProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Alana Cho",
                    Role = "Saxophone & Woodwinds",
                    Spotlight = "Doubles on clarinet for vintage textures and modern solo fireworks.",
                    DisplayOrder = 3,
                    IsActive = true,
                    CreatedAt = timestamp
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
