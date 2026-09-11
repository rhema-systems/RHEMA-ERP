using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

public static class CompanyHrPolicyMappingExtensions
{
    public static CompanyHrPolicySettingsDto ToDto(this CompanyHrPolicySettings entity)
    {
        return new CompanyHrPolicySettingsDto
        {
            Id                             = entity.Id,
            TenantId                       = entity.TenantId,
            CreatedAt                      = entity.CreatedAt,
            CreatedBy                      = entity.CreatedBy ?? string.Empty,
            UpdatedAt                      = entity.UpdatedAt,
            UpdatedBy                      = entity.UpdatedBy,

            CompulsoryRetirementAge        = entity.CompulsoryRetirementAge,
            VoluntaryRetirementAge         = entity.VoluntaryRetirementAge,
            UseGenderSpecificRetirementAge = entity.UseGenderSpecificRetirementAge,
            MaleRetirementAge              = entity.MaleRetirementAge,
            FemaleRetirementAge            = entity.FemaleRetirementAge,

            DefaultProbationMonths         = entity.DefaultProbationMonths,
            DefaultResignationNoticeDays   = entity.DefaultResignationNoticeDays,
            DefaultTerminationNoticeDays   = entity.DefaultTerminationNoticeDays,
            ProceduralAbsenceDays          = entity.ProceduralAbsenceDays,

            VacancyAlertLeadDays           = entity.VacancyAlertLeadDays,
            ReviewDueLeadDays              = entity.ReviewDueLeadDays,
            ContractExpiryLeadDays         = entity.ContractExpiryLeadDays,
            ProbationEndLeadDays           = entity.ProbationEndLeadDays,
            TeamTaskReminderLeadDays       = entity.TeamTaskReminderLeadDays,
            SalaryChangeRequiresApproval   = entity.SalaryChangeRequiresApproval,
            CertificationExpiryLeadDays    = entity.CertificationExpiryLeadDays,
            GrievanceRungChaseDays         = entity.GrievanceRungChaseDays,
            ConcernTriageChaseDays         = entity.ConcernTriageChaseDays,
            GrievanceAgreementChaseDays    = entity.GrievanceAgreementChaseDays,

            RetirementCountdownLeadDays    = entity.RetirementCountdownLeadDays,
            LongServiceMilestoneYears      = entity.LongServiceMilestoneYears,

            DefaultCurrencyCode            = entity.DefaultCurrencyCode,
            FiscalYearStartMonth           = entity.FiscalYearStartMonth,
            MinimumWorkingAge              = entity.MinimumWorkingAge,

            BudgetEnforcementMode          = entity.BudgetEnforcementMode,

            // ⚠ This line was missing too, and on the read side it was worse than a lost value.
            // `BudgetEnforcementMode` has no zero member (Off=1, Warn=2, Block=3), so an unassigned
            // property left the DTO carrying `(BudgetEnforcementMode)0` — an enum value that does
            // not exist, serialised as a bare `0` that maps to no member name. The database said
            // Block; every reader of this endpoint was told 0.
            EstablishmentEnforcementMode   = entity.EstablishmentEnforcementMode,
            SalaryStructureTiers           = entity.SalaryStructureTiers,
            SalaryStructureSource          = entity.SalaryStructureSource,

            FitWeightPerformance           = entity.FitWeightPerformance,
            FitWeightCompetency            = entity.FitWeightCompetency,
            FitWeightPotential             = entity.FitWeightPotential,
            FitWeightTenure                = entity.FitWeightTenure,

            SuccessionPlanNumberPrefix     = entity.SuccessionPlanNumberPrefix,

            WrittenQueryHours                     = entity.WrittenQueryHours,
            QueryResponseWindowHours              = entity.QueryResponseWindowHours,
            InvestigationDays                     = entity.InvestigationDays,
            DisciplineBacklogHorizonDays          = entity.DisciplineBacklogHorizonDays,
            SettlementDaysPerYear                 = entity.SettlementDaysPerYear,
            AttendanceRateIncludesApprovedLeave   = entity.AttendanceRateIncludesApprovedLeave,
        };
    }

    /// <summary>Applies editable fields from an update command onto an existing entity.</summary>
    public static void ApplyUpdate(this CompanyHrPolicySettings entity, UpdateCompanyHrPolicySettingsDto dto)
    {
        entity.CompulsoryRetirementAge        = dto.CompulsoryRetirementAge;
        entity.VoluntaryRetirementAge         = dto.VoluntaryRetirementAge;
        entity.UseGenderSpecificRetirementAge = dto.UseGenderSpecificRetirementAge;
        entity.MaleRetirementAge              = dto.MaleRetirementAge;
        entity.FemaleRetirementAge            = dto.FemaleRetirementAge;

        entity.DefaultProbationMonths         = dto.DefaultProbationMonths;
        entity.DefaultResignationNoticeDays   = dto.DefaultResignationNoticeDays;
        entity.DefaultTerminationNoticeDays   = dto.DefaultTerminationNoticeDays;
        entity.ProceduralAbsenceDays          = dto.ProceduralAbsenceDays;

        entity.VacancyAlertLeadDays           = dto.VacancyAlertLeadDays;
        entity.ReviewDueLeadDays              = dto.ReviewDueLeadDays;
        entity.ContractExpiryLeadDays         = dto.ContractExpiryLeadDays;
        entity.ProbationEndLeadDays           = dto.ProbationEndLeadDays;
        entity.TeamTaskReminderLeadDays       = dto.TeamTaskReminderLeadDays;
        entity.SalaryChangeRequiresApproval   = dto.SalaryChangeRequiresApproval;
        entity.CertificationExpiryLeadDays    = dto.CertificationExpiryLeadDays;
        entity.GrievanceRungChaseDays         = dto.GrievanceRungChaseDays;
        entity.ConcernTriageChaseDays         = dto.ConcernTriageChaseDays;
        entity.GrievanceAgreementChaseDays    = dto.GrievanceAgreementChaseDays;

        entity.RetirementCountdownLeadDays    = dto.RetirementCountdownLeadDays;
        entity.LongServiceMilestoneYears      = dto.LongServiceMilestoneYears?.Trim() ?? string.Empty;

        entity.DefaultCurrencyCode            = (dto.DefaultCurrencyCode ?? string.Empty).Trim().ToUpperInvariant();
        entity.FiscalYearStartMonth           = dto.FiscalYearStartMonth;
        entity.MinimumWorkingAge              = dto.MinimumWorkingAge;

        entity.BudgetEnforcementMode          = dto.BudgetEnforcementMode;

        // ⚠ This line was missing. `EstablishmentEnforcementMode` sat on BOTH DTOs and was simply
        // never assigned, so FR-HR-136's enforcement posture accepted every value and kept none —
        // the write succeeded, the status was 200, and the setting never moved. Area 14's signature
        // defect, in the one place where it decides whether exceeding an authorised establishment
        // blocks a vacancy or merely warns about it.
        entity.EstablishmentEnforcementMode   = dto.EstablishmentEnforcementMode;
        entity.SalaryStructureTiers           = dto.SalaryStructureTiers;
        entity.SalaryStructureSource          = dto.SalaryStructureSource;

        entity.FitWeightPerformance           = dto.FitWeightPerformance;
        entity.FitWeightCompetency            = dto.FitWeightCompetency;
        entity.FitWeightPotential             = dto.FitWeightPotential;
        entity.FitWeightTenure                = dto.FitWeightTenure;

        entity.SuccessionPlanNumberPrefix     = (dto.SuccessionPlanNumberPrefix ?? "SP").Trim().ToUpperInvariant();

        entity.WrittenQueryHours                   = dto.WrittenQueryHours;
        entity.QueryResponseWindowHours            = dto.QueryResponseWindowHours;
        entity.InvestigationDays                   = dto.InvestigationDays;
        entity.DisciplineBacklogHorizonDays        = dto.DisciplineBacklogHorizonDays;
        entity.SettlementDaysPerYear               = dto.SettlementDaysPerYear;
        entity.AttendanceRateIncludesApprovedLeave = dto.AttendanceRateIncludesApprovedLeave;
    }
}
