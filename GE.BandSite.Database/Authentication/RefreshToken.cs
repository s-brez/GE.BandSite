using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GE.BandSite.Database.Authentication;

[Table(nameof(RefreshToken), Schema = Schemas.Authentication)]
public class RefreshToken : Entity, IEntityTypeConfiguration<RefreshToken>
{
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    [Required]
    [StringLength(255)]
    public string Token { get; set; } = null!;

    public Instant CreatedAt { get; set; }

    [Required]
    public Instant ExpiresAt { get; set; }

    public Instant? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    [PersonalData]
    public string? DeviceInfo { get; set; }
    [PersonalData]
    public string? IPAddress { get; set; }

    [NotMapped]
    public bool IsExpired => ExpiresAt <= SystemClock.Instance.GetCurrentInstant();

    [NotMapped]
    public bool IsActive => RevokedAt == null && !IsExpired;

    public void Revoke(string? reason = null, string? replacementToken = null)
    {
        RevokedAt = SystemClock.Instance.GetCurrentInstant();
        ReplacedByToken = replacementToken;
    }

    void IEntityTypeConfiguration<RefreshToken>.Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder
            .HasIndex(x => x.Token)
            .IsUnique();
    }
}
