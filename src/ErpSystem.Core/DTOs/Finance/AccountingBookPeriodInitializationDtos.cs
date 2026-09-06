namespace ErpSystem.Core.DTOs.Finance;

public sealed class AccountingBookPeriodDto
{
    public Guid Id { get; set; }
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public Guid FiscalPeriodId { get; set; }
    public string FiscalPeriodCode { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PendingStatus { get; set; }
    public string? PendingReason { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime? RequestedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CreateAccountingBookPeriodDto
{
    public Guid FiscalPeriodId { get; set; }
    public string InitialStatus { get; set; } = "Future";
}

public sealed class RequestAccountingBookPeriodTransitionDto
{
    public string TargetStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DecideAccountingBookPeriodTransitionDto
{
    public string Reason { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class AccountingBookInitializationLineDto
{
    public Guid AccountId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal BaseBookSignedBalance { get; set; }
    public decimal OpeningAdjustment { get; set; }
}

public sealed class ConfigureAccountingBookInitializationDto
{
    public string Mode { get; set; } = string.Empty;
    public DateTime CutoffDate { get; set; }
    public Guid? SourceAccountingBookId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public IReadOnlyCollection<AccountingBookInitializationLineDto> Lines { get; set; } = Array.Empty<AccountingBookInitializationLineDto>();
    public string? RowVersion { get; set; }
}

public sealed class AccountingBookInitializationDto
{
    public Guid Id { get; set; }
    public Guid AccountingBookId { get; set; }
    public int Version { get; set; }
    public Guid? SupersedesInitializationId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CutoffDate { get; set; }
    public Guid? SourceAccountingBookId { get; set; }
    public string? SourceAccountingBookCode { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public int RequiredAccountCount { get; set; }
    public int CoveredAccountCount { get; set; }
    public bool IsBalanced { get; set; }
    public bool IsCoverageComplete { get; set; }
    public string EvidenceFingerprint { get; set; } = string.Empty;
    public string ReconciliationFingerprint { get; set; } = string.Empty;
    public Guid PreparedByUserId { get; set; }
    public DateTime PreparedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyCollection<AccountingBookInitializationLineDto> Lines { get; set; } = Array.Empty<AccountingBookInitializationLineDto>();
}

public sealed class DecideAccountingBookInitializationDto
{
    public string Reason { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class AccountingBookInitializationPreparationDto
{
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public DateTime CutoffDate { get; set; }
    public Guid? SourceAccountingBookId { get; set; }
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public IReadOnlyCollection<AccountingBookInitializationPreparationLineDto> Accounts { get; set; } = Array.Empty<AccountingBookInitializationPreparationLineDto>();
}

public sealed class AccountingBookInitializationPreparationLineDto
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid AccountClassificationId { get; set; }
    public string AccountClassificationCode { get; set; } = string.Empty;
    public decimal AuthoritativeSignedBalance { get; set; }
}

public sealed class AccountingBookActivationReadinessDto
{
    public bool IsReady { get; set; }
    public IReadOnlyCollection<string> Blockers { get; set; } = Array.Empty<string>();
    public string? InitializationFingerprint { get; set; }
    public int RequiredPeriodCount { get; set; }
    public int ReadyPeriodCount { get; set; }
}
