using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementFrameworkAgreementSearchRequest
{
    public string? Search { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public ProcurementFrameworkAgreementStatus? Status { get; set; }
    public ProcurementAwardReadinessSourceType? SourceType { get; set; }
    public bool? EffectiveOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class ProcurementFrameworkAgreementSummaryDto
{
    public int TotalVersions { get; set; }
    public int DraftCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int PublishedCount { get; set; }
    public int EffectiveCount { get; set; }
    public int ExpiringWithin90DaysCount { get; set; }
    public int PendingExtensionCount { get; set; }
    public decimal TotalEffectiveCeiling { get; set; }
    public decimal TotalEffectiveAvailable { get; set; }
    public Dictionary<string, decimal> EffectiveCeilingByCurrency { get; set; } = new();
    public Dictionary<string, decimal> EffectiveAvailableByCurrency { get; set; } = new();
}

public sealed class ProcurementFrameworkAgreementPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementFrameworkAgreementListItemDto> Items { get; set; } = new();
}

public class ProcurementFrameworkAgreementListItemDto
{
    public Guid Id { get; set; }
    public Guid AgreementKey { get; set; }
    public string AgreementNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Version { get; set; }
    public ProcurementFrameworkAgreementStatus Status { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string PriceListReference { get; set; } = string.Empty;
    public int PriceListVersion { get; set; }
    public decimal CeilingAmount { get; set; }
    public decimal AvailableCeiling { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveToUtc { get; set; }
    public DateTime EffectiveEndUtc { get; set; }
    public bool IsEffective { get; set; }
    public int CategoryCount { get; set; }
    public int PriceLineCount { get; set; }
    public int AuthorityCount { get; set; }
    public int DocumentCount { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = Array.Empty<string>();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkAgreementDto :
    ProcurementFrameworkAgreementListItemDto
{
    public Guid AwardReadinessDecisionId { get; set; }
    public string SourceIntegrityHash { get; set; } = string.Empty;
    public string SupplierEligibilityDecisionHash { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SupersedesAgreementId { get; set; }
    public Guid? SupersededByAgreementId { get; set; }
    public string? Description { get; set; }
    public string? TermsSummary { get; set; }
    public string? ReviewComment { get; set; }
    public Guid? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public Guid? TerminatedById { get; set; }
    public DateTime? TerminatedAtUtc { get; set; }
    public string? TerminationReason { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementFrameworkAgreementCategoryDto> Categories { get; set; } = new();
    public List<ProcurementFrameworkPriceListLineDto> PriceLines { get; set; } = new();
    public List<ProcurementFrameworkCallOffAuthorityDto> CallOffAuthorities { get; set; } = new();
    public List<ProcurementFrameworkAgreementDocumentDto> Documents { get; set; } = new();
    public List<ProcurementFrameworkAgreementExtensionDto> Extensions { get; set; } = new();
}

public sealed class ProcurementFrameworkAgreementCategoryDto
{
    public Guid Id { get; set; }
    public Guid PartnerCategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkPriceListLineDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal MinimumQuantity { get; set; }
    public decimal? MaximumQuantity { get; set; }
    public int LeadTimeDays { get; set; }
    public string? Specifications { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkCallOffAuthorityDto
{
    public Guid Id { get; set; }
    public ProcurementFrameworkAuthorityKind AuthorityKind { get; set; }
    public Guid? AuthorityUserId { get; set; }
    public string AuthorityValue { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal? MaximumCallOffAmount { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkAgreementDocumentDto
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public string DmsReference { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkAgreementExtensionDto
{
    public Guid Id { get; set; }
    public int SequenceNumber { get; set; }
    public ProcurementFrameworkExtensionStatus Status { get; set; }
    public DateTime PreviousEndUtc { get; set; }
    public DateTime ProposedEndUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid SubmittedById { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public Guid? DecidedById { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionComment { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CreateProcurementFrameworkAgreementRequest
{
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal CeilingAmount { get; set; }
    [Required, StringLength(3, MinimumLength = 3)] public string CurrencyCode { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveToUtc { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(1000)] public string? TermsSummary { get; set; }
    [MinLength(1)] public List<Guid> CategoryIds { get; set; } = new();
    [MinLength(1)] public List<SaveProcurementFrameworkPriceListLineRequest> PriceLines { get; set; } = new();
    [MinLength(1)] public List<SaveProcurementFrameworkCallOffAuthorityRequest> CallOffAuthorities { get; set; } = new();
}

public sealed class UpdateProcurementFrameworkAgreementRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal CeilingAmount { get; set; }
    [Required, StringLength(3, MinimumLength = 3)] public string CurrencyCode { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveToUtc { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(1000)] public string? TermsSummary { get; set; }
    [MinLength(1)] public List<Guid> CategoryIds { get; set; } = new();
    [MinLength(1)] public List<SaveProcurementFrameworkPriceListLineRequest> PriceLines { get; set; } = new();
    [MinLength(1)] public List<SaveProcurementFrameworkCallOffAuthorityRequest> CallOffAuthorities { get; set; } = new();
}

public sealed class SaveProcurementFrameworkPriceListLineRequest
{
    public Guid InventoryItemId { get; set; }
    [Range(typeof(decimal), "0.0001", "9999999999999999")] public decimal UnitPrice { get; set; }
    [Range(typeof(decimal), "0.0001", "9999999999999999")] public decimal MinimumQuantity { get; set; } = 1m;
    public decimal? MaximumQuantity { get; set; }
    [Range(0, 3650)] public int LeadTimeDays { get; set; }
    [StringLength(500)] public string? Specifications { get; set; }
}

public sealed class SaveProcurementFrameworkCallOffAuthorityRequest
{
    public ProcurementFrameworkAuthorityKind AuthorityKind { get; set; }
    public Guid? AuthorityUserId { get; set; }
    [Required, StringLength(200)] public string AuthorityValue { get; set; } = string.Empty;
    [Required, StringLength(300)] public string DisplayName { get; set; } = string.Empty;
    public decimal? MaximumCallOffAmount { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProcurementFrameworkAgreementLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class CloneProcurementFrameworkAgreementRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveToUtc { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    [Required, StringLength(1000)] public string ChangeSummary { get; set; } = string.Empty;
}

public sealed class AddProcurementFrameworkAgreementDocumentRequest
{
    public Guid FileUploadRecordId { get; set; }
    [Required, StringLength(100)] public string DocumentType { get; set; } = string.Empty;
    [Required, StringLength(250)] public string Title { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}

public sealed class RequestProcurementFrameworkAgreementExtension
{
    [Required] public string AgreementRowVersion { get; set; } = string.Empty;
    public DateTime ProposedEndUtc { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class DecideProcurementFrameworkAgreementExtension
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    public bool Approve { get; set; }
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ProcurementFrameworkWorkflowOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
}

public sealed class ProcurementFrameworkCategoryOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkItemOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkSourceOptionDto
{
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal AwardAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime AwardedAtUtc { get; set; }
    public Guid AwardReadinessDecisionId { get; set; }
}
