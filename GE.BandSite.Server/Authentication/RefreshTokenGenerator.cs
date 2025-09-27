using GE.BandSite.Database.Authentication;
using NodaTime;
using System.Security.Cryptography;

namespace GE.BandSite.Server.Authentication;

public class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public RefreshToken Generate(Guid userId, string ipAddress, string deviceInfo)
    {
        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        string token = Convert.ToBase64String(randomBytes);

        var now = SystemClock.Instance.GetCurrentInstant();
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = token,
            CreatedAt = now,
            ExpiresAt = now + Duration.FromDays(AuthenticationConfiguration.RefreshTokenExpirationDays),
            IPAddress = ipAddress,
            DeviceInfo = deviceInfo
        };

        return refreshToken;
    }
}
