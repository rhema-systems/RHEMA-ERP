using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementSodGuardRequest
{
    [Required, StringLength(50)] public string ControlCode { get; set; } = string.Empty;
    [Required, StringLength(100)] public string SourceType { get; set; } = string.Empty;
    [Required, StringLength(200)] public string SourceReference { get; set; } = string.Empty;
    [MinLength(1)] public List<Guid> ProhibitedActorUserIds { get; set; } = new();
}

public sealed class ApplyRequiredProcurementSodControlsRequest
{
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class ProcurementSodRequiredControlDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string InitiatorRole { get; init; } = string.Empty;
    public string ConflictingRole { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public string SourceRequirement { get; init; } = string.Empty;
    public string SourceDecisionKey { get; init; } = string.Empty;
    public bool IsConfigured { get; init; }
    public bool IsEffective { get; init; }
    public bool IsHardStop { get; init; }
    public Guid? RuleId { get; init; }
    public string? RuleCode { get; init; }
    public ProcurementSodEnforcement? Enforcement { get; init; }
    public Guid? SourceRuleId { get; init; }
    public ProcurementPolicyOverrideAction? OverrideAction { get; init; }
    public string? ConfigurationIssue { get; init; }
}

public sealed class ProcurementSodCoverageDto
{
    public DateTime EvaluatedAtUtc { get; init; }
    public Guid CurrentActorUserId { get; init; }
    public IReadOnlyList<string> CurrentActorRoles { get; init; } = Array.Empty<string>();
    public string Status { get; init; } = string.Empty;
    public bool IsComplete { get; init; }
    public Guid? PolicySetId { get; init; }
    public string? PolicyCode { get; init; }
    public string? PolicyName { get; init; }
    public int? PolicyVersion { get; init; }
    public IReadOnlyList<ProcurementSodRequiredControlDto> Controls { get; init; } = Array.Empty<ProcurementSodRequiredControlDto>();
}

public sealed class ProcurementSodGuardDecisionDto
{
    public Guid DecisionId { get; init; } = Guid.NewGuid();
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; init; }
    public bool Allowed { get; init; }
    public bool IsHardStop { get; init; }
    public bool WasAudited { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public IReadOnlyList<string> ActorRoles { get; init; } = Array.Empty<string>();
    public string ControlCode { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string SourceReference { get; init; } = string.Empty;
    public Guid? PolicySetId { get; init; }
    public string? PolicyCode { get; init; }
    public int? PolicyVersion { get; init; }
    public Guid? RuleId { get; init; }
    public string? RuleCode { get; init; }
    public string? SourceDecisionKey { get; init; }
    public string? SourceRequirement { get; init; }
}

public sealed class ProcurementSodProvisionResultDto
{
    public Guid PolicySetId { get; init; }
    public int CreatedCount { get; init; }
    public int ExistingCount { get; init; }
    public IReadOnlyList<string> CreatedControlCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExistingControlCodes { get; init; } = Array.Empty<string>();
}

public sealed class ProcurementSodBypassAuditDto
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string ControlCode { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string SourceReference { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Guid? RuleId { get; init; }
    public string? RuleCode { get; init; }
}
