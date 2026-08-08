using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementSupplierOnboardingTokens")]
public sealed class ProcurementSupplierOnboardingToken : TenantEntity
{
    public Guid RegistrationId { get; set; }

    [Required, StringLength(50)]
    public string TokenReference { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string TokenHashSha256 { get; set; } = string.Empty;

    [Required, StringLength(8)]
    public string TokenLastFour { get; set; } = string.Empty;

    public int Generation { get; set; } = 1;
    public ProcurementSupplierOnboardingTokenStatus Status { get; set; }
    public ProcurementSupplierOnboardingPaymentStatus PaymentStatus { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? ExpiredAtUtc { get; set; }

    [StringLength(500)]
    public string? ExpiryReason { get; set; }

    public Guid SourceConfigurationProfileId { get; set; }

    [Required, StringLength(50)]
    public string SourceConfigurationProfileCode { get; set; } = string.Empty;

    public int SourceConfigurationProfileVersion { get; set; }
    public Guid SourceConfigurationDecisionId { get; set; }
    public ProcurementSupplierOnboardingFeeMode FeeMode { get; set; }

    [Required, StringLength(150)]
    public string FeeType { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FeeAmount { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Required, StringLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    public Guid? RevenueAccountId { get; set; }
    public Guid? TaxAccountId { get; set; }
    public Guid? ExemptionWorkflowDefinitionId { get; set; }

    [Required, Column(TypeName = "nvarchar(max)")]
    public string PaymentChannelsJson { get; set; } = "[]";

    [Required, StringLength(100)]
    public string ReceiptNumberFormat { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string ExemptionRule { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string RefundRule { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string RenewalRule { get; set; } = string.Empty;

    [Required, Column(TypeName = "nvarchar(max)")]
    public string DecisionSnapshotJson { get; set; } = "{}";

    [Required, StringLength(64)]
    public string DecisionSnapshotHash { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CreationCorrelationId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastOperationCorrelationId { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastOperation { get; set; } = "Issued";

    public DateTime? ReissuedAtUtc { get; set; }
    public Guid? ReissuedById { get; set; }

    [StringLength(500)]
    public string? ReissueReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public BusinessPartnerRegistration Registration { get; set; } = null!;
    public ProcurementConfigurationProfile SourceConfigurationProfile { get; set; } = null!;
    public ProcurementConfigurationDecision SourceConfigurationDecision { get; set; } = null!;
    public Account? RevenueAccount { get; set; }
    public Account? TaxAccount { get; set; }
    public WorkflowDefinition? ExemptionWorkflowDefinition { get; set; }
    public ICollection<ProcurementSupplierOnboardingPayment> Payments { get; set; } =
        new List<ProcurementSupplierOnboardingPayment>();
    public ICollection<ProcurementSupplierOnboardingExemption> Exemptions { get; set; } =
        new List<ProcurementSupplierOnboardingExemption>();
    public ProcurementSupplierApplicantAccess? ApplicantAccess { get; set; }
}

[Table("ProcurementSupplierOnboardingPayments")]
public sealed class ProcurementSupplierOnboardingPayment : TenantEntity
{
    public Guid TokenId { get; set; }
    public Guid? SubmittedByApplicantSessionId { get; set; }
    public Guid PaymentMethodId { get; set; }

    [Required, StringLength(20)]
    public string PaymentMethodCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string PaymentMethodName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? PaymentReference { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FeeAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Required, StringLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    public ProcurementSupplierOnboardingPaymentStatus Status { get; set; }
    public DateTime PaidAtUtc { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? JournalEntryId { get; set; }

    [StringLength(50)]
    public string? ReceiptNumber { get; set; }

    public DateTime? ReceiptIssuedAtUtc { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }
    public Guid? ReconciledById { get; set; }

    [StringLength(200)]
    public string? ReconciliationReference { get; set; }

    [StringLength(1000)]
    public string? ReconciliationNotes { get; set; }

    [StringLength(1000)]
    public string? FailureReason { get; set; }

    [Required, StringLength(100)]
    public string CreationCorrelationId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastOperationCorrelationId { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastOperation { get; set; } = "Recorded";

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementSupplierOnboardingToken Token { get; set; } = null!;
    public ProcurementSupplierApplicantSession? SubmittedByApplicantSession { get; set; }
    public FinancePaymentMethod PaymentMethod { get; set; } = null!;
}

[Table("ProcurementSupplierOnboardingExemptions")]
public sealed class ProcurementSupplierOnboardingExemption : TenantEntity
{
    public Guid TokenId { get; set; }

    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public ProcurementSupplierOnboardingExemptionStatus Status { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid RequestedById { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public Guid? DecidedById { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    [StringLength(1000)]
    public string? DecisionComment { get; set; }

    [Required, Column(TypeName = "nvarchar(max)")]
    public string EvidenceJson { get; set; } = "[]";

    [Required, StringLength(64)]
    public string EvidenceHash { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CreationCorrelationId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastOperationCorrelationId { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastOperation { get; set; } = "Requested";

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementSupplierOnboardingToken Token { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
}

[Table("ProcurementSupplierApplicantAccesses")]
public sealed class ProcurementSupplierApplicantAccess : TenantEntity
{
    public Guid RegistrationId { get; set; }
    public Guid TokenId { get; set; }
    public ProcurementSupplierApplicantVerificationChannel VerifiedChannel { get; set; }

    [Required, StringLength(64)]
    public string VerifiedContactHashSha256 { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string VerifiedContactMasked { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string VerifiedContact { get; set; } = string.Empty;

    public DateTime VerifiedAtUtc { get; set; }
    public ProcurementSupplierApplicantAccessStatus Status { get; set; }
    public Guid? ApprovedUserId { get; set; }
    public Guid? BusinessPartnerId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string ApprovedIdentityRolesJson { get; set; } = "[]";

    [StringLength(50)]
    public string? ApprovedBusinessPartnerRole { get; set; }

    [StringLength(200)]
    public string? LoginIdentifier { get; set; }

    public DateTime? TemporaryCredentialIssuedAtUtc { get; set; }
    public DateTime? TemporaryCredentialExpiresAtUtc { get; set; }
    public DateTime? CredentialActivatedAtUtc { get; set; }
    public DateTime? LastNotificationAtUtc { get; set; }
    public int NotificationAttemptCount { get; set; }

    [StringLength(50)]
    public string? LastNotificationStatus { get; set; }

    [StringLength(1000)]
    public string? LastNotificationFailure { get; set; }

    public DateTime? TerminalAtUtc { get; set; }

    [StringLength(50)]
    public string? TerminalOutcome { get; set; }

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public BusinessPartnerRegistration Registration { get; set; } = null!;
    public ProcurementSupplierOnboardingToken Token { get; set; } = null!;
    public ApplicationUser? ApprovedUser { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
    public ICollection<ProcurementSupplierApplicantSession> Sessions { get; set; } =
        new List<ProcurementSupplierApplicantSession>();
}

[Table("ProcurementSupplierApplicantSessions")]
public sealed class ProcurementSupplierApplicantSession : TenantEntity
{
    public Guid ApplicantAccessId { get; set; }
    public Guid SessionReference { get; set; }
    public ProcurementSupplierApplicantSessionStatus Status { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    [StringLength(200)]
    public string? RevocationReason { get; set; }

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementSupplierApplicantAccess ApplicantAccess { get; set; } = null!;
    public ICollection<ProcurementSupplierOnboardingPayment> SubmittedPayments { get; set; } =
        new List<ProcurementSupplierOnboardingPayment>();
}
