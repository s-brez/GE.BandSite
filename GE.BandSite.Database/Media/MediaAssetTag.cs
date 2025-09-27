using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GE.BandSite.Database.Media;

[Table(nameof(MediaAssetTag), Schema = Schemas.Media)]
public class MediaAssetTag : IEntityTypeConfiguration<MediaAssetTag>
{
    public Guid MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;

    public Guid MediaTagId { get; set; }
    public MediaTag MediaTag { get; set; } = null!;

    public void Configure(EntityTypeBuilder<MediaAssetTag> builder)
    {
        builder.HasKey(x => new { x.MediaAssetId, x.MediaTagId });

        builder
            .HasOne(x => x.MediaAsset)
            .WithMany(x => x.MediaAssetTags)
            .HasForeignKey(x => x.MediaAssetId);

        builder
            .HasOne(x => x.MediaTag)
            .WithMany(x => x.MediaAssetTags)
            .HasForeignKey(x => x.MediaTagId);
    }
}
