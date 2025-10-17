using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace GE.BandSite.Database.Organization;

[Table(nameof(ContactSubmission), Schema = Schemas.Organization)]
public class ContactSubmission : Entity, IEntityTypeConfiguration<ContactSubmission>
{
    [Required]
    [StringLength(200)]
    public string OrganizerName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [EmailAddress]
    [PersonalData]
    public string OrganizerEmail { get; set; } = string.Empty;

    [StringLength(40)]
    [PersonalData]
    public string? OrganizerPhone { get; set; }

    [Required]
    [StringLength(100)]
    public string EventType { get; set; } = string.Empty;

    public LocalDate? EventDate { get; set; }

    [StringLength(100)]
    public string? EventTimezone { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    [Required]
    [StringLength(100)]
    public string PreferredBandSize { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string BudgetRange { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Message { get; set; }

    [Required]
    public Instant CreatedAt { get; set; }

    public void Configure(EntityTypeBuilder<ContactSubmission> builder)
    {
        builder
            .HasIndex(x => x.CreatedAt);
    }
}
