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

    /// <summary>
    /// Unauthorised absence, in days, beyond which a termination counts as <b>procedural</b> and HR
    /// may approve it without the Managing Director's signature (FR-HR-092).
    /// </summary>
    /// <remarks>
    /// <para>FR-HR-092 says the MD signs all terminations "except procedural ones, which HR
    /// approves automatically per policy (e.g. absence beyond 10 days)". The FRD gives exactly one
    /// example and no list, and the user settled it on 2026-08-20: that example <b>is</b> the
    /// list — everything else, resignation and retirement included, goes to the MD.</para>
    ///
    /// <para>It is a setting rather than a constant so widening the exception later is a
    /// configuration change and not a new trust boundary. Set it to 0 to make every termination
    /// require the MD's signature.</para>
    /// </remarks>
    [Range(0, 365)]
    public int ProceduralAbsenceDays { get; set; } = 10;

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

    /// <summary>
    /// How far ahead of a credential's expiry the certification sweep warns, when the catalogue
    /// row sets no lead time of its own (round 2, lane C2).
    /// </summary>
    /// <remarks>⚠ Non-nullable: it is in the HasData seed AND the migration adds it with a real
    /// DEFAULT, so no tenant's row is left at zero.</remarks>
    public int CertificationExpiryLeadDays { get; set; } = 60;

    /// <summary>
    /// How many days ahead of its due date a team task starts reminding its assignee
    /// (round 2, lane F2).
    /// </summary>
    /// <remarks>
    /// <para>Three, not thirty, and the difference is the point. A probation end is a date somebody
    /// plans a month around; a committee action item is a thing somebody does on Tuesday. A lead
    /// time long enough for the first would make the second a background hum nobody reads.</para>
    ///
    /// <para>⚠ Non-nullable with a real DEFAULT in the migration, like
    /// <see cref="CertificationExpiryLeadDays"/> — otherwise every existing tenant's row sits at
    /// zero and the sweep silently warns about nothing.</para>
    /// </remarks>
    [Range(0, 365)]
    public int TeamTaskReminderLeadDays { get; set; } = 3;

    // ═══════════════════════════════════════════
    //  EMPLOYEE-RELATIONS CLOCKS (area 9c slice 7)
    // ═══════════════════════════════════════════

    /// <summary>
    /// Days an employee-relations case may sit at a rung unanswered before it is chased.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Ours, not TDC's, and that is why it lives here.</b> FR-HR-181 names the escalation route
    /// and sets no time limit at any rung; five days is a working assumption raised with TDC in
    /// <c>docs/HR-OPEN-QUESTIONS-FOR-TDC.md</c> §2 and still unanswered. Until slice 7 it was a
    /// <c>const</c> in <c>DisciplineReminderService</c>, so TDC's eventual answer would have cost a
    /// code change and a deploy. Now it costs a settings edit.
    ///
    /// <para>A reminder threshold only: it does not auto-escalate, and the employee keeps the sole
    /// right to decide whether to escalate.</para>
    /// </remarks>
    [Range(1, 90)]
    public int GrievanceRungChaseDays { get; set; } = 5;

    /// <summary>
    /// Days an anonymously-reported concern may sit untriaged before HR is chased.
    /// </summary>
    /// <remarks>
    /// Deliberately shorter than the rung clock. A whistleblower has taken a risk to report
    /// something and has no way to chase it themselves — they cannot walk into an office and ask,
    /// because that would identify them. An untriaged concern is the failure this module can least
    /// afford, so three days rather than five.
    /// </remarks>
    [Range(1, 90)]
    public int ConcernTriageChaseDays { get; set; } = 3;

    /// <summary>
    /// Days after a case is resolved by agreement before the missing signed agreement is chased.
    /// </summary>
    /// <remarks>
    /// FR-HR-181 requires the final signed agreement be retained. A case resolved
    /// <c>SettledByAgreement</c> with no agreement on file is the requirement unmet, and nothing
    /// else would ever surface it — the case reads as resolved and drops off every open queue.
    /// </remarks>
    [Range(1, 365)]
    public int GrievanceAgreementChaseDays { get; set; } = 14;

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

    /// <summary>
    /// How strictly FR-HR-136 is applied: verify the position against the approved establishment
    /// before a vacancy may be approved. Default <see cref="Enums.BudgetEnforcementMode.Block"/>.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Block by default, where the budget ladder defaults to Warn</b>, and the difference
    /// is deliberate. The budget check reads numbers a department typed into a budget line; this one
    /// reads <c>EmployeePosition.ExpectedHeadcount</c>, and only ever fires for positions whose
    /// <c>EstablishmentApprovedOn</c> is set — meaning a manpower budget went the whole way up
    /// FR-HR-135's chain to Department Head, HR and the Managing Director to establish that number.
    /// Warning about exceeding something three people authorised would make the authorisation
    /// pointless.</para>
    ///
    /// <para>A position nobody has established is not constrained at all, whatever this is set to.
    /// That is what makes Block safe on a tenant where most positions still carry the default.</para>
    /// </remarks>
    public BudgetEnforcementMode EstablishmentEnforcementMode { get; set; } = BudgetEnforcementMode.Block;

    // ═══════════════════════════════════════════
    //  SALARY STRUCTURE (lane G)
    // ═══════════════════════════════════════════

    /// <summary>
    /// Two-tier (grade → notch) or three-tier (grade → level → notch). Default two-tier — what
    /// payroll is, what TDC's 2026 scale is, and what every existing tenant was implicitly.
    /// </summary>
    /// <remarks>
    /// ⚠ Governs screens and placement rules only; the tables are three-tier either way, with one
    /// implicit level per grade in the two-tier case. Switching to three-tier is refused while
    /// <see cref="SalaryStructureSource"/> is Payroll (payroll has no level tier); switching back to
    /// two-tier is refused while any active grade holds more than one active level. See
    /// <c>CompanyHrPolicySettingsService.ValidateSalaryStructureAsync</c>.
    /// </remarks>
    public SalaryStructureTiers SalaryStructureTiers { get; set; } = SalaryStructureTiers.GradeAndNotch;

    /// <summary>
    /// Whether the scale is maintained in Payroll (mirrored into HR, HR read-only) or in HR
    /// (projection off, HR's own grade/level/notch screens open). Default Payroll.
    /// </summary>
    public SalaryStructureSource SalaryStructureSource { get; set; } = SalaryStructureSource.Payroll;

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

    // ═══════════════════════════════════════════
    //  STAFF NUMBERING — deliberately NOT here
    // ═══════════════════════════════════════════
    //
    // ⚠ Staff numbering is per-REGISTER, not per-company. One organisation numbers permanent staff
    // as bare digits (10482) and contract staff with a prefix (ABC123); the next uses EMP/26/0417
    // for everyone. Four flat columns were written here first and could not express the first case,
    // and adding a ContractStaffNumberPrefix beside them would have hardcoded a two-register
    // assumption into a multi-tenant product.
    //
    // See StaffNumberFormat: one row per register per tenant, each with its own format and its own
    // sequence. There is also no global auto/manual switch — the ABSENCE of a rule for a register
    // means the number is typed by hand. A toggle here beside that flag would be a second source of
    // truth for one fact, and the two would eventually disagree.

    // ═══════════════════════════════════════════
    //  ANSWERS TDC HAS NOT GIVEN YET
    // ═══════════════════════════════════════════
    //
    // Every default below is EXACTLY what the code did as a constant before it moved here, so
    // adopting these settings changed no behaviour on any tenant. What changed is that the answer
    // stops being a deployment: when TDC says "72 hours should be five working days", somebody
    // edits a field instead of waiting for a release.
    //
    // ⚠ Two questions from the same list deliberately did NOT become settings — the exit-pay
    // basis's siblings and the grievance-document rule. See the finish plan's lane 2a: a setting
    // whose alternative branch is unimplemented is a control that does not control, which is worse
    // than an honest open question.

    /// <summary>
    /// Hours from an allegation being reported to the formal written query being due (FR-HR-177).
    /// </summary>
    [Range(1, 720)]
    public int WrittenQueryHours { get; set; } = 48;

    /// <summary>
    /// Hours the employee has to answer a written query before a decision may be proposed without
    /// them.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>This figure is not in the specification.</b> FR-HR-177 gives 48 hours for ISSUING the
    /// query and FR-HR-180 gives five working days to appeal; nothing states how long the employee
    /// has to respond. 72 hours was a defensible default and is now an editable one.
    /// <para>TDC may want this in WORKING days, as the appeal window is. That needs the holiday
    /// calendar, which is not loaded — so the unit stays hours until it is.</para>
    /// </remarks>
    [Range(1, 720)]
    public int QueryResponseWindowHours { get; set; } = 72;

    /// <summary>Days an investigation is tracked to completion within (FR-HR-178).</summary>
    [Range(1, 365)]
    public int InvestigationDays { get; set; } = 28;

    /// <summary>
    /// How long a disciplinary reminder keeps chasing an overdue step before it goes quiet.
    /// </summary>
    /// <remarks>
    /// ⚠ Only the CHASING stops. The breach stays on the record and in every report — a reminder
    /// engine that shouts for ever trains people to ignore it, which costs more than the silence.
    /// </remarks>
    [Range(1, 3650)]
    public int DisciplineBacklogHorizonDays { get; set; } = 90;

    /// <summary>
    /// Days per year used to turn a monthly salary into a daily rate in a final settlement
    /// (FR-HR-184). Calendar days by default: monthly × 12 ÷ 365.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>This one moves money and TDC has not answered it.</b> The four bases TDC was asked
    /// about are all expressible here — 365 calendar, 360 for thirty-day months, 264 for a 22-day
    /// working month — and they differ by <b>38% on the same facts</b> (GHS 3,156.16 against
    /// GHS 4,363.64 on the worked example in the open-questions document).
    /// <para>The settlement writes the basis onto every computed line <i>in words</i>, derived from
    /// this number, so a settlement computed under one basis still says which one it used after the
    /// setting changes. That makes an early settlement auditable — <b>it does not make it right.</b>
    /// Do not run real final settlements until TDC has answered, or expect a correction exercise.</para>
    /// </remarks>
    [Range(1, 366)]
    public int SettlementDaysPerYear { get; set; } = 365;

    /// <summary>
    /// Whether approved leave counts as an expected working day in the attendance rate.
    /// </summary>
    /// <remarks>
    /// True as built: leave is a scheduled working day the person did not attend, and DaysOnLeave is
    /// reported alongside so the reason stays visible. Set false and leave leaves the calculation
    /// entirely, the way weekends, public holidays and off-days already do.
    /// <para>⚠ The rate is computed in two places in <c>AttendanceDashboardService</c> — today's
    /// figure and the trend. Both read this; a policy honoured by one and not the other would make
    /// the dashboard disagree with itself.</para>
    /// </remarks>
    public bool AttendanceRateIncludesApprovedLeave { get; set; } = true;
}
