using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAccountSegmentIdentityService
{
    Task<AccountSegmentIdentityResultDto> ValidateAndComposeAsync(
        Guid tenantId,
        IReadOnlyCollection<AccountSegmentValueCreateDto> values,
        string? clientAccountNumber = null,
        Guid? existingAccountId = null,
        CancellationToken cancellationToken = default);

    Task<AccountSegmentIdentityResultDto> ResolveProvisioningIdentityAsync(
        Guid tenantId,
        string naturalAccountCode,
        Guid? existingAccountId = null,
        CancellationToken cancellationToken = default);

    Task<AccountSegmentReadinessDto> GetReadinessAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default);
}
