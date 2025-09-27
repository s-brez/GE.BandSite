using GE.BandSite.Database;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace GE.BandSite.Server.Authentication;

public class RSASHA512JWTGenerator : ISecurityTokenGenerator
{
    private SigningCredentials Credentials { get; set; }

    public RSASHA512JWTGenerator(RSAParameters rsaParameters)
    {
        var rsa = RSA.Create(rsaParameters);
        var key = new RsaSecurityKey(rsa);

        Credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha512);
    }

    public SecurityToken Generate()
    {
        return Generate(new SecurityTokenDescriptor());
    }

    public SecurityToken Generate(SecurityTokenDescriptor securityTokenDescriptor)
    {
        securityTokenDescriptor.SigningCredentials = Credentials;

        var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();

        return (JwtSecurityToken)jwtSecurityTokenHandler.CreateToken(securityTokenDescriptor);
    }

    public string GenerateJwt(User user, HostString host)
    {
        // Use NodaTime for time source; convert to DateTime for JWT API
        var nowInstant = NodaTime.SystemClock.Instance.GetCurrentInstant();
        DateTime utcNow = nowInstant.ToDateTimeUtc();

        var claims = new List<Claim>
        {
            new (ClaimTypes.Email, user.Email),
            new (ClaimTypes.FirstName, user.FirstName),
            new (ClaimTypes.LastName, user.LastName),
            new (ClaimTypes.UserId, user.Id.ToString()),
            new (JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims);

        SecurityToken securityToken = Generate(new SecurityTokenDescriptor
        {
            Issuer = host.Host,
            Audience = host.Host,
            IssuedAt = utcNow,
            NotBefore = utcNow,
            Expires = utcNow.AddMinutes(AuthenticationConfiguration.AccessTokenExpirationMinutes),
            Subject = claimsIdentity,
        });

        return new JwtSecurityTokenHandler().WriteToken(securityToken);
    }
}
