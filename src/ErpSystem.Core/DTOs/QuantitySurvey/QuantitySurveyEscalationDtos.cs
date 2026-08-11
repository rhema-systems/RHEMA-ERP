using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyIndexFamilyListRequest
{
    public string? Search { get; init; }
    public bool IncludeInactive { get; init; }
}

public sealed class SaveQuantitySurveyIndexFamilyRequest
{
    [Required, StringLength(40)] public string Code { get; init; } = string.Empty;
    [Required, StringLength(160)] public string Name { get; init; } = string.Empty;
    public QuantitySurveyIndexSource Source { get; init; }
    [Required, StringLength(160)] public string Publisher { get; init; } = string.Empty;
    [StringLength(1000)] public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    [Required, StringLength(1000)] public string Reason { get; init; } = string.Empty;
    public string? RowVersion { get; init; }
}

public sealed class QuantitySurveyIndexFamilyDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public QuantitySurveyIndexSource Source { get; init; }
    public string Publisher { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class QuantitySurveyEscalationPolicyDto
{
    public Guid ConfigurationProfileId { get; init; }
    public Guid ConfigurationDecisionId { get; init; }
    public QuantitySurveyEscalationFormula FormulaType { get; init; }
    public decimal MaterialCoefficient { get; init; }
    public decimal LabourCoefficient { get; init; }
    public decimal PlantCoefficient { get; init; }
    public decimal OtherCoefficient { get; init; }
    public string ImportFormat { get; init; } = string.Empty;
    public decimal TotalCoefficient => MaterialCoefficient + LabourCoefficient + PlantCoefficient + OtherCoefficient;
}

public sealed class QuantitySurveyEscalationLookupsDto
{
    public IReadOnlyDictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>> Sources { get; init; } =
        new Dictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>>();
    public QuantitySurveyEscalationPolicyDto Policy { get; init; } = new();
}

public sealed class QuantitySurveyEscalationFormulaListRequest
{
    public string? Search { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ContractId { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class QuantitySurveyEscalationFormulaComponentRequest
{
    public QuantitySurveyEscalationComponentType Component { get; init; }
    [Range(0, 100)] public decimal Coefficient { get; init; }
    public Guid IndexFamilyId { get; init; }
}

public class CreateQuantitySurveyEscalationFormulaRequest
{
    public Guid ClientRequestId { get; init; }
    [Required, StringLength(40)] public string Code { get; init; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public Guid ContractId { get; init; }
    [Required, StringLength(120)] public string ContractClauseReference { get; init; } = string.Empty;
    public QuantitySurveyEscalationFormula FormulaType { get; init; }
    public DateTime BaseDate { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public Guid AuthorityRoleId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public Guid? SourceFormulaId { get; init; }
    [Required, MinLength(4), MaxLength(4)] public List<QuantitySurveyEscalationFormulaComponentRequest> Components { get; init; } = [];
    [Required, StringLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class UpdateQuantitySurveyEscalationFormulaRequest : CreateQuantitySurveyEscalationFormulaRequest
{
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public sealed class QuantitySurveyEscalationLifecycleRequest
{
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyEscalationFormulaComponentDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public QuantitySurveyEscalationComponentType Component { get; init; }
    public decimal Coefficient { get; init; }
    public Guid IndexFamilyId { get; init; }
    public QuantitySurveyIndexSource IndexSource { get; init; }
    public string IndexFamilyCode { get; init; } = string.Empty;
    public string IndexFamilyName { get; init; } = string.Empty;
}

public sealed class QuantitySurveyEscalationFormulaDto
{
    public Guid Id { get; init; }
    public Guid FormulaKey { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public Guid ContractId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractTitle { get; init; } = string.Empty;
    public string ContractClauseReference { get; init; } = string.Empty;
    public QuantitySurveyEscalationFormula FormulaType { get; init; }
    public DateTime BaseDate { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public Guid AuthorityRoleId { get; init; }
    public string AuthorityRoleName { get; init; } = string.Empty;
    public Guid ConfigurationProfileId { get; init; }
    public Guid ConfigurationDecisionId { get; init; }
    public Guid ApprovalWorkflowDefinitionId { get; init; }
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string EvidenceLabel { get; init; } = string.Empty;
    public Guid? SupersedesFormulaId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid PreparedById { get; init; }
    public DateTime PreparedAt { get; init; }
    public Guid? SubmittedById { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string SnapshotHash { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyEscalationFormulaComponentDto> Components { get; init; } = [];
}

public sealed class QuantitySurveyEscalationFormulaPageDto
{
    public IReadOnlyList<QuantitySurveyEscalationFormulaDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class QuantitySurveyEscalationRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public DateTime CreatedAt { get; init; }
}
