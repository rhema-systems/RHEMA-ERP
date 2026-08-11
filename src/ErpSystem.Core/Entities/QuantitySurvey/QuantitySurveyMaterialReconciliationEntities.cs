using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public static class QuantitySurveyMaterialReconciliationStatuses
{
    public const string Draft = "Draft";
    public const string ContractorConfirmed = "ContractorConfirmed";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public enum QuantitySurveyMaterialLineType
{
    MaterialOnSite = 0,
    MaterialOffSite = 1,
    TdcSuppliedMaterial = 2
}

[Table("QuantitySurveyMaterialReconciliations")]
public sealed class QuantitySurveyMaterialReconciliation : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ValuationWorksheetId { get; set; }
    public Guid ContractorBusinessPartnerId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(50)] public string ReconciliationNumber { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveyMaterialReconciliationStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    [Required, StringLength(50)] public string ContractNumberSnapshot { get; set; } = string.Empty;
    [Required, StringLength(200)] public string ContractorNameSnapshot { get; set; } = string.Empty;
    [Required, StringLength(10)] public string CurrencyCodeSnapshot { get; set; } = string.Empty;
    public QuantitySurveyMaterialValuationBasis ValuationBasis { get; set; }
    public bool InventoryReconciliationRequired { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MaterialOnSiteAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MaterialOffSiteAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TdcSuppliedDeductionAmount { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid MaterialDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid PreparedById { get; set; }
    public DateTime PreparedAt { get; set; }
    public Guid? ContractorConfirmedById { get; set; }
    public DateTime? ContractorConfirmedAt { get; set; }
    [StringLength(64)] public string? ContractorConfirmationHash { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public BusinessPartner ContractorBusinessPartner { get; set; } = null!;
    public QuantitySurveyValuationWorksheet ValuationWorksheet { get; set; } = null!;
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision MaterialDecision { get; set; } = null!;
    public ICollection<QuantitySurveyMaterialReconciliationLine> Lines { get; set; } = new List<QuantitySurveyMaterialReconciliationLine>();
    public ICollection<QuantitySurveyMaterialReconciliationRevision> Revisions { get; set; } = new List<QuantitySurveyMaterialReconciliationRevision>();
}

[Table("QuantitySurveyMaterialReconciliationLines")]
public sealed class QuantitySurveyMaterialReconciliationLine : TenantEntity
{
    public Guid ReconciliationId { get; set; }
    public int Sequence { get; set; }
    public QuantitySurveyMaterialLineType LineType { get; set; }
    public Guid InventoryItemId { get; set; }
    [Required, StringLength(100)] public string InventoryItemCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(200)] public string InventoryItemNameSnapshot { get; set; } = string.Empty;
    [Required, StringLength(20)] public string UnitOfMeasureSnapshot { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal? DeliveredUnitCost { get; set; }
    public Guid? ApprovedRateId { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal? ApprovedUnitRateSnapshot { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal AppliedUnitRate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalValue { get; set; }
    public Guid? InventoryIssueVoucherLineId { get; set; }
    [StringLength(50)] public string? IssueVoucherNumberSnapshot { get; set; }
    [StringLength(64)] public string? IssueVoucherIntegrityHashSnapshot { get; set; }
    public Guid? ValuationEvidenceId { get; set; }
    public Guid? CentralDocumentRecordIdSnapshot { get; set; }
    public Guid? CentralDocumentVersionIdSnapshot { get; set; }
    [StringLength(64)] public string? EvidenceChecksumSnapshot { get; set; }
    [Required, StringLength(64)] public string SourceHash { get; set; } = string.Empty;

    public QuantitySurveyMaterialReconciliation Reconciliation { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
    public QuantitySurveyRateLibraryRate? ApprovedRate { get; set; }
    public InventoryIssueVoucherLine? InventoryIssueVoucherLine { get; set; }
    public QuantitySurveyValuationWorksheetEvidence? ValuationEvidence { get; set; }
}

[Table("QuantitySurveyMaterialReconciliationRevisions")]
public sealed class QuantitySurveyMaterialReconciliationRevision : TenantEntity
{
    public Guid ReconciliationId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string AfterJson { get; set; } = string.Empty;
    public QuantitySurveyMaterialReconciliation Reconciliation { get; set; } = null!;
}
