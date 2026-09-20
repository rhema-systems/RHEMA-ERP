using System.ComponentModel;

namespace ErpSystem.Core.Enums;

/// <summary>
/// The chart-of-accounts roles an HR money event needs. HR configures WHICH Finance account plays
/// each role (lane 8 of the HR finish plan; design in
/// <c>docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md</c>); Finance owns what the account IS.
/// </summary>
/// <remarks>
/// ⚠ Roles, not per-event account pairs, on purpose. Medical claims, travel claims, leave
/// encashment and awards all owe an employee money; if each event chose its own payable account
/// the module would have four answers to "what does the company owe its staff" — the twenty-seven
/// treatments the register was opened to prevent. One payable, one clearing, one receivable.
/// </remarks>
public enum HrFinanceAccountRole
{
    /// <summary>Liability: what the company owes employees for approved claims and awards.</summary>
    [Description("Staff claims payable")]
    StaffClaimsPayable = 1,

    /// <summary>Asset: money handed to an employee ahead of the spend (travel advances).</summary>
    [Description("Staff advances receivable")]
    StaffAdvancesReceivable = 2,

    /// <summary>
    /// Asset (clearing): where HR's "paid" lands until Finance's Cash module clears it against the
    /// bank. The same shape payroll already uses (<c>1010 Cash and Bank - Payroll Clearing</c>):
    /// HR records that the payment was made; the bank movement stays Finance's.
    /// </summary>
    [Description("Staff payments clearing")]
    StaffPaymentsClearing = 3,

    /// <summary>Expense: medical reimbursements to employees and dependants.</summary>
    [Description("Medical expense")]
    MedicalExpense = 4,

    /// <summary>Expense: staff travel — per diems, accommodation, transport, incidentals.</summary>
    [Description("Travel expense")]
    TravelExpense = 5
}

/// <summary>Where an HR money event stands with Finance.</summary>
public enum HrFinancePostingStatus
{
    /// <summary>Finance created and posted the journal; the register holds its identity.</summary>
    [Description("Posted")]
    Posted = 1,

    /// <summary>
    /// Finance refused (closed period, unmapped account, module lock…). The HR action that
    /// triggered it was rolled back; the row exists so the refusal is visible and retryable.
    /// </summary>
    [Description("Failed")]
    Failed = 2,

    /// <summary>
    /// The event fired but its posting rule is disabled or its accounts are not mapped. The HR
    /// action went ahead — this is the "recorded decision not to post" the finish plan asks for,
    /// and the row is what a later back-fill posts from.
    /// </summary>
    [Description("Unposted")]
    Unposted = 3,

    /// <summary>
    /// Nothing to post for this instance — a zero amount, or a settlement that payroll clears
    /// (a travel claim paid by payroll offset). Not a failure and not retryable.
    /// </summary>
    [Description("Skipped")]
    Skipped = 4,

    /// <summary>The posted journal has been reversed through Finance's exact reversal.</summary>
    [Description("Reversed")]
    Reversed = 5
}
