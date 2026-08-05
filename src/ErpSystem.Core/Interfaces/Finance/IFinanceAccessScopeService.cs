using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Maintains and enforces Finance data scopes independently from identity permissions.
/// A null permitted-ID collection means the current actor has tenant-wide access; an empty
/// collection means enforcement is active and no matching scoped resource is permitted.
/// </summary>
public interface IFinanceAccessScopeService
{
    Task<IReadOnlyList<FinanceAccessScopeGrantDto>> GetGrantsAsync(
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceAccessUserOptionDto>> GetUsersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceAccessBankAccountOptionDto>> GetBankAccountsAsync(
        CancellationToken cancellationToken = default);

    Task<FinanceAccessScopeGrantDto> SaveGrantAsync(
        Guid? id,
        SaveFinanceAccessScopeGrantDto dto,
        CancellationToken cancellationToken = default);

    Task DeactivateGrantAsync(
        Guid id,
        string reason,
        string rowVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>?> GetPermittedBankAccountIdsAsync(
        FinanceAccessLevel requiredLevel,
        CancellationToken cancellationToken = default);

    Task EnsureBankAccountAccessAsync(
        Guid? bankAccountId,
        FinanceAccessLevel requiredLevel,
        CancellationToken cancellationToken = default);
}
