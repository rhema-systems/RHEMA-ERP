using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// HR → FINANCE POSTING (HR finish plan lane 8)
// ============================================================================

/// <summary>
/// Finance refused an HR posting. The HR action that raised it has been rolled back; the message
/// is Finance's own reason (closed period, unmapped account, module lock…) so the desk knows what
/// to ask Finance for. Mapped to 422 by the HR business-rule filters.
/// </summary>
public sealed class HrFinancePostingException : InvalidOperationException
{
    public string EventCode { get; }
    public Guid SourceDocumentId { get; }

    public HrFinancePostingException(string eventCode, Guid sourceDocumentId, string message, Exception? inner = null)
        : base(message, inner)
    {
        EventCode = eventCode;
        SourceDocumentId = sourceDocumentId;
    }
}

/// <summary>
/// The HR side of FIN-INT-001. An HR area calls <see cref="RunAsync"/> around the mutation that
/// authorises accounting; the adapter owns the transaction, the account resolution, the Finance
/// call and the register row, and guarantees the source is never left changed with the journal
/// missing, nor the journal posted with the source unchanged.
/// </summary>
/// <remarks>
/// <para><b>Strict, by design.</b> When a rule is enabled and Finance refuses, the HR action is
/// refused too — a claim cannot be "paid" in HR while Finance has no record of it. When the rule
/// is disabled or unmapped the action proceeds and the event is logged <c>Unposted</c>, so nothing
/// that works today stops working the day this ships; posting starts when an administrator maps
/// the accounts and enables the event, and the log is the back-fill queue.</para>
///
/// <para>Uses the <c>IFinancePostingEngine</c> overload WITHOUT a producer route: Finance's route
/// catalogue has one HR route today (payroll). The hand-off in
/// <c>docs/HR/integration/handoffs/HANDOFF-FINANCE-HR-POSTING-ROUTES.md</c> asks for the others;
/// switching an event onto its route is a one-line change in the adapter.</para>
/// </remarks>
public interface IHrFinancePostingAdapter
{
    /// <summary>
    /// Runs <paramref name="mutate"/> inside a transaction, posts the command it returns (if any)
    /// and commits both together. Returns the register outcome, or null when the mutation produced
    /// no command.
    /// </summary>
    /// <param name="mutate">
    /// Applies the HR change and returns the posting command, or null when the change carries no
    /// money event (a rejection). May call <c>SaveChangesAsync</c>; it runs inside the transaction.
    /// </param>
    /// <param name="actedByUserId">Platform user, recorded on the register row.</param>
    /// <exception cref="HrFinancePostingException">Finance refused; everything was rolled back.</exception>
    Task<HrFinancePostingOutcome?> RunAsync(
        Func<CancellationToken, Task<HrFinancePostingCommand?>> mutate,
        Guid actedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a command on its own (used by the register's retry). Same guarantees as
    /// <see cref="RunAsync"/>; the source document is not touched.
    /// </summary>
    Task<HrFinancePostingOutcome> PostAsync(
        HrFinancePostingCommand command,
        Guid actedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refuses a mutation of a source document that already has a Posted journal, naming the
    /// journal and the action. The way out is the register's reverse, which is explicit and reasoned.
    /// </summary>
    Task EnsureNotPostedAsync(
        string sourceDocumentType,
        Guid sourceDocumentId,
        string action,
        CancellationToken cancellationToken = default);

    /// <summary>True when the event's rule is enabled for the tenant (used by screens to decide what to promise).</summary>
    Task<bool> IsEnabledAsync(string eventCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when this event has a Posted row for the source document. Lets a settlement builder
    /// know whether the recognition it settles ever reached Finance (an award priced after it was
    /// conferred, a claim approved while the rule was off).
    /// </summary>
    Task<bool> IsPostedAsync(string eventCode, Guid sourceDocumentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The settings screen and the register: mappings, rules, the posting log, retry and reversal.
/// Reads are HR.Company.Read; every write is HR.Company.Admin.
/// </summary>
public interface IHrFinancePostingAdminService
{
    Task<HrFinancePostingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<HrFinancePostingSettingsDto> UpsertMappingAsync(UpsertHrFinanceAccountMappingDto dto, CancellationToken cancellationToken = default);
    Task<HrFinancePostingSettingsDto> ClearMappingAsync(HrFinanceAccountRole role, CancellationToken cancellationToken = default);
    Task<HrFinancePostingSettingsDto> UpsertRuleAsync(UpsertHrFinancePostingRuleDto dto, CancellationToken cancellationToken = default);

    Task<HrFinancePostingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<HrFinancePostingRecordDto>> GetRecordsAsync(HrFinancePostingRecordQueryDto query, CancellationToken cancellationToken = default);
    Task<HrFinancePostingRecordDto> GetRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HrFinancePostingRecordDto>> GetRecordsForSourceAsync(Guid sourceDocumentId, CancellationToken cancellationToken = default);

    /// <summary>Rebuilds the command from the source document as it stands now and posts it.</summary>
    Task<HrFinancePostingRecordDto> RetryAsync(Guid recordId, CancellationToken cancellationToken = default);

    /// <summary>Finance's exact reversal of a Posted row; the row becomes Reversed and keeps both identities.</summary>
    Task<HrFinancePostingRecordDto> ReverseAsync(Guid recordId, ReverseHrFinancePostingDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls Finance's current state onto an AP row (slice 5): the invoice status, Reversed when
    /// Finance rejected or voided it, and the payment voucher back onto the source once paid. A
    /// journal row is returned unchanged.
    /// </summary>
    Task<HrFinancePostingRecordDto> RefreshAsync(Guid recordId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Finance's actuals against an HR budget (lane 8, slice 6): read from Finance's book balances on
/// the account the budget is charged to, over the budget's period. A read; HR writes nothing.
/// </summary>
public interface IHrFinanceActualsService
{
    Task<HrBudgetFinanceActualsDto> GetManpowerBudgetActualsAsync(Guid budgetId, CancellationToken cancellationToken = default);
    Task<HrBudgetFinanceActualsDto> GetTrainingBudgetActualsAsync(Guid budgetId, CancellationToken cancellationToken = default);
}
