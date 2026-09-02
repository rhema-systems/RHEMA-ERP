using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyEstimateType
{
    CostPlan = 0,
    TenderEstimate = 1,
    BudgetEstimate = 2
}

public static class QuantitySurveyEstimateStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Retired = "Retired";
}

[Table("QuantitySurveyEstimateVersions")]
public sealed class QuantitySurveyEstimateVersion : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ProjectBoqVersionId { get; set; }
    public Guid? SourceEstimateVersionId { get; set; }
    public Guid ClientRequestId { get; set; }
    public int VersionNumber { get; set; }
    public QuantitySurveyEstimateType EstimateType { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = string.Empty;
    public DateTime EstimateDate { get; set; }
    public Guid CurrencyId { get; set; }
    [Required, StringLength(3)] public string CurrencyCodeSnapshot { get; set; } = string.Empty;
    [StringLength(500)] public string? FundingSourceSnapshot { get; set; }
    [StringLength(2000)] public string? PropertyReferenceSnapshot { get; set; }
    public int SourceSnapshotSchemaVersion { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DirectCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MarkupTotal { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveyEstimateStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = QuantitySurveyEstimateStatuses.Draft;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    [Required, StringLength(1000)] public string ChangeReason { get; set; } = string.Empty;
    [Required, StringLength(64)] public string SnapshotHash { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int AssumptionCount { get; set; }
    public int MarkupCount { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public int ConfigurationProfileVersion { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectBoqVersion ProjectBoqVersion { get; set; } = null!;
    public QuantitySurveyEstimateVersion? SourceEstimateVersion { get; set; }
    public Currency Currency { get; set; } = null!;
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
    public ICollection<QuantitySurveyEstimateLine> Lines { get; set; } = new List<QuantitySurveyEstimateLine>();
    public ICollection<QuantitySurveyEstimateAssumption> Assumptions { get; set; } = new List<QuantitySurveyEstimateAssumption>();
    public ICollection<QuantitySurveyEstimateMarkup> Markups { get; set; } = new List<QuantitySurveyEstimateMarkup>();
}

[Table("QuantitySurveyEstimateLines")]
public sealed class QuantitySurveyEstimateLine : TenantEntity
{
    public Guid EstimateVersionId { get; set; }
    public int Sequence { get; set; }
    public Guid ProjectBoqVersionLineId { get; set; }
    public Guid? SourceRateId { get; set; }
    [Required, StringLength(50)] public string LineNumberSnapshot { get; set; } = string.Empty;
    [StringLength(50)] public string? ItemCodeSnapshot { get; set; }
    [Required, StringLength(1000)] public string DescriptionSnapshot { get; set; } = string.Empty;
    [StringLength(20)] public string? UnitOfMeasureSnapshot { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal UnitRate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal LineAmount { get; set; }
    [StringLength(50)] public string? SourceRateItemCodeSnapshot { get; set; }
    public int? SourceRateVersionSnapshot { get; set; }
    [StringLength(50)] public string RateSourceSnapshot { get; set; } = "BoQ";

    public QuantitySurveyEstimateVersion EstimateVersion { get; set; } = null!;
    public ProjectBoqVersionLine ProjectBoqVersionLine { get; set; } = null!;
    public QuantitySurveyRateLibraryRate? SourceRate { get; set; }
}

[Table("QuantitySurveyEstimateAssumptions")]
public sealed class QuantitySurveyEstimateAssumption : TenantEntity
{
    public Guid EstimateVersionId { get; set; }
    public int Sequence { get; set; }
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(250)] public string Description { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Value { get; set; } = string.Empty;
    [StringLength(50)] public string? Unit { get; set; }
    public QuantitySurveyEstimateVersion EstimateVersion { get; set; } = null!;
}

[Table("QuantitySurveyEstimateMarkups")]
public sealed class QuantitySurveyEstimateMarkup : TenantEntity
{
    public Guid EstimateVersionId { get; set; }
    public int Sequence { get; set; }
    public QuantitySurveyRateComponent Component { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal Percentage { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal BasisAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    public QuantitySurveyEstimateVersion EstimateVersion { get; set; } = null!;
}

[Table("QuantitySurveyEstimateRevisions")]
public sealed class QuantitySurveyEstimateRevision : TenantEntity
{
    public Guid EstimateVersionId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }
    public QuantitySurveyEstimateVersion EstimateVersion { get; set; } = null!;
}
