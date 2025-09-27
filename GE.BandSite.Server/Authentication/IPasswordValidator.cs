namespace GE.BandSite.Server.Authentication;

/// <summary>
/// Provides functionality to validate and generate passwords
/// </summary>
public interface IPasswordValidator
{
    /// <summary>
    /// The minimum allowable password length.
    /// </summary>
    int MinLength { get; }

    /// <summary>
    /// The maximum allowable password length.
    /// </summary>
    int MaxLength { get; }

    /// <summary>
    /// A descriptive text of password requirements.
    /// </summary>
    string PasswordRequirementsText { get; }

    /// <summary>
    /// Generates a random password that meets (as implemented) password criteria
    /// </summary>
    /// <param name="length">The desired length of the password. Defaults to the maximum length if not provided</param>
    /// <returns>A randomly generated password</returns>
    string GenerateRandomPassword(int length = 64);

    /// <summary>
    /// Validates a plaintext password
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>True if the password meets all criteria, otherwise false</returns>
    bool Validate(string? password);

    /// <summary>
    /// Validates a password according to established criteria and returns feedback on failures
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>A tuple containing a boolean indicating validity and a list of failed requirements</returns>
    (bool IsValid, List<string> FailedRequirements) ValidateWithFeedback(string? password);

}
