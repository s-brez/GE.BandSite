using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace GE.BandSite.Database.Organization;

[Table(nameof(SesFeedbackEvent), Schema = Schemas.Organization)]
public class SesFeedbackEvent : Entity, IEntityTypeConfiguration<SesFeedbackEvent>
{
    [Required]
    [StringLength(32)]
    public string NotificationType { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string SesMessageId { get; set; } = string.Empty;

    [StringLength(200)]
    public string? SesFeedbackId { get; set; }

    [Required]
    public Instant ReceivedAt { get; set; }

    [StringLength(200)]
    public string? SourceEmail { get; set; }

    [StringLength(512)]
    public string? SourceArn { get; set; }

    [StringLength(512)]
    public string? TopicArn { get; set; }

    public string? RawPayload { get; set; }

    public ICollection<SesFeedbackRecipient> Recipients { get; set; } = new List<SesFeedbackRecipient>();

    public void Configure(EntityTypeBuilder<SesFeedbackEvent> builder)
    {
        builder
            .HasIndex(x => x.SesMessageId)
            .IsUnique();

        builder
            .HasIndex(x => new { x.NotificationType, x.ReceivedAt });

        builder
            .HasMany(x => x.Recipients)
            .WithOne(x => x.FeedbackEvent)
            .HasForeignKey(x => x.FeedbackEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
