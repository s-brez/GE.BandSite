using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace GE.BandSite.Database.Media;

[Table(nameof(MediaAsset), Schema = Schemas.Media)]
public class MediaAsset : Entity, IEntityTypeConfiguration<MediaAsset>
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(400)]
    public string? Description { get; set; }

    [Required]
    [StringLength(300)]
    public string StoragePath { get; set; } = string.Empty;

    [StringLength(300)]
    public string? PosterPath { get; set; }

    [Required]
    public MediaAssetType AssetType { get; set; }

    [Required]
    public MediaProcessingState ProcessingState { get; set; } = MediaProcessingState.Pending;

    public bool IsFeatured { get; set; }

    public bool ShowOnHome { get; set; }

    public bool IsPublished { get; set; }

    [StringLength(500)]
    public string? SourcePath { get; set; }

    [StringLength(500)]
    public string? PlaybackPath { get; set; }

    public int DisplayOrder { get; set; }

    [Required]
    public Instant CreatedAt { get; set; }

    public int? DurationSeconds { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    [StringLength(400)]
    public string? ProcessingError { get; set; }

    public Instant? LastProcessedAt { get; set; }

    public ICollection<MediaAssetTag> MediaAssetTags { get; set; } = new List<MediaAssetTag>();

    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder
            .Property(x => x.AssetType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder
            .Property(x => x.ProcessingState)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(MediaProcessingState.Pending);

        builder
            .HasIndex(x => x.IsFeatured);

        builder
            .HasIndex(x => new { x.AssetType, x.IsPublished, x.ShowOnHome });

        builder
            .HasIndex(x => x.ProcessingState);

        builder
            .Property(x => x.DisplayOrder)
            .HasDefaultValue(0);
    }
}
