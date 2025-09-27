using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace GE.BandSite.Server.Authentication;

public class RsaSecurityTokenValidator(RSAParameters rsaParameters) : ISecurityTokenValidator
{
    private SecurityKey Key { get; set; } = new RsaSecurityKey(RSA.Create(rsaParameters));

    public SecurityToken? Validate(string token)
    {
        var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();

        try
        {
            jwtSecurityTokenHandler.ValidateToken(token, new TokenValidationParameters()
            {
                IssuerSigningKey = Key,
                ValidateAudience = false,
                ValidateIssuer = false
            }, out SecurityToken securityToken);

            return securityToken;
        }
        catch
        {
            return null;
        }
    }
}
