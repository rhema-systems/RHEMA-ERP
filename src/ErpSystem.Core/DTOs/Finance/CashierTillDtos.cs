using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Opens physical custody without posting an accounting entry. The opening float is a count
/// baseline; posted Finance activity remains the sole source for subsequent movement.
/// </summary>
public sealed class OpenCashierTillSessionDto
{
    public Guid LiquidityAccountId { get; set; }
    public DateTime BusinessDate { get; set; }
    public decimal OpeningFloatAmount { get; set; }
    public string? OpeningNotes { get; set; }
    public Guid? OpeningEvidenceFileId { get; set; }
}

/// <summary>
/// Replaces the mutable opening evidence on an unused custody session. Till, cashier,
/// business date, currency, session number, and opening timestamp remain immutable.
/// </summary>
public sealed class UpdateCashierTillOpeningDto
{
    public decimal OpeningFloatAmount { get; set; }
    public string? OpeningNotes { get; set; }
    public Guid? OpeningEvidenceFileId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CancelCashierTillSessionDto
{
    public string Reason { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CashierTillCountLineInputDto
{
    public decimal Denomination { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Submits a reproducible denomination count. Counted cash is calculated by the server from
/// denomination multiplied by quantity; clients cannot send a competing total.
/// </summary>
public sealed class SubmitCashierTillCountDto
{
    public IReadOnlyList<CashierTillCountLineInputDto> CountLines { get; set; } =
        Array.Empty<CashierTillCountLineInputDto>();
    public string? VarianceReason { get; set; }
    public Guid? ClosingEvidenceFileId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReviewCashierTillSessionDto
{
    public string? Comments { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReopenCashierTillSessionDto
{
    public string Reason { get; set; } = string.Empty;
    public Guid? OpeningEvidenceFileId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CashierTillCountLineDto
{
    public Guid Id { get; set; }
    public decimal Denomination { get; set; }
    public int Quantity { get; set; }
    public decimal LineAmount { get; set; }
}

public sealed class CashierTillCustodyEntryDto
{
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public DateTime EntryDate { get; set; }
    public LiquidityEntryType EntryType { get; set; }
    public LiquidityEntryDirection Direction { get; set; }
    public decimal SignedAmount { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
}

public sealed class CashierTillSessionDto
{
    public Guid Id { get; set; }
    public string SessionNumber { get; set; } = string.Empty;
    public Guid LiquidityAccountId { get; set; }
    public string TillCode { get; set; } = string.Empty;
    public string TillName { get; set; } = string.Empty;
    public DateTime BusinessDate { get; set; }
    public string Currency { get; set; } = "GHS";
    public Guid CashierUserId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public CashierTillSessionStatus Status { get; set; }
    public decimal OpeningFloatAmount { get; set; }
    public string? OpeningNotes { get; set; }
    public Guid? OpeningEvidenceFileId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ActivityCutoffAt { get; set; }
    public decimal TransactionMovementAmount { get; set; }
    public decimal DepositedAmount { get; set; }
    public decimal ExpectedClosingAmount { get; set; }
    public decimal CountedClosingAmount { get; set; }
    public decimal VarianceAmount { get; set; }
    public decimal VarianceApprovalThresholdAmount { get; set; }
    public bool VarianceExceedsThreshold { get; set; }
    public int CustodyEntryCount { get; set; }
    public string? VarianceReason { get; set; }
    public Guid? ClosingEvidenceFileId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewComments { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancellationReason { get; set; }
    public bool OpeningDetailsMutable { get; set; }
    public string? OpeningDetailsLockReason { get; set; }
    public Guid? CorrectsSessionId { get; set; }
    public string? CorrectionReason { get; set; }
    public IReadOnlyList<CashierTillCountLineDto> CountLines { get; set; } =
        Array.Empty<CashierTillCountLineDto>();
    public IReadOnlyList<CashierTillCustodyEntryDto> CustodyEntries { get; set; } =
        Array.Empty<CashierTillCustodyEntryDto>();
    public string RowVersion { get; set; } = string.Empty;
}
