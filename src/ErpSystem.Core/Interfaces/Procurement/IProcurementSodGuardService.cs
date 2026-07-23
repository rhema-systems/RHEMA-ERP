using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSodGuardService
{
    Task<ProcurementSodCoverageDto> GetCoverageAsync(DateTime atUtc, CancellationToken cancellationToken = default);
    Task<ProcurementSodProvisionResultDto> ApplyRequiredControlsAsync(Guid policySetId, ApplyRequiredProcurementSodControlsRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSodGuardDecisionDto> CheckAsync(ProcurementSodGuardRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSodGuardDecisionDto> EnforceAsync(ProcurementSodGuardRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSodBypassAuditDto>> GetBlockedAttemptsAsync(int take = 50, CancellationToken cancellationToken = default);
}
