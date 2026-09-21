using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAccountingBookPeriodService
{
    Task<IReadOnlyList<AccountingBookPeriodDto>> GetAsync(Guid accountingBookId, CancellationToken cancellationToken = default);
    Task<AccountingBookPeriodDto> CreateAsync(Guid accountingBookId, CreateAccountingBookPeriodDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookPeriodDto> RequestTransitionAsync(Guid accountingBookId, Guid id, RequestAccountingBookPeriodTransitionDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookPeriodDto> ApproveAsync(Guid accountingBookId, Guid id, DecideAccountingBookPeriodTransitionDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookPeriodDto> RejectAsync(Guid accountingBookId, Guid id, DecideAccountingBookPeriodTransitionDto request, CancellationToken cancellationToken = default);
}

public interface IAccountingBookInitializationService
{
    Task<AccountingBookInitializationDto?> GetAsync(Guid accountingBookId, CancellationToken cancellationToken = default);
    Task<AccountingBookInitializationPreparationDto> PrepareAsync(Guid accountingBookId, string mode, DateTime cutoffDate, Guid? sourceAccountingBookId, CancellationToken cancellationToken = default);
    Task<AccountingBookInitializationDto> ConfigureAsync(Guid accountingBookId, ConfigureAccountingBookInitializationDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookInitializationDto> SubmitAsync(Guid accountingBookId, CancellationToken cancellationToken = default);
    Task<AccountingBookInitializationDto> ApproveAsync(Guid accountingBookId, DecideAccountingBookInitializationDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookInitializationDto> RejectAsync(Guid accountingBookId, DecideAccountingBookInitializationDto request, CancellationToken cancellationToken = default);
    Task<AccountingBookActivationReadinessDto> GetReadinessAsync(Guid accountingBookId, CancellationToken cancellationToken = default);
    Task<AccountingBookInitializationEvidenceValidationDto> ValidateCurrentApprovedEvidenceAsync(Guid accountingBookId, CancellationToken cancellationToken = default);
    Task<DeltaBookStructurePreparationDto> EnsureDeltaStructureAsync(Guid accountingBookId, CancellationToken cancellationToken = default);
    Task<int> ReplayHistoricalParallelTransactionsAsync(Guid accountingBookId, CancellationToken cancellationToken = default);
}
