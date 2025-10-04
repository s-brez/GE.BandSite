using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace GE.BandSite.Database.Authentication;

/// <summary>
/// Represents a password reset link issued to an administrator.
/// </summary>
[Table(nameof(PasswordResetRequest), Schema = Schemas.Authentication)]
public class PasswordResetRequest : Entity, IEntityTypeConfiguration<PasswordResetRequest>
{
    [Required]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// SHA-256 hash of the reset token bytes (hex encoded).
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = null!;

    public Instant CreatedAt { get; set; }

    public Instant ExpiresAt { get; set; }

    public Instant? ConsumedAt { get; set; }

    public Instant? CancelledAt { get; set; }

    void IEntityTypeConfiguration<PasswordResetRequest>.Configure(EntityTypeBuilder<PasswordResetRequest> builder)
    {
        builder
            .HasIndex(x => x.TokenHash)
            .IsUnique();

        builder
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
