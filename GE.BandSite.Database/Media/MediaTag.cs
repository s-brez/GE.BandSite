using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GE.BandSite.Database.Media;

[Table(nameof(MediaTag), Schema = Schemas.Media)]
public class MediaTag : Entity, IEntityTypeConfiguration<MediaTag>
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<MediaAssetTag> MediaAssetTags { get; set; } = new List<MediaAssetTag>();

    public void Configure(EntityTypeBuilder<MediaTag> builder)
    {
        builder
            .HasIndex(x => x.Name)
            .IsUnique();
    }
}
