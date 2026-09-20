using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Finance;

/// <summary>
/// One HR money event that can reach Finance through FIN-INT-001, and the account roles it needs.
/// </summary>
/// <param name="Code">Stable code, kept on <c>HrFinancePostingRule</c> and <c>HrFinancePostingRecord</c>.</param>
/// <param name="Name">What the settings screen calls it.</param>
/// <param name="Area">The HR area that owns the source document.</param>
/// <param name="SourceDocumentType">Finance's <c>SourceDocumentType</c> discriminator for the journal.</param>
/// <param name="Trigger">The HR action that authorises accounting — the checklist's "not saved, not updated".</param>
/// <param name="Treatment">The debit/credit in words, so an administrator can see what they are enabling.</param>
/// <param name="DebitRoles">Roles that may appear on the debit side.</param>
/// <param name="CreditRoles">Roles that may appear on the credit side.</param>
public sealed record HrFinancePostingEventDefinition(
    string Code,
    string Name,
    string Area,
    string SourceDocumentType,
    string Trigger,
    string Treatment,
    IReadOnlyList<HrFinanceAccountRole> DebitRoles,
    IReadOnlyList<HrFinanceAccountRole> CreditRoles)
{
    public IEnumerable<HrFinanceAccountRole> AllRoles => DebitRoles.Concat(CreditRoles).Distinct();
}

/// <summary>
/// The HR money events wired to Finance, code-backed so the settings screen, the adapter and the
/// tests share one list. Adding an event here without a builder in
/// <c>HrFinancePostingCommandFactory</c> fails the catalogue test on purpose.
/// </summary>
/// <remarks>
/// <para><b>The one treatment</b> (design § 3): an approval RECOGNISES the cost and the debt to
/// the employee (Dr expense / Cr staff claims payable); a payment SETTLES the debt against the
/// staff payments clearing account (Dr payable / Cr clearing), which Finance's Cash module clears
/// against the bank; an advance is a RECEIVABLE (Dr staff advances receivable / Cr clearing) that a
/// later claim recovers (Cr receivable inside the claim's settlement). The bank movement itself is
/// never HR's — the same split payroll already lives by.</para>
///
/// <para><b>Why approval and payment are two events.</b> The HR desk may approve in one month and
/// pay in the next; Finance sees the liability the day it arises and the cash the day it goes. A
/// single "paid" posting would understate liabilities at every month-end.</para>
/// </remarks>
public static class HrFinancePostingEventCatalog
{
    public const string MedicalClaimApproved = "MEDICAL_CLAIM_APPROVED";
    public const string MedicalClaimPaid = "MEDICAL_CLAIM_PAID";
    public const string TravelClaimApproved = "TRAVEL_CLAIM_APPROVED";
    public const string TravelClaimPaid = "TRAVEL_CLAIM_PAID";
    public const string TravelAdvanceDisbursed = "TRAVEL_ADVANCE_DISBURSED";

    public const string SourceMedicalExpenseClaim = "MedicalExpenseClaim";
    public const string SourceStaffTravelExpenseClaim = "StaffTravelExpenseClaim";
    public const string SourceStaffTravelAdvance = "StaffTravelAdvance";

    public static IReadOnlyList<HrFinancePostingEventDefinition> Events { get; } =
    [
        new(MedicalClaimApproved,
            "Medical claim approved",
            "Medical",
            SourceMedicalExpenseClaim,
            "The claim's adjudication is recorded as Approved (POST medical-expense-claims/{id}/approval).",
            "Dr Medical expense / Cr Staff claims payable, for the approved amount.",
            [HrFinanceAccountRole.MedicalExpense],
            [HrFinanceAccountRole.StaffClaimsPayable]),

        new(MedicalClaimPaid,
            "Medical claim paid",
            "Medical",
            SourceMedicalExpenseClaim,
            "The claim's payment is recorded (POST medical-expense-claims/{id}/payment).",
            "Dr Staff claims payable / Cr Staff payments clearing, for the approved amount.",
            [HrFinanceAccountRole.StaffClaimsPayable],
            [HrFinanceAccountRole.StaffPaymentsClearing]),

        new(TravelClaimApproved,
            "Travel expense claim approved",
            "Staff Travel",
            SourceStaffTravelExpenseClaim,
            "The claim is reviewed to Approved or Partially Approved (POST staff-travel/finance/claims/{id}/review).",
            "Dr Travel expense / Cr Staff claims payable, for the approved total.",
            [HrFinanceAccountRole.TravelExpense],
            [HrFinanceAccountRole.StaffClaimsPayable]),

        new(TravelClaimPaid,
            "Travel expense claim paid",
            "Staff Travel",
            SourceStaffTravelExpenseClaim,
            "The claim is paid (POST staff-travel/finance/claims/{id}/pay).",
            "Dr Staff claims payable (approved total) / Cr Staff advances receivable (advance recovered) / Cr Staff payments clearing (net paid). Paid by payroll offset: only the advance recovery posts; payroll's own journal clears the rest.",
            [HrFinanceAccountRole.StaffClaimsPayable],
            [HrFinanceAccountRole.StaffAdvancesReceivable, HrFinanceAccountRole.StaffPaymentsClearing]),

        new(TravelAdvanceDisbursed,
            "Travel advance disbursed",
            "Staff Travel",
            SourceStaffTravelAdvance,
            "The advance is disbursed (POST staff-travel/finance/advances/{id}/disburse).",
            "Dr Staff advances receivable / Cr Staff payments clearing, for the approved amount. A foreign-currency advance carries Finance's own rate record as evidence.",
            [HrFinanceAccountRole.StaffAdvancesReceivable],
            [HrFinanceAccountRole.StaffPaymentsClearing])
    ];

    private static readonly IReadOnlyDictionary<string, HrFinancePostingEventDefinition> ByCode =
        Events.ToDictionary(e => e.Code, StringComparer.Ordinal);

    public static HrFinancePostingEventDefinition GetRequired(string code)
        => ByCode.TryGetValue(code ?? string.Empty, out var definition)
            ? definition
            : throw new KeyNotFoundException($"HR Finance posting event '{code}' is not in the catalogue.");

    public static bool TryGet(string code, out HrFinancePostingEventDefinition definition)
        => ByCode.TryGetValue(code ?? string.Empty, out definition!);

    /// <summary>
    /// The account type each role must have. Enforced when a mapping is saved AND again when a
    /// posting is built, because the account can be re-typed in Finance in between.
    /// </summary>
    public static AccountType RequiredAccountType(HrFinanceAccountRole role) => role switch
    {
        HrFinanceAccountRole.StaffClaimsPayable => AccountType.Liability,
        HrFinanceAccountRole.StaffAdvancesReceivable => AccountType.Asset,
        HrFinanceAccountRole.StaffPaymentsClearing => AccountType.Asset,
        HrFinanceAccountRole.MedicalExpense => AccountType.Expense,
        HrFinanceAccountRole.TravelExpense => AccountType.Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown HR Finance account role.")
    };
}
