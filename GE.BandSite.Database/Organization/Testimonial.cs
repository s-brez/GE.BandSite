using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace GE.BandSite.Database.Organization;

[Table(nameof(Testimonial), Schema = Schemas.Organization)]
public class Testimonial : Entity, IEntityTypeConfiguration<Testimonial>
{
    [Required]
    [StringLength(800)]
    public string Quote { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Role { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    [Required]
    public Instant CreatedAt { get; set; }

    public void Configure(EntityTypeBuilder<Testimonial> builder)
    {
        builder.HasIndex(x => x.IsPublished);
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
    }
}
