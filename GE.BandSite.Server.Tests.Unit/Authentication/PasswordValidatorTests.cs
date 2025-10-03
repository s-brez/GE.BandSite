using GE.BandSite.Server.Authentication;

namespace GE.BandSite.Server.Tests.Authentication;

[TestFixture]
public class PasswordValidatorTests
{
    private PasswordValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new PasswordValidator();
    }

    [Test]
    public void Validate_NullPassword_ReturnsFalse()
    {
        Assert.That(_validator.Validate(null), Is.False);
    }

    [Test]
    public void Validate_EmptyPassword_ReturnsFalse()
    {
        Assert.That(_validator.Validate(string.Empty), Is.False);
    }

    [Test]
    public void Validate_PasswordTooShort_ReturnsFalse()
    {
        var password = "Ab1!" + new string('x', _validator.MinLength - 5);

        Assert.That(_validator.Validate(password), Is.False);
    }

    [Test]
    public void Validate_PasswordTooLong_ReturnsFalse()
    {
        var password = new string('A', _validator.MaxLength + 1);

        Assert.That(_validator.Validate(password), Is.False);
    }

    [Test]
    public void Validate_PasswordMissingDigit_ReturnsFalse()
    {
        const string password = "Abcdefghijklmnop!";

        Assert.That(_validator.Validate(password), Is.False);
    }

    [Test]
    public void Validate_PasswordMissingUppercase_ReturnsFalse()
    {
        const string password = "abc123!@#defghijk";

        Assert.That(_validator.Validate(password), Is.False);
    }

    [Test]
    public void Validate_PasswordMissingLowercase_ReturnsFalse()
    {
        const string password = "ABC123!@#DEFGHIJK";

        Assert.That(_validator.Validate(password), Is.False);
    }

    [Test]
    public void Validate_PasswordMissingSpecial_ReturnsFalse()
    {
        const string password = "Abcdefghijk12345";

        Assert.That(_validator.Validate(password), Is.False);
    }

    [Test]
    public void Validate_PasswordWithWhitespace_ReturnsFalse()
    {
        var password = "Abc 123!@#defGhij";

        Assert.That(_validator.Validate(password), Is.False);
    }

    [Test]
    public void Validate_ValidPassword_ReturnsTrue()
    {
        var password = "Abcdef123!@#4567";

        Assert.That(_validator.Validate(password), Is.True);
    }

    [Test]
    public void ValidateWithFeedback_InvalidPassword_ReturnsReasons()
    {
        var (isValid, reasons) = _validator.ValidateWithFeedback("abc");

        Assert.That(isValid, Is.False);
        Assert.That(reasons, Is.Not.Empty);
        Assert.That(reasons, Does.Contain($"Password must be between {_validator.MinLength} and {_validator.MaxLength} characters"));
    }

    [Test]
    public void ValidateWithFeedback_ValidPassword_HasNoFailures()
    {
        var (isValid, reasons) = _validator.ValidateWithFeedback("Abcdef123!@#4567");

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(reasons, Is.Empty);
        });
    }

    [Test]
    public void GenerateRandomPassword_ProducesPasswordMeetingRequirements()
    {
        var password = _validator.GenerateRandomPassword();

        var (isValid, failures) = _validator.ValidateWithFeedback(password);

        Assert.That(isValid, Is.True, () => string.Join(", ", failures));
    }

    [Test]
    public void GenerateRandomPassword_CustomLengthHonoured()
    {
        var password = _validator.GenerateRandomPassword(_validator.MinLength);

        Assert.That(password.Length, Is.EqualTo(_validator.MinLength));
    }

    [Test]
    public void GenerateRandomPassword_InvalidLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _validator.GenerateRandomPassword(_validator.MinLength - 1));
    }
}
