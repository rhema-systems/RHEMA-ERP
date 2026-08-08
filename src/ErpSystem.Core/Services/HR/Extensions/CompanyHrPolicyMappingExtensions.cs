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

            VacancyAlertLeadDays           = entity.VacancyAlertLeadDays,
            ReviewDueLeadDays              = entity.ReviewDueLeadDays,
            ContractExpiryLeadDays         = entity.ContractExpiryLeadDays,
            ProbationEndLeadDays           = entity.ProbationEndLeadDays,

            RetirementCountdownLeadDays    = entity.RetirementCountdownLeadDays,
            LongServiceMilestoneYears      = entity.LongServiceMilestoneYears,

            DefaultCurrencyCode            = entity.DefaultCurrencyCode,
            FiscalYearStartMonth           = entity.FiscalYearStartMonth,
            MinimumWorkingAge              = entity.MinimumWorkingAge,

            BudgetEnforcementMode          = entity.BudgetEnforcementMode,

            FitWeightPerformance           = entity.FitWeightPerformance,
            FitWeightCompetency            = entity.FitWeightCompetency,
            FitWeightPotential             = entity.FitWeightPotential,
            FitWeightTenure                = entity.FitWeightTenure,

            SuccessionPlanNumberPrefix     = entity.SuccessionPlanNumberPrefix,
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

        entity.VacancyAlertLeadDays           = dto.VacancyAlertLeadDays;
        entity.ReviewDueLeadDays              = dto.ReviewDueLeadDays;
        entity.ContractExpiryLeadDays         = dto.ContractExpiryLeadDays;
        entity.ProbationEndLeadDays           = dto.ProbationEndLeadDays;

        entity.RetirementCountdownLeadDays    = dto.RetirementCountdownLeadDays;
        entity.LongServiceMilestoneYears      = dto.LongServiceMilestoneYears?.Trim() ?? string.Empty;

        entity.DefaultCurrencyCode            = (dto.DefaultCurrencyCode ?? string.Empty).Trim().ToUpperInvariant();
        entity.FiscalYearStartMonth           = dto.FiscalYearStartMonth;
        entity.MinimumWorkingAge              = dto.MinimumWorkingAge;

        entity.BudgetEnforcementMode          = dto.BudgetEnforcementMode;

        entity.FitWeightPerformance           = dto.FitWeightPerformance;
        entity.FitWeightCompetency            = dto.FitWeightCompetency;
        entity.FitWeightPotential             = dto.FitWeightPotential;
        entity.FitWeightTenure                = dto.FitWeightTenure;

        entity.SuccessionPlanNumberPrefix     = (dto.SuccessionPlanNumberPrefix ?? "SP").Trim().ToUpperInvariant();
    }
}
