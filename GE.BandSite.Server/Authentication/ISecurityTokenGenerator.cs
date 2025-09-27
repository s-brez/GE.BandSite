using GE.BandSite.Database;
using Microsoft.IdentityModel.Tokens;

namespace GE.BandSite.Server.Authentication;

public interface ISecurityTokenGenerator
{
    SecurityToken Generate(SecurityTokenDescriptor securityTokenDescriptor);

    string GenerateJwt(User user, HostString host);
}
