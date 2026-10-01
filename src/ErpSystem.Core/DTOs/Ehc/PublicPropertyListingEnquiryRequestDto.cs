using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Ehc;

/// <summary>
/// Public input contract for an anonymous property-listing enquiry.
/// Internal routing, assignment, customer and workflow fields are intentionally absent.
/// </summary>
public sealed class PublicPropertyListingEnquiryRequestDto : IValidatableObject
{
    public Guid SubmissionId { get; init; }

    [Required(ErrorMessage = "Enter your enquiry message.")]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "The enquiry message must be between 1 and 4,000 characters.")]
    public string Message { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter your name.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "The contact name must be between 2 and 200 characters.")]
    public string ContactName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(320, ErrorMessage = "The email address cannot exceed 320 characters.")]
    public string ContactEmail { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter your phone number.")]
    [StringLength(40, MinimumLength = 7, ErrorMessage = "The phone number must be between 7 and 40 characters.")]
    public string ContactPhone { get; init; } = string.Empty;

    [StringLength(40, MinimumLength = 7, ErrorMessage = "The alternative phone number must be between 7 and 40 characters.")]
    public string? AlternativePhoneNumber { get; init; }

    [StringLength(100, ErrorMessage = "The contact reference cannot exceed 100 characters.")]
    public string? ContactReference { get; init; }

    [StringLength(16, ErrorMessage = "The preferred contact method cannot exceed 16 characters.")]
    public string PreferredContactMethod { get; init; } = "Email";

    [StringLength(8192, ErrorMessage = "The security token is invalid.")]
    public string? CaptchaToken { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SubmissionId == Guid.Empty)
        {
            yield return new ValidationResult(
                "A submission identifier is required.",
                new[] { nameof(SubmissionId) });
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            yield return new ValidationResult(
                "Enter your enquiry message.",
                new[] { nameof(Message) });
        }

        if (string.IsNullOrWhiteSpace(ContactName))
        {
            yield return new ValidationResult(
                "Enter your name.",
                new[] { nameof(ContactName) });
        }

        if (!HasValidPhoneShape(ContactPhone))
        {
            yield return new ValidationResult(
                "Enter a valid phone number.",
                new[] { nameof(ContactPhone) });
        }

        if (!string.IsNullOrWhiteSpace(AlternativePhoneNumber) &&
            !HasValidPhoneShape(AlternativePhoneNumber))
        {
            yield return new ValidationResult(
                "Enter a valid alternative phone number.",
                new[] { nameof(AlternativePhoneNumber) });
        }

        if (!string.Equals(PreferredContactMethod, "Email", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(PreferredContactMethod, "Phone", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(PreferredContactMethod, "Either", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Preferred contact method must be Email, Phone or Either.",
                new[] { nameof(PreferredContactMethod) });
        }
    }

    private static bool HasValidPhoneShape(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length is < 7 or > 40 || trimmed.Any(character =>
                !char.IsDigit(character) && character is not '+' and not '-' and not '(' and not ')' and not ' '))
        {
            return false;
        }

        var digitCount = trimmed.Count(char.IsDigit);
        return digitCount is >= 7 and <= 20 &&
               (trimmed[0] == '+' || char.IsDigit(trimmed[0]));
    }
}
