using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class PublicPropertyEnquiryContactChallengeRequestDto
{
    public Guid ListingId { get; init; }

    [Required, StringLength(10)]
    public string Channel { get; init; } = string.Empty;

    [Required, StringLength(320)]
    public string Contact { get; init; } = string.Empty;

    [StringLength(8192)]
    public string? CaptchaToken { get; init; }
}

public sealed class PublicPropertyEnquiryContactVerificationRequestDto
{
    public Guid ListingId { get; init; }

    [Required, StringLength(10)]
    public string Channel { get; init; } = string.Empty;

    [Required, StringLength(320)]
    public string Contact { get; init; } = string.Empty;

    [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "Enter the six-digit verification code.")]
    public string OtpCode { get; init; } = string.Empty;
}
