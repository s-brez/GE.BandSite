namespace GE.BandSite.Server.Authentication;

public class PasswordValidator : IPasswordValidator
{
    private const int MinPasswordLength = 16;
    private const int MaxPasswordLength = 64;

    public int MinLength => MinPasswordLength;
    public int MaxLength => MaxPasswordLength;

    public string PasswordRequirementsText => $"A valid password requires {MinPasswordLength}-{MaxPasswordLength} characters and must include at least one digit, one UPPERCASE letter, one lowercase letter, and one special symbol. Spaces not permitted.";

    private static class SpecialCharacterGroups
    {
        public const string Punctuation = "!,.?'\"`;:";
        public const string Math = "@#$%^&*()-_=+~";
        public const string Brackets = "[]{}()<>";
        public const string Slashes = "/\\|";
        public const string Currency = "¢£€¥₹₽";
        public const string Additional = "§¶©®™°";
    }

    private static readonly string SpecialCharacters =
        SpecialCharacterGroups.Punctuation +
        SpecialCharacterGroups.Math +
        SpecialCharacterGroups.Brackets +
        SpecialCharacterGroups.Slashes +
        SpecialCharacterGroups.Currency +
        SpecialCharacterGroups.Additional;

    public bool Validate(string? password)
    {
        if (password == null)
        {
            return false;
        }
        bool hasLength = password.Length >= MinPasswordLength && password.Length <= MaxPasswordLength;
        bool hasDigit = password.Any(char.IsDigit);
        bool hasNoWhitespace = !password.Any(char.IsWhiteSpace);
        bool hasUppercase = password.Any(char.IsUpper);
        bool hasLowercase = password.Any(char.IsLower);
        bool hasSpecialChar = password.Any(x => SpecialCharacters.Contains(x));

        return hasLength && hasDigit && hasNoWhitespace && hasUppercase && hasLowercase && hasSpecialChar;
    }

    public (bool IsValid, List<string> FailedRequirements) ValidateWithFeedback(string? password)
    {
        var failedRequirements = new List<string>();
        if (password == null)
        {
            failedRequirements.Add("Password cannot be null");

            return (false, failedRequirements);
        }

        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
        {
            failedRequirements.Add($"Password must be between {MinPasswordLength} and {MaxPasswordLength} characters");
        }

        if (!password.Any(char.IsDigit))
        {
            failedRequirements.Add("Password must contain at least one number");
        }

        if (password.Any(char.IsWhiteSpace))
        {
            failedRequirements.Add("Password must not contain whitespace");
        }

        if (!password.Any(char.IsUpper))
        {
            failedRequirements.Add("Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            failedRequirements.Add("Password must contain at least one lowercase letter");
        }

        if (!password.Any(x => SpecialCharacters.Contains(x)))
        {
            failedRequirements.Add("Password must contain at least one special character");
        }

        return (failedRequirements.Count == 0, failedRequirements);
    }

    public string GenerateRandomPassword(int length = MaxPasswordLength)
    {
        if (length < MinPasswordLength || length > MaxPasswordLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"Password length must be between {MinPasswordLength} and {MaxPasswordLength}");
        }

        var random = new Random();
        var password = new char[length];

        password[0] = (char)random.Next('0', '9' + 1);
        password[1] = (char)random.Next('A', 'Z' + 1);
        password[2] = (char)random.Next('a', 'z' + 1);
        password[3] = SpecialCharacters[random.Next(SpecialCharacters.Length)];

        for (int i = 4; i < length; i++)
        {
            int charType = random.Next(4);

            switch (charType)
            {
                case 0:
                    password[i] = (char)random.Next('0', '9' + 1);
                    break;
                case 1:
                    password[i] = (char)random.Next('A', 'Z' + 1);
                    break;
                case 2:
                    password[i] = (char)random.Next('a', 'z' + 1);
                    break;
                case 3:
                    password[i] = SpecialCharacters[random.Next(SpecialCharacters.Length)];
                    break;
            }
        }

        for (int i = 0; i < length; i++)
        {
            int swapIndex = random.Next(length);
            (password[i], password[swapIndex]) = (password[swapIndex], password[i]);
        }

        return new string(password);
    }
}