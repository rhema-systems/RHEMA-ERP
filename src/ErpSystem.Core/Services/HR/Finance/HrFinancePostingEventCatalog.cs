using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Finance;

/// <summary>
/// One HR money event that can reach Finance through FIN-INT-001, and the account roles it needs.
/// </summary>
/// <param name="Code">Stable code, kept on <c>HrFinancePostingRule</c> and <c>HrFinancePostingRecord</c>.</param>
/// <param name="Name">What the settings screen calls it.</param>
/// <param name="Area">The HR area that owns the source document.</param>
/// <param name="SourceDocumentType">Finance's <c>SourceDocumentType</c> discriminator for the journal.</param>
/// <param name="PostingAction">Finance's posting action. ⚠ Finance de-duplicates on (source type, source id, action): two events on one document must differ here.</param>
/// <param name="Trigger">The HR action that authorises accounting — the checklist's "not saved, not updated".</param>
/// <param name="Treatment">The debit/credit in words, so an administrator can see what they are enabling.</param>
/// <param name="DebitRoles">Roles that may appear on the debit side.</param>
/// <param name="CreditRoles">Roles that may appear on the credit side.</param>
/// <param name="SupportsSettlementRoute">True when the document names no payment method, so the rule decides payroll vs direct.</param>
/// <param name="DefaultSettlementRoute">The route used until an administrator chooses one.</param>
public sealed record HrFinancePostingEventDefinition(
    string Code,
    string Name,
    string Area,
    string SourceDocumentType,
    string PostingAction,
    string Trigger,
    string Treatment,
    IReadOnlyList<HrFinanceAccountRole> DebitRoles,
    IReadOnlyList<HrFinanceAccountRole> CreditRoles,
    bool SupportsSettlementRoute = false,
    HrFinanceSettlementRoute DefaultSettlementRoute = HrFinanceSettlementRoute.Direct)
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
/// <para><b>Single-step events</b> (leave encashment, long-service award) recognise and settle in
/// one journal; the rule's settlement route decides whether the credit is clearing (direct) or the
/// payable (payroll clears it). Events whose document names a payment method (travel, medical)
/// take the route from the document.</para>
/// </remarks>
public static class HrFinancePostingEventCatalog
{
    // slice 1
    public const string MedicalClaimApproved = "MEDICAL_CLAIM_APPROVED";
    public const string MedicalClaimPaid = "MEDICAL_CLAIM_PAID";
    public const string TravelClaimApproved = "TRAVEL_CLAIM_APPROVED";
    public const string TravelClaimPaid = "TRAVEL_CLAIM_PAID";
    public const string TravelAdvanceDisbursed = "TRAVEL_ADVANCE_DISBURSED";
    // slice 2 — employee payables
    public const string LeaveEncashmentProcessed = "LEAVE_ENCASHMENT_PROCESSED";
    public const string AwardConferred = "AWARD_CONFERRED";
    public const string AwardPaid = "AWARD_PAID";
    public const string LongServiceAwardProcessed = "LONG_SERVICE_AWARD_PROCESSED";
    public const string BenefitUtilizationApproved = "BENEFIT_UTILIZATION_APPROVED";
    public const string BenefitUtilizationPaid = "BENEFIT_UTILIZATION_PAID";

    public const string SourceMedicalExpenseClaim = "MedicalExpenseClaim";
    public const string SourceStaffTravelExpenseClaim = "StaffTravelExpenseClaim";
    public const string SourceStaffTravelAdvance = "StaffTravelAdvance";
    public const string SourceLeaveEncashment = "LeaveEncashment";
    public const string SourceEmployeeAward = "EmployeeAward";
    public const string SourceLongServiceAward = "LongServiceAward";
    public const string SourceBenefitUtilization = "BenefitUtilization";

    public static IReadOnlyList<HrFinancePostingEventDefinition> Events { get; } =
    [
        // ── slice 1 ──────────────────────────────────────────────────────────────────────────
        new(MedicalClaimApproved, "Medical claim approved", "Medical", SourceMedicalExpenseClaim, "Approve",
            "The claim's adjudication is recorded as Approved (POST medical-expense-claims/{id}/approval).",
            "Dr Medical expense / Cr Staff claims payable, for the approved amount.",
            [HrFinanceAccountRole.MedicalExpense],
            [HrFinanceAccountRole.StaffClaimsPayable]),

        new(MedicalClaimPaid, "Medical claim paid", "Medical", SourceMedicalExpenseClaim, "Pay",
            "The claim's payment is recorded (POST medical-expense-claims/{id}/payment).",
            "Dr Staff claims payable / Cr Staff payments clearing, for the approved amount. Paid by salary deduction: nothing posts; payroll clears the payable.",
            [HrFinanceAccountRole.StaffClaimsPayable],
            [HrFinanceAccountRole.StaffPaymentsClearing]),

        new(TravelClaimApproved, "Travel expense claim approved", "Staff Travel", SourceStaffTravelExpenseClaim, "Approve",
            "The claim is reviewed to Approved or Partially Approved (POST staff-travel/finance/claims/{id}/review).",
            "Dr Travel expense / Cr Staff claims payable, for the approved total.",
            [HrFinanceAccountRole.TravelExpense],
            [HrFinanceAccountRole.StaffClaimsPayable]),

        new(TravelClaimPaid, "Travel expense claim paid", "Staff Travel", SourceStaffTravelExpenseClaim, "Pay",
            "The claim is paid (POST staff-travel/finance/claims/{id}/pay).",
            "Dr Staff claims payable (approved total) / Cr Staff advances receivable (advance recovered) / Cr Staff payments clearing (net paid). Paid by payroll offset: only the advance recovery posts; payroll's own journal clears the rest.",
            [HrFinanceAccountRole.StaffClaimsPayable],
            [HrFinanceAccountRole.StaffAdvancesReceivable, HrFinanceAccountRole.StaffPaymentsClearing]),

        new(TravelAdvanceDisbursed, "Travel advance disbursed", "Staff Travel", SourceStaffTravelAdvance, "Disburse",
            "The advance is disbursed (POST staff-travel/finance/advances/{id}/disburse).",
            "Dr Staff advances receivable / Cr Staff payments clearing, for the approved amount. A foreign-currency advance is valued through Finance's rate.",
            [HrFinanceAccountRole.StaffAdvancesReceivable],
            [HrFinanceAccountRole.StaffPaymentsClearing]),

        // ── slice 2 — employee payables ──────────────────────────────────────────────────────
        new(LeaveEncashmentProcessed, "Leave encashment processed", "Leave", SourceLeaveEncashment, "Process",
            "The approved encashment is marked processed, i.e. paid (PATCH hr/leave-encashments/{id}/process). Whether in-service encashment exists at all is the policy flag AllowInServiceEncashment and the leave type's AllowCashConversion.",
            "Dr Leave encashment expense / Cr Staff payments clearing (direct) — or Cr Staff claims payable when the route is payroll, which then clears it in the run.",
            [HrFinanceAccountRole.LeaveEncashmentExpense],
            [HrFinanceAccountRole.StaffPaymentsClearing, HrFinanceAccountRole.StaffClaimsPayable],
            SupportsSettlementRoute: true, DefaultSettlementRoute: HrFinanceSettlementRoute.Payroll),

        new(AwardConferred, "Award conferred", "Awards", SourceEmployeeAward, "Confer",
            "An award with a monetary value is conferred, directly or from a winning nomination (POST Awards, POST Awards/nominations/{id}/confer).",
            "Dr Awards expense / Cr Staff claims payable, for the award's monetary value. Awards without money post nothing.",
            [HrFinanceAccountRole.AwardsExpense],
            [HrFinanceAccountRole.StaffClaimsPayable]),

        new(AwardPaid, "Award paid", "Awards", SourceEmployeeAward, "Pay",
            "The award's payment is recorded (POST Awards/{id}/payment).",
            "Dr Staff claims payable (conferred value) / Cr Staff payments clearing (amount paid); a difference between the two adjusts Awards expense. Route payroll: nothing posts; payroll clears the payable.",
            [HrFinanceAccountRole.StaffClaimsPayable, HrFinanceAccountRole.AwardsExpense],
            [HrFinanceAccountRole.StaffPaymentsClearing, HrFinanceAccountRole.AwardsExpense, HrFinanceAccountRole.StaffClaimsPayable],
            SupportsSettlementRoute: true, DefaultSettlementRoute: HrFinanceSettlementRoute.Direct),

        new(LongServiceAwardProcessed, "Long-service award processed", "Awards", SourceLongServiceAward, "Process",
            "The long-service award is processed and presented (POST Awards/long-service/{id}/process). An unpriced rung (null amount) posts nothing.",
            "Dr Awards expense / Cr Staff payments clearing (direct) — or Cr Staff claims payable when the route is payroll.",
            [HrFinanceAccountRole.AwardsExpense],
            [HrFinanceAccountRole.StaffPaymentsClearing, HrFinanceAccountRole.StaffClaimsPayable],
            SupportsSettlementRoute: true, DefaultSettlementRoute: HrFinanceSettlementRoute.Payroll),

        new(BenefitUtilizationApproved, "Benefit claim approved", "Benefits", SourceBenefitUtilization, "Approve",
            "A benefit utilisation is moved to Approved (POST hr/employee-benefit-enrollments/utilizations/{id}/status).",
            "Dr Benefits expense / Cr Staff claims payable, for the claim amount in the enrolment's currency (valued through Finance's rate when foreign).",
            [HrFinanceAccountRole.BenefitsExpense],
            [HrFinanceAccountRole.StaffClaimsPayable]),

        new(BenefitUtilizationPaid, "Benefit claim paid", "Benefits", SourceBenefitUtilization, "Pay",
            "An approved benefit utilisation is moved to Paid (same route). A claim must be Approved before it can be Paid.",
            "Dr Staff claims payable / Cr Staff payments clearing. Route payroll: nothing posts; payroll clears the payable.",
            [HrFinanceAccountRole.StaffClaimsPayable],
            [HrFinanceAccountRole.StaffPaymentsClearing],
            SupportsSettlementRoute: true, DefaultSettlementRoute: HrFinanceSettlementRoute.Direct)
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
        HrFinanceAccountRole.LeaveEncashmentExpense => AccountType.Expense,
        HrFinanceAccountRole.AwardsExpense => AccountType.Expense,
        HrFinanceAccountRole.BenefitsExpense => AccountType.Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown HR Finance account role.")
    };
}
