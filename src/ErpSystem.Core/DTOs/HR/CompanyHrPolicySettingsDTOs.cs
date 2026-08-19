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

    // Alert / reminder lead times
    public int VacancyAlertLeadDays { get; set; }
    public int ReviewDueLeadDays { get; set; }
    public int ContractExpiryLeadDays { get; set; }
    public int ProbationEndLeadDays { get; set; }

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

    // Succession fit-score weights (relative)
    public int FitWeightPerformance { get; set; }
    public int FitWeightCompetency { get; set; }
    public int FitWeightPotential { get; set; }
    public int FitWeightTenure { get; set; }

    // Record-number prefixes
    public string SuccessionPlanNumberPrefix { get; set; } = string.Empty;
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

    // Alert / reminder lead times
    [Range(0, 3650)] public int VacancyAlertLeadDays { get; set; } = 90;
    [Range(0, 3650)] public int ReviewDueLeadDays { get; set; } = 30;
    [Range(0, 3650)] public int ContractExpiryLeadDays { get; set; } = 60;
    [Range(0, 3650)] public int ProbationEndLeadDays { get; set; } = 30;

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

    // Succession fit-score weights (relative)
    [Range(0, 100)] public int FitWeightPerformance { get; set; } = 35;
    [Range(0, 100)] public int FitWeightCompetency { get; set; } = 30;
    [Range(0, 100)] public int FitWeightPotential { get; set; } = 20;
    [Range(0, 100)] public int FitWeightTenure { get; set; } = 15;

    // Record-number prefixes
    [MaxLength(10)] public string SuccessionPlanNumberPrefix { get; set; } = "SP";
}
