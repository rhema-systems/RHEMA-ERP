using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Ehc;

/// <summary>
/// Public input contract for an anonymous property-listing enquiry.
/// Internal routing, assignment, customer and workflow fields are intentionally absent.
/// </summary>
public sealed class PublicPropertyListingEnquiryRequestDto : IValidatableObject
{
    public Guid SubmissionId { get; init; }

    public Guid IdentificationTypeId { get; init; }

    [Required(ErrorMessage = "Enter your identification number.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "The identification number must be between 3 and 100 characters.")]
    public string IdentificationNumber { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter your enquiry message.")]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "The enquiry message must be between 1 and 4,000 characters.")]
    public string Message { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter your name.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "The contact name must be between 2 and 200 characters.")]
    public string ContactName { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(320, ErrorMessage = "The email address cannot exceed 320 characters.")]
    public string? ContactEmail { get; init; }

    [StringLength(20, MinimumLength = 8, ErrorMessage = "The phone number must be between 8 and 20 characters.")]
    public string? ContactPhone { get; init; }

    [StringLength(40, MinimumLength = 7, ErrorMessage = "The alternative phone number must be between 7 and 40 characters.")]
    public string? AlternativePhoneNumber { get; init; }

    [StringLength(100, ErrorMessage = "The contact reference cannot exceed 100 characters.")]
    public string? ContactReference { get; init; }

    [StringLength(16, ErrorMessage = "The preferred contact method cannot exceed 16 characters.")]
    public string PreferredContactMethod { get; init; } = "Email";

    [Required(ErrorMessage = "Verify your selected contact method before sending the enquiry.")]
    [StringLength(200, MinimumLength = 32, ErrorMessage = "The contact verification has expired or is invalid.")]
    public string ContactVerificationToken { get; init; } = string.Empty;

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

        if (IdentificationTypeId == Guid.Empty)
        {
            yield return new ValidationResult(
                "Select an identification type.",
                new[] { nameof(IdentificationTypeId) });
        }

        if (string.IsNullOrWhiteSpace(IdentificationNumber))
        {
            yield return new ValidationResult(
                "Enter your identification number.",
                new[] { nameof(IdentificationNumber) });
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

        var usesEmail = string.Equals(PreferredContactMethod, "Email", StringComparison.OrdinalIgnoreCase);
        var usesPhone = string.Equals(PreferredContactMethod, "Phone", StringComparison.OrdinalIgnoreCase);

        if (usesEmail && string.IsNullOrWhiteSpace(ContactEmail))
        {
            yield return new ValidationResult(
                "Enter your email address.",
                new[] { nameof(ContactEmail) });
        }

        if (usesEmail && !string.IsNullOrWhiteSpace(ContactPhone))
        {
            yield return new ValidationResult(
                "Only the selected email address may be submitted.",
                new[] { nameof(ContactPhone) });
        }

        if (usesPhone && !HasValidPhoneShape(ContactPhone))
        {
            yield return new ValidationResult(
                "Enter a valid international phone number including its country code.",
                new[] { nameof(ContactPhone) });
        }

        if (usesPhone && !string.IsNullOrWhiteSpace(ContactEmail))
        {
            yield return new ValidationResult(
                "Only the selected phone number may be submitted.",
                new[] { nameof(ContactEmail) });
        }

        if (!string.IsNullOrWhiteSpace(AlternativePhoneNumber))
        {
            yield return new ValidationResult(
                "An alternative phone number is not accepted for public enquiries.",
                new[] { nameof(AlternativePhoneNumber) });
        }

        if (!usesEmail && !usesPhone)
        {
            yield return new ValidationResult(
                "Preferred contact method must be Email or Phone.",
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
        if (trimmed.Length is < 9 or > 16 || trimmed[0] != '+' || trimmed.Skip(1).Any(character => !char.IsDigit(character)))
        {
            return false;
        }

        var digitCount = trimmed.Count(char.IsDigit);
        return digitCount is >= 8 and <= 15 && trimmed[1] != '0';
    }
}
