using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF DISCIPLINE ACTION STEP REPOSITORY
// ============================================================================

#region Staff Discipline Action Step Repository

public class StaffDisciplineActionStepRepository : GenericRepository<StaffDisciplineActionStep>, IStaffDisciplineActionStepRepository
{
    public StaffDisciplineActionStepRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffDisciplineActionStep>> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(s => s.OffenseProcedure)
            .Include(s => s.ActionedBy)
            .Where(s => s.DisciplinaryActionId == caseId && !s.IsDeleted)
            .OrderBy(s => s.OffenseProcedure.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineActionStep>> GetPendingStepsAsync(Guid caseId)
    {
        return await _dbSet
            .Include(s => s.OffenseProcedure)
            .Where(s => s.DisciplinaryActionId == caseId
                     && !s.IsDeleted
                     && s.Status != DisciplinaryActionStepStatus.Completed
                     && s.Status != DisciplinaryActionStepStatus.Cancelled
                     && s.Status != DisciplinaryActionStepStatus.Skipped)
            .OrderBy(s => s.OffenseProcedure.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineActionStep>> GetOverdueStepsAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(s => s.OffenseProcedure)
            .Include(s => s.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Where(s => !s.IsDeleted
                     && s.DueDate != null
                     && s.DueDate < today
                     && s.Status != DisciplinaryActionStepStatus.Completed
                     && s.Status != DisciplinaryActionStepStatus.Cancelled
                     && s.Status != DisciplinaryActionStepStatus.Skipped)
            .OrderBy(s => s.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineActionStep>> GetByActionedByAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(s => s.OffenseProcedure)
            .Include(s => s.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(s => s.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(s => s.ActionedById == employeeId && !s.IsDeleted)
            .OrderByDescending(s => s.CompletedDate)
            .ToListAsync();
    }

    public async Task<StaffDisciplineActionStep?> GetWithDocumentsAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.OffenseProcedure)
            .Include(s => s.ActionedBy)
            .Include(s => s.Documents).ThenInclude(d => d.UploadedBy)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WITNESS REPOSITORY
// ============================================================================

#region Staff Discipline Witness Repository

public class StaffDisciplineWitnessRepository : GenericRepository<StaffDisciplineWitness>, IStaffDisciplineWitnessRepository
{
    public StaffDisciplineWitnessRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffDisciplineWitness>> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(w => w.Employee)
            .Where(w => w.DisciplinaryActionId == caseId && !w.IsDeleted)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWitness>> GetByEmployeeWitnessAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(w => w.Employee)
            .Include(w => w.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(w => w.EmployeeId == employeeId && w.IsEmployee && !w.IsDeleted)
            .OrderByDescending(w => w.StatementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWitness>> GetWithoutStatementAsync(Guid caseId)
    {
        return await _dbSet
            .Include(w => w.Employee)
            .Where(w => w.DisciplinaryActionId == caseId
                     && !w.IsDeleted
                     && string.IsNullOrEmpty(w.Statement))
            .OrderBy(w => w.Name)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE DOCUMENT REPOSITORY
// ============================================================================

#region Staff Discipline Document Repository

public class StaffDisciplineDocumentRepository : GenericRepository<StaffDisciplineDocument>, IStaffDisciplineDocumentRepository
{
    public StaffDisciplineDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => d.DisciplinaryActionId == caseId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByScopeAsync(Guid caseId, DisciplinaryDocumentScope scope)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => d.DisciplinaryActionId == caseId && d.Scope == scope && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByActionStepIdAsync(Guid actionStepId)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => d.ActionStepId == actionStepId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByAppealIdAsync(Guid appealId)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => d.AppealId == appealId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByCategoryAsync(Guid caseId, DisciplinaryDocumentCategory category)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Where(d => d.DisciplinaryActionId == caseId && d.Category == category && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByUploaderAsync(Guid uploadedById)
    {
        return await _dbSet
            .Include(d => d.UploadedBy)
            .Include(d => d.DisciplinaryAction).ThenInclude(a => a.Employee)
            .Where(d => d.UploadedById == uploadedById && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTE REPOSITORY
// ============================================================================

#region Staff Discipline Note Repository

public class StaffDisciplineNoteRepository : GenericRepository<StaffDisciplineNote>, IStaffDisciplineNoteRepository
{
    public StaffDisciplineNoteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffDisciplineNote>> GetByCaseIdAsync(Guid caseId, bool includeConfidential = true)
    {
        return await _dbSet
            .Include(n => n.CreatedByEmployee)
            .Where(n => n.DisciplinaryActionId == caseId
                     && !n.IsDeleted
                     && (includeConfidential || !n.IsConfidential))
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNote>> GetByAuthorAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(n => n.CreatedByEmployee)
            .Include(n => n.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(n => n.CreatedByEmployeeId == employeeId && !n.IsDeleted)
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNote>> GetConfidentialAsync(Guid caseId)
    {
        return await _dbSet
            .Include(n => n.CreatedByEmployee)
            .Where(n => n.DisciplinaryActionId == caseId && n.IsConfidential && !n.IsDeleted)
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTIFICATION REPOSITORY
// ============================================================================

#region Staff Discipline Notification Repository

public class StaffDisciplineNotificationRepository : GenericRepository<StaffDisciplineNotification>, IStaffDisciplineNotificationRepository
{
    public StaffDisciplineNotificationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffDisciplineNotification>> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(n => n.SentBy)
            .Where(n => n.DisciplinaryActionId == caseId && !n.IsDeleted)
            .OrderByDescending(n => n.SentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNotification>> GetUnacknowledgedAsync(Guid caseId)
    {
        return await _dbSet
            .Include(n => n.SentBy)
            .Where(n => n.DisciplinaryActionId == caseId
                     && !n.IsDeleted
                     && n.AcknowledgedDate == null)
            .OrderBy(n => n.SentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNotification>> GetPendingFollowupAsync(int daysOld = 3)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysOld);
        return await _dbSet
            .Include(n => n.SentBy)
            .Include(n => n.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Where(n => !n.IsDeleted
                     && n.AcknowledgedDate == null
                     && !n.IsFollowupSent
                     && n.SentDate <= cutoff)
            .OrderBy(n => n.SentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNotification>> GetByTypeAsync(Guid caseId, DisciplinaryNotificationType type)
    {
        return await _dbSet
            .Include(n => n.SentBy)
            .Where(n => n.DisciplinaryActionId == caseId && n.NotificationType == type && !n.IsDeleted)
            .OrderByDescending(n => n.SentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNotification>> GetBySenderAsync(Guid sentById)
    {
        return await _dbSet
            .Include(n => n.SentBy)
            .Include(n => n.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(n => n.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(n => n.SentById == sentById && !n.IsDeleted)
            .OrderByDescending(n => n.SentDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE LEGAL REVIEW REPOSITORY
// ============================================================================

#region Staff Discipline Legal Review Repository

public class StaffDisciplineLegalReviewRepository : GenericRepository<StaffDisciplineLegalReview>, IStaffDisciplineLegalReviewRepository
{
    public StaffDisciplineLegalReviewRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetByCaseIdAsync(Guid caseId)
    {
        return await _dbSet
            .Include(lr => lr.ReferredBy)
            .Include(lr => lr.ExternalCounsel)
            .Where(lr => lr.DisciplinaryActionId == caseId && !lr.IsDeleted)
            .OrderByDescending(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetOpenReviewsAsync()
    {
        return await _dbSet
            .Include(lr => lr.ReferredBy)
            .Include(lr => lr.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(lr => lr.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(lr => !lr.IsDeleted && lr.LegalReviewCompleteDate == null)
            .OrderBy(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetByRiskLevelAsync(DisciplineLegalRiskLevel minimumRisk)
    {
        return await _dbSet
            .Include(lr => lr.ReferredBy)
            .Include(lr => lr.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(lr => lr.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(lr => !lr.IsDeleted && lr.LegalRiskLevel >= minimumRisk)
            .OrderByDescending(lr => lr.LegalRiskLevel)
            .ThenByDescending(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetRequiringExternalCounselAsync()
    {
        return await _dbSet
            .Include(lr => lr.ReferredBy)
            .Include(lr => lr.ExternalCounsel)
            .Include(lr => lr.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Where(lr => !lr.IsDeleted
                      && lr.RequiresExternalCounsel
                      && lr.LegalReviewCompleteDate == null)
            .OrderByDescending(lr => lr.LegalRiskLevel)
            .ThenBy(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetByReferrerAsync(Guid referredById)
    {
        return await _dbSet
            .Include(lr => lr.ReferredBy)
            .Include(lr => lr.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(lr => lr.DisciplinaryAction).ThenInclude(d => d.StaffOffense)
            .Where(lr => lr.ReferredById == referredById && !lr.IsDeleted)
            .OrderByDescending(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalLegalCostsForCaseAsync(Guid caseId)
    {
        return await _dbSet
            .Where(lr => lr.DisciplinaryActionId == caseId && !lr.IsDeleted)
            .SumAsync(lr => lr.LegalCostsIncurred ?? 0m);
    }
}

#endregion
