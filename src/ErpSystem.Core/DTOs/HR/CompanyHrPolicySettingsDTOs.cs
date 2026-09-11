using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>Read model for the tenant's company-wide HR policy settings.</summary>
public class CompanyHrPolicySettingsDto : BaseDto
{
    public Guid TenantId { get; set; }

    // Retirement policy
    public int CompulsoryRetirementAge { get; set; }
    public int VoluntaryRetirementAge { get; set; }
    public bool UseGenderSpecificRetirementAge { get; set; }
    public int? MaleRetirementAge { get; set; }
    public int? FemaleRetirementAge { get; set; }

    // Probation & notice
    public int DefaultProbationMonths { get; set; }
    public int DefaultResignationNoticeDays { get; set; }
    public int DefaultTerminationNoticeDays { get; set; }

    /// <summary>
    /// FR-HR-092's trust boundary: unauthorised absence, in days, beyond which a termination is
    /// <b>procedural</b> and HR may approve it without the Managing Director's signature.
    /// </summary>
    /// <remarks>
    /// Added here in areas 19-23 slice 2. The entity has carried this field since area 9b and
    /// `SeparationService` reads it live, but it was on neither DTO — so it could not be seen and
    /// could not be changed, and the setting the entity describes as "a configuration change and
    /// not a new trust boundary" was in practice a constant of 10.
    /// </remarks>
    public int ProceduralAbsenceDays { get; set; }

    // Alert / reminder lead times
    public int VacancyAlertLeadDays { get; set; }
    public int ReviewDueLeadDays { get; set; }
    public int ContractExpiryLeadDays { get; set; }
    public int ProbationEndLeadDays { get; set; }

    /// <summary>
    /// How many days ahead of its due date a team task reminds its assignee (round 2, lane F2).
    /// </summary>
    /// <remarks>
    /// ⚠ Days, not weeks, and much shorter than every other lead time here. A probation end is a
    /// date somebody plans a month around; a committee action item is a thing somebody does on
    /// Tuesday.
    /// </remarks>
    public int TeamTaskReminderLeadDays { get; set; }

    /// <summary>Round 3, lane S. A change of pay must go through an approved salary change request.</summary>
    public bool SalaryChangeRequiresApproval { get; set; }

    /// <summary>
    /// How many days ahead of expiry a credential without its own lead days reminds (lane C2).
    /// </summary>
    /// <remarks>
    /// ⚠ Closed by lane F2, not by C2. It existed on the ENTITY and the sweep read it, but it
    /// reached neither DTO — a setting the engine honoured and nobody could change. F2 was wiring
    /// its own lead-days field through the same four places and the screen card was already open,
    /// so leaving the sibling dead would have meant it was never picked up again. It is the exact
    /// shape the employee-relations note below warns about.
    /// </remarks>
    public int CertificationExpiryLeadDays { get; set; }

    // Employee-relations clocks (area 9c slice 7). ⚠ A setting the service reads is only
    // configurable if it reaches BOTH DTOs and BOTH mapping halves — miss one and it is a dead
    // field: the engine honours it and nobody can change it, which is the state slice 7 existed
    // to end.
    public int GrievanceRungChaseDays { get; set; }
    public int ConcernTriageChaseDays { get; set; }
    public int GrievanceAgreementChaseDays { get; set; }

    // Long-service & retirement reminders
    public int RetirementCountdownLeadDays { get; set; }
    public string LongServiceMilestoneYears { get; set; } = string.Empty;

    // Org-wide defaults
    public string DefaultCurrencyCode { get; set; } = string.Empty;
    public int FiscalYearStartMonth { get; set; }
    public int MinimumWorkingAge { get; set; }

    // Budget-aware requisitions
    public BudgetEnforcementMode BudgetEnforcementMode { get; set; }

    /// <summary>FR-HR-136 enforcement. See the entity for why this defaults to Block.</summary>
    public BudgetEnforcementMode EstablishmentEnforcementMode { get; set; }

    // Salary structure (lane G)
    public SalaryStructureTiers SalaryStructureTiers { get; set; }
    public SalaryStructureSource SalaryStructureSource { get; set; }

    // Succession fit-score weights (relative)
    public int FitWeightPerformance { get; set; }
    public int FitWeightCompetency { get; set; }
    public int FitWeightPotential { get; set; }
    public int FitWeightTenure { get; set; }

    // Record-number prefixes
    public string SuccessionPlanNumberPrefix { get; set; } = string.Empty;

    // Answers TDC has not given yet (finish plan, lane 2a). Each default is what the code did as a
    // constant before it moved here, so nothing changed behaviour — only who can change it.
    public int WrittenQueryHours { get; set; }
    public int QueryResponseWindowHours { get; set; }
    public int InvestigationDays { get; set; }
    public int DisciplineBacklogHorizonDays { get; set; }
    public int SettlementDaysPerYear { get; set; }
    public bool AttendanceRateIncludesApprovedLeave { get; set; }
}

/// <summary>
/// Command to upsert the tenant's HR policy settings. There is exactly one record per
/// tenant, so no Id is required — the service resolves (or creates) the current tenant's row.
/// </summary>
public class UpdateCompanyHrPolicySettingsDto
{
    // Retirement policy
    [Range(40, 100)] public int CompulsoryRetirementAge { get; set; } = 60;
    [Range(40, 100)] public int VoluntaryRetirementAge { get; set; } = 55;
    public bool UseGenderSpecificRetirementAge { get; set; }
    [Range(40, 100)] public int? MaleRetirementAge { get; set; }
    [Range(40, 100)] public int? FemaleRetirementAge { get; set; }

    // Probation & notice
    [Range(0, 60)]  public int DefaultProbationMonths { get; set; } = 6;
    [Range(0, 365)] public int DefaultResignationNoticeDays { get; set; } = 30;
    [Range(0, 365)] public int DefaultTerminationNoticeDays { get; set; } = 30;

    /// <summary>
    /// FR-HR-092: days of unauthorised absence beyond which HR may approve a termination without
    /// the Managing Director's signature. <b>0 sends every termination to the MD.</b>
    /// </summary>
    [Range(0, 365)] public int ProceduralAbsenceDays { get; set; } = 10;

    // Alert / reminder lead times
    [Range(0, 3650)] public int VacancyAlertLeadDays { get; set; } = 90;
    [Range(0, 3650)] public int ReviewDueLeadDays { get; set; } = 30;
    [Range(0, 3650)] public int ContractExpiryLeadDays { get; set; } = 60;
    [Range(0, 3650)] public int ProbationEndLeadDays { get; set; } = 30;

    // Round 2, lane F2. Range matches the entity's — a task cannot usefully remind more than a
    // year ahead, and 0 means "only once it is due".
    [Range(0, 365)] public int TeamTaskReminderLeadDays { get; set; } = 3;

    // Round 3, lane S. Default matches the entity's: on.
    public bool SalaryChangeRequiresApproval { get; set; } = true;

    // Lane C2's, closed by F2. Default matches the entity's 60 — see the read DTO for why it is
    // being added here rather than in its own lane.
    [Range(0, 3650)] public int CertificationExpiryLeadDays { get; set; } = 60;

    // Employee-relations clocks (area 9c slice 7). Ranges match the entity's: a chase threshold of
    // 0 would make everything overdue the instant it is created.
    [Range(1, 90)] public int GrievanceRungChaseDays { get; set; } = 5;
    [Range(1, 90)] public int ConcernTriageChaseDays { get; set; } = 3;
    [Range(1, 365)] public int GrievanceAgreementChaseDays { get; set; } = 14;

    // Long-service & retirement reminders
    [Range(0, 3650)] public int RetirementCountdownLeadDays { get; set; } = 365;
    [MaxLength(200)] public string LongServiceMilestoneYears { get; set; } = "5,10,15,20,25";

    // Org-wide defaults
    [MaxLength(3)] public string DefaultCurrencyCode { get; set; } = "GHS";
    [Range(1, 12)] public int FiscalYearStartMonth { get; set; } = 1;
    [Range(10, 30)] public int MinimumWorkingAge { get; set; } = 18;

    // Budget-aware requisitions
    public BudgetEnforcementMode BudgetEnforcementMode { get; set; } = BudgetEnforcementMode.Warn;

    /// <summary>FR-HR-136 enforcement. See the entity for why this defaults to Block.</summary>
    public BudgetEnforcementMode EstablishmentEnforcementMode { get; set; } = BudgetEnforcementMode.Block;

    // Salary structure (lane G). Both changes are validated against the live structure — see the service.
    public SalaryStructureTiers SalaryStructureTiers { get; set; } = SalaryStructureTiers.GradeAndNotch;
    public SalaryStructureSource SalaryStructureSource { get; set; } = SalaryStructureSource.Payroll;

    // Succession fit-score weights (relative)
    [Range(0, 100)] public int FitWeightPerformance { get; set; } = 35;
    [Range(0, 100)] public int FitWeightCompetency { get; set; } = 30;
    [Range(0, 100)] public int FitWeightPotential { get; set; } = 20;
    [Range(0, 100)] public int FitWeightTenure { get; set; } = 15;

    // Record-number prefixes
    [MaxLength(10)] public string SuccessionPlanNumberPrefix { get; set; } = "SP";

    // Answers TDC has not given yet (finish plan, lane 2a). Ranges match the entity's.
    [Range(1, 720)] public int WrittenQueryHours { get; set; } = 48;
    [Range(1, 720)] public int QueryResponseWindowHours { get; set; } = 72;
    [Range(1, 365)] public int InvestigationDays { get; set; } = 28;
    [Range(1, 3650)] public int DisciplineBacklogHorizonDays { get; set; } = 90;

    /// <summary>
    /// ⚠ Moves money: 365 calendar, 360 for thirty-day months, 264 for a 22-day working month —
    /// a 38% spread on the same facts. See the entity.
    /// </summary>
    [Range(1, 366)] public int SettlementDaysPerYear { get; set; } = 365;

    public bool AttendanceRateIncludesApprovedLeave { get; set; } = true;
}
