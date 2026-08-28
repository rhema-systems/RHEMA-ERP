using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public static class QuantitySurveyAdvanceRecoveryStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Closed = "Closed";
}

[Table("QuantitySurveyAdvanceRecoveryAgreements")]
public sealed class QuantitySurveyAdvanceRecoveryAgreement : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid ContractId { get; set; }
    public Guid VendorPaymentId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(50)] public string RecoveryNumber { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveyAdvanceRecoveryStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    [Required, StringLength(50)] public string ContractNumberSnapshot { get; set; } = string.Empty;
    [Required, StringLength(200)] public string ContractorNameSnapshot { get; set; } = string.Empty;
    [Required, StringLength(50)] public string PaymentNumberSnapshot { get; set; } = string.Empty;
    public DateTime PaymentDateSnapshot { get; set; }
    [Required, StringLength(10)] public string CurrencyCodeSnapshot { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal OriginalAdvanceAmount { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal RecoveryPercentage { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ValuationDecisionId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid PreparedById { get; set; }
    public DateTime PreparedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public VendorPayment VendorPayment { get; set; } = null!;
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision ValuationDecision { get; set; } = null!;
}

[Table("QuantitySurveyAdvanceRecoveryRevisions")]
public sealed class QuantitySurveyAdvanceRecoveryRevision : TenantEntity
{
    public Guid AgreementId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string AfterJson { get; set; } = string.Empty;
    public QuantitySurveyAdvanceRecoveryAgreement Agreement { get; set; } = null!;
}
