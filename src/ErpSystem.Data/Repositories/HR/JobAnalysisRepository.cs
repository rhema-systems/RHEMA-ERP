using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

#region Job Description Repository

public class JobDescriptionRepository : GenericRepository<JobDescription>, IJobDescriptionRepository
{
    public JobDescriptionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<JobDescription?> GetByJobDescriptionNumberAsync(string jobDescriptionNumber)
    {
        return await _dbSet
            .WithLookups()
            .Include(jd => jd.Responsibilities)
            .FirstOrDefaultAsync(jd => jd.JobDescriptionNumber == jobDescriptionNumber);
    }

    public async Task<IEnumerable<JobDescription>> GetByPositionIdAsync(Guid positionId)
    {
        return await _dbSet
            .WithLookups()
            .Where(jd => jd.PositionId == positionId)
            .OrderByDescending(jd => jd.VersionNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobDescription>> GetByStatusAsync(JobDescriptionStatus status)
    {
        return await _dbSet
            .WithLookups()
            .Where(jd => jd.Status == status)
            .OrderByDescending(jd => jd.EffectiveDate)
            .ToListAsync();
    }

    public async Task<JobDescription?> GetCurrentVersionForPositionAsync(Guid positionId)
    {
        return await _dbSet
            .WithLookups()
            .Include(jd => jd.Responsibilities)
            .Where(jd => jd.PositionId == positionId &&
                        jd.Status == JobDescriptionStatus.Approved &&
                        jd.EffectiveDate <= DateTime.Today &&
                        (jd.ExpiryDate == null || jd.ExpiryDate >= DateTime.Today))
            .OrderByDescending(jd => jd.VersionNumber)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<JobDescription>> GetDueForReviewAsync(int daysAhead = 30)
    {
        var futureDate = DateTime.Today.AddDays(daysAhead);

        return await _dbSet
            .WithLookups()
            .Where(jd => jd.Status == JobDescriptionStatus.Approved &&
                        jd.NextReviewDate != null &&
                        jd.NextReviewDate <= futureDate)
            .OrderBy(jd => jd.NextReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobDescription>> GetVersionHistoryAsync(Guid positionId)
    {
        return await _dbSet
            .WithLookups()
            .Where(jd => jd.PositionId == positionId)
            .OrderByDescending(jd => jd.VersionNumber)
            .ToListAsync();
    }

    public async Task<int> GetNextVersionNumberAsync(Guid positionId)
    {
        var maxVersion = await _dbSet
            .Where(jd => jd.PositionId == positionId)
            .MaxAsync(jd => (int?)jd.VersionNumber) ?? 0;

        return maxVersion + 1;
    }
}

#endregion Job Description Repository

#region Job Responsibility Repository

public class JobResponsibilityRepository : GenericRepository<JobResponsibility>, IJobResponsibilityRepository
{
    public JobResponsibilityRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<JobResponsibility>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Include(r => r.Qualifications)
            .Include(r => r.Competencies)
            .Include(r => r.Kpis)
            .Where(r => r.JobDescriptionId == jobDescriptionId)
            .OrderBy(r => r.Type)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobResponsibility>> GetByTypeAsync(Guid jobDescriptionId, ResponsibilityType type)
    {
        return await _dbSet
            .Where(r => r.JobDescriptionId == jobDescriptionId && r.Type == type)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobResponsibility>> GetCoreResponsibilitiesAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Where(r => r.JobDescriptionId == jobDescriptionId && r.Type == ResponsibilityType.Core)
            .OrderByDescending(r => r.ImportanceWeight)
            .ToListAsync();
    }
}

#endregion Job Responsibility Repository

#region Job Qualification Repository

public class JobQualificationRepository : GenericRepository<JobQualification>, IJobQualificationRepository
{
    public JobQualificationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<JobQualification>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Include(q => q.Qualification)
            .Include(q => q.Certification)
            .Where(q => q.JobDescriptionId == jobDescriptionId)
            .OrderBy(q => q.Type)
            .ThenByDescending(q => q.IsRequired)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobQualification>> GetByResponsibilityIdAsync(Guid responsibilityId)
    {
        return await _dbSet
            .Include(q => q.Qualification)
            .Where(q => q.JobResponsibilityId == responsibilityId)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobQualification>> GetByTypeAsync(Guid jobDescriptionId, QualificationType type)
    {
        return await _dbSet
            .Include(q => q.Qualification)
            .Where(q => q.JobDescriptionId == jobDescriptionId && q.Type == type)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobQualification>> GetRequiredQualificationsAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Include(q => q.Qualification)
            .Where(q => q.JobDescriptionId == jobDescriptionId && q.IsRequired)
            .ToListAsync();
    }
}

#endregion Job Qualification Repository

#region Job Competency Repository

public class JobCompetencyRepository : GenericRepository<JobCompetency>, IJobCompetencyRepository
{
    public JobCompetencyRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<JobCompetency>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Include(c => c.Skill)
            .Include(c => c.Competency)
            .Where(c => c.JobDescriptionId == jobDescriptionId)
            .OrderBy(c => c.Type)
            .ThenByDescending(c => c.IsCritical)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobCompetency>> GetByResponsibilityIdAsync(Guid responsibilityId)
    {
        return await _dbSet
            .Where(c => c.JobResponsibilityId == responsibilityId)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobCompetency>> GetByTypeAsync(Guid jobDescriptionId, CompetencyType type)
    {
        return await _dbSet
            .Where(c => c.JobDescriptionId == jobDescriptionId && c.Type == type)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobCompetency>> GetCriticalCompetenciesAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Where(c => c.JobDescriptionId == jobDescriptionId && c.IsCritical)
            .ToListAsync();
    }
}

#endregion Job Competency Repository

#region Manpower Budget Repository

public class ManpowerBudgetRepository : GenericRepository<ManpowerBudget>, IManpowerBudgetRepository
{
    public ManpowerBudgetRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<ManpowerBudget?> GetByBudgetNumberAsync(string budgetNumber)
    {
        return await _dbSet
            .WithLookups()
            .Include(b => b.BudgetLines).ThenInclude(l => l.Position)
            .FirstOrDefaultAsync(b => b.BudgetNumber == budgetNumber);
    }

    public async Task<IEnumerable<ManpowerBudget>> GetByFiscalYearAsync(int fiscalYear)
    {
        return await _dbSet
            .WithLookups()
            .Where(b => b.FiscalYear == fiscalYear)
            .OrderBy(b => b.OrganizationUnit != null ? b.OrganizationUnit.Name : string.Empty)
            .ToListAsync();
    }

    public async Task<IEnumerable<ManpowerBudget>> GetByOrganizationUnitIdAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .WithLookups()
            .Where(b => b.OrganizationUnitId == organizationUnitId)
            .OrderByDescending(b => b.FiscalYear)
            .ToListAsync();
    }

    public async Task<IEnumerable<ManpowerBudget>> GetByOrganizationLevelIdAsync(Guid organizationLevelId)
    {
        return await _dbSet
            .WithLookups()
            .Where(b => b.OrganizationLevelId == organizationLevelId)
            .OrderByDescending(b => b.FiscalYear)
            .ToListAsync();
    }

    public async Task<IEnumerable<ManpowerBudget>> GetByStatusAsync(ManpowerBudgetStatus status)
    {
        return await _dbSet
            .WithLookups()
            .Where(b => b.Status == status)
            .OrderByDescending(b => b.FiscalYear)
            .ToListAsync();
    }

    public async Task<ManpowerBudget?> GetCurrentBudgetForOrganizationUnitAsync(Guid organizationUnitId)
    {
        var currentYear = DateTime.Today.Year;

        // ⚠ Ordered, because "current" has to be a single answer. This took FirstOrDefault with no
        // ordering, so once a unit had two approved budgets for the same year — a revision approved
        // mid-year, or simply two submissions — the endpoint returned an arbitrary one, and could
        // return a different one on the next call. Found by running the content audit twice.
        //
        // Most recently approved wins, which makes the READ agree with what the WRITE already does:
        // approving a budget overwrites ExpectedHeadcount for every position it names (decision
        // D-2), so the latest approval is already the establishment in force. Anything else would
        // report one budget while a different one governs recruitment.
        return await _dbSet
            .WithLookups()
            .Include(b => b.BudgetLines).ThenInclude(l => l.Position)
            .Where(b => b.OrganizationUnitId == organizationUnitId &&
                       b.FiscalYear == currentYear &&
                       (b.Status == ManpowerBudgetStatus.Active ||
                        b.Status == ManpowerBudgetStatus.Approved))
            .OrderByDescending(b => b.Status == ManpowerBudgetStatus.Active)
            .ThenByDescending(b => b.ApprovalDate)
            .ThenByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ManpowerBudget>> GetPendingApprovalsAsync()
    {
        return await _dbSet
            .WithLookups()
            .Where(b => b.Status == ManpowerBudgetStatus.Submitted)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync();
    }
}

#endregion Manpower Budget Repository

#region Manpower Budget Line Repository

public class ManpowerBudgetLineRepository : GenericRepository<ManpowerBudgetLine>, IManpowerBudgetLineRepository
{
    public ManpowerBudgetLineRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ManpowerBudgetLine>> GetByBudgetIdAsync(Guid budgetId)
    {
        return await _dbSet
            .Include(l => l.Position)
            .Include(l => l.JobDescription)
            .Include(l => l.SalaryGrade).Include(l => l.SalaryLevel).Include(l => l.SalaryNotch)
            .Where(l => l.ManpowerBudgetId == budgetId)
            .OrderBy(l => l.Position.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<ManpowerBudgetLine>> GetByPositionIdAsync(Guid positionId)
    {
        return await _dbSet
            .Include(l => l.ManpowerBudget)
            .Where(l => l.PositionId == positionId)
            .OrderByDescending(l => l.ManpowerBudget.FiscalYear)
            .ToListAsync();
    }

    public async Task<IEnumerable<ManpowerBudgetLine>> GetCriticalPositionsAsync(Guid budgetId)
    {
        return await _dbSet
            .Include(l => l.Position)
            .Include(l => l.SalaryGrade).Include(l => l.SalaryLevel).Include(l => l.SalaryNotch)
            .Where(l => l.ManpowerBudgetId == budgetId && l.IsCritical)
            .OrderBy(l => l.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<ManpowerBudgetLine>> GetByQuarterAsync(Guid budgetId, int quarter)
    {
        return await _dbSet
            .Include(l => l.Position)
            .Where(l => l.ManpowerBudgetId == budgetId && l.Quarter == quarter)
            .OrderBy(l => l.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<ManpowerBudgetLine>> GetByPriorityAsync(Guid budgetId, BudgetPriority priority)
    {
        return await _dbSet
            .Include(l => l.Position)
            .Where(l => l.ManpowerBudgetId == budgetId && l.Priority == priority)
            .ToListAsync();
    }
}

#endregion Manpower Budget Line Repository

#region Job Physical Demand Repository

public class JobPhysicalDemandRepository : GenericRepository<JobPhysicalDemand>, IJobPhysicalDemandRepository
{
    public JobPhysicalDemandRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobPhysicalDemand>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Where(p => p.JobDescriptionId == jobDescriptionId)
            .OrderBy(p => p.DemandType)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobPhysicalDemand>> GetByTypeAsync(Guid jobDescriptionId, PhysicalDemandType demandType)
    {
        return await _dbSet
            .Where(p => p.JobDescriptionId == jobDescriptionId && p.DemandType == demandType)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobPhysicalDemand>> GetEssentialDemandsAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Where(p => p.JobDescriptionId == jobDescriptionId && p.IsEssential)
            .OrderBy(p => p.DemandType)
            .ToListAsync();
    }
}

#endregion Job Physical Demand Repository

#region Job Working Condition Repository

public class JobWorkingConditionRepository : GenericRepository<JobWorkingCondition>, IJobWorkingConditionRepository
{
    public JobWorkingConditionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobWorkingCondition>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Where(w => w.JobDescriptionId == jobDescriptionId)
            .OrderBy(w => w.EnvironmentType)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobWorkingCondition>> GetByEnvironmentTypeAsync(Guid jobDescriptionId, WorkEnvironmentType environmentType)
    {
        return await _dbSet
            .Where(w => w.JobDescriptionId == jobDescriptionId && w.EnvironmentType == environmentType)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobWorkingCondition>> GetRequiringPPEAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Where(w => w.JobDescriptionId == jobDescriptionId && w.RequiresPPE)
            .ToListAsync();
    }
}

#endregion Job Working Condition Repository

#region Job Equipment Tool Repository

public class JobEquipmentToolRepository : GenericRepository<JobEquipmentTool>, IJobEquipmentToolRepository
{
    public JobEquipmentToolRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobEquipmentTool>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Include(e => e.TrainingRequirements).ThenInclude(t => t.TrainingProgram)
            .Where(e => e.JobDescriptionId == jobDescriptionId)
            .OrderBy(e => e.Type)
            .ThenBy(e => e.ItemName)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobEquipmentTool>> GetByTypeAsync(Guid jobDescriptionId, EquipmentType type)
    {
        return await _dbSet
            .Where(e => e.JobDescriptionId == jobDescriptionId && e.Type == type)
            .OrderBy(e => e.ItemName)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobEquipmentTool>> GetEssentialToolsAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Where(e => e.JobDescriptionId == jobDescriptionId && e.IsEssential)
            .OrderBy(e => e.ItemName)
            .ToListAsync();
    }
}

#endregion Job Equipment Tool Repository

#region Job Duty Item Repository

public class JobDutyItemRepository : GenericRepository<JobDutyItem>, IJobDutyItemRepository
{
    public JobDutyItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobDutyItem>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Where(d => d.JobDescriptionId == jobDescriptionId)
            .OrderBy(d => d.SequenceNumber)
            .ToListAsync();
    }

    public async Task<int> GetNextSequenceNumberAsync(Guid jobDescriptionId)
    {
        var max = await _dbSet
            .Where(d => d.JobDescriptionId == jobDescriptionId)
            .MaxAsync(d => (int?)d.SequenceNumber) ?? 0;
        return max + 1;
    }
}

#endregion Job Duty Item Repository

#region Job PPE Requirement Repository

public class JobPpeRequirementRepository : GenericRepository<JobPpeRequirement>, IJobPpeRequirementRepository
{
    public JobPpeRequirementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobPpeRequirement>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Include(p => p.PpeType)
            .Where(p => p.JobDescriptionId == jobDescriptionId)
            .OrderByDescending(p => p.IsMandatory)
            .ToListAsync();
    }
}

#endregion Job PPE Requirement Repository

#region Job Equipment Training Repository

public class JobEquipmentTrainingRepository : GenericRepository<JobEquipmentTraining>, IJobEquipmentTrainingRepository
{
    public JobEquipmentTrainingRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobEquipmentTraining>> GetByEquipmentToolIdAsync(Guid jobEquipmentToolId)
    {
        return await _dbSet
            .Include(t => t.TrainingProgram)
            .Where(t => t.JobEquipmentToolId == jobEquipmentToolId)
            .OrderByDescending(t => t.IsMandatory)
            .ToListAsync();
    }
}

#endregion Job Equipment Training Repository

#region Job Medical Requirement Repository

public class JobMedicalRequirementRepository : GenericRepository<JobMedicalRequirement>, IJobMedicalRequirementRepository
{
    public JobMedicalRequirementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobMedicalRequirement>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Where(m => m.JobDescriptionId == jobDescriptionId)
            .OrderBy(m => m.Category)
            .ToListAsync();
    }
}

#endregion Job Medical Requirement Repository

#region Job Responsibility KPI Repository

public class JobResponsibilityKpiRepository : GenericRepository<JobResponsibilityKpi>, IJobResponsibilityKpiRepository
{
    public JobResponsibilityKpiRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobResponsibilityKpi>> GetByResponsibilityIdAsync(Guid responsibilityId)
    {
        return await _dbSet
            .Where(k => k.JobResponsibilityId == responsibilityId)
            .OrderBy(k => k.SequenceNumber)
            .ToListAsync();
    }
}

#endregion Job Responsibility KPI Repository

#region Job Reporting Relationship Repository

public class JobReportingRelationshipRepository : GenericRepository<JobReportingRelationship>, IJobReportingRelationshipRepository
{
    public JobReportingRelationshipRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobReportingRelationship>> GetByJobDescriptionIdAsync(Guid jobDescriptionId)
    {
        return await _dbSet
            .Include(r => r.RelatedPosition)
            .Where(r => r.JobDescriptionId == jobDescriptionId)
            .OrderBy(r => r.RelationshipType)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobReportingRelationship>> GetByRelationshipTypeAsync(Guid jobDescriptionId, ReportingRelationshipType relationshipType)
    {
        return await _dbSet
            .Include(r => r.RelatedPosition)
            .Where(r => r.JobDescriptionId == jobDescriptionId && r.RelationshipType == relationshipType)
            .ToListAsync();
    }
}

#endregion Job Reporting Relationship Repository
