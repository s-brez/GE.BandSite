using GE.BandSite.Database.Authentication;
using GE.BandSite.Database.Media;
using GE.BandSite.Database.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GE.BandSite.Database;

public interface IGeBandSiteDbContext
{
    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
    EntityEntry Entry(object entity);
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    int SaveChanges();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    DbSet<User> Users { get; set; }
    DbSet<RefreshToken> RefreshTokens { get; set; }
    DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
    DbSet<ContactSubmission> ContactSubmissions { get; set; }
    DbSet<ContactNotificationRecipient> ContactNotificationRecipients { get; set; }
    DbSet<Testimonial> Testimonials { get; set; }
    DbSet<EventListing> EventListings { get; set; }
    DbSet<BandMemberProfile> BandMembers { get; set; }
    DbSet<MediaAsset> MediaAssets { get; set; }
    DbSet<MediaTag> MediaTags { get; set; }
    DbSet<MediaAssetTag> MediaAssetTags { get; set; }
}

public class GeBandSiteDbContext : DbContext, IGeBandSiteDbContext
{
    public GeBandSiteDbContext() : base() { }
    public GeBandSiteDbContext(DbContextOptions<GeBandSiteDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;

    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; } = null!;

    public DbSet<ContactSubmission> ContactSubmissions { get; set; } = null!;

    public DbSet<ContactNotificationRecipient> ContactNotificationRecipients { get; set; } = null!;

    public DbSet<Testimonial> Testimonials { get; set; } = null!;

    public DbSet<EventListing> EventListings { get; set; } = null!;

    public DbSet<BandMemberProfile> BandMembers { get; set; } = null!;

    public DbSet<MediaAsset> MediaAssets { get; set; } = null!;

    public DbSet<MediaTag> MediaTags { get; set; } = null!;

    public DbSet<MediaAssetTag> MediaAssetTags { get; set; } = null!;


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GeBandSiteDbContext).Assembly);
    }
}
