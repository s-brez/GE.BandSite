using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace GE.BandSite.Database.Organization;

[Table(nameof(EmailSuppression), Schema = Schemas.Organization)]
public class EmailSuppression : Entity, IEntityTypeConfiguration<EmailSuppression>
{
    [Required]
    [StringLength(320)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(320)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(256)]
    public string? ReasonDetail { get; set; }

    public Guid? FeedbackEventId { get; set; }

    public SesFeedbackEvent? FeedbackEvent { get; set; }

    [Required]
    public Instant FirstSuppressedAt { get; set; }

    [Required]
    public Instant LastSuppressedAt { get; set; }

    [Required]
    public int SuppressionCount { get; set; }

    public Instant? ReleasedAt { get; set; }

    [StringLength(256)]
    public string? ReleaseDetail { get; set; }

    public void Configure(EntityTypeBuilder<EmailSuppression> builder)
    {
        builder
            .HasIndex(x => x.NormalizedEmail)
            .IsUnique();

        builder
            .HasIndex(x => x.ReleasedAt);

        builder
            .HasOne(x => x.FeedbackEvent)
            .WithMany()
            .HasForeignKey(x => x.FeedbackEventId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
