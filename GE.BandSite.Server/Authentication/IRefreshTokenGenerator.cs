using GE.BandSite.Database.Authentication;

namespace GE.BandSite.Server.Authentication;

public interface IRefreshTokenGenerator
{
    RefreshToken Generate(Guid userId, string ipAddress, string deviceInfo);
}