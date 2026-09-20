using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAccountBookCurrencyPolicyService
{
    Task<IReadOnlyList<AccountBookCurrencyPolicyDto>> GetForAccountAsync(
        Guid accountId, CancellationToken cancellationToken = default);

    Task<AccountBookCurrencyPolicyDto> SaveAsync(
        Guid accountId,
        Guid accountCurrencyLinkId,
        Guid accountingBookId,
        SaveAccountBookCurrencyPolicyDto request,
        CancellationToken cancellationToken = default);

    Task<AccountBookCurrencyPolicyDto> ApproveAsync(
        Guid accountId,
        Guid policyId,
        DecideAccountBookCurrencyPolicyDto request,
        CancellationToken cancellationToken = default);

    Task<AccountBookCurrencyPolicyDto> RejectAsync(
        Guid accountId,
        Guid policyId,
        DecideAccountBookCurrencyPolicyDto request,
        CancellationToken cancellationToken = default);
}
