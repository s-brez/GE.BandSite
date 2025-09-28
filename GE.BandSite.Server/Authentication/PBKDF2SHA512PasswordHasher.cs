using System.Security.Cryptography;
using System.Text;

namespace GE.BandSite.Server.Authentication;

public class PBKDF2SHA512PasswordHasher : IPasswordHasher
{
    private static readonly HashAlgorithmName HashAlgorithmName = HashAlgorithmName.SHA512;
    private const int Iterations = 400000;
    private static readonly int OutputLength = 64;
    private static readonly int SaltLength = 32;

    public PBKDF2SHA512PasswordHasher() { }

    public byte[] GenerateSalt(int saltLength = 0)
    {
        saltLength = saltLength > 0 ? saltLength : SaltLength;
        var salt = new byte[saltLength];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        return salt;
    }

    public byte[] Hash(string password, byte[] salt)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentNullException(nameof(password));
        }

        if (salt == null || salt.Length == 0)
        {
            throw new ArgumentNullException(nameof(salt));
        }

        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName, OutputLength);
    }
}