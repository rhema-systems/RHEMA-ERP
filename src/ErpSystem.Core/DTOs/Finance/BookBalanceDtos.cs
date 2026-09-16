namespace ErpSystem.Core.DTOs.Finance;

public sealed record AccountBookBalanceDto(
    Guid AccountId, Guid AccountingBookId, string AccountingBookCode, Guid FiscalPeriodId,
    string FunctionalCurrencyCode, decimal OpeningSignedBalance, decimal PeriodDebits,
    decimal PeriodCredits, decimal PeriodNetMovement, decimal ClosingSignedBalance,
    decimal YearToDateDebits, decimal YearToDateCredits, decimal YearToDateNetMovement,
    int TransactionCount, DateTime? LastTransactionDate);

public sealed record AccountCurrencyExposureDto(
    Guid AccountId, Guid AccountingBookId, string AccountingBookCode, string FunctionalCurrencyCode,
    string TransactionCurrencyCode, decimal SignedForeignBalance, decimal SignedFunctionalBalance,
    int TransactionCount, DateTime? FirstTransactionDate, DateTime? LastTransactionDate);

public sealed record BookBalanceInquiryDto(
    Guid AccountingBookId, string AccountingBookCode, IReadOnlyList<AccountBookBalanceDto> Balances,
    IReadOnlyList<AccountCurrencyExposureDto> Exposures);

public sealed record BookBalanceReconciliationRequestDto(
    string AccountingBookCode, bool Apply, string? Reason, string? IdempotencyKey,
    Guid? ApprovedByUserId);

public sealed record BookBalanceReconciliationDto(
    Guid AccountingBookId, string AccountingBookCode, bool Applied, int BalanceDriftCount,
    int ExposureDriftCount, int PrimaryCompatibilityDriftCount, decimal AbsoluteDrift,
    string SourceFingerprint, Guid? RebuildRunId);
