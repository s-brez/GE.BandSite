using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace GE.BandSite.Database.Organization;

[Table(nameof(EventListing), Schema = Schemas.Organization)]
public class EventListing : Entity, IEntityTypeConfiguration<EventListing>
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public LocalDate? EventDate { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    [StringLength(600)]
    public string? Description { get; set; }

    public bool IsPublished { get; set; }

    [Required]
    public Instant CreatedAt { get; set; }

    public int DisplayOrder { get; set; }

    public void Configure(EntityTypeBuilder<EventListing> builder)
    {
        builder.HasIndex(x => x.IsPublished);
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
    }
}
