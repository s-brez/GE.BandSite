using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace GE.BandSite.Database.Organization;

[Table(nameof(BandMemberProfile), Schema = Schemas.Organization)]
public class BandMemberProfile : Entity, IEntityTypeConfiguration<BandMemberProfile>
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Role { get; set; } = string.Empty;

    [StringLength(600)]
    public string? Spotlight { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    [Required]
    public Instant CreatedAt { get; set; }

    public void Configure(EntityTypeBuilder<BandMemberProfile> builder)
    {
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
        builder.HasIndex(x => x.IsActive);
    }
}
