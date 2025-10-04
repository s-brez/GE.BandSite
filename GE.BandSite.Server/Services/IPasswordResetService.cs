namespace GE.BandSite.Server.Services;

public interface IPasswordResetService
{
    Task<PasswordResetRequestResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
}

public sealed class PasswordResetRequestResult
{
    /// <summary>
    /// Indicates the request was accepted (always true for callers to avoid leaking account status).
    /// </summary>
    public bool Accepted { get; init; }

    /// <summary>
    /// Whether an email dispatch was attempted (primarily for diagnostics/testing).
    /// </summary>
    public bool EmailDispatched { get; init; }
}

public sealed class PasswordResetResult
{
    public bool Success { get; init; }

    public PasswordResetError? Error { get; init; }

    public List<string> PasswordValidationErrors { get; init; } = new();
}

public enum PasswordResetError
{
    InvalidToken,
    Expired,
    AlreadyUsed,
    PasswordValidationFailed,
    PasswordMatchesCurrent
}
