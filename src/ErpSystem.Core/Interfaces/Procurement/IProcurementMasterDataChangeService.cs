using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementMasterDataChangeService
{
    Task<IReadOnlyList<ProcurementMasterDataResourceDefinitionDto>> GetRegistryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangeSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementMasterDataPolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataPolicyDto> SavePolicyAsync(Guid? id, SaveProcurementMasterDataPolicyRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataPolicyDto> ActivatePolicyAsync(Guid id, ProcurementMasterDataPolicyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataPolicyDto> RetirePolicyAsync(Guid id, ProcurementMasterDataPolicyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangePageDto> SearchAsync(ProcurementMasterDataChangeSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangeDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangeDto> SaveDraftAsync(Guid? id, SaveProcurementMasterDataChangeRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangeDto> SubmitAsync(Guid id, ProcurementMasterDataChangeLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangeDto> RevalidateAsync(Guid id, ProcurementMasterDataChangeLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangeDto> ApproveAsync(Guid id, ProcurementMasterDataChangeDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangeDto> RejectAsync(Guid id, ProcurementMasterDataChangeDecisionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangeDto> ApplyAsync(Guid id, ProcurementMasterDataChangeLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataChangeDto> CancelAsync(Guid id, ProcurementMasterDataChangeLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementMasterDataDirectMutationDecisionDto> CheckDirectMutationAsync(IReadOnlyCollection<ProcurementMasterDataResourceType> resourceTypes, Guid? targetId, string sourceReference, string correlationId, CancellationToken cancellationToken = default);
}

public class ProcurementMasterDataChangeException(string message) : Exception(message);
public sealed class ProcurementMasterDataChangeNotFoundException(string message) : ProcurementMasterDataChangeException(message);
public sealed class ProcurementMasterDataChangeConflictException(string message) : ProcurementMasterDataChangeException(message);
public sealed class ProcurementMasterDataChangeAuthorizationException(string message) : ProcurementMasterDataChangeException(message);
public sealed class ProcurementMasterDataChangeValidationException(string code, string message) : ProcurementMasterDataChangeException(message)
{
    public string Code { get; } = code;
}
