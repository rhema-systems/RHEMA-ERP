using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// HR → FINANCE POSTING — the command an HR area hands the adapter
// ============================================================================

/// <summary>
/// One line of an HR money event, stated in account ROLES. The adapter turns roles into Finance
/// account ids from the tenant's mappings; an HR area never names a GL account itself.
/// </summary>
/// <param name="Role">Which mapped account this line hits.</param>
/// <param name="IsDebit">Debit or credit side.</param>
/// <param name="Amount">Positive, in the command's transaction currency (functional when none is set).</param>
/// <param name="Description">Line narration as it will read in Finance.</param>
public sealed record HrFinancePostingLine(
    HrFinanceAccountRole Role,
    bool IsDebit,
    decimal Amount,
    string Description);

/// <summary>
/// What an HR area asks Finance to record. Built only by <c>HrFinancePostingCommandFactory</c>,
/// so every event's lines come from one place and a retry rebuilds exactly what the trigger built.
/// </summary>
public sealed class HrFinancePostingCommand
{
    public string EventCode { get; init; } = string.Empty;
    public Guid SourceDocumentId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public Guid? EmployeeId { get; init; }
    public string Description { get; init; } = string.Empty;

    /// <summary>The source document's own date; used when the rule posts on document date.</summary>
    public DateTime? SourceDate { get; init; }

    /// <summary>
    /// Null means the amounts are already in the tenant's functional currency. Otherwise every
    /// line amount is in this currency and the adapter attaches Finance's rate record as evidence.
    /// </summary>
    public string? TransactionCurrencyCode { get; init; }

    public IReadOnlyList<HrFinancePostingLine> Lines { get; init; } = Array.Empty<HrFinancePostingLine>();

    /// <summary>
    /// Set by the factory when this instance has nothing to post (zero amount, settled through
    /// payroll). The adapter records a <see cref="HrFinancePostingStatus.Skipped"/> row and stops.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// For events whose document names no payment method, the lines to post when the rule's
    /// settlement route is <see cref="HrFinanceSettlementRoute.Direct"/>. Null means the command is
    /// route-independent and <see cref="Lines"/> always applies.
    /// </summary>
    public IReadOnlyList<HrFinancePostingLine>? RouteDirectLines { get; init; }

    /// <summary>Lines when the route is <see cref="HrFinanceSettlementRoute.Payroll"/>; empty = skip with <see cref="RoutePayrollSkipReason"/>.</summary>
    public IReadOnlyList<HrFinancePostingLine> RoutePayrollLines { get; init; } = Array.Empty<HrFinancePostingLine>();

    public string? RoutePayrollSkipReason { get; init; }

    /// <summary>
    /// For a <see cref="HrFinancePostingKind.VendorInvoice"/> event: the Procurement supplier the
    /// invoice is raised to. Null means the payee is not a supplier and the factory sets a skip reason.
    /// </summary>
    public Guid? PayeeSupplierId { get; init; }

    /// <summary>The payee's name as HR recorded it, for the invoice notes and the register.</summary>
    public string? PayeeName { get; init; }
}

/// <summary>What the adapter hands back to the HR area after an event ran.</summary>
public sealed record HrFinancePostingOutcome
{
    public Guid RecordId { get; init; }
    public HrFinancePostingStatus Status { get; init; }
    public Guid? PostingEventId { get; init; }
    public Guid? JournalEntryId { get; init; }
    public string? JournalEntryNumber { get; init; }
    public string? StatusReason { get; init; }
    public bool WasDuplicate { get; init; }
}

// ============================================================================
// SETTINGS — account roles and event rules
// ============================================================================

public sealed class HrFinanceAccountMappingDto
{
    public HrFinanceAccountRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleDescription { get; set; } = string.Empty;
    /// <summary>Asset / Liability / Expense — what the chosen account must be.</summary>
    public string RequiredAccountType { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    /// <summary>Live from Finance at read time, not the snapshot: an account deactivated since it was mapped shows false.</summary>
    public bool? AccountIsActive { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpsertHrFinanceAccountMappingDto
{
    [Required]
    public HrFinanceAccountRole Role { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public sealed class HrFinancePostingRuleDto
{
    public string EventCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public IReadOnlyList<HrFinanceAccountRole> DebitRoles { get; set; } = Array.Empty<HrFinanceAccountRole>();
    public IReadOnlyList<HrFinanceAccountRole> CreditRoles { get; set; } = Array.Empty<HrFinanceAccountRole>();
    public bool IsEnabled { get; set; }
    public bool PostOnActionDate { get; set; } = true;
    /// <summary>Whether this event's settlement is decided by the rule (true) or by the document's own payment method (false).</summary>
    public bool SupportsSettlementRoute { get; set; }
    /// <summary>Journal, or an AP vendor invoice (slice 5).</summary>
    public HrFinancePostingKind Kind { get; set; } = HrFinancePostingKind.Journal;
    public string KindName => Kind.ToString();
    /// <summary>The effective route: the saved one, else the catalogue default.</summary>
    public HrFinanceSettlementRoute? SettlementRoute { get; set; }
    public HrFinanceSettlementRoute? DefaultSettlementRoute { get; set; }
    public string? Notes { get; set; }
    /// <summary>True when every role the event uses is mapped to an active account of the right type.</summary>
    public bool IsReady { get; set; }
    public IReadOnlyList<HrFinanceAccountRole> MissingRoles { get; set; } = Array.Empty<HrFinanceAccountRole>();
    /// <summary>How many register rows sit Unposted or Failed for this event — the back-fill queue.</summary>
    public int PendingCount { get; set; }
    public int PostedCount { get; set; }
}

public sealed class UpsertHrFinancePostingRuleDto
{
    [Required]
    [MaxLength(60)]
    public string EventCode { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
    public bool PostOnActionDate { get; set; } = true;

    /// <summary>Only honoured for events that support a settlement route; null keeps the catalogue default.</summary>
    public HrFinanceSettlementRoute? SettlementRoute { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public sealed class HrFinancePostingSettingsDto
{
    /// <summary>Finance's functional currency; every HR posting is stated in it.</summary>
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    /// <summary>The single accounting book HR posts to, resolved from Finance settings.</summary>
    public string? AccountingBookCode { get; set; }
    /// <summary>Non-null when Finance's book setting cannot be resolved; postings will refuse until it is.</summary>
    public string? AccountingBookProblem { get; set; }
    public IReadOnlyList<HrFinanceAccountMappingDto> Mappings { get; set; } = Array.Empty<HrFinanceAccountMappingDto>();
    public IReadOnlyList<HrFinancePostingRuleDto> Rules { get; set; } = Array.Empty<HrFinancePostingRuleDto>();
}

// ============================================================================
// REGISTER
// ============================================================================

public sealed class HrFinancePostingLineSnapshotDto
{
    public HrFinanceAccountRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

public sealed class HrFinancePostingRecordDto
{
    public Guid Id { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? TransactionCurrencyCode { get; set; }
    public decimal? TransactionAmount { get; set; }
    public DateTime PostingDate { get; set; }
    public HrFinancePostingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? StatusReason { get; set; }
    public string? AccountingBookCode { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
    public DateTime? PostedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public IReadOnlyList<HrFinancePostingLineSnapshotDto> Lines { get; set; } = Array.Empty<HrFinancePostingLineSnapshotDto>();
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public string? ReversalJournalEntryNumber { get; set; }
    public DateTime? ReversedAt { get; set; }
    public string? ReversalReason { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Journal or AP vendor invoice (slice 5).</summary>
    public HrFinancePostingKind Kind { get; set; } = HrFinancePostingKind.Journal;
    public string KindName => Kind.ToString();
    /// <summary>For an AP row: the Finance vendor invoice this event created.</summary>
    public Guid? VendorInvoiceId { get; set; }
    public string? VendorInvoiceNumber { get; set; }
    /// <summary>For an AP row: Finance's invoice status when last read (pull sync).</summary>
    public string? ExternalStatus { get; set; }
    public DateTime? ExternalStatusAt { get; set; }
    /// <summary>True for Unposted, Failed and Reversed rows: the register can post (again) — a re-post after a reversal is the next generation.</summary>
    public bool CanRetry => Status is HrFinancePostingStatus.Unposted or HrFinancePostingStatus.Failed or HrFinancePostingStatus.Reversed;
    /// <summary>True for Posted rows: the register can reverse through Finance (an AP invoice only while Finance still holds it as a draft).</summary>
    public bool CanReverse => Status == HrFinancePostingStatus.Posted;
    /// <summary>True for a posted AP row: Finance's status and the payment voucher can be pulled.</summary>
    public bool CanRefresh => Kind == HrFinancePostingKind.VendorInvoice && Status == HrFinancePostingStatus.Posted && VendorInvoiceId.HasValue;
}

public sealed class HrFinancePostingRecordQueryDto
{
    public HrFinancePostingStatus? Status { get; set; }
    public string? EventCode { get; set; }
    public Guid? EmployeeId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary>Matches the source reference or the journal number.</summary>
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class HrFinancePostingSummaryDto
{
    public int Posted { get; set; }
    public int Failed { get; set; }
    public int Unposted { get; set; }
    public int Skipped { get; set; }
    public int Reversed { get; set; }
    /// <summary>Functional-currency total of Posted rows that have not been reversed — a reconciliation figure, not a balance.</summary>
    public decimal PostedAmount { get; set; }
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
}

public sealed class ReverseHrFinancePostingDto
{
    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
