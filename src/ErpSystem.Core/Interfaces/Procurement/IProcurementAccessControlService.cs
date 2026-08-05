using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementAccessControlService
{
    Task<ProcurementAccessReadinessDto> GetReadinessAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementAccessRoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementAccessPermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementAccessUserOptionDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementAccessWarehouseOptionDto>> GetWarehousesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementAccessLocationOptionDto>> GetLocationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementResponsibilityAssignmentDto>> GetAssignmentsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementResponsibilityAssignmentDto> SaveAssignmentAsync(Guid? id, SaveProcurementResponsibilityAssignmentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementCommitteeDto>> GetCommitteesAsync(CancellationToken cancellationToken = default);
    Task<ProcurementCommitteeDto> UpdateCommitteeAsync(Guid id, UpdateProcurementCommitteeRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementCommitteeMemberDto> AddCommitteeMemberAsync(Guid committeeId, SaveProcurementCommitteeMemberRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task RemoveCommitteeMemberAsync(Guid committeeId, Guid memberId, RemoveProcurementCommitteeMemberRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementAccessWorkflowDto>> GetWorkflowsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementAccessCapabilityDecisionDto> CheckCapabilityAsync(ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementAccessCapabilityDecisionDto> EnforceCapabilityAsync(ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementAccessAuditDto>> GetAuditAsync(int take = 100, CancellationToken cancellationToken = default);
}

public sealed class ProcurementAccessNotFoundException(string message) : InvalidOperationException(message);
public sealed class ProcurementAccessConflictException(string message) : InvalidOperationException(message);
public sealed class ProcurementAccessValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementAccessAuthorizationException(string message) : UnauthorizedAccessException(message);
