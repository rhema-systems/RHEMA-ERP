using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementAccessReadinessDto
{
    public int RequiredRoleCount { get; set; }
    public int ConfiguredRoleCount { get; set; }
    public int RequiredPermissionCount { get; set; }
    public int ConfiguredPermissionCount { get; set; }
    public int RequiredCommitteeCount { get; set; }
    public int ConfiguredCommitteeCount { get; set; }
    public int ReadyCommitteeCount { get; set; }
    public int RequiredWorkflowCount { get; set; }
    public int ConfiguredWorkflowCount { get; set; }
    public int PublishedWorkflowCount { get; set; }
    public int ActiveAssignmentCount { get; set; }
    public bool InternalAuditIsReadOnly { get; set; }
    public bool IsReadyForUat { get; set; }
    public List<string> Issues { get; set; } = new();
}

public sealed class ProcurementAccessRoleDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public bool IsReadOnly { get; set; }
    public int RequiredPermissionCount { get; set; }
    public int ConfiguredPermissionCount { get; set; }
    public List<string> PermissionCodes { get; set; } = new();
    public List<string> MissingPermissionCodes { get; set; } = new();
    public List<string> UnexpectedMutationPermissions { get; set; } = new();
}

public sealed class ProcurementAccessPermissionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsMutation { get; set; }
    public bool IsWarehouseScoped { get; set; }
    public bool IsConfigured { get; set; }
}

public sealed class ProcurementAccessUserOptionDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ProcurementAccessWarehouseOptionDto
{
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ProcurementAccessLocationOptionDto
{
    public Guid LocationId { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ProcurementResponsibilityAssignmentDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleDisplayName { get; set; } = string.Empty;
    public ProcurementWarehouseScopeMode WarehouseScopeMode { get; set; }
    public List<ProcurementAccessWarehouseOptionDto> Warehouses { get; set; } = new();
    public ProcurementLocationScopeMode LocationScopeMode { get; set; }
    public List<ProcurementAccessLocationOptionDto> Locations { get; set; } = new();
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SaveProcurementResponsibilityAssignmentRequest
{
    [Required] public Guid UserId { get; set; }
    [Required, StringLength(100)] public string RoleName { get; set; } = string.Empty;
    public ProcurementWarehouseScopeMode WarehouseScopeMode { get; set; }
    public List<Guid> WarehouseIds { get; set; } = new();
    public ProcurementLocationScopeMode LocationScopeMode { get; set; }
    public List<Guid> LocationIds { get; set; } = new();
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}

public sealed class ProcurementCommitteeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProcurementCommitteeType CommitteeType { get; set; }
    public ProcurementCommitteeStatus Status { get; set; }
    public int RequiredQuorum { get; set; }
    public int ActiveVotingMemberCount { get; set; }
    public bool MeetsQuorum { get; set; }
    public string RequiredRoleName { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementCommitteeMemberDto> Members { get; set; } = new();
}

public sealed class ProcurementCommitteeMemberDto
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public ProcurementCommitteeMemberKind MemberKind { get; set; }
    public bool IsVoting { get; set; }
    public bool IsActive { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class UpdateProcurementCommitteeRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Range(1, 50)] public int RequiredQuorum { get; set; }
    public ProcurementCommitteeStatus Status { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class SaveProcurementCommitteeMemberRequest
{
    [Required] public Guid AssignmentId { get; set; }
    public ProcurementCommitteeMemberKind MemberKind { get; set; }
    public bool IsVoting { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class RemoveProcurementCommitteeMemberRequest
{
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementAccessWorkflowDto
{
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string EntityTypeCode { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    public int? Version { get; set; }
    public string Status { get; set; } = "Missing";
    public bool IsPublished { get; set; }
    public string InitiatorRoleName { get; set; } = string.Empty;
    public string ApprovalRoleName { get; set; } = string.Empty;
    public string SourceDecisionKeys { get; set; } = "DEC-003,DEC-004";
}

public sealed class ProcurementAccessCapabilityRequest
{
    [Required, StringLength(100)] public string PermissionCode { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public bool RequireLocationScope { get; set; }
    [StringLength(50)] public string? CommitteeCode { get; set; }
    [Required, StringLength(100)] public string SourceType { get; set; } = string.Empty;
    [Required, StringLength(100)] public string SourceReference { get; set; } = string.Empty;
}

public sealed class ProcurementAccessCapabilityDecisionDto
{
    public bool Allowed { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid TenantId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public string? CommitteeCode { get; set; }
    public List<Guid> MatchedAssignmentIds { get; set; } = new();
    public List<string> MatchedRoles { get; set; } = new();
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; }
}

public sealed class ProcurementAccessAuditDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}
