using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Read-only, book-blind replay verifier for owner services. This contract cannot execute, approve or mutate
/// accounting; it only verifies owner-held coordinates against durable Finance authority.
/// </summary>
public interface IFinanceProducerReplayVerificationService
{
    Task<FinanceProducerReplayVerificationResultDto> VerifyPostedAsync(
        Guid accountingEventId,
        FinanceProducerReplayVerificationRequestDto request,
        CancellationToken cancellationToken = default);
}
