using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementComplianceDecisionService
{
    Task<IReadOnlyList<ProcurementCompliancePolicyOptionDto>> GetEffectivePolicyOptionsAsync(
        DateTime atUtc,
        CancellationToken cancellationToken = default);

    Task<ProcurementComplianceDecisionDto> EvaluateAsync(
        ProcurementComplianceDecisionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
