namespace GE.BandSite.Server.Services;

/// <summary>
/// Handles end-to-end user login including password verification, JWT + refresh token issuance, and setting auth cookies on the HTTP response.
/// </summary>
public interface ILoginService
{
    /// <summary>
    /// Attempts to authenticate the supplied credentials.
    /// </summary>
    /// <param name="email">User email (username).</param>
    /// <param name="password">User password.</param>
    /// <param name="httpContext">HttpContext for IP/User-Agent and to set cookies on success.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="LoginServiceResult"/> describing the outcome.</returns>
    Task<LoginServiceResult> AuthenticateAsync(string email, string password, HttpContext httpContext, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a login attempt via <see cref="ILoginService"/>.
/// </summary>
public sealed class LoginServiceResult
{
    /// <summary>
    /// True when credentials are valid and cookies were issued.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// A human-readable error message for failed logins.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Suggested HTTP status code for the error case.
    /// </summary>
    public int? ErrorStatus { get; init; }
}

