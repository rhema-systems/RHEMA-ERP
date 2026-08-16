using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF DISCIPLINE SUPPORT REPOSITORIES
//
// Same shape as the sub-entity repositories: a private Scoped(tenantId) helper owning the tenant
// predicate, the soft-delete predicate and the include set, and reads that add only their own filter
// and ordering. See the header of StaffDisciplineSubEntityRepositories.cs for why.
//
// These carry the investigation's working papers — witness statements, case notes, legal reviews —
// so the cross-tenant reads they used to perform were the most sensitive in the module.
// ============================================================================

// ============================================================================
// STAFF DISCIPLINE ACTION STEP REPOSITORY
// ============================================================================

#region Staff Discipline Action Step Repository

public class StaffDisciplineActionStepRepository : GenericRepository<StaffDisciplineActionStep>, IStaffDisciplineActionStepRepository
{
    public StaffDisciplineActionStepRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplineActionStep> Scoped(Guid tenantId) =>
        _dbSet
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Include(s => s.OffenseProcedure)
            .Include(s => s.ActionedBy)
            .Include(s => s.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(s => s.DisciplinaryAction).ThenInclude(d => d.StaffOffense);

    public async Task<IEnumerable<StaffDisciplineActionStep>> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .Where(s => s.DisciplinaryActionId == caseId)
            .OrderBy(s => s.OffenseProcedure.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineActionStep>> GetPendingStepsAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .Where(s => s.DisciplinaryActionId == caseId
                     && s.Status != DisciplinaryActionStepStatus.Completed
                     && s.Status != DisciplinaryActionStepStatus.Cancelled
                     && s.Status != DisciplinaryActionStepStatus.Skipped)
            .OrderBy(s => s.OffenseProcedure.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineActionStep>> GetOverdueStepsAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await Scoped(tenantId)
            .Where(s => s.DueDate != null
                     && s.DueDate < today
                     && s.Status != DisciplinaryActionStepStatus.Completed
                     && s.Status != DisciplinaryActionStepStatus.Cancelled
                     && s.Status != DisciplinaryActionStepStatus.Skipped)
            .OrderBy(s => s.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineActionStep>> GetByActionedByAsync(Guid tenantId, Guid employeeId)
    {
        return await Scoped(tenantId)
            .Where(s => s.ActionedById == employeeId)
            .OrderByDescending(s => s.CompletedDate)
            .ToListAsync();
    }

    public async Task<StaffDisciplineActionStep?> GetWithDocumentsAsync(Guid tenantId, Guid id)
    {
        return await Scoped(tenantId)
            .Include(s => s.Documents).ThenInclude(d => d.UploadedBy)
            .FirstOrDefaultAsync(s => s.Id == id);
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

    private IQueryable<StaffDisciplineWitness> Scoped(Guid tenantId) =>
        _dbSet
            .Where(w => w.TenantId == tenantId && !w.IsDeleted)
            .Include(w => w.Employee)
            .Include(w => w.DisciplinaryAction).ThenInclude(d => d.StaffOffense);

    public async Task<IEnumerable<StaffDisciplineWitness>> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .Where(w => w.DisciplinaryActionId == caseId)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWitness>> GetByEmployeeWitnessAsync(Guid tenantId, Guid employeeId)
    {
        return await Scoped(tenantId)
            .Where(w => w.EmployeeId == employeeId && w.IsEmployee)
            .OrderByDescending(w => w.StatementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineWitness>> GetWithoutStatementAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .Where(w => w.DisciplinaryActionId == caseId && string.IsNullOrEmpty(w.Statement))
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

    private IQueryable<StaffDisciplineDocument> Scoped(Guid tenantId) =>
        _dbSet
            .Where(d => d.TenantId == tenantId && !d.IsDeleted)
            .Include(d => d.UploadedBy)
            .Include(d => d.DisciplinaryAction).ThenInclude(a => a.Employee);

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .Where(d => d.DisciplinaryActionId == caseId)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByScopeAsync(Guid tenantId, Guid caseId, DisciplinaryDocumentScope scope)
    {
        return await Scoped(tenantId)
            .Where(d => d.DisciplinaryActionId == caseId && d.Scope == scope)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByActionStepIdAsync(Guid tenantId, Guid actionStepId)
    {
        return await Scoped(tenantId)
            .Where(d => d.ActionStepId == actionStepId)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByAppealIdAsync(Guid tenantId, Guid appealId)
    {
        return await Scoped(tenantId)
            .Where(d => d.AppealId == appealId)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByCategoryAsync(Guid tenantId, Guid caseId, DisciplinaryDocumentCategory category)
    {
        return await Scoped(tenantId)
            .Where(d => d.DisciplinaryActionId == caseId && d.Category == category)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineDocument>> GetByUploaderAsync(Guid tenantId, Guid uploadedById)
    {
        return await Scoped(tenantId)
            .Where(d => d.UploadedById == uploadedById)
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

    private IQueryable<StaffDisciplineNote> Scoped(Guid tenantId) =>
        _dbSet
            .Where(n => n.TenantId == tenantId && !n.IsDeleted)
            .Include(n => n.CreatedByEmployee)
            .Include(n => n.DisciplinaryAction).ThenInclude(d => d.StaffOffense);

    public async Task<IEnumerable<StaffDisciplineNote>> GetByCaseIdAsync(Guid tenantId, Guid caseId, bool includeConfidential = true)
    {
        return await Scoped(tenantId)
            .Where(n => n.DisciplinaryActionId == caseId
                     && (includeConfidential || !n.IsConfidential))
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNote>> GetByAuthorAsync(Guid tenantId, Guid employeeId)
    {
        return await Scoped(tenantId)
            .Where(n => n.CreatedByEmployeeId == employeeId)
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNote>> GetConfidentialAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .Where(n => n.DisciplinaryActionId == caseId && n.IsConfidential)
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

    private IQueryable<StaffDisciplineNotification> Scoped(Guid tenantId) =>
        _dbSet
            .Where(n => n.TenantId == tenantId && !n.IsDeleted)
            .Include(n => n.SentBy)
            .Include(n => n.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(n => n.DisciplinaryAction).ThenInclude(d => d.StaffOffense);

    public async Task<IEnumerable<StaffDisciplineNotification>> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .Where(n => n.DisciplinaryActionId == caseId)
            .OrderByDescending(n => n.SentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNotification>> GetUnacknowledgedAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .Where(n => n.DisciplinaryActionId == caseId && n.AcknowledgedDate == null)
            .OrderBy(n => n.SentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNotification>> GetPendingFollowupAsync(Guid tenantId, int daysOld = 3)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysOld);
        return await Scoped(tenantId)
            .Where(n => n.AcknowledgedDate == null
                     && !n.IsFollowupSent
                     && n.SentDate <= cutoff)
            .OrderBy(n => n.SentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNotification>> GetByTypeAsync(Guid tenantId, Guid caseId, DisciplinaryNotificationType type)
    {
        return await Scoped(tenantId)
            .Where(n => n.DisciplinaryActionId == caseId && n.NotificationType == type)
            .OrderByDescending(n => n.SentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineNotification>> GetBySenderAsync(Guid tenantId, Guid sentById)
    {
        return await Scoped(tenantId)
            .Where(n => n.SentById == sentById)
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

    private IQueryable<StaffDisciplineLegalReview> Scoped(Guid tenantId) =>
        _dbSet
            .Where(lr => lr.TenantId == tenantId && !lr.IsDeleted)
            .Include(lr => lr.ReferredBy)
            .Include(lr => lr.ExternalCounsel)
            .Include(lr => lr.DisciplinaryAction).ThenInclude(d => d.Employee)
            .Include(lr => lr.DisciplinaryAction).ThenInclude(d => d.StaffOffense);

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetByCaseIdAsync(Guid tenantId, Guid caseId)
    {
        return await Scoped(tenantId)
            .Where(lr => lr.DisciplinaryActionId == caseId)
            .OrderByDescending(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetOpenReviewsAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(lr => lr.LegalReviewCompleteDate == null)
            .OrderBy(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetByRiskLevelAsync(Guid tenantId, DisciplineLegalRiskLevel minimumRisk)
    {
        return await Scoped(tenantId)
            .Where(lr => lr.LegalRiskLevel >= minimumRisk)
            .OrderByDescending(lr => lr.LegalRiskLevel)
            .ThenByDescending(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetRequiringExternalCounselAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(lr => lr.RequiresExternalCounsel && lr.LegalReviewCompleteDate == null)
            .OrderByDescending(lr => lr.LegalRiskLevel)
            .ThenBy(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReview>> GetByReferrerAsync(Guid tenantId, Guid referredById)
    {
        return await Scoped(tenantId)
            .Where(lr => lr.ReferredById == referredById)
            .OrderByDescending(lr => lr.ReferredToLegalDate)
            .ToListAsync();
    }

    /// <remarks>
    /// A money figure, so the tenant predicate matters more here than anywhere else in the file:
    /// unscoped, this summed another tenant's legal costs into this case's total.
    /// </remarks>
    public async Task<decimal> GetTotalLegalCostsForCaseAsync(Guid tenantId, Guid caseId)
    {
        return await _dbSet
            .Where(lr => lr.TenantId == tenantId
                      && !lr.IsDeleted
                      && lr.DisciplinaryActionId == caseId)
            .SumAsync(lr => lr.LegalCostsIncurred ?? 0m);
    }
}

#endregion
