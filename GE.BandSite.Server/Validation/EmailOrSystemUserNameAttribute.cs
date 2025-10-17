using System.ComponentModel.DataAnnotations;
using System.Linq;
using GE.BandSite.Server.Configuration;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Validation;

/// <summary>
/// Allows either a traditional email address or a configured system user name.
/// </summary>
public sealed class EmailOrSystemUserNameAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute EmailAttribute = new();

    public EmailOrSystemUserNameAttribute()
    {
        ErrorMessage = "The {0} field is not a valid e-mail address.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string rawValue)
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }

        string input = rawValue.Trim();
        if (input.Length == 0)
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }

        if (EmailAttribute.IsValid(input))
        {
            return ValidationResult.Success;
        }

        var options = validationContext.GetService(typeof(IOptions<SystemUserOptions>)) as IOptions<SystemUserOptions>;
        if (options?.Value != null && options.Value.Enabled)
        {
            bool match = options.Value.Users.Any(
                user => string.Equals(user.UserName, input, StringComparison.OrdinalIgnoreCase));

            if (match)
            {
                return ValidationResult.Success;
            }
        }

        return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
    }
}
