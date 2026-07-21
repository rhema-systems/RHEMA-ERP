using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// Company-wide, tenant-scoped HR policy settings that HR administrators configure
/// once and that <b>drive behaviour in other modules</b> (retirement-age → succession
/// "service years left", probation defaults → confirmation dates, alert lead times →
/// dashboard reminders, and so on).
///
/// <para><b>Cardinality:</b> exactly one active record per tenant. The
/// <c>ICompanyHrPolicyProvider</c> loads it (read-only, <c>AsNoTracking</c>) and, when
/// no row exists yet, returns coded defaults instead of throwing — so a fresh tenant
/// keeps working before HR ever opens the settings page.</para>
///
/// <para><b>Scope discipline:</b> only genuinely cross-cutting, company-level policy
/// belongs here. Do NOT duplicate settings other modules already own —
/// <c>LeaveAccrualPolicy</c> (leave accrual), <c>AppraisalSettings</c> (appraisal
/// weights), Company Schedule / Attendance (working time). Add a field here only when
/// more than one module needs it, or it is a true organisation-level policy knob.</para>
///
/// <para><b>Record-number prefixes (proof-of-concept):</b> the codebase currently
/// hardcodes document-number prefixes inline in each service (e.g. <c>"SP-"</c>,
/// <c>"APR-"</c>, <c>"JD-"</c>). <see cref="SuccessionPlanNumberPrefix"/> is the first
/// prefix pulled into settings; other modules can migrate their prefixes here
/// incrementally following the same pattern.</para>
/// </summary>
public class CompanyHrPolicySettings : TenantEntity
{
    // ═══════════════════════════════════════════
    //  RETIREMENT POLICY
    // ═══════════════════════════════════════════

    /// <summary>Statutory / compulsory retirement age. Ghana default: 60.</summary>
    [Range(40, 100)]
    public int CompulsoryRetirementAge { get; set; } = 60;

    /// <summary>Earliest voluntary retirement age. Ghana default: 55.</summary>
    [Range(40, 100)]
    public int VoluntaryRetirementAge { get; set; } = 55;

    /// <summary>
    /// When <c>true</c>, <see cref="MaleRetirementAge"/> / <see cref="FemaleRetirementAge"/>
    /// are used per employee gender; otherwise <see cref="CompulsoryRetirementAge"/>
    /// applies to everyone.
    /// </summary>
    public bool UseGenderSpecificRetirementAge { get; set; } = false;

    /// <summary>Gender-specific override; when null, falls back to <see cref="CompulsoryRetirementAge"/>.</summary>
    [Range(40, 100)]
    public int? MaleRetirementAge { get; set; }

    /// <summary>Gender-specific override; when null, falls back to <see cref="CompulsoryRetirementAge"/>.</summary>
    [Range(40, 100)]
    public int? FemaleRetirementAge { get; set; }

    // ═══════════════════════════════════════════
    //  PROBATION & NOTICE DEFAULTS
    // ═══════════════════════════════════════════

    /// <summary>Default probation length in months — drives confirmation dates / IsOnProbation.</summary>
    [Range(0, 60)]
    public int DefaultProbationMonths { get; set; } = 6;

    /// <summary>Default notice period (days) an employee must give when resigning.</summary>
    [Range(0, 365)]
    public int DefaultResignationNoticeDays { get; set; } = 30;

    /// <summary>Default notice period (days) the employer must give on termination.</summary>
    [Range(0, 365)]
    public int DefaultTerminationNoticeDays { get; set; } = 30;

    // ═══════════════════════════════════════════
    //  ALERT / REMINDER LEAD TIMES
    // ═══════════════════════════════════════════

    /// <summary>How many days ahead an anticipated vacancy should start alerting HR.</summary>
    [Range(0, 3650)]
    public int VacancyAlertLeadDays { get; set; } = 90;

    /// <summary>How many days ahead a plan/record due for review should start alerting.</summary>
    [Range(0, 3650)]
    public int ReviewDueLeadDays { get; set; } = 30;

    /// <summary>How many days ahead a contract expiry should start alerting.</summary>
    [Range(0, 3650)]
    public int ContractExpiryLeadDays { get; set; } = 60;

    /// <summary>How many days ahead a probation end date should start alerting.</summary>
    [Range(0, 3650)]
    public int ProbationEndLeadDays { get; set; } = 30;

    // ═══════════════════════════════════════════
    //  LONG-SERVICE & RETIREMENT REMINDERS
    // ═══════════════════════════════════════════

    /// <summary>How many days ahead an upcoming retirement should start being flagged.</summary>
    [Range(0, 3650)]
    public int RetirementCountdownLeadDays { get; set; } = 365;

    /// <summary>
    /// Comma-separated years-of-service milestones for long-service recognition
    /// (e.g. "5,10,15,20,25"). Feeds succession "service years" context and awards
    /// eligibility. Parsed by consumers; stored as CSV for simplicity.
    /// </summary>
    [MaxLength(200)]
    public string LongServiceMilestoneYears { get; set; } = "5,10,15,20,25";

    // ═══════════════════════════════════════════
    //  ORG-WIDE DEFAULTS
    // ═══════════════════════════════════════════

    /// <summary>Default currency (ISO 4217, e.g. "GHS", "USD") used where no explicit currency is set.</summary>
    [MaxLength(3)]
    public string DefaultCurrencyCode { get; set; } = "GHS";

    /// <summary>Month (1–12) the organisation's fiscal year begins.</summary>
    [Range(1, 12)]
    public int FiscalYearStartMonth { get; set; } = 1;

    /// <summary>Minimum legal working age — used for recruitment / hire validation.</summary>
    [Range(10, 30)]
    public int MinimumWorkingAge { get; set; } = 18;

    // ═══════════════════════════════════════════
    //  BUDGET-AWARE REQUISITIONS
    // ═══════════════════════════════════════════

    /// <summary>
    /// How strictly staff requisitions are checked against the position's approved manpower
    /// budget line for the fiscal year. Default <see cref="Enums.BudgetEnforcementMode.Warn"/>.
    /// Enforcement only ever applies when a budget line actually exists for the position — an
    /// unbudgeted position is never blocked, so hiring is never bricked by a missing budget.
    /// </summary>
    public BudgetEnforcementMode BudgetEnforcementMode { get; set; } = BudgetEnforcementMode.Warn;

    // ═══════════════════════════════════════════
    //  SUCCESSION FIT-SCORE WEIGHTS (relative)
    // ═══════════════════════════════════════════
    // Relative weights for the succession candidate fit score. Components with no data are
    // skipped and the remaining weights renormalised, so these are relative — they need not
    // sum to any particular total.

    [Range(0, 100)] public int FitWeightPerformance { get; set; } = 35;
    [Range(0, 100)] public int FitWeightCompetency { get; set; } = 30;
    [Range(0, 100)] public int FitWeightPotential { get; set; } = 20;
    [Range(0, 100)] public int FitWeightTenure { get; set; } = 15;

    // ═══════════════════════════════════════════
    //  RECORD-NUMBER PREFIXES (proof-of-concept)
    // ═══════════════════════════════════════════

    /// <summary>
    /// Prefix for generated succession plan numbers (default "SP" → "SP-2026-0001").
    /// First adopter of settings-driven numbering; other modules can follow suit.
    /// </summary>
    [MaxLength(10)]
    public string SuccessionPlanNumberPrefix { get; set; } = "SP";
}
