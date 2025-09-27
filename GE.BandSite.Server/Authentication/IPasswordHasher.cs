namespace GE.BandSite.Server.Authentication;

/// <summary>
/// Defines contract for hashing passwords
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Generates a cryptographically secure random salt
    /// </summary>
    /// <param name="saltLength">Optional: The length of the salt. Defaults to an internal constant if not set.</param>
    /// <returns>Byte array containing salt</returns>
    byte[] GenerateSalt(int saltLength = 0);

    /// <summary>
    /// Computes the hash of the given password and salt
    /// </summary>
    /// <param name="password">The password to hash</param>
    /// <param name="salt">The salt to use in hashing</param>
    /// <returns>Byte array containing the derived key</returns>
    byte[] Hash(string password, byte[] salt);
}
