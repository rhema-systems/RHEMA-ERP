using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// An immutable-after-approval version of a tenant's period-close checklist. Keeping each
/// version as a separate row lets TDC prove exactly which approved control design governed a
/// historical close, even after Finance improves the checklist for later periods.
/// </summary>
public class FinanceCloseTemplate : TenantEntity
{
    [Required]
    [MaxLength(40)]
    public string TemplateCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CloseType { get; set; } = FinanceCloseTemplateTypes.MonthEnd;

    [Required]
    public int Version { get; set; } = 1;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = FinanceCloseTemplateStatuses.Draft;

    /// <summary>
    /// Only one approved version per close type is active. Older approved rows are retained as
    /// superseded evidence and remain valid references for cycles already created from them.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Identifies the TDC baseline created by the application when a tenant has no configured
    /// templates. A baseline can be superseded but is never silently edited.
    /// </summary>
    public bool IsSystemDefault { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    [MaxLength(200)]
    public string? ApprovedByUserName { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(2000)]
    public string? ApprovalDeclaration { get; set; }

    public DateTime? SupersededAt { get; set; }

    public virtual ICollection<FinanceCloseTemplateTaskDefinition> TaskDefinitions { get; set; }
        = new List<FinanceCloseTemplateTaskDefinition>();
    public virtual ICollection<FinanceCloseCycle> CloseCycles { get; set; }
        = new List<FinanceCloseCycle>();
}

/// <summary>
/// A task definition owned by one template version. Definitions are copied into a close cycle;
/// runtime assignment, completion and evidence therefore never mutate the approved template.
/// </summary>
public class FinanceCloseTemplateTaskDefinition : TenantEntity
{
    [Required]
    public Guid FinanceCloseTemplateId { get; set; }

    [Required]
    [MaxLength(60)]
    public string TaskCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? DependsOnTaskCode { get; set; }

    /// <summary>
    /// Links an automated task to an existing Finance close provider. This is deliberately a
    /// controlled code rather than a type name so configuration cannot execute arbitrary code.
    /// </summary>
    [MaxLength(60)]
    public string? CheckCode { get; set; }

    public int Sequence { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool IsAutomated { get; set; }

    /// <summary>
    /// Calendar-day offset from period end. Negative values allow preparatory tasks to fall due
    /// before period end; TDC defaults use +5, +10 and +15 for month/quarter/year close.
    /// </summary>
    public int DueDaysAfterPeriodEnd { get; set; } = 5;

    public Guid? DefaultAssigneeUserId { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }

    public virtual FinanceCloseTemplate FinanceCloseTemplate { get; set; } = null!;
}

/// <summary>
/// Close types are stored as readable strings because they appear in audit extracts and template
/// administration. The service derives one of these values from the fiscal period being closed.
/// </summary>
public static class FinanceCloseTemplateTypes
{
    public const string MonthEnd = "MonthEnd";
    public const string QuarterEnd = "QuarterEnd";
    public const string YearEnd = "YearEnd";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MonthEnd,
        QuarterEnd,
        YearEnd
    };
}

public static class FinanceCloseTemplateStatuses
{
    public const string Draft = "Draft";
    public const string Approved = "Approved";
    public const string Superseded = "Superseded";
}

/// <summary>
/// Describes one TDC system close template without coupling the policy catalogue to a database
/// context. Both tenant seeding and the close service consume this catalogue so task codes,
/// mandatory status and due-date policy cannot drift between provisioning paths.
/// </summary>
public sealed record FinanceCloseTemplateBaselineDefinition(
    string CloseType,
    string TemplateCode,
    string Name,
    int DueDaysAfterPeriodEnd);

/// <summary>
/// Describes one task in the current TDC system control set. A new catalogue control set is
/// installed as a new immutable template version; approved versions are never edited in place.
/// </summary>
public sealed record FinanceCloseTaskBaselineDefinition(
    string TaskCode,
    string Title,
    string Category,
    int Sequence,
    string? CheckCode,
    string? DependsOnTaskCode = null,
    bool IsAutomated = true,
    bool IsMandatory = true,
    string? Instructions = null);

/// <summary>
/// TDC's application-provided close policy. Control-set version 3 extends the subledger controls
/// with two Finance-only governance checks: unresolved recurring-journal generation exceptions
/// and approved budget scenarios awaiting an explicit adoption decision. Approved historical
/// versions remain unchanged so prior close evidence always retains its governing policy.
/// </summary>
public static class FinanceCloseTemplateBaselineCatalog
{
    public const int ControlSetVersion = 3;
    public const string SystemActorName = "TDC system baseline";
    public const string Description =
        "TDC baseline v3: Finance integrity, recurring-journal exceptions, budget-adoption review, subledger reconciliation and maker-checker certification.";

    public static IReadOnlyList<FinanceCloseTemplateBaselineDefinition> Templates { get; } =
    [
        new(FinanceCloseTemplateTypes.MonthEnd, "TDC-MONTH-END", "TDC month-end close", 5),
        new(FinanceCloseTemplateTypes.QuarterEnd, "TDC-QUARTER-END", "TDC quarter-end close", 10),
        new(FinanceCloseTemplateTypes.YearEnd, "TDC-YEAR-END", "TDC year-end close", 15)
    ];

    public static IReadOnlyList<FinanceCloseTaskBaselineDefinition> Tasks { get; } =
    [
        new("GL_POSTING_INTEGRITY", "General ledger and posting integrity", "General Ledger", 10, "POSTING_INTEGRITY"),
        new("TRIAL_BALANCE", "Trial balance and posted journal balance", "General Ledger", 20, "TRIAL_BALANCE", "GL_POSTING_INTEGRITY"),
        new("RECURRING_JOURNAL_EXCEPTIONS", "Resolve recurring-journal generation exceptions", "General Ledger", 30, "RECURRING_JOURNAL_EXCEPTIONS", "GL_POSTING_INTEGRITY",
            Instructions: "Resolve, regenerate or complete the existing controlled waiver workflow for every due recurring-journal occurrence before close."),
        new("AP_CONTROL_RECONCILIATION", "Accounts payable control-account reconciliation", "Accounts Payable", 40, "AP_CONTROL_RECONCILIATION", "TRIAL_BALANCE",
            Instructions: "Resolve all AP subledger-to-control-account differences and settlement diagnostics before close."),
        new("AR_CONTROL_RECONCILIATION", "Accounts receivable control-account reconciliation", "Accounts Receivable", 50, "AR_CONTROL_RECONCILIATION", "TRIAL_BALANCE",
            Instructions: "Resolve all AR subledger-to-control-account differences and settlement diagnostics before close."),
        new("AP_UNAPPLIED_BALANCES", "Review unapplied supplier payments and advances", "Accounts Payable", 60, "AP_UNAPPLIED_BALANCES", "AP_CONTROL_RECONCILIATION",
            IsMandatory: false,
            Instructions: "Review supplier advances and unapplied vendor payments; retain follow-up references for unusual balances."),
        new("AR_UNAPPLIED_BALANCES", "Review unapplied customer receipts and advances", "Accounts Receivable", 70, "AR_UNAPPLIED_BALANCES", "AR_CONTROL_RECONCILIATION",
            IsMandatory: false,
            Instructions: "Review customer advances and unapplied receipts; retain allocation or refund follow-up references where required."),
        new("BANK_RECONCILIATION", "Cash and bank reconciliation", "Cash & Bank", 80, "BANK_RECONCILIATION", "TRIAL_BALANCE"),
        new("BUDGET_ADOPTION_REVIEW", "Review approved budgets awaiting adoption", "Budgeting", 90, "BUDGET_ADOPTION_REVIEW", "TRIAL_BALANCE",
            IsMandatory: false,
            Instructions: "Confirm whether each approved but inactive scenario should be adopted as the official budget or retained as a non-adopted planning scenario."),
        new("FIXED_ASSET_DEPRECIATION", "Fixed-asset depreciation", "Fixed Assets", 100, "FIXED_ASSET_DEPRECIATION", "GL_POSTING_INTEGRITY"),
        new("FX_REVALUATION", "Foreign-currency revaluation", "Foreign Exchange", 110, "FX_REVALUATION", "GL_POSTING_INTEGRITY"),
        new("PREPARER_CERTIFICATION", "Preparer declaration and evidence sign-off", "Certification", 120, null, "FX_REVALUATION", false)
    ];
}
