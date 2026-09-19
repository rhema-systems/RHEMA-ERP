using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAccountingBookApplicabilityService
{
    Task<IReadOnlyList<AccountingBookApplicabilityPolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountingBookApplicabilityEligibleBookDto>> GetEligibleBooksAsync(CancellationToken cancellationToken = default);
    Task<AccountingBookApplicabilityPolicyDto> CreateDraftAsync(SaveAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookApplicabilityPolicyDto> UpdateDraftAsync(Guid id, SaveAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookApplicabilityPolicyDto> SubmitAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookApplicabilityPolicyDto> ApproveAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookApplicabilityPolicyDto> RejectAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookApplicabilityPolicyDto> RetireAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookApplicabilityPolicyDto> ApproveRetirementAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookApplicabilityPolicyDto> RejectRetirementAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookSelectionDto> ResolveAsync(ResolveAccountingBookApplicabilityDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookSelectionDto> FreezeAsync(FreezeAccountingBookSelectionDto request, CancellationToken cancellationToken = default);
}
