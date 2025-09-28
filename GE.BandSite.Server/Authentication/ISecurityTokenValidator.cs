using Microsoft.IdentityModel.Tokens;

namespace GE.BandSite.Server.Authentication;

public interface ISecurityTokenValidator
{
    SecurityToken? Validate(string token);
}
