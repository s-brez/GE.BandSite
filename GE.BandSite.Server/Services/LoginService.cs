using GE.BandSite.Database;
using GE.BandSite.Server.Authentication;

namespace GE.BandSite.Server.Services;

/// <summary>
/// Concrete implementation of <see cref="ILoginService"/> that reuses existing hashing/validation/token services and persists login attempts.
/// </summary>
public class LoginService : ILoginService
{
    private readonly GeBandSiteDbContext _db;
    private readonly ILogger<LoginService> _logger;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly ISecurityTokenGenerator _securityTokenGenerator;

    public LoginService(
        GeBandSiteDbContext db,
        ILogger<LoginService> logger,
        IPasswordHasher passwordHasher,
        IRefreshTokenGenerator refreshTokenGenerator,
        ISecurityTokenGenerator securityTokenGenerator)
    {
        _db = db;
        _logger = logger;
        _passwordHasher = passwordHasher;
        _refreshTokenGenerator = refreshTokenGenerator;
        _securityTokenGenerator = securityTokenGenerator;
    }

    public async Task<LoginServiceResult> AuthenticateAsync(string email, string password, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return new LoginServiceResult { Success = false, ErrorStatus = StatusCodes.Status400BadRequest, ErrorMessage = "Missing required parameters" };
            }

            string ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            string userAgent = httpContext.Request.Headers.TryGetValue("User-Agent", out var uav) ? uav.ToString() : "unknown";

            var (user, errorMessage) = await _db.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
            if (errorMessage != null || user == null)
            {
                return new LoginServiceResult { Success = false, ErrorStatus = StatusCodes.Status401Unauthorized, ErrorMessage = "Invalid email or password." };
            }

            if (!user.IsActive)
            {
                return new LoginServiceResult { Success = false, ErrorStatus = StatusCodes.Status401Unauthorized, ErrorMessage = "User's account is inactive." };
            }

            var actualPasswordHash = _passwordHasher.Hash(password, user.Salt);
            var expectedPasswordHash = user.PasswordHash;
            if (!actualPasswordHash.SequenceEqual(expectedPasswordHash))
            {
                return new LoginServiceResult { Success = false, ErrorStatus = StatusCodes.Status401Unauthorized, ErrorMessage = "Invalid email or password." };
            }

            // Success: issue tokens + set cookies
            string jwtSecurityToken = _securityTokenGenerator.GenerateJwt(user, httpContext.Request.Host);
            var refreshToken = _refreshTokenGenerator.Generate(user.Id, ipAddress, userAgent);

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            httpContext.Response.Cookies.Append(AuthenticationConfiguration.AccessTokenKey, jwtSecurityToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromMinutes(AuthenticationConfiguration.AccessTokenExpirationMinutes)
                });

            httpContext.Response.Cookies.Append(AuthenticationConfiguration.RefreshTokenKey, refreshToken.Token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromDays(AuthenticationConfiguration.RefreshTokenExpirationDays)
                });

            return new LoginServiceResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login attempt");
            return new LoginServiceResult { Success = false, ErrorStatus = StatusCodes.Status500InternalServerError, ErrorMessage = "Error occurred during login" };
        }
    }
}
