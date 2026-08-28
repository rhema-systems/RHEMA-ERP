using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class SupplierApplicantVerificationPreparationDto
{
    public Guid TenantId { get; set; }
    public ProcurementSupplierApplicantVerificationChannel Channel { get; set; }
    public string NormalizedContact { get; set; } = string.Empty;
    public string MaskedContact { get; set; } = string.Empty;
    public bool ResumesExistingApplication { get; set; }
}

public sealed class VerifyAndIssueSupplierApplicantTokenRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public ProcurementSupplierApplicantVerificationChannel Channel { get; set; }

    [Required, StringLength(200)]
    public string Contact { get; set; } = string.Empty;

    [StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    public ProcurementSupplierRegistrationCategory RegistrationCategory { get; set; } =
        ProcurementSupplierRegistrationCategory.Goods;

    public Guid? RetainedRegistrationId { get; set; }
}

public sealed class SupplierApplicantTokenIssueDto
{
    public Guid RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public Guid TokenId { get; set; }
    public string TokenReference { get; set; } = string.Empty;
    public string? PlaintextToken { get; set; }
    public ProcurementSupplierOnboardingFeeMode FeeMode { get; set; }
    public ProcurementSupplierOnboardingTokenStatus TokenStatus { get; set; }
    public ProcurementSupplierOnboardingPaymentStatus PaymentStatus { get; set; }
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool ResumedExistingApplication { get; set; }
    public SupplierApplicantSessionDto? RestrictedSession { get; set; }
}

public sealed class SupplierApplicantTokenDeliveryDto
{
    public bool ApplicantAccessFound { get; set; }
    public bool Delivered { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailureMessage { get; set; }
}

public sealed class StartSupplierApplicantSessionRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required, StringLength(200)]
    public string ApplicationToken { get; set; } = string.Empty;
}

public sealed class SupplierApplicantSessionDto
{
    public Guid SessionId { get; set; }
    public Guid SessionReference { get; set; }
    public Guid ApplicantActorId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid TokenId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool PaymentOnly { get; set; }
}

public sealed class SupplierApplicantPortalDto
{
    public Guid RegistrationId { get; set; }
    public Guid TokenId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PartnerType { get; set; } = "Supplier";
    public ProcurementSupplierRegistrationCategory? RegistrationCategory { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RegistrationData { get; set; }
    public string? RejectionReason { get; set; }
    public ProcurementSupplierOnboardingTokenStatus TokenStatus { get; set; }
    public ProcurementSupplierOnboardingPaymentStatus PaymentStatus { get; set; }
    public ProcurementSupplierOnboardingFeeMode FeeMode { get; set; }
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public string TokenRowVersion { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool PaymentOnly { get; set; }
    public IReadOnlyList<BusinessPartnerRegistrationDocumentDto> Documents { get; set; } =
        Array.Empty<BusinessPartnerRegistrationDocumentDto>();
    public IReadOnlyList<BusinessPartnerRegistrationStatusHistoryDto> StatusHistory { get; set; } =
        Array.Empty<BusinessPartnerRegistrationStatusHistoryDto>();
    public ProcurementSupplierEvidenceReadinessDto? EvidenceReadiness { get; set; }
}

public sealed class UpdateSupplierApplicantApplicationRequest
{
    [Required, StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    public ProcurementSupplierRegistrationCategory RegistrationCategory { get; set; }

    [StringLength(200)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [Required]
    public string RegistrationData { get; set; } = "{}";
}

public sealed class SupplierApplicantAccessSummaryDto
{
    public int TotalApplications { get; set; }
    public int ApplicationInProgress { get; set; }
    public int PendingCredentialDelivery { get; set; }
    public int CredentialDelivered { get; set; }
    public int Activated { get; set; }
    public int Rejected { get; set; }
    public int ActivationFailed { get; set; }
}

public sealed class SupplierApplicantAccessListItemDto
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string VerifiedChannel { get; set; } = string.Empty;
    public string VerifiedContactMasked { get; set; } = string.Empty;
    public ProcurementSupplierApplicantAccessStatus Status { get; set; }
    public string? LoginIdentifier { get; set; }
    public DateTime VerifiedAtUtc { get; set; }
    public DateTime? TemporaryCredentialExpiresAtUtc { get; set; }
    public DateTime? CredentialActivatedAtUtc { get; set; }
    public int NotificationAttemptCount { get; set; }
    public string? LastNotificationStatus { get; set; }
}

public class PrepareSupplierApplicantContactCorrectionRequest
{
    [Required]
    public ProcurementSupplierApplicantVerificationChannel Channel { get; set; }

    [Required, StringLength(200)]
    public string Contact { get; set; } = string.Empty;
}

public sealed class SupplierApplicantContactCorrectionPreparationDto
{
    public Guid TenantId { get; set; }
    public ProcurementSupplierApplicantVerificationChannel Channel { get; set; }
    public string NormalizedContact { get; set; } = string.Empty;
    public string MaskedContact { get; set; } = string.Empty;
}

public sealed class CorrectSupplierApplicantVerifiedContactRequest :
    PrepareSupplierApplicantContactCorrectionRequest
{
    [Required, StringLength(500, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class SupplierApplicantContactCorrectionResultDto
{
    public Guid RegistrationId { get; set; }
    public string MaskedContact { get; set; } = string.Empty;
    public ProcurementSupplierApplicantAccessStatus Status { get; set; }
    public bool ContactCorrected { get; set; }
    public bool ProvisioningRetried { get; set; }
    public bool CredentialDelivered { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class SupplierApplicantAccessOptions
{
    public const string SectionName = "ProcurementSupplierApplicantAccess";
    public int ApplicantSessionMinutes { get; set; } = 60;
    public int TemporaryPasswordExpiryDays { get; set; } = 7;
    public string[] ApprovedIdentityRoles { get; set; } = ["ExternalUser"];
    public string ApprovedBusinessPartnerRole { get; set; } = "Admin";
}
