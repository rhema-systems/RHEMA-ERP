using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ErpSystem.Core.Validation;

/// <summary>
/// Validates email address format only when the value is not null or empty.
/// Allows empty/null values to pass validation.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class OptionalEmailAddressAttribute : ValidationAttribute
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public OptionalEmailAddressAttribute() : base("The {0} field is not a valid e-mail address.")
    {
    }

    public override bool IsValid(object? value)
    {
        // Null or empty string is valid (field is optional)
        if (value == null)
            return true;

        if (value is string stringValue)
        {
            // Empty or whitespace-only string is valid
            if (string.IsNullOrWhiteSpace(stringValue))
                return true;

            // Validate email format
            return EmailRegex.IsMatch(stringValue);
        }

        return false;
    }
}

/// <summary>
/// Validates phone number format only when the value is not null or empty.
/// Allows empty/null values to pass validation.
/// Accepts various phone formats including international numbers.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class OptionalPhoneAttribute : ValidationAttribute
{
    // Accepts: +1234567890, (123) 456-7890, 123-456-7890, 123.456.7890, 123 456 7890, etc.
    private static readonly Regex PhoneRegex = new(
        @"^[\+]?[(]?[0-9]{1,4}[)]?[-\s\./0-9]*$",
        RegexOptions.Compiled);

    public OptionalPhoneAttribute() : base("The {0} field is not a valid phone number.")
    {
    }

    public override bool IsValid(object? value)
    {
        // Null or empty string is valid (field is optional)
        if (value == null)
            return true;

        if (value is string stringValue)
        {
            // Empty or whitespace-only string is valid
            if (string.IsNullOrWhiteSpace(stringValue))
                return true;

            // Remove common formatting characters for length check
            var digitsOnly = Regex.Replace(stringValue, @"[^\d]", "");
            
            // Phone number should have at least 7 digits and no more than 15
            if (digitsOnly.Length < 7 || digitsOnly.Length > 15)
                return false;

            // Validate phone format
            return PhoneRegex.IsMatch(stringValue);
        }

        return false;
    }
}
