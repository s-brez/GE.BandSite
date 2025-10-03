using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace GE.BandSite.Database.Organization;

[Table(nameof(ContactNotificationRecipient), Schema = Schemas.Organization)]
public class ContactNotificationRecipient : Entity, IEntityTypeConfiguration<ContactNotificationRecipient>
{
    [Required]
    [StringLength(200)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public Instant CreatedAt { get; set; }

    public Instant? UpdatedAt { get; set; }

    public void Configure(EntityTypeBuilder<ContactNotificationRecipient> builder)
    {
        builder
            .HasIndex(x => x.Email)
            .IsUnique();
    }
}
