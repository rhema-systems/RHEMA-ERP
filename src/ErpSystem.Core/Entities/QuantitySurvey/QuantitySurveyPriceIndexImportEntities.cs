using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

[Table("QuantitySurveyPriceIndexImportBatches")]
public sealed class QuantitySurveyPriceIndexImportBatch : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid IndexFamilyId { get; set; }
    public QuantitySurveyIndexSource IndexSource { get; set; }
    [Required, StringLength(30)] public string ImportFormat { get; set; } = string.Empty;
    [Required, StringLength(260)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(64)] public string FileHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string NormalizedPayloadHash { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string NormalizedPayloadJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string IssuesJson { get; set; } = "[]";
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid FileUploadRecordId { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid AuthorityRoleId { get; set; }
    [Required, StringLength(160)] public string AuthorityRoleNameSnapshot { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    public int LineCount { get; set; }
    public int ErrorCount { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Staged";
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    public Guid PreparedById { get; set; }
    public DateTime PreparedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(1000)] public string? RejectionReason { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? ChangeReason { get; set; }
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public QuantitySurveyPriceIndexFamily IndexFamily { get; set; } = null!;
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public WorkflowDefinition ApprovalWorkflowDefinition { get; set; } = null!;
    public ApplicationRole AuthorityRole { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public ICollection<QuantitySurveyPriceIndexValue> Values { get; set; } = new List<QuantitySurveyPriceIndexValue>();
}

[Table("QuantitySurveyPriceIndexValues")]
public sealed class QuantitySurveyPriceIndexValue : TenantEntity
{
    public Guid ImportBatchId { get; set; }
    public Guid IndexFamilyId { get; set; }
    public int Sequence { get; set; }
    public DateTime IndexPeriod { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal IndexValue { get; set; }
    public DateTime PublicationDate { get; set; }
    [Required, StringLength(120)] public string SourceReference { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = "Staged";
    public bool IsCurrent { get; set; }
    public Guid ValueKey { get; set; } = Guid.NewGuid();
    public int Version { get; set; } = 1;
    public Guid? SupersedesValueId { get; set; }

    public QuantitySurveyPriceIndexImportBatch ImportBatch { get; set; } = null!;
    public QuantitySurveyPriceIndexFamily IndexFamily { get; set; } = null!;
    public QuantitySurveyPriceIndexValue? SupersedesValue { get; set; }
}

[Table("QuantitySurveyPriceIndexImportRevisions")]
public sealed class QuantitySurveyPriceIndexImportRevision : TenantEntity
{
    public Guid ImportBatchId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }

    public QuantitySurveyPriceIndexImportBatch ImportBatch { get; set; } = null!;
}
