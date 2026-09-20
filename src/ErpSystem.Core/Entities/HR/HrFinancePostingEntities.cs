using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

// ============================================================================
// HR → FINANCE POSTING  (HR finish plan lane 8; HR-FINANCE-POSTING-DESIGN.md)
// ============================================================================
//
// Three tables, one idea: HR owns its money events and the record of whether each one reached
// Finance; Finance owns the journal. Nothing here is a ledger — the amounts on a posting record
// are what HR SENT, kept so the register can be reconciled against Finance, never summed as a
// balance.

/// <summary>
/// Which Finance account plays one <see cref="HrFinanceAccountRole"/> for this tenant.
/// One row per role; the account is chosen through HR's <c>api/hr/finance-accounts</c> read door.
/// </summary>
/// <remarks>
/// ⚠ The FK to <c>Accounts</c> is <c>Restrict</c>: Finance cannot delete an account HR posts to
/// without HR first re-mapping the role — the same protection an organisation unit's cost code has.
/// </remarks>
public class HrFinanceAccountMapping : TenantEntity
{
    public HrFinanceAccountRole Role { get; set; }

    /// <summary>Finance <c>Account.Id</c>. The code and name below are display snapshots only.</summary>
    public Guid AccountId { get; set; }

    [MaxLength(50)]
    public string AccountCodeSnapshot { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AccountNameSnapshot { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Whether one HR money event (see <c>HrFinancePostingEventCatalog</c>) posts to Finance for this
/// tenant. Absent row = disabled. The catalogue, not this table, says which roles the event uses.
/// </summary>
public class HrFinancePostingRule : TenantEntity
{
    [Required]
    [MaxLength(60)]
    public string EventCode { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    /// <summary>
    /// Posting date policy: <c>true</c> posts on the HR action date (default); <c>false</c> posts
    /// on the source document's own date (claim date, disbursement date). Kept per event because a
    /// back-filled claim from a closed month must be able to post into the current open period.
    /// </summary>
    public bool PostOnActionDate { get; set; } = true;

    /// <summary>
    /// For events whose document carries no payment method (leave encashment, award payment,
    /// long-service award, benefit payment): whether HR's action settles the employee directly
    /// or the liability is left for payroll to clear. Null = the catalogue's default for the event.
    /// Ignored by events whose document names the method (travel claims, medical claims).
    /// </summary>
    public HrFinanceSettlementRoute? SettlementRoute { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// The register: one row per (event, source document). It is the back-reference the Finance
/// adapter checklist requires (<c>PostingEventId</c> / <c>JournalEntryId</c> kept on the HR side),
/// the retry queue for events that fired before Finance was configured, and the audit of every
/// refusal.
/// </summary>
/// <remarks>
/// <para>⚠ One row per event per source, enforced by a filtered unique index. A retry UPDATES the
/// row; it never creates a sibling. Finance's own idempotency key
/// (<see cref="IdempotencyKey"/>) is deterministic from the same identity, so the two sides agree
/// on what "the same posting" means.</para>
///
/// <para>⚠ <see cref="Amount"/> is the functional-currency total of the debit side as sent. It is
/// evidence for reconciliation, not a balance — do not sum this column and call it a liability.</para>
/// </remarks>
public class HrFinancePostingRecord : TenantEntity
{
    [Required]
    [MaxLength(60)]
    public string EventCode { get; set; } = string.Empty;

    /// <summary>Finance's <c>SourceDocumentType</c> discriminator, e.g. <c>MedicalExpenseClaim</c>.</summary>
    [Required]
    [MaxLength(100)]
    public string SourceDocumentType { get; set; } = string.Empty;

    public Guid SourceDocumentId { get; set; }

    /// <summary>The human reference — claim number, advance number.</summary>
    [Required]
    [MaxLength(100)]
    public string SourceReference { get; set; } = string.Empty;

    /// <summary>The employee the money concerns; a bare Guid, no navigation, on purpose (no fixup).</summary>
    public Guid? EmployeeId { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>Functional currency the journal was (or would be) posted in.</summary>
    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = "GHS";

    /// <summary>
    /// The source's own currency where it differs from the functional one (a USD travel advance).
    /// Null when the source is already functional.
    /// </summary>
    [Column(TypeName = "char(3)")]
    public string? TransactionCurrencyCode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TransactionAmount { get; set; }

    public DateTime PostingDate { get; set; }

    public HrFinancePostingStatus Status { get; set; }

    [MaxLength(20)]
    public string? AccountingBookCode { get; set; }

    [Required]
    [MaxLength(200)]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Finance's <c>PostingAction</c> for this event (<c>Approve</c>, <c>Pay</c>, <c>Disburse</c>).
    /// ⚠ Finance de-duplicates on (source type, source id, action), NOT only on the idempotency
    /// key — so approval and payment of one claim MUST carry different actions, and a re-post after
    /// a reversal carries a generation suffix (<c>Approve#2</c>) or Finance would hand back the
    /// reversed original as a duplicate.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PostingAction { get; set; } = string.Empty;

    /// <summary>1 for the first posting of this event; incremented when a Reversed row is posted again.</summary>
    public int Generation { get; set; } = 1;

    // ── Finance's answer ─────────────────────────────────────────────────────────────────────
    public Guid? PostingEventId { get; set; }
    public Guid? JournalEntryId { get; set; }

    [MaxLength(50)]
    public string? JournalEntryNumber { get; set; }

    public DateTime? PostedAt { get; set; }

    // ── Failure / skip evidence ──────────────────────────────────────────────────────────────
    [MaxLength(2000)]
    public string? StatusReason { get; set; }

    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>The lines as sent, JSON, for the register screen and for reconciliation.</summary>
    public string? LinesSnapshot { get; set; }

    // ── Reversal ─────────────────────────────────────────────────────────────────────────────
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }

    [MaxLength(50)]
    public string? ReversalJournalEntryNumber { get; set; }

    public DateTime? ReversedAt { get; set; }

    [MaxLength(500)]
    public string? ReversalReason { get; set; }

    /// <summary>Platform user who triggered the last attempt or the reversal.</summary>
    public Guid? LastActedByUserId { get; set; }
}
