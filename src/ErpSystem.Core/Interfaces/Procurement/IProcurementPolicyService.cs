using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementPolicyService
{
    Task<ProcurementConfigurationPagedResult<ProcurementPolicySetSummaryDto>> GetPolicySetsAsync(ProcurementPolicySetListRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementPolicyRoleOptionDto>> GetRoleOptionsAsync(Guid? workflowDefinitionId = null, CancellationToken cancellationToken = default);
    Task<ProcurementPolicySetDto> GetPolicySetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementPolicySetDto?> GetEffectivePolicySetAsync(string code, DateTime atUtc, CancellationToken cancellationToken = default);
    Task<ProcurementPolicySetDto> CreatePolicySetAsync(CreateProcurementPolicySetRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPolicySetDto> UpdatePolicySetAsync(Guid id, UpdateProcurementPolicySetRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPolicyRuleDto> SaveRuleAsync(Guid policySetId, Guid? ruleId, SaveProcurementPolicyRuleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteRuleAsync(Guid policySetId, ProcurementPolicyRuleKind kind, Guid ruleId, DeleteProcurementPolicyRuleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPolicyValidationResultDto> ValidatePolicySetAsync(Guid id, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPolicySetDto> PublishPolicySetAsync(Guid id, ProcurementPolicyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPolicySetDto> RetirePolicySetAsync(Guid id, ProcurementPolicyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementPolicySetDto> CloneDraftAsync(Guid id, CloneProcurementPolicySetRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(Guid id, ProcurementPolicyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementPolicyRevisionDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
