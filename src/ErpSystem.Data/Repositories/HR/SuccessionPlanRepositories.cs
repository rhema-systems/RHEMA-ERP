using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// SUCCESSION PLAN REPOSITORY
// ============================================================================

#region Succession Plan Repository

public class SuccessionPlanRepository : GenericRepository<SuccessionPlan>, ISuccessionPlanRepository
{
    public SuccessionPlanRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SuccessionPlan?> GetByPlanNumberAsync(string planNumber)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .FirstOrDefaultAsync(p => p.PlanNumber == planNumber && !p.IsDeleted);
    }

    public async Task<SuccessionPlan?> GetActiveVersionForPositionAsync(Guid positionId)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Include(p => p.EmergencySuccessor)
            .Include(p => p.ReviewedBy)
            .Include(p => p.ApprovedBy)
            .Include(p => p.CompetencyRequirements).ThenInclude(r => r.Competency)
            .Include(p => p.Candidates).ThenInclude(c => c.Employee)
            .Include(p => p.Actions).ThenInclude(a => a.ResponsiblePerson)
            .Include(p => p.Documents).ThenInclude(d => d.UploadedBy)
            .FirstOrDefaultAsync(p => p.PositionId == positionId && p.IsActiveVersion && !p.IsDeleted);
    }

    public async Task<IEnumerable<SuccessionPlan>> GetAllVersionsForPositionAsync(Guid positionId)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.PositionId == positionId && !p.IsDeleted)
            .OrderByDescending(p => p.VersionNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionPlan>> GetByStatusAsync(SuccessionPlanStatus status)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.Status == status && p.IsActiveVersion && !p.IsDeleted)
            .OrderBy(p => p.Position.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionPlan>> GetByYearAsync(int planYear)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.PlanYear == planYear && p.IsActiveVersion && !p.IsDeleted)
            .OrderBy(p => p.Position.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionPlan>> GetByYearAndStatusAsync(int planYear, SuccessionPlanStatus status)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.PlanYear == planYear && p.Status == status && p.IsActiveVersion && !p.IsDeleted)
            .OrderBy(p => p.Position.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionPlan>> GetByCriticalityAsync(PositionCriticality criticality)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.Criticality == criticality && p.IsActiveVersion && !p.IsDeleted)
            .OrderBy(p => p.Position.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionPlan>> GetByRiskLevelAsync(SuccessionRisk riskLevel)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.RiskLevel >= riskLevel && p.IsActiveVersion && !p.IsDeleted)
            .OrderByDescending(p => p.RiskLevel)
            .ThenBy(p => p.Position.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionPlan>> GetDueForReviewAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.IsActiveVersion && !p.IsDeleted
                     && p.NextReviewDate != null
                     && p.NextReviewDate <= cutoff)
            .OrderBy(p => p.NextReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionPlan>> GetWithNoReadyNowSuccessorAsync()
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.IsActiveVersion && !p.IsDeleted && !p.HasReadyNowSuccessor)
            .OrderByDescending(p => p.Criticality)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionPlan>> GetWithNoSuccessorsAsync()
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.IsActiveVersion && !p.IsDeleted && p.NumberOfIdentifiedSuccessors == 0)
            .OrderByDescending(p => p.Criticality)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionPlan>> GetByIncumbentAsync(Guid incumbentEmployeeId)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.CurrentIncumbentId == incumbentEmployeeId && p.IsActiveVersion && !p.IsDeleted)
            .OrderBy(p => p.Position.Title)
            .ToListAsync();
    }

    public async Task<SuccessionPlan?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Include(p => p.EmergencySuccessor)
            .Include(p => p.SupersededByPlan)
            .Include(p => p.ReviewedBy)
            .Include(p => p.ApprovedBy)
            .Include(p => p.CompetencyRequirements).ThenInclude(r => r.Competency)
            .Include(p => p.Candidates).ThenInclude(c => c.Employee)
            .Include(p => p.Candidates).ThenInclude(c => c.TalentPoolMember).ThenInclude(m => m!.TalentPool)
            .Include(p => p.Candidates).ThenInclude(c => c.CompetencyGaps).ThenInclude(g => g.Competency)
            .Include(p => p.Candidates).ThenInclude(c => c.DevelopmentActivities).ThenInclude(a => a.Milestones)
            .Include(p => p.Candidates).ThenInclude(c => c.Feedback)
            .Include(p => p.Actions).ThenInclude(a => a.ResponsiblePerson)
            .Include(p => p.Actions).ThenInclude(a => a.AssignedBy)
            .Include(p => p.History).ThenInclude(h => h.SnapshotCreatedBy)
            .Include(p => p.Documents).ThenInclude(d => d.UploadedBy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<int> GetNextVersionNumberAsync(Guid positionId)
    {
        var maxVersion = await _dbSet
            .Where(p => p.PositionId == positionId && !p.IsDeleted)
            .MaxAsync(p => (int?)p.VersionNumber) ?? 0;

        return maxVersion + 1;
    }

    public async Task<IEnumerable<SuccessionPlan>> GetWithImpendingVacancyAsync(int daysAhead = 90)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent)
            .Where(p => p.IsActiveVersion && !p.IsDeleted
                     && p.AnticipatedVacancyDate != null
                     && p.AnticipatedVacancyDate <= cutoff)
            .OrderBy(p => p.AnticipatedVacancyDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SUCCESSION COMPETENCY REQUIREMENT REPOSITORY
// ============================================================================

#region Succession Competency Requirement Repository

public class SuccessionCompetencyRequirementRepository : GenericRepository<SuccessionCompetencyRequirement>, ISuccessionCompetencyRequirementRepository
{
    public SuccessionCompetencyRequirementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SuccessionCompetencyRequirement>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(r => r.Competency)
            .Where(r => r.SuccessionPlanId == planId && !r.IsDeleted)
            .OrderBy(r => r.Competency.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionCompetencyRequirement>> GetByCompetencyIdAsync(Guid competencyId)
    {
        return await _dbSet
            .Include(r => r.SuccessionPlan).ThenInclude(p => p.Position)
            .Where(r => r.CompetencyId == competencyId && !r.IsDeleted)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SUCCESSION CANDIDATE REPOSITORY
// ============================================================================

#region Succession Candidate Repository

public class SuccessionCandidateRepository : GenericRepository<SuccessionCandidate>, ISuccessionCandidateRepository
{
    public SuccessionCandidateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SuccessionCandidate>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.TalentPoolMember).ThenInclude(m => m!.TalentPool)
            .Where(c => c.SuccessionPlanId == planId && !c.IsDeleted)
            .OrderBy(c => c.Rank)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionCandidate>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(c => c.SuccessionPlan).ThenInclude(p => p.Position)
            .Where(c => c.EmployeeId == employeeId && !c.IsDeleted)
            .OrderByDescending(c => c.SuccessionPlan.PlanYear)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionCandidate>> GetByReadinessAsync(Guid planId, ReadinessLevel readiness)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => c.SuccessionPlanId == planId && c.CurrentReadiness == readiness && !c.IsDeleted)
            .OrderBy(c => c.Rank)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionCandidate>> GetReadyNowCandidatesForPlanAsync(Guid planId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => c.SuccessionPlanId == planId
                     && c.CurrentReadiness == ReadinessLevel.ReadyNow
                     && !c.IsEmergencyOnly
                     && !c.IsDeleted)
            .OrderBy(c => c.Rank)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionCandidate>> GetEmergencyCandidatesForPlanAsync(Guid planId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => c.SuccessionPlanId == planId && c.IsEmergencyOnly && !c.IsDeleted)
            .OrderBy(c => c.Rank)
            .ToListAsync();
    }

    public async Task<SuccessionCandidate?> GetSelectedCandidateForPlanAsync(Guid planId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.SuccessionPlanId == planId && c.IsSelected && !c.IsDeleted);
    }

    public async Task<SuccessionCandidate?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.SuccessionPlan).ThenInclude(p => p.Position)
            .Include(c => c.TalentPoolMember).ThenInclude(m => m!.TalentPool)
            .Include(c => c.TalentReviewRating)
            .Include(c => c.AssessedBy)
            .Include(c => c.RecommendedBy)
            .Include(c => c.CompetencyGaps).ThenInclude(g => g.Competency)
            .Include(c => c.CompetencyGaps).ThenInclude(g => g.AddressedByActivity)
            .Include(c => c.DevelopmentActivities).ThenInclude(a => a.Milestones)
            .Include(c => c.DevelopmentActivities).ThenInclude(a => a.Supervisor)
            .Include(c => c.Feedback).ThenInclude(f => f.Reviewer)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public async Task<IEnumerable<SuccessionCandidate>> GetByRetentionRiskAsync(Guid planId, RetentionRisk minimumRisk)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => c.SuccessionPlanId == planId
                     && c.RetentionRisk >= minimumRisk
                     && !c.IsDeleted)
            .OrderByDescending(c => c.RetentionRisk)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SUCCESSION CANDIDATE GAP REPOSITORY
// ============================================================================

#region Succession Candidate Gap Repository

public class SuccessionCandidateGapRepository : GenericRepository<SuccessionCandidateGap>, ISuccessionCandidateGapRepository
{
    public SuccessionCandidateGapRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SuccessionCandidateGap>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Include(g => g.Competency)
            .Include(g => g.AddressedByActivity)
            .Where(g => g.CandidateId == candidateId && !g.IsDeleted)
            .OrderBy(g => g.Competency.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionCandidateGap>> GetUnaddressedGapsAsync(Guid candidateId)
    {
        return await _dbSet
            .Include(g => g.Competency)
            .Where(g => g.CandidateId == candidateId && !g.Addressed && !g.IsDeleted)
            .OrderByDescending(g => g.RequiredLevel - g.CurrentLevel)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionCandidateGap>> GetByCompetencyIdAsync(Guid competencyId)
    {
        return await _dbSet
            .Include(g => g.Candidate).ThenInclude(c => c.Employee)
            .Include(g => g.Competency)
            .Where(g => g.CompetencyId == competencyId && !g.IsDeleted)
            .OrderByDescending(g => g.RequiredLevel - g.CurrentLevel)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SUCCESSION DEVELOPMENT ACTIVITY REPOSITORY
// ============================================================================

#region Succession Development Activity Repository

public class SuccessionDevelopmentActivityRepository : GenericRepository<SuccessionDevelopmentActivity>, ISuccessionDevelopmentActivityRepository
{
    public SuccessionDevelopmentActivityRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SuccessionDevelopmentActivity>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Include(a => a.Milestones)
            .Include(a => a.Supervisor)
            .Where(a => a.CandidateId == candidateId && !a.IsDeleted)
            .OrderBy(a => a.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivity>> GetByTalentPoolMemberIdAsync(Guid memberId)
    {
        return await _dbSet
            .Include(a => a.Milestones)
            .Include(a => a.Supervisor)
            .Where(a => a.TalentPoolMemberId == memberId && !a.IsDeleted)
            .OrderBy(a => a.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivity>> GetByStatusAsync(DevelopmentActivityStatus status)
    {
        return await _dbSet
            .Include(a => a.Supervisor)
            .Include(a => a.Candidate).ThenInclude(c => c!.Employee)
            .Include(a => a.TalentPoolMember).ThenInclude(m => m!.Employee)
            .Where(a => a.Status == status && !a.IsDeleted)
            .OrderBy(a => a.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivity>> GetOverdueActivitiesAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(a => a.Supervisor)
            .Include(a => a.Candidate).ThenInclude(c => c!.Employee)
            .Include(a => a.TalentPoolMember).ThenInclude(m => m!.Employee)
            .Where(a => !a.IsDeleted
                     && a.PlannedEndDate != null
                     && a.PlannedEndDate < today
                     && a.Status != DevelopmentActivityStatus.Completed
                     && a.Status != DevelopmentActivityStatus.Cancelled)
            .OrderBy(a => a.PlannedEndDate)
            .ToListAsync();
    }

    public async Task<SuccessionDevelopmentActivity?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.Candidate).ThenInclude(c => c!.Employee)
            .Include(a => a.TalentPoolMember).ThenInclude(m => m!.Employee)
            .Include(a => a.Supervisor)
            .Include(a => a.Milestones)
            .Include(a => a.AddressedGaps).ThenInclude(g => g.Competency)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }
}

#endregion

// ============================================================================
// SUCCESSION DEVELOPMENT MILESTONE REPOSITORY
// ============================================================================

#region Succession Development Milestone Repository

public class SuccessionDevelopmentMilestoneRepository : GenericRepository<SuccessionDevelopmentMilestone>, ISuccessionDevelopmentMilestoneRepository
{
    public SuccessionDevelopmentMilestoneRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SuccessionDevelopmentMilestone>> GetByActivityIdAsync(Guid activityId)
    {
        return await _dbSet
            .Where(m => m.ActivityId == activityId && !m.IsDeleted)
            .OrderBy(m => m.TargetDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionDevelopmentMilestone>> GetOverdueMilestonesAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(m => m.Activity).ThenInclude(a => a.Candidate).ThenInclude(c => c!.Employee)
            .Include(m => m.Activity).ThenInclude(a => a.TalentPoolMember).ThenInclude(t => t!.Employee)
            .Where(m => !m.IsDeleted && !m.IsCompleted && m.TargetDate < today)
            .OrderBy(m => m.TargetDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SUCCESSION ACTION REPOSITORY
// ============================================================================

#region Succession Action Repository

public class SuccessionActionRepository : GenericRepository<SuccessionAction>, ISuccessionActionRepository
{
    public SuccessionActionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SuccessionAction>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(a => a.ResponsiblePerson)
            .Include(a => a.AssignedBy)
            .Include(a => a.Candidate).ThenInclude(c => c!.Employee)
            .Where(a => a.SuccessionPlanId == planId && !a.IsDeleted)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionAction>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Include(a => a.ResponsiblePerson)
            .Include(a => a.AssignedBy)
            .Where(a => a.CandidateId == candidateId && !a.IsDeleted)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionAction>> GetByStatusAsync(ActionStatus status)
    {
        return await _dbSet
            .Include(a => a.ResponsiblePerson)
            .Include(a => a.SuccessionPlan).ThenInclude(p => p.Position)
            .Where(a => a.Status == status && !a.IsDeleted)
            .OrderBy(a => a.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionAction>> GetByPriorityAsync(ActionPriority priority)
    {
        return await _dbSet
            .Include(a => a.ResponsiblePerson)
            .Include(a => a.SuccessionPlan).ThenInclude(p => p.Position)
            .Where(a => a.Priority == priority && !a.IsDeleted)
            .OrderBy(a => a.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionAction>> GetOverdueActionsAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(a => a.ResponsiblePerson)
            .Include(a => a.SuccessionPlan).ThenInclude(p => p.Position)
            .Where(a => !a.IsDeleted
                     && a.DueDate != null
                     && a.DueDate < today
                     && a.Status != ActionStatus.Completed
                     && a.Status != ActionStatus.Cancelled)
            .OrderBy(a => a.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionAction>> GetByResponsiblePersonAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.SuccessionPlan).ThenInclude(p => p.Position)
            .Include(a => a.AssignedBy)
            .Where(a => a.ResponsiblePersonId == employeeId && !a.IsDeleted)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.DueDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SUCCESSION PLAN HISTORY REPOSITORY
// ============================================================================

#region Succession Plan History Repository

public class SuccessionPlanHistoryRepository : GenericRepository<SuccessionPlanHistory>, ISuccessionPlanHistoryRepository
{
    public SuccessionPlanHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SuccessionPlanHistory>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(h => h.SnapshotCreatedBy)
            .Where(h => h.SuccessionPlanId == planId && !h.IsDeleted)
            .OrderByDescending(h => h.SnapshotDate)
            .ToListAsync();
    }

    public async Task<SuccessionPlanHistory?> GetLatestSnapshotAsync(Guid planId)
    {
        return await _dbSet
            .Include(h => h.SnapshotCreatedBy)
            .Where(h => h.SuccessionPlanId == planId && !h.IsDeleted)
            .OrderByDescending(h => h.SnapshotDate)
            .FirstOrDefaultAsync();
    }
}

#endregion

// ============================================================================
// SUCCESSION DOCUMENT REPOSITORY
// ============================================================================

#region Succession Document Repository

public class SuccessionDocumentRepository : GenericRepository<SuccessionDocument>, ISuccessionDocumentRepository
{
    public SuccessionDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SuccessionDocument>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => d.SuccessionPlanId == planId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionDocument>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => d.CandidateId == candidateId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionDocument>> GetByTalentPoolMemberIdAsync(Guid memberId)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => d.TalentPoolMemberId == memberId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionDocument>> GetConfidentialDocumentsAsync(Guid planId)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => d.SuccessionPlanId == planId && d.IsConfidential && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuccessionDocument>> GetExpiredRetentionDocumentsAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => !d.IsDeleted && d.RetentionDate != null && d.RetentionDate <= today)
            .OrderBy(d => d.RetentionDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// TALENT POOL REPOSITORY
// ============================================================================

#region Talent Pool Repository

public class TalentPoolRepository : GenericRepository<TalentPool>, ITalentPoolRepository
{
    public TalentPoolRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TalentPool>> GetByPoolTypeAsync(Guid poolTypeId)
    {
        return await _dbSet
            .Include(p => p.Owner)
            .Include(p => p.PoolType)
            .Where(p => p.PoolTypeId == poolTypeId && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentPool>> GetActivePoolsAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(p => p.Owner)
            .Include(p => p.PoolType)
            .Where(p => p.IsActive && !p.IsDeleted
                     && (p.ValidFrom == null || p.ValidFrom <= today)
                     && (p.ValidTo == null || p.ValidTo >= today))
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentPool>> GetByOwnerAsync(Guid ownerEmployeeId)
    {
        return await _dbSet
            .Include(p => p.Owner)
            .Include(p => p.PoolType)
            .Where(p => p.OwnerId == ownerEmployeeId && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<TalentPool?> GetWithMembersAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Owner)
            .Include(p => p.PoolType)
            .Include(p => p.TargetPosition)
            .Include(p => p.Members.Where(m => m.IsActive && !m.IsDeleted))
                .ThenInclude(m => m.Employee)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

// ============================================================================
// TALENT POOL MEMBER REPOSITORY
// ============================================================================

#region Talent Pool Member Repository

public class TalentPoolMemberRepository : GenericRepository<TalentPoolMember>, ITalentPoolMemberRepository
{
    public TalentPoolMemberRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TalentPoolMember>> GetByTalentPoolIdAsync(Guid poolId)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Where(m => m.TalentPoolId == poolId && m.IsActive && !m.IsDeleted)
            .OrderBy(m => m.Rank)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentPoolMember>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(m => m.TalentPool)
            .Include(m => m.NominatedBy)
            .Where(m => m.EmployeeId == employeeId && !m.IsDeleted)
            .OrderBy(m => m.TalentPool.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentPoolMember>> GetByReadinessAsync(Guid poolId, ReadinessLevel readiness)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Where(m => m.TalentPoolId == poolId && m.Readiness == readiness && m.IsActive && !m.IsDeleted)
            .OrderBy(m => m.Rank)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentPoolMember>> GetDueForReviewAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.TalentPool)
            .Where(m => m.IsActive && !m.IsDeleted
                     && m.NextReviewDate != null
                     && m.NextReviewDate <= cutoff)
            .OrderBy(m => m.NextReviewDate)
            .ToListAsync();
    }

    public async Task<TalentPoolMember?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.TalentPool)
            .Include(m => m.NominatedBy)
            .Include(m => m.ReviewRatings).ThenInclude(r => r.Session)
            .Include(m => m.ReviewRatings).ThenInclude(r => r.RatedBy)
            .Include(m => m.DevelopmentActivities).ThenInclude(a => a.Milestones)
            .Include(m => m.DevelopmentActivities).ThenInclude(a => a.Supervisor)
            .Include(m => m.Documents).ThenInclude(d => d.UploadedBy)
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
    }

    public async Task<TalentPoolMember?> GetMembershipAsync(Guid poolId, Guid employeeId)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.TalentPool)
            .FirstOrDefaultAsync(m => m.TalentPoolId == poolId
                                   && m.EmployeeId == employeeId
                                   && !m.IsDeleted);
    }
}

#endregion

// ============================================================================
// TALENT REVIEW SESSION REPOSITORY
// ============================================================================

#region Talent Review Session Repository

public class TalentReviewSessionRepository : GenericRepository<TalentReviewSession>, ITalentReviewSessionRepository
{
    public TalentReviewSessionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TalentReviewSession>> GetByYearAsync(int reviewYear)
    {
        return await _dbSet
            .Include(s => s.FacilitatedBy)
            .Include(s => s.OrganizationUnit)
            .Where(s => s.ReviewYear == reviewYear && !s.IsDeleted)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentReviewSession>> GetByOrganizationUnitAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .Include(s => s.FacilitatedBy)
            .Include(s => s.OrganizationUnit)
            .Where(s => s.OrganizationUnitId == organizationUnitId && !s.IsDeleted)
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentReviewSession>> GetFinalizedSessionsAsync()
    {
        return await _dbSet
            .Include(s => s.FacilitatedBy)
            .Include(s => s.OrganizationUnit)
            .Include(s => s.FinalizedBy)
            .Where(s => s.IsFinalized && !s.IsDeleted)
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentReviewSession>> GetPendingSessionsAsync()
    {
        return await _dbSet
            .Include(s => s.FacilitatedBy)
            .Include(s => s.OrganizationUnit)
            .Where(s => !s.IsFinalized && !s.IsDeleted)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<TalentReviewSession?> GetWithRatingsAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.FacilitatedBy)
            .Include(s => s.OrganizationLevel)
            .Include(s => s.OrganizationUnit)
            .Include(s => s.FinalizedBy)
            .Include(s => s.Ratings).ThenInclude(r => r.Employee)
            .Include(s => s.Ratings).ThenInclude(r => r.RatedBy)
            .Include(s => s.Ratings).ThenInclude(r => r.CalibrationConfirmedBy)
            .Include(s => s.Ratings).ThenInclude(r => r.TalentPoolMember).ThenInclude(m => m!.TalentPool)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }
}

#endregion

// ============================================================================
// TALENT REVIEW RATING REPOSITORY
// ============================================================================

#region Talent Review Rating Repository

public class TalentReviewRatingRepository : GenericRepository<TalentReviewRating>, ITalentReviewRatingRepository
{
    public TalentReviewRatingRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TalentReviewRating>> GetBySessionIdAsync(Guid sessionId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.RatedBy)
            .Include(r => r.TalentPoolMember).ThenInclude(m => m!.TalentPool)
            .Where(r => r.SessionId == sessionId && !r.IsDeleted)
            .OrderBy(r => r.Employee.LastName)
            .ThenBy(r => r.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentReviewRating>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.Session)
            .Include(r => r.RatedBy)
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.Session.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentReviewRating>> GetByTalentPoolMemberIdAsync(Guid memberId)
    {
        return await _dbSet
            .Include(r => r.Session)
            .Include(r => r.Employee)
            .Where(r => r.TalentPoolMemberId == memberId && !r.IsDeleted)
            .OrderByDescending(r => r.Session.SessionDate)
            .ToListAsync();
    }

    public async Task<TalentReviewRating?> GetLatestConfirmedRatingForEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.Session)
            .Where(r => r.EmployeeId == employeeId
                     && r.CalibrationConfirmed
                     && !r.IsDeleted)
            .OrderByDescending(r => r.Session.SessionDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TalentReviewRating>> GetByNineBoxPositionAsync(Guid sessionId, PerformanceRating performance, PotentialRating potential)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.TalentPoolMember).ThenInclude(m => m!.TalentPool)
            .Where(r => r.SessionId == sessionId
                     && r.Performance == performance
                     && r.Potential == potential
                     && !r.IsDeleted)
            .OrderBy(r => r.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentReviewRating>> GetCalibratedRatingsAsync(Guid sessionId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.CalibrationConfirmedBy)
            .Where(r => r.SessionId == sessionId && r.CalibrationConfirmed && !r.IsDeleted)
            .OrderBy(r => r.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TalentReviewRating>> GetPendingCalibrationAsync(Guid sessionId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.RatedBy)
            .Where(r => r.SessionId == sessionId && !r.CalibrationConfirmed && !r.IsDeleted)
            .OrderBy(r => r.Employee.LastName)
            .ToListAsync();
    }

    public async Task<TalentReviewRating?> GetBySessionAndEmployeeAsync(Guid sessionId, Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.RatedBy)
            .Include(r => r.CalibrationConfirmedBy)
            .Include(r => r.TalentPoolMember).ThenInclude(m => m!.TalentPool)
            .Include(r => r.PreviousRatingSession)
            .FirstOrDefaultAsync(r => r.SessionId == sessionId
                                    && r.EmployeeId == employeeId
                                    && !r.IsDeleted);
    }
}

#endregion
