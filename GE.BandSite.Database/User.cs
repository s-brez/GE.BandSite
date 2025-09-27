using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using NodaTime;

namespace GE.BandSite.Database;

/// <summary>
/// Represents a user of the system.
/// </summary>
[Table(nameof(User), Schema = Schemas.Organization)]
public partial class User : Entity, IEntityTypeConfiguration<User>
{
    [Required]
    [PersonalData]
    public string FirstName { get; set; } = null!;

    [Required]
    [PersonalData]
    public string LastName { get; set; } = null!;

    [Required]
    [PersonalData]
    public string Email { get; set; } = null!;

    [PersonalData]
    public string ExternalPositionDescription { get; set; } = null!;

    public Instant? EmailConfirmedDateTime { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public Instant? LockedDateTime { get; set; }
    public Instant? PasswordChangeDateTime { get; set; }
    public byte[] PasswordHash { get; set; } = null!;
    [PersonalData]
    public string? Phone { get; set; }
    public Instant? PhoneConfirmedDateTime { get; set; }
    public List<byte[]> PreviousPasswordHashes { get; set; } = new();
    public byte[] Salt { get; set; } = null!;

    void IEntityTypeConfiguration<User>.Configure(EntityTypeBuilder<User> builder)
    {
        builder
            .HasIndex(x => x.Email)
            .IsUnique();
    }
}
