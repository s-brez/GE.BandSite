using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GE.BandSite.Database.Organization;

[Table(nameof(SesFeedbackRecipient), Schema = Schemas.Organization)]
public class SesFeedbackRecipient : Entity, IEntityTypeConfiguration<SesFeedbackRecipient>
{
    [Required]
    public Guid FeedbackEventId { get; set; }

    public SesFeedbackEvent FeedbackEvent { get; set; } = null!;

    [Required]
    [StringLength(320)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(320)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [StringLength(40)]
    public string? BounceType { get; set; }

    [StringLength(40)]
    public string? BounceSubType { get; set; }

    [StringLength(40)]
    public string? BounceAction { get; set; }

    [StringLength(50)]
    public string? BounceStatus { get; set; }

    [StringLength(512)]
    public string? DiagnosticCode { get; set; }

    [StringLength(100)]
    public string? ComplaintFeedbackType { get; set; }

    [StringLength(100)]
    public string? ComplaintSubType { get; set; }

    [StringLength(100)]
    public string? ComplaintType { get; set; }

    [StringLength(1024)]
    public string? Detail { get; set; }

    public int RecipientIndex { get; set; }

    public void Configure(EntityTypeBuilder<SesFeedbackRecipient> builder)
    {
        builder
            .HasIndex(x => x.NormalizedEmail);

        builder
            .Property(x => x.RecipientIndex)
            .HasDefaultValue(0);
    }
}
