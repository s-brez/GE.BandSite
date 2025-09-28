namespace GE.BandSite.Database.Configuration;

public class SeedUserConfiguration
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Salt { get; set; } = null!;
}
