using GE.BandSite.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GE.BandSite.Server.Authentication;

public class JWTTokenValidationMiddleware
{
    private RequestDelegate Next { get; set; }
    private IRefreshTokenGenerator RefreshTokenGenerator { get; set; }
    private ISecurityTokenGenerator SecurityTokenGenerator { get; set; }
    private ISecurityTokenValidator SecurityTokenValidator { get; set; }


    public JWTTokenValidationMiddleware(
        RequestDelegate next,
        IRefreshTokenGenerator refreshTokenGenerator,
        ISecurityTokenGenerator securityTokenGenerator,
        ISecurityTokenValidator securityTokenValidator)
    {
        Next = next;
        RefreshTokenGenerator = refreshTokenGenerator;
        SecurityTokenGenerator = securityTokenGenerator;
        SecurityTokenValidator = securityTokenValidator;
    }

    public async Task InvokeAsync(HttpContext httpContext, GeBandSiteDbContext dbContext)
    {
        var endpoint = httpContext.GetEndpoint();
        var endpointAllowsAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null;
        var authorizeMetadata = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>();
        var endpointRequiresAuthorization = authorizeMetadata != null && authorizeMetadata.Count > 0;
        var shouldEnforceAuthorization = endpointRequiresAuthorization && !endpointAllowsAnonymous;

        // 1) Validate the current Access Token if present
        string? accessToken = httpContext.Request.Cookies[AuthenticationConfiguration.AccessTokenKey];
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            var securityToken = SecurityTokenValidator.Validate(accessToken);
            if (securityToken is JwtSecurityToken jwtSecurityToken)
            {
                var identity = new ClaimsIdentity(jwtSecurityToken.Claims, "Custom");
                httpContext.User = new ClaimsPrincipal(identity);
            }
        }

        // 2) If user is still not authenticated, attempt a refresh IF we have a refresh token cookie
        if (shouldEnforceAuthorization && httpContext.User?.Identity?.IsAuthenticated != true)
        {
            var refreshTokenCookie = httpContext.Request.Cookies[AuthenticationConfiguration.RefreshTokenKey];
            if (!string.IsNullOrEmpty(refreshTokenCookie))
            {
                var (refreshed, principal) = await TryRefreshAsync(refreshTokenCookie, httpContext, dbContext);
                if (refreshed && principal != null)
                {
                    httpContext.User = principal;
                }
            }
        }

        // 3) If we STILL don’t have an authenticated user, proceed with either 401 or 302
        if (shouldEnforceAuthorization && httpContext.User?.Identity?.IsAuthenticated != true)
        {
            var path = httpContext.Request.Path.Value ?? "";

            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                httpContext.Response.Cookies.Delete(AuthenticationConfiguration.AccessTokenKey);
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            else
            {
                httpContext.Response.Cookies.Delete(AuthenticationConfiguration.AccessTokenKey);
                httpContext.Response.StatusCode = StatusCodes.Status302Found;
                httpContext.Response.Headers.Location = $"{AuthenticationConfiguration.LoginPath}";
                return;
            }
        }

        await Next(httpContext);
    }

    public async Task<(bool Refreshed, ClaimsPrincipal? Principal)> TryRefreshAsync(
        string refreshToken,
        HttpContext httpContext,
        GeBandSiteDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var tokenEntity = await dbContext.RefreshTokens.Include(x => x.User).FirstOrDefaultAsync(x => x.Token == refreshToken, cancellationToken).ConfigureAwait(false);
        if (tokenEntity == null || tokenEntity.IsExpired || tokenEntity.RevokedAt != null)
        {
            return (false, null);
        }

        var (user, errorMessage) = await dbContext.GetUserByEmailAsync(tokenEntity.User.Email, cancellationToken).ConfigureAwait(false);
        if (user == null || errorMessage != null)
        {
            return (false, null);
        }

        tokenEntity.RevokedAt = NodaTime.SystemClock.Instance.GetCurrentInstant();

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";

        var newRefresh = RefreshTokenGenerator.Generate(tokenEntity.UserId, ipAddress, userAgent);
        tokenEntity.ReplacedByToken = newRefresh.Token;
        dbContext.RefreshTokens.Add(newRefresh);

        string newAccessToken = SecurityTokenGenerator.GenerateJwt(user, httpContext.Request.Host);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        httpContext.Response.Cookies.Append(AuthenticationConfiguration.AccessTokenKey, newAccessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromMinutes(AuthenticationConfiguration.AccessTokenExpirationMinutes)
            }
        );

        httpContext.Response.Cookies.Append(AuthenticationConfiguration.RefreshTokenKey, newRefresh.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromDays(AuthenticationConfiguration.RefreshTokenExpirationDays)
            }
        );

        var securityToken = SecurityTokenValidator.Validate(newAccessToken);
        if (securityToken is not JwtSecurityToken jwtSecurityToken)
        {
            return (false, null);
        }

        var identity = new ClaimsIdentity(jwtSecurityToken.Claims, "Custom");
        var principal = new ClaimsPrincipal(identity);

        return (true, principal);
    }
}
