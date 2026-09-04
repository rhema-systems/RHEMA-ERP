using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>Read model for the tenant's company (legal-employer) profile.</summary>
public class CompanyProfileDto : BaseDto
{
    public Guid TenantId { get; set; }

    // Legal identity
    public string LegalName { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public CompanyLegalForm LegalForm { get; set; }
    public string LegalFormName => LegalForm.ToString();
    public string? RegistrationNumber { get; set; }
    public DateTime? DateOfIncorporation { get; set; }
    public Guid? CountryOfIncorporationId { get; set; }
    public string? CountryOfIncorporationName { get; set; }

    // Statutory / tax
    public string? TaxIdentificationNumber { get; set; }
    public string? VatNumber { get; set; }
    public string? SsnitEmployerNumber { get; set; }
    public string? OtherStatutoryRegistrations { get; set; }

    // Registered address & contact
    public string? RegisteredAddress { get; set; }
    public string? DigitalAddress { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public Guid? CountryId { get; set; }
    /// <summary>Administrative area of the registered address; City/Region are snapshots of it.</summary>
    public Guid? GeoAreaId { get; set; }
    public string? CountryName { get; set; }
    public string? PostalCode { get; set; }
    public string? PhonePrimary { get; set; }
    public string? HrEmail { get; set; }
    public string? GeneralEmail { get; set; }
    public string? Website { get; set; }

    // Employer / document presentation
    public string? DefaultSignatoryName { get; set; }
    public string? DefaultSignatoryTitle { get; set; }
    public string? SignatureImageUrl { get; set; }
    public string? CompanySealImageUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? OfferAcceptanceInstructions { get; set; }
    public string? DocumentFooterText { get; set; }
}

/// <summary>
/// Command to upsert the tenant's company profile. Exactly one record per tenant, so no Id is
/// required — the service resolves (or creates) the current tenant's row.
/// </summary>
public class UpdateCompanyProfileDto
{
    // Legal identity
    [Required(ErrorMessage = "Legal name is required.")]
    [MaxLength(200)] public string LegalName { get; set; } = string.Empty;
    [MaxLength(200)] public string? TradingName { get; set; }
    public CompanyLegalForm LegalForm { get; set; } = CompanyLegalForm.LimitedCompany;
    [MaxLength(100)] public string? RegistrationNumber { get; set; }
    public DateTime? DateOfIncorporation { get; set; }
    public Guid? CountryOfIncorporationId { get; set; }

    // Statutory / tax
    [MaxLength(50)] public string? TaxIdentificationNumber { get; set; }
    [MaxLength(50)] public string? VatNumber { get; set; }
    [MaxLength(50)] public string? SsnitEmployerNumber { get; set; }
    [MaxLength(500)] public string? OtherStatutoryRegistrations { get; set; }

    // Registered address & contact
    [MaxLength(500)] public string? RegisteredAddress { get; set; }
    [MaxLength(100)] public string? DigitalAddress { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(100)] public string? Region { get; set; }
    /// <summary>Administrative area of the registered address. City and Region are rewritten from it.</summary>
    public Guid? GeoAreaId { get; set; }
    public Guid? CountryId { get; set; }
    [MaxLength(20)] public string? PostalCode { get; set; }
    [MaxLength(50)] public string? PhonePrimary { get; set; }
    [MaxLength(200)][EmailAddress] public string? HrEmail { get; set; }
    [MaxLength(200)][EmailAddress] public string? GeneralEmail { get; set; }
    [MaxLength(200)] public string? Website { get; set; }

    // Employer / document presentation
    [MaxLength(200)] public string? DefaultSignatoryName { get; set; }
    [MaxLength(200)] public string? DefaultSignatoryTitle { get; set; }
    // ⚠ No SignatureImageUrl or CompanySealImageUrl. They were caller-supplied path strings
    // substituted straight into rendered offer and probation letters, which made an arbitrary value
    // an image source in a document sent to a candidate. Both are set by UPLOADING an image to
    // POST seal-assets/{kind}, which is Admin-gated, scanned and versioned. The two columns survive
    // read-only so a tenant that has not uploaded yet keeps rendering what it had.
    [MaxLength(500)] public string? LogoUrl { get; set; }
    [MaxLength(2000)] public string? OfferAcceptanceInstructions { get; set; }
    [MaxLength(1000)] public string? DocumentFooterText { get; set; }
}

/// <summary>
/// One seal or signature image, and the window it was current for.
/// </summary>
/// <remarks>
/// ⚠ Carries no URL and no bytes. A seal is an instrument of authority, so it is never reachable by
/// address — a letter embeds it, and this screen-facing shape says only which image is in force and
/// who put it there.
/// </remarks>
public class CompanySealAssetDto
{
    public Guid Id { get; set; }
    public CompanySealAssetKind Kind { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? RetiredOn { get; set; }
    public string? RetiredReason { get; set; }

    /// <summary>Derived from <see cref="RetiredOn"/>, never stored — see the entity's remarks.</summary>
    public bool IsCurrent { get; set; }

    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>Why a seal is being withdrawn or replaced. Recorded, because "why" is the audit.</summary>
public class RetireCompanySealAssetDto
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}
