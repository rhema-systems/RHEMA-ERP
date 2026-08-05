using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.JobAnalysis;

namespace ErpSystem.Core.Services.HR.Extensions;

public static class JobAnalysisMappingExtensions
{
    #region JobDescription

    public static JobDescriptionDto ToDto(this JobDescription entity)
    {
        return new JobDescriptionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionNumber = entity.JobDescriptionNumber,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            JobTitle = entity.JobTitle,
            VersionNumber = entity.VersionNumber,
            EffectiveDate = entity.EffectiveDate,
            ExpiryDate = entity.ExpiryDate,
            RevisionReason = entity.RevisionReason,
            SupersededByVersionId = entity.SupersededByVersionId,
            JobSummary = entity.JobSummary,
            Status = entity.Status,
            PreparedById = entity.PreparedById,
            PreparedByName = entity.PreparedBy?.FullName,
            PreparedDate = entity.PreparedDate,
            ReviewedById = entity.ReviewedById,
            ReviewedByName = entity.ReviewedBy?.FullName,
            ReviewedDate = entity.ReviewedDate,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            NextReviewDate = entity.NextReviewDate,
            ReviewCycleMonths = entity.ReviewCycleMonths,
            RoleIntrinsicValue = entity.RoleIntrinsicValue,
            RoleCriticality = entity.RoleCriticality,
            IndustryBenchmarkSalary = entity.IndustryBenchmarkSalary,
            EstimatedSalaryLow = entity.EstimatedSalaryLow,
            EstimatedSalaryHigh = entity.EstimatedSalaryHigh,
            SuggestedSalaryGradeId = entity.SuggestedSalaryGradeId,
            SuggestedSalaryGradeName = entity.SuggestedSalaryGrade != null ? entity.SuggestedSalaryGrade.Name : null,
            ValuationNotes = entity.ValuationNotes,
            AutonomyLevel = entity.AutonomyLevel,
            DecisionMakingScope = entity.DecisionMakingScope,
            FinancialAuthorityLimit = entity.FinancialAuthorityLimit,
            ApprovalAuthorityNotes = entity.ApprovalAuthorityNotes,
            StaffLevelId = entity.StaffLevelId,
            StaffLevelName = entity.StaffLevel != null ? entity.StaffLevel.Name : null,
            IntendedEmploymentType = entity.IntendedEmploymentType,
            IsBargainingUnitRole = entity.IsBargainingUnitRole,
            UnionId = entity.UnionId,
            UnionName = entity.Union != null ? entity.Union.Name : null,
            OccupationCode = entity.OccupationCode,
            EssentialFunctionsSummary = entity.EssentialFunctionsSummary,
            JobFamilyId = entity.JobFamilyId,
            JobFamilyName = entity.JobFamily != null ? entity.JobFamily.Name : null,
            JobSubFamilyId = entity.JobSubFamilyId,
            JobSubFamilyName = entity.JobSubFamily != null ? entity.JobSubFamily.Name : null,
            JobLevelId = entity.JobLevelId,
            JobLevelName = entity.JobLevel != null ? entity.JobLevel.Name : null,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobDescriptionSummaryDto ToSummaryDto(this JobDescription entity)
    {
        return new JobDescriptionSummaryDto
        {
            Id = entity.Id,
            JobDescriptionNumber = entity.JobDescriptionNumber,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            JobTitle = entity.JobTitle,
            VersionNumber = entity.VersionNumber,
            EffectiveDate = entity.EffectiveDate,
            Status = entity.Status,
            NextReviewDate = entity.NextReviewDate
        };
    }

    public static JobDescriptionDetailDto ToDetailDto(this JobDescription entity,
        IEnumerable<JobQualification> qualifications,
        IEnumerable<JobCompetency> competencies,
        IEnumerable<JobPhysicalDemand>? physicalDemands = null,
        IEnumerable<JobWorkingCondition>? workingConditions = null,
        IEnumerable<JobEquipmentTool>? equipmentTools = null,
        IEnumerable<JobReportingRelationship>? reportingRelationships = null,
        IEnumerable<JobDutyItem>? dutyItems = null,
        IEnumerable<JobPpeRequirement>? ppeRequirements = null,
        IEnumerable<JobMedicalRequirement>? medicalRequirements = null)
    {
        return new JobDescriptionDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionNumber = entity.JobDescriptionNumber,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            JobTitle = entity.JobTitle,
            VersionNumber = entity.VersionNumber,
            EffectiveDate = entity.EffectiveDate,
            ExpiryDate = entity.ExpiryDate,
            RevisionReason = entity.RevisionReason,
            SupersededByVersionId = entity.SupersededByVersionId,
            JobSummary = entity.JobSummary,
            Status = entity.Status,
            PreparedById = entity.PreparedById,
            PreparedByName = entity.PreparedBy?.FullName,
            PreparedDate = entity.PreparedDate,
            ReviewedById = entity.ReviewedById,
            ReviewedByName = entity.ReviewedBy?.FullName,
            ReviewedDate = entity.ReviewedDate,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            NextReviewDate = entity.NextReviewDate,
            ReviewCycleMonths = entity.ReviewCycleMonths,
            RoleIntrinsicValue = entity.RoleIntrinsicValue,
            RoleCriticality = entity.RoleCriticality,
            IndustryBenchmarkSalary = entity.IndustryBenchmarkSalary,
            EstimatedSalaryLow = entity.EstimatedSalaryLow,
            EstimatedSalaryHigh = entity.EstimatedSalaryHigh,
            SuggestedSalaryGradeId = entity.SuggestedSalaryGradeId,
            SuggestedSalaryGradeName = entity.SuggestedSalaryGrade != null ? entity.SuggestedSalaryGrade.Name : null,
            ValuationNotes = entity.ValuationNotes,
            AutonomyLevel = entity.AutonomyLevel,
            DecisionMakingScope = entity.DecisionMakingScope,
            FinancialAuthorityLimit = entity.FinancialAuthorityLimit,
            ApprovalAuthorityNotes = entity.ApprovalAuthorityNotes,
            StaffLevelId = entity.StaffLevelId,
            StaffLevelName = entity.StaffLevel != null ? entity.StaffLevel.Name : null,
            IntendedEmploymentType = entity.IntendedEmploymentType,
            IsBargainingUnitRole = entity.IsBargainingUnitRole,
            UnionId = entity.UnionId,
            UnionName = entity.Union != null ? entity.Union.Name : null,
            OccupationCode = entity.OccupationCode,
            EssentialFunctionsSummary = entity.EssentialFunctionsSummary,
            JobFamilyId = entity.JobFamilyId,
            JobFamilyName = entity.JobFamily != null ? entity.JobFamily.Name : null,
            JobSubFamilyId = entity.JobSubFamilyId,
            JobSubFamilyName = entity.JobSubFamily != null ? entity.JobSubFamily.Name : null,
            JobLevelId = entity.JobLevelId,
            JobLevelName = entity.JobLevel != null ? entity.JobLevel.Name : null,
            DutyItems = dutyItems?.Select(d => d.ToDto()).ToList() ?? new List<JobDutyItemDto>(),
            Responsibilities = entity.Responsibilities?.Select(r => r.ToDto()).ToList() ?? new List<JobResponsibilityDto>(),
            Qualifications = qualifications.Select(q => q.ToDto()).ToList(),
            Competencies = competencies.Select(c => c.ToDto()).ToList(),
            PhysicalDemands = physicalDemands?.Select(p => p.ToDto()).ToList() ?? new List<JobPhysicalDemandDto>(),
            WorkingConditions = workingConditions?.Select(w => w.ToDto()).ToList() ?? new List<JobWorkingConditionDto>(),
            PpeRequirements = ppeRequirements?.Select(p => p.ToDto()).ToList() ?? new List<JobPpeRequirementDto>(),
            EquipmentTools = equipmentTools?.Select(e => e.ToDto()).ToList() ?? new List<JobEquipmentToolDto>(),
            ReportingRelationships = reportingRelationships?.Select(r => r.ToDto()).ToList() ?? new List<JobReportingRelationshipDto>(),
            MedicalRequirements = medicalRequirements?.Select(m => m.ToDto()).ToList() ?? new List<JobMedicalRequirementDto>(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobDescription ToEntity(this CreateJobDescriptionDto dto)
    {
        return new JobDescription
        {
            PositionId = dto.PositionId,
            JobTitle = dto.JobTitle,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            RevisionReason = dto.RevisionReason,
            JobSummary = dto.JobSummary,
            ReviewCycleMonths = dto.ReviewCycleMonths
        };
    }

    public static void UpdateEntity(this UpdateJobDescriptionDto dto, JobDescription entity)
    {
        entity.PositionId = dto.PositionId;
        entity.JobTitle = dto.JobTitle;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.RevisionReason = dto.RevisionReason;
        entity.JobSummary = dto.JobSummary;
        entity.Status = dto.Status;
        entity.NextReviewDate = dto.NextReviewDate;
        entity.ReviewCycleMonths = dto.ReviewCycleMonths;
        entity.RoleIntrinsicValue = dto.RoleIntrinsicValue;
        entity.RoleCriticality = dto.RoleCriticality;
        entity.IndustryBenchmarkSalary = dto.IndustryBenchmarkSalary;
        entity.SuggestedSalaryGradeId = dto.SuggestedSalaryGradeId;
        entity.ValuationNotes = dto.ValuationNotes;
        entity.AutonomyLevel = dto.AutonomyLevel;
        entity.DecisionMakingScope = dto.DecisionMakingScope;
        entity.FinancialAuthorityLimit = dto.FinancialAuthorityLimit;
        entity.ApprovalAuthorityNotes = dto.ApprovalAuthorityNotes;
        entity.StaffLevelId = dto.StaffLevelId;
        entity.IntendedEmploymentType = dto.IntendedEmploymentType;
        entity.IsBargainingUnitRole = dto.IsBargainingUnitRole;
        entity.UnionId = dto.UnionId;
        entity.OccupationCode = dto.OccupationCode;
        entity.EssentialFunctionsSummary = dto.EssentialFunctionsSummary;
        entity.JobFamilyId = dto.JobFamilyId;
        entity.JobSubFamilyId = dto.JobSubFamilyId;
        entity.JobLevelId = dto.JobLevelId;
    }

    public static List<JobDescriptionDto> ToDtoList(this IEnumerable<JobDescription> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<JobDescriptionSummaryDto> ToSummaryDtoList(this IEnumerable<JobDescription> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region JobResponsibility

    public static JobResponsibilityDto ToDto(this JobResponsibility entity)
    {
        return new JobResponsibilityDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionId = entity.JobDescriptionId,
            ResponsibilityDescription = entity.ResponsibilityDescription,
            Type = entity.Type,
            PercentageOfTime = entity.PercentageOfTime,
            ImportanceWeight = entity.ImportanceWeight,
            Qualifications = entity.Qualifications?.Select(q => q.ToDto()).ToList() ?? new List<JobQualificationDto>(),
            Competencies = entity.Competencies?.Select(c => c.ToDto()).ToList() ?? new List<JobCompetencyDto>(),
            Kpis = entity.Kpis?.Select(k => k.ToDto()).ToList() ?? new List<JobResponsibilityKpiDto>(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobResponsibility ToEntity(this CreateJobResponsibilityDto dto)
    {
        return new JobResponsibility
        {
            JobDescriptionId = dto.JobDescriptionId,
            ResponsibilityDescription = dto.ResponsibilityDescription,
            Type = dto.Type,
            PercentageOfTime = dto.PercentageOfTime,
            ImportanceWeight = dto.ImportanceWeight
        };
    }

    public static void UpdateEntity(this UpdateJobResponsibilityDto dto, JobResponsibility entity)
    {
        entity.ResponsibilityDescription = dto.ResponsibilityDescription;
        entity.Type = dto.Type;
        entity.PercentageOfTime = dto.PercentageOfTime;
        entity.ImportanceWeight = dto.ImportanceWeight;
    }

    public static List<JobResponsibilityDto> ToDtoList(this IEnumerable<JobResponsibility> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region JobQualification

    public static JobQualificationDto ToDto(this JobQualification entity)
    {
        return new JobQualificationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobResponsibilityId = entity.JobResponsibilityId,
            JobDescriptionId = entity.JobDescriptionId,
            Type = entity.Type,
            QualificationId = entity.QualificationId,
            QualificationName = entity.Qualification?.Name,
            Title = entity.Title,
            Description = entity.Description,
            IsRequired = entity.IsRequired,
            JobSpecificRequirements = entity.JobSpecificRequirements,
            MonetaryValue = entity.MonetaryValue,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobQualification ToEntity(this CreateJobQualificationDto dto)
    {
        return new JobQualification
        {
            JobResponsibilityId = dto.JobResponsibilityId,
            JobDescriptionId = dto.JobDescriptionId,
            Type = dto.Type,
            QualificationId = dto.QualificationId,
            Title = dto.Title,
            Description = dto.Description,
            IsRequired = dto.IsRequired,
            JobSpecificRequirements = dto.JobSpecificRequirements,
            MonetaryValue = dto.MonetaryValue
        };
    }

    public static void UpdateEntity(this UpdateJobQualificationDto dto, JobQualification entity)
    {
        entity.Type = dto.Type;
        entity.QualificationId = dto.QualificationId;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.IsRequired = dto.IsRequired;
        entity.JobSpecificRequirements = dto.JobSpecificRequirements;
        entity.MonetaryValue = dto.MonetaryValue;
    }

    public static List<JobQualificationDto> ToDtoList(this IEnumerable<JobQualification> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region JobCompetency

    public static JobCompetencyDto ToDto(this JobCompetency entity)
    {
        return new JobCompetencyDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobResponsibilityId = entity.JobResponsibilityId,
            JobDescriptionId = entity.JobDescriptionId,
            SkillId = entity.SkillId,
            SkillName = entity.Skill?.Name,
            CompetencyId = entity.CompetencyId,
            MasterCompetencyName = entity.Competency?.Name,
            CompetencyName = entity.CompetencyName,
            Description = entity.Description,
            Type = entity.Type,
            RequiredLevel = entity.RequiredLevel,
            IsCritical = entity.IsCritical,
            MonetaryValue = entity.MonetaryValue,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobCompetency ToEntity(this CreateJobCompetencyDto dto)
    {
        return new JobCompetency
        {
            JobResponsibilityId = dto.JobResponsibilityId,
            JobDescriptionId = dto.JobDescriptionId,
            SkillId = dto.SkillId,
            CompetencyId = dto.CompetencyId,
            CompetencyName = dto.CompetencyName,
            Description = dto.Description,
            Type = dto.Type,
            RequiredLevel = dto.RequiredLevel,
            IsCritical = dto.IsCritical,
            MonetaryValue = dto.MonetaryValue
        };
    }

    public static void UpdateEntity(this UpdateJobCompetencyDto dto, JobCompetency entity)
    {
        entity.SkillId = dto.SkillId;
        entity.CompetencyId = dto.CompetencyId;
        entity.CompetencyName = dto.CompetencyName;
        entity.Description = dto.Description;
        entity.Type = dto.Type;
        entity.RequiredLevel = dto.RequiredLevel;
        entity.IsCritical = dto.IsCritical;
        entity.MonetaryValue = dto.MonetaryValue;
    }

    public static List<JobCompetencyDto> ToDtoList(this IEnumerable<JobCompetency> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region ManpowerBudget

    public static ManpowerBudgetDto ToDto(this ManpowerBudget entity)
    {
        return new ManpowerBudgetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            BudgetNumber = entity.BudgetNumber,
            FiscalYear = entity.FiscalYear,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            Status = entity.Status,
            PeriodStartDate = entity.PeriodStartDate,
            PeriodEndDate = entity.PeriodEndDate,
            CurrentHeadcount = entity.CurrentHeadcount,
            CurrentSalaryCost = entity.CurrentSalaryCost,
            PlannedHeadcount = entity.PlannedHeadcount,
            PlannedSalaryCost = entity.PlannedSalaryCost,
            PlannedNewHires = entity.PlannedNewHires,
            PlannedTerminations = entity.PlannedTerminations,
            PlannedPromotions = entity.PlannedPromotions,
            PlannedTransfers = entity.PlannedTransfers,
            SalaryBudget = entity.SalaryBudget,
            BenefitsBudget = entity.BenefitsBudget,
            RecruitmentBudget = entity.RecruitmentBudget,
            TrainingBudget = entity.TrainingBudget,
            TotalBudget = entity.TotalBudget,
            ActualSpent = entity.ActualSpent,
            Variance = entity.Variance,
            BusinessJustification = entity.BusinessJustification,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static ManpowerBudgetSummaryDto ToSummaryDto(this ManpowerBudget entity)
    {
        return new ManpowerBudgetSummaryDto
        {
            Id = entity.Id,
            BudgetNumber = entity.BudgetNumber,
            FiscalYear = entity.FiscalYear,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            Status = entity.Status,
            CurrentHeadcount = entity.CurrentHeadcount,
            PlannedHeadcount = entity.PlannedHeadcount,
            TotalBudget = entity.TotalBudget,
            ActualSpent = entity.ActualSpent
        };
    }

    public static ManpowerBudgetDetailDto ToDetailDto(this ManpowerBudget entity)
    {
        return new ManpowerBudgetDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            BudgetNumber = entity.BudgetNumber,
            FiscalYear = entity.FiscalYear,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            Status = entity.Status,
            PeriodStartDate = entity.PeriodStartDate,
            PeriodEndDate = entity.PeriodEndDate,
            CurrentHeadcount = entity.CurrentHeadcount,
            CurrentSalaryCost = entity.CurrentSalaryCost,
            PlannedHeadcount = entity.PlannedHeadcount,
            PlannedSalaryCost = entity.PlannedSalaryCost,
            PlannedNewHires = entity.PlannedNewHires,
            PlannedTerminations = entity.PlannedTerminations,
            PlannedPromotions = entity.PlannedPromotions,
            PlannedTransfers = entity.PlannedTransfers,
            SalaryBudget = entity.SalaryBudget,
            BenefitsBudget = entity.BenefitsBudget,
            RecruitmentBudget = entity.RecruitmentBudget,
            TrainingBudget = entity.TrainingBudget,
            TotalBudget = entity.TotalBudget,
            ActualSpent = entity.ActualSpent,
            Variance = entity.Variance,
            BusinessJustification = entity.BusinessJustification,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            BudgetLines = entity.BudgetLines?.Select(l => l.ToDto()).ToList() ?? new List<ManpowerBudgetLineDto>(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static ManpowerBudget ToEntity(this CreateManpowerBudgetDto dto)
    {
        return new ManpowerBudget
        {
            FiscalYear = dto.FiscalYear,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PeriodStartDate = dto.PeriodStartDate,
            PeriodEndDate = dto.PeriodEndDate,
            CurrentHeadcount = dto.CurrentHeadcount,
            CurrentSalaryCost = dto.CurrentSalaryCost,
            PlannedHeadcount = dto.PlannedHeadcount,
            PlannedSalaryCost = dto.PlannedSalaryCost,
            PlannedNewHires = dto.PlannedNewHires,
            PlannedTerminations = dto.PlannedTerminations,
            PlannedPromotions = dto.PlannedPromotions,
            PlannedTransfers = dto.PlannedTransfers,
            SalaryBudget = dto.SalaryBudget,
            BenefitsBudget = dto.BenefitsBudget,
            RecruitmentBudget = dto.RecruitmentBudget,
            TrainingBudget = dto.TrainingBudget,
            BusinessJustification = dto.BusinessJustification
        };
    }

    public static void UpdateEntity(this UpdateManpowerBudgetDto dto, ManpowerBudget entity)
    {
        entity.PeriodStartDate = dto.PeriodStartDate;
        entity.PeriodEndDate = dto.PeriodEndDate;
        entity.CurrentHeadcount = dto.CurrentHeadcount;
        entity.CurrentSalaryCost = dto.CurrentSalaryCost;
        entity.PlannedHeadcount = dto.PlannedHeadcount;
        entity.PlannedSalaryCost = dto.PlannedSalaryCost;
        entity.PlannedNewHires = dto.PlannedNewHires;
        entity.PlannedTerminations = dto.PlannedTerminations;
        entity.PlannedPromotions = dto.PlannedPromotions;
        entity.PlannedTransfers = dto.PlannedTransfers;
        entity.SalaryBudget = dto.SalaryBudget;
        entity.BenefitsBudget = dto.BenefitsBudget;
        entity.RecruitmentBudget = dto.RecruitmentBudget;
        entity.TrainingBudget = dto.TrainingBudget;
        entity.ActualSpent = dto.ActualSpent;
        entity.Status = dto.Status;
        entity.BusinessJustification = dto.BusinessJustification;
    }

    public static List<ManpowerBudgetDto> ToDtoList(this IEnumerable<ManpowerBudget> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<ManpowerBudgetSummaryDto> ToSummaryDtoList(this IEnumerable<ManpowerBudget> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region ManpowerBudgetLine

    public static ManpowerBudgetLineDto ToDto(this ManpowerBudgetLine entity)
    {
        return new ManpowerBudgetLineDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ManpowerBudgetId = entity.ManpowerBudgetId,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            JobDescriptionId = entity.JobDescriptionId,
            JobDescriptionNumber = entity.JobDescription?.JobDescriptionNumber,
            CurrentCount = entity.CurrentCount,
            CurrentFilled = entity.CurrentFilled,
            CurrentVacant = entity.CurrentVacant,
            CurrentAverageSalary = entity.CurrentAverageSalary,
            CurrentTotalCost = entity.CurrentTotalCost,
            PlannedCount = entity.PlannedCount,
            PlannedNewPositions = entity.PlannedNewPositions,
            PlannedEliminations = entity.PlannedEliminations,
            PlannedAverageSalary = entity.PlannedAverageSalary,
            PlannedTotalCost = entity.PlannedTotalCost,
            Quarter = entity.Quarter,
            TargetFillDate = entity.TargetFillDate,
            Priority = entity.Priority,
            IsCritical = entity.IsCritical,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static ManpowerBudgetLine ToEntity(this CreateManpowerBudgetLineDto dto)
    {
        return new ManpowerBudgetLine
        {
            ManpowerBudgetId = dto.ManpowerBudgetId,
            PositionId = dto.PositionId,
            JobDescriptionId = dto.JobDescriptionId,
            CurrentCount = dto.CurrentCount,
            CurrentFilled = dto.CurrentFilled,
            CurrentVacant = dto.CurrentVacant,
            CurrentAverageSalary = dto.CurrentAverageSalary,
            CurrentTotalCost = dto.CurrentTotalCost,
            PlannedCount = dto.PlannedCount,
            PlannedNewPositions = dto.PlannedNewPositions,
            PlannedEliminations = dto.PlannedEliminations,
            PlannedAverageSalary = dto.PlannedAverageSalary,
            PlannedTotalCost = dto.PlannedTotalCost,
            Quarter = dto.Quarter,
            TargetFillDate = dto.TargetFillDate,
            Priority = dto.Priority,
            IsCritical = dto.IsCritical,
            Notes = dto.Notes
        };
    }

    public static void UpdateEntity(this UpdateManpowerBudgetLineDto dto, ManpowerBudgetLine entity)
    {
        entity.CurrentCount = dto.CurrentCount;
        entity.CurrentFilled = dto.CurrentFilled;
        entity.CurrentVacant = dto.CurrentVacant;
        entity.CurrentAverageSalary = dto.CurrentAverageSalary;
        entity.CurrentTotalCost = dto.CurrentTotalCost;
        entity.PlannedCount = dto.PlannedCount;
        entity.PlannedNewPositions = dto.PlannedNewPositions;
        entity.PlannedEliminations = dto.PlannedEliminations;
        entity.PlannedAverageSalary = dto.PlannedAverageSalary;
        entity.PlannedTotalCost = dto.PlannedTotalCost;
        entity.Quarter = dto.Quarter;
        entity.TargetFillDate = dto.TargetFillDate;
        entity.Priority = dto.Priority;
        entity.IsCritical = dto.IsCritical;
        entity.Notes = dto.Notes;
    }

    public static List<ManpowerBudgetLineDto> ToDtoList(this IEnumerable<ManpowerBudgetLine> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region JobPhysicalDemand

    public static JobPhysicalDemandDto ToDto(this JobPhysicalDemand entity)
    {
        return new JobPhysicalDemandDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionId = entity.JobDescriptionId,
            DemandType = entity.DemandType,
            DemandDescription = entity.DemandDescription,
            Frequency = entity.Frequency,
            WeightOrForceKg = entity.WeightOrForceKg,
            DistanceOrDuration = entity.DistanceOrDuration,
            IsEssential = entity.IsEssential,
            NotesOrExamples = entity.NotesOrExamples,
            IsPhysicalAttribute = entity.IsPhysicalAttribute,
            AttributeRequirement = entity.AttributeRequirement,
            Justification = entity.Justification,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobPhysicalDemand ToEntity(this CreateJobPhysicalDemandDto dto)
    {
        return new JobPhysicalDemand
        {
            JobDescriptionId = dto.JobDescriptionId,
            DemandType = dto.DemandType,
            DemandDescription = dto.DemandDescription,
            Frequency = dto.Frequency,
            WeightOrForceKg = dto.WeightOrForceKg,
            DistanceOrDuration = dto.DistanceOrDuration,
            IsEssential = dto.IsEssential,
            NotesOrExamples = dto.NotesOrExamples,
            IsPhysicalAttribute = dto.IsPhysicalAttribute,
            AttributeRequirement = dto.AttributeRequirement,
            Justification = dto.Justification
        };
    }

    public static void UpdateEntity(this UpdateJobPhysicalDemandDto dto, JobPhysicalDemand entity)
    {
        entity.DemandType = dto.DemandType;
        entity.DemandDescription = dto.DemandDescription;
        entity.Frequency = dto.Frequency;
        entity.WeightOrForceKg = dto.WeightOrForceKg;
        entity.DistanceOrDuration = dto.DistanceOrDuration;
        entity.IsEssential = dto.IsEssential;
        entity.NotesOrExamples = dto.NotesOrExamples;
        entity.IsPhysicalAttribute = dto.IsPhysicalAttribute;
        entity.AttributeRequirement = dto.AttributeRequirement;
        entity.Justification = dto.Justification;
    }

    public static List<JobPhysicalDemandDto> ToDtoList(this IEnumerable<JobPhysicalDemand> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobWorkingCondition

    public static JobWorkingConditionDto ToDto(this JobWorkingCondition entity)
    {
        return new JobWorkingConditionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionId = entity.JobDescriptionId,
            EnvironmentType = entity.EnvironmentType,
            Description = entity.Description,
            ExposureLevel = entity.ExposureLevel,
            RequiresPPE = entity.RequiresPPE,
            PPERequirements = entity.PPERequirements,
            TravelPercentage = entity.TravelPercentage,
            TravelRequirements = entity.TravelRequirements,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobWorkingCondition ToEntity(this CreateJobWorkingConditionDto dto)
    {
        return new JobWorkingCondition
        {
            JobDescriptionId = dto.JobDescriptionId,
            EnvironmentType = dto.EnvironmentType,
            Description = dto.Description,
            ExposureLevel = dto.ExposureLevel,
            RequiresPPE = dto.RequiresPPE,
            PPERequirements = dto.PPERequirements,
            TravelPercentage = dto.TravelPercentage,
            TravelRequirements = dto.TravelRequirements
        };
    }

    public static void UpdateEntity(this UpdateJobWorkingConditionDto dto, JobWorkingCondition entity)
    {
        entity.EnvironmentType = dto.EnvironmentType;
        entity.Description = dto.Description;
        entity.ExposureLevel = dto.ExposureLevel;
        entity.RequiresPPE = dto.RequiresPPE;
        entity.PPERequirements = dto.PPERequirements;
        entity.TravelPercentage = dto.TravelPercentage;
        entity.TravelRequirements = dto.TravelRequirements;
    }

    public static List<JobWorkingConditionDto> ToDtoList(this IEnumerable<JobWorkingCondition> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobEquipmentTool

    public static JobEquipmentToolDto ToDto(this JobEquipmentTool entity)
    {
        return new JobEquipmentToolDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionId = entity.JobDescriptionId,
            ItemName = entity.ItemName,
            Type = entity.Type,
            DescriptionOrSpecification = entity.DescriptionOrSpecification,
            RequiredProficiency = entity.RequiredProficiency,
            IsEssential = entity.IsEssential,
            TrainingRequired = entity.TrainingRequired,
            LinkedQualificationId = entity.LinkedQualificationId,
            TrainingRequirements = entity.TrainingRequirements?.Select(t => t.ToDto()).ToList() ?? new List<JobEquipmentTrainingDto>(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobEquipmentTool ToEntity(this CreateJobEquipmentToolDto dto)
    {
        return new JobEquipmentTool
        {
            JobDescriptionId = dto.JobDescriptionId,
            ItemName = dto.ItemName,
            Type = dto.Type,
            DescriptionOrSpecification = dto.DescriptionOrSpecification,
            RequiredProficiency = dto.RequiredProficiency,
            IsEssential = dto.IsEssential,
            TrainingRequired = dto.TrainingRequired,
            LinkedQualificationId = dto.LinkedQualificationId
        };
    }

    public static void UpdateEntity(this UpdateJobEquipmentToolDto dto, JobEquipmentTool entity)
    {
        entity.ItemName = dto.ItemName;
        entity.Type = dto.Type;
        entity.DescriptionOrSpecification = dto.DescriptionOrSpecification;
        entity.RequiredProficiency = dto.RequiredProficiency;
        entity.IsEssential = dto.IsEssential;
        entity.TrainingRequired = dto.TrainingRequired;
        entity.LinkedQualificationId = dto.LinkedQualificationId;
    }

    public static List<JobEquipmentToolDto> ToDtoList(this IEnumerable<JobEquipmentTool> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobReportingRelationship

    public static JobReportingRelationshipDto ToDto(this JobReportingRelationship entity)
    {
        return new JobReportingRelationshipDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionId = entity.JobDescriptionId,
            RelationshipType = entity.RelationshipType,
            TitleOrRole = entity.TitleOrRole,
            EmployeeOrPositionId = entity.EmployeeOrPositionId,
            RelatedPositionTitle = entity.RelatedPosition?.Title,
            Description = entity.Description,
            NumberOfDirectReports = entity.NumberOfDirectReports,
            IsPrimarySupervisor = entity.IsPrimarySupervisor,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobReportingRelationship ToEntity(this CreateJobReportingRelationshipDto dto)
    {
        return new JobReportingRelationship
        {
            JobDescriptionId = dto.JobDescriptionId,
            RelationshipType = dto.RelationshipType,
            TitleOrRole = dto.TitleOrRole,
            EmployeeOrPositionId = dto.EmployeeOrPositionId,
            Description = dto.Description,
            NumberOfDirectReports = dto.NumberOfDirectReports,
            IsPrimarySupervisor = dto.IsPrimarySupervisor
        };
    }

    public static void UpdateEntity(this UpdateJobReportingRelationshipDto dto, JobReportingRelationship entity)
    {
        entity.RelationshipType = dto.RelationshipType;
        entity.TitleOrRole = dto.TitleOrRole;
        entity.EmployeeOrPositionId = dto.EmployeeOrPositionId;
        entity.Description = dto.Description;
        entity.NumberOfDirectReports = dto.NumberOfDirectReports;
        entity.IsPrimarySupervisor = dto.IsPrimarySupervisor;
    }

    public static List<JobReportingRelationshipDto> ToDtoList(this IEnumerable<JobReportingRelationship> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobDutyItem

    public static JobDutyItemDto ToDto(this JobDutyItem entity)
    {
        return new JobDutyItemDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionId = entity.JobDescriptionId,
            SequenceNumber = entity.SequenceNumber,
            DutyStatement = entity.DutyStatement,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobDutyItem ToEntity(this CreateJobDutyItemDto dto)
    {
        return new JobDutyItem
        {
            JobDescriptionId = dto.JobDescriptionId,
            SequenceNumber = dto.SequenceNumber,
            DutyStatement = dto.DutyStatement,
            Notes = dto.Notes
        };
    }

    public static void UpdateEntity(this UpdateJobDutyItemDto dto, JobDutyItem entity)
    {
        entity.SequenceNumber = dto.SequenceNumber;
        entity.DutyStatement = dto.DutyStatement;
        entity.Notes = dto.Notes;
    }

    public static List<JobDutyItemDto> ToDtoList(this IEnumerable<JobDutyItem> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobPpeRequirement

    public static JobPpeRequirementDto ToDto(this JobPpeRequirement entity)
    {
        return new JobPpeRequirementDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionId = entity.JobDescriptionId,
            PpeTypeId = entity.PpeTypeId,
            PpeTypeName = entity.PpeType?.Name,
            CustomPpeName = entity.CustomPpeName,
            IsMandatory = entity.IsMandatory,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobPpeRequirement ToEntity(this CreateJobPpeRequirementDto dto)
    {
        return new JobPpeRequirement
        {
            JobDescriptionId = dto.JobDescriptionId,
            PpeTypeId = dto.PpeTypeId,
            CustomPpeName = dto.CustomPpeName,
            IsMandatory = dto.IsMandatory,
            Notes = dto.Notes
        };
    }

    public static void UpdateEntity(this UpdateJobPpeRequirementDto dto, JobPpeRequirement entity)
    {
        entity.PpeTypeId = dto.PpeTypeId;
        entity.CustomPpeName = dto.CustomPpeName;
        entity.IsMandatory = dto.IsMandatory;
        entity.Notes = dto.Notes;
    }

    public static List<JobPpeRequirementDto> ToDtoList(this IEnumerable<JobPpeRequirement> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobEquipmentTraining

    public static JobEquipmentTrainingDto ToDto(this JobEquipmentTraining entity)
    {
        return new JobEquipmentTrainingDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobEquipmentToolId = entity.JobEquipmentToolId,
            TrainingProgramId = entity.TrainingProgramId,
            TrainingProgramName = entity.TrainingProgram?.ProgramName,
            RequirementText = entity.RequirementText,
            IsMandatory = entity.IsMandatory,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobEquipmentTraining ToEntity(this CreateJobEquipmentTrainingDto dto)
    {
        return new JobEquipmentTraining
        {
            JobEquipmentToolId = dto.JobEquipmentToolId,
            TrainingProgramId = dto.TrainingProgramId,
            RequirementText = dto.RequirementText,
            IsMandatory = dto.IsMandatory
        };
    }

    public static void UpdateEntity(this UpdateJobEquipmentTrainingDto dto, JobEquipmentTraining entity)
    {
        entity.TrainingProgramId = dto.TrainingProgramId;
        entity.RequirementText = dto.RequirementText;
        entity.IsMandatory = dto.IsMandatory;
    }

    public static List<JobEquipmentTrainingDto> ToDtoList(this IEnumerable<JobEquipmentTraining> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobMedicalRequirement

    public static JobMedicalRequirementDto ToDto(this JobMedicalRequirement entity)
    {
        return new JobMedicalRequirementDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobDescriptionId = entity.JobDescriptionId,
            Category = entity.Category,
            RequirementDescription = entity.RequirementDescription,
            Rationale = entity.Rationale,
            Contraindications = entity.Contraindications,
            IsMandatory = entity.IsMandatory,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobMedicalRequirement ToEntity(this CreateJobMedicalRequirementDto dto)
    {
        return new JobMedicalRequirement
        {
            JobDescriptionId = dto.JobDescriptionId,
            Category = dto.Category,
            RequirementDescription = dto.RequirementDescription,
            Rationale = dto.Rationale,
            Contraindications = dto.Contraindications,
            IsMandatory = dto.IsMandatory
        };
    }

    public static void UpdateEntity(this UpdateJobMedicalRequirementDto dto, JobMedicalRequirement entity)
    {
        entity.Category = dto.Category;
        entity.RequirementDescription = dto.RequirementDescription;
        entity.Rationale = dto.Rationale;
        entity.Contraindications = dto.Contraindications;
        entity.IsMandatory = dto.IsMandatory;
    }

    public static List<JobMedicalRequirementDto> ToDtoList(this IEnumerable<JobMedicalRequirement> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobResponsibilityKpi

    public static JobResponsibilityKpiDto ToDto(this JobResponsibilityKpi entity)
    {
        return new JobResponsibilityKpiDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            JobResponsibilityId = entity.JobResponsibilityId,
            KpiStatement = entity.KpiStatement,
            TargetOrStandard = entity.TargetOrStandard,
            UnitOfMeasure = entity.UnitOfMeasure,
            Weight = entity.Weight,
            SequenceNumber = entity.SequenceNumber,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static JobResponsibilityKpi ToEntity(this CreateJobResponsibilityKpiDto dto)
    {
        return new JobResponsibilityKpi
        {
            JobResponsibilityId = dto.JobResponsibilityId,
            KpiStatement = dto.KpiStatement,
            TargetOrStandard = dto.TargetOrStandard,
            UnitOfMeasure = dto.UnitOfMeasure,
            Weight = dto.Weight,
            SequenceNumber = dto.SequenceNumber
        };
    }

    public static void UpdateEntity(this UpdateJobResponsibilityKpiDto dto, JobResponsibilityKpi entity)
    {
        entity.KpiStatement = dto.KpiStatement;
        entity.TargetOrStandard = dto.TargetOrStandard;
        entity.UnitOfMeasure = dto.UnitOfMeasure;
        entity.Weight = dto.Weight;
        entity.SequenceNumber = dto.SequenceNumber;
    }

    public static List<JobResponsibilityKpiDto> ToDtoList(this IEnumerable<JobResponsibilityKpi> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion
}
