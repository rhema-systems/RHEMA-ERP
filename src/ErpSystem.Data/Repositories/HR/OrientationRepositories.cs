using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// ORIENTATION MODULE REPOSITORIES
// ============================================================================

// ============================================================================
// SECTION 1 — CATALOG
// ============================================================================

#region Orientation Category Repository

public class OrientationCategoryRepository : GenericRepository<OrientationCategory>, IOrientationCategoryRepository
{
    public OrientationCategoryRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// The single include set every category read routes through. <see cref="OrientationCategoryDto"/>
    /// renders ParentCategoryName, SubCategories and ProgramCount, so a read that omits any of the three
    /// returns a confident wrong answer (an un-included collection is empty, not null, so ProgramCount
    /// silently renders 0). Keeping one choke point stops the reads drifting apart again.
    /// </summary>
    private IQueryable<OrientationCategory> WithSummaryNavigations()
        => _dbSet
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories.Where(s => !s.IsDeleted))
            .Include(c => c.Programs.Where(p => !p.IsDeleted));

    public override async Task<OrientationCategory?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public override async Task<IEnumerable<OrientationCategory>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationCategory>> GetRootCategoriesAsync()
    {
        return await WithSummaryNavigations()
            .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationCategory>> GetByParentAsync(Guid? parentCategoryId)
    {
        return await WithSummaryNavigations()
            .Where(c => c.ParentCategoryId == parentCategoryId && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationCategory>> GetActiveAsync()
    {
        return await WithSummaryNavigations()
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<OrientationCategory?> GetWithSubCategoriesAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }
}

#endregion

#region Orientation Program Repository

public class OrientationProgramRepository : GenericRepository<OrientationProgram>, IOrientationProgramRepository
{
    public OrientationProgramRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// The single include set every program list read routes through. The summary DTO renders
    /// CategoryName, so a read without it leaves the Category column blank. The three count fields
    /// (module / enrollment / completed) are filled from a batched count query in the service rather
    /// than by including the collections — a program's enrollments run to thousands of rows and are
    /// never needed by a list, only counted.
    /// </summary>
    private IQueryable<OrientationProgram> WithSummaryNavigations()
        => _dbSet.Include(p => p.Category);

    public override async Task<OrientationProgram?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public override async Task<IEnumerable<OrientationProgram>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<OrientationProgram?> GetByProgramCodeAsync(string programCode)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(p => p.ProgramCode == programCode && !p.IsDeleted);
    }

    public async Task<bool> ProgramCodeExistsAsync(Guid tenantId, string programCode, Guid? excludeId = null)
    {
        return await GetQueryableIncludingDeleted(p => p.TenantId == tenantId && p.ProgramCode == programCode)
            .AnyAsync(p => excludeId == null || p.Id != excludeId);
    }

    public async Task<OrientationProgram?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.OwnerOrganizationUnit)
            .Include(p => p.Modules).ThenInclude(m => m.ContentItems)
            .Include(p => p.AudienceRules)
            .Include(p => p.Prerequisites).ThenInclude(pr => pr.PrerequisiteProgram)
            .Include(p => p.AssessmentQuestions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<IEnumerable<OrientationProgram>> GetByStatusAsync(OrientationProgramStatus status)
    {
        return await WithSummaryNavigations()
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationProgram>> GetByCategoryAsync(Guid categoryId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.CategoryId == categoryId && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationProgram>> GetByTypeAsync(OrientationProgramType programType)
    {
        return await WithSummaryNavigations()
            .Where(p => p.ProgramType == programType && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationProgram>> GetActiveProgramsAsync()
    {
        return await WithSummaryNavigations()
            .Where(p => p.Status == OrientationProgramStatus.Active && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationProgram>> GetByOwnerOrganizationUnitAsync(Guid organizationUnitId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.OwnerOrganizationUnitId == organizationUnitId && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Scans soft-deleted rows as well as live ones. (TenantId, ProgramCode) is UNIQUE and a soft delete
    /// does not release the value, so a generator that only saw live rows would hand back a code that is
    /// still occupied — the next create would die on a duplicate key. A program code is an identifier,
    /// not a slot: once issued it is spent.
    /// </summary>
    public async Task<int> GetMaxProgramCodeSequenceAsync(Guid tenantId, string prefix)
    {
        var codes = await GetQueryableIncludingDeleted(p => p.TenantId == tenantId && p.ProgramCode.StartsWith(prefix))
            .Select(p => p.ProgramCode)
            .ToListAsync();

        var max = 0;
        foreach (var code in codes)
        {
            var suffix = code.Substring(prefix.Length);
            if (int.TryParse(suffix, out var n) && n > max) max = n;
        }
        return max;
    }
}

#endregion

#region Orientation Module Repository

public class OrientationModuleRepository : GenericRepository<OrientationModule>, IOrientationModuleRepository
{
    public OrientationModuleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationModule>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(m => m.ContentItems)
            .Where(m => m.ProgramId == programId && !m.IsDeleted)
            .OrderBy(m => m.SequenceOrder)
            .ToListAsync();
    }

    public async Task<OrientationModule?> GetWithContentAsync(Guid id)
    {
        return await _dbSet
            .Include(m => m.Program)
            .Include(m => m.ContentItems.OrderBy(c => c.SequenceOrder))
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
    }

    public async Task<int> GetMaxSequenceOrderAsync(Guid programId)
    {
        return await _dbSet
            .Where(m => m.ProgramId == programId && !m.IsDeleted)
            .Select(m => (int?)m.SequenceOrder)
            .MaxAsync() ?? 0;
    }
}

#endregion

#region Orientation Content Item Repository

public class OrientationContentItemRepository : GenericRepository<OrientationContentItem>, IOrientationContentItemRepository
{
    public OrientationContentItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationContentItem>> GetByModuleIdAsync(Guid moduleId)
    {
        return await _dbSet
            .Where(c => c.ModuleId == moduleId && !c.IsDeleted)
            .OrderBy(c => c.SequenceOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationContentItem>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(c => c.Module)
            .Where(c => c.Module.ProgramId == programId && !c.IsDeleted)
            .OrderBy(c => c.Module.SequenceOrder).ThenBy(c => c.SequenceOrder)
            .ToListAsync();
    }

    public async Task<int> GetMaxSequenceOrderAsync(Guid moduleId)
    {
        return await _dbSet
            .Where(c => c.ModuleId == moduleId && !c.IsDeleted)
            .Select(c => (int?)c.SequenceOrder)
            .MaxAsync() ?? 0;
    }
}

#endregion

#region Orientation Prerequisite Repository

public class OrientationPrerequisiteRepository : GenericRepository<OrientationPrerequisite>, IOrientationPrerequisiteRepository
{
    public OrientationPrerequisiteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationPrerequisite>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(p => p.PrerequisiteProgram)
            .Where(p => p.ProgramId == programId && !p.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationPrerequisite>> GetDependentsAsync(Guid prerequisiteProgramId)
    {
        return await _dbSet
            .Include(p => p.Program)
            .Where(p => p.PrerequisiteProgramId == prerequisiteProgramId && !p.IsDeleted)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(Guid programId, Guid prerequisiteProgramId)
    {
        return await _dbSet.AnyAsync(p => p.ProgramId == programId
                                          && p.PrerequisiteProgramId == prerequisiteProgramId
                                          && !p.IsDeleted);
    }
}

#endregion

#region Orientation Audience Rule Repository

public class OrientationAudienceRuleRepository : GenericRepository<OrientationAudienceRule>, IOrientationAudienceRuleRepository
{
    public OrientationAudienceRuleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationAudienceRule>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Where(r => r.ProgramId == programId && !r.IsDeleted)
            .OrderBy(r => r.RuleName)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationAudienceRule>> GetActiveByTriggerAsync(OrientationEnrollmentTrigger trigger)
    {
        return await _dbSet
            .Include(r => r.Program)
            .Where(r => r.Trigger == trigger && r.IsActive && !r.IsDeleted)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SECTION 2 — DELIVERY
// ============================================================================

#region Orientation Session Repository

public class OrientationSessionRepository : GenericRepository<OrientationSession>, IOrientationSessionRepository
{
    public OrientationSessionRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// The single include set every session list read routes through — the summary DTO renders
    /// ProgramTitle. EnrolledCount is filled from a batched count in the service so that the list,
    /// the detail read and the capacity check all use the same "occupies a seat" rule.
    /// </summary>
    private IQueryable<OrientationSession> WithSummaryNavigations()
        => _dbSet.Include(s => s.Program);

    public override async Task<OrientationSession?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public override async Task<IEnumerable<OrientationSession>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.ScheduledStartAt)
            .ToListAsync();
    }

    public async Task<OrientationSession?> GetBySessionCodeAsync(string sessionCode)
    {
        return await GetWithDetailsQuery()
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode && !s.IsDeleted);
    }

    public async Task<bool> SessionCodeExistsAsync(Guid tenantId, string sessionCode, Guid? excludeId = null)
    {
        return await GetQueryableIncludingDeleted(s => s.TenantId == tenantId && s.SessionCode == sessionCode)
            .AnyAsync(s => excludeId == null || s.Id != excludeId);
    }

    public async Task<IEnumerable<OrientationSession>> GetByProgramIdAsync(Guid programId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.ProgramId == programId && !s.IsDeleted)
            .OrderByDescending(s => s.ScheduledStartAt)
            .ToListAsync();
    }

    /// <summary>Full session graph — the detail DTO renders the facilitator list and seat counts.</summary>
    private IQueryable<OrientationSession> GetWithDetailsQuery()
        => _dbSet
            .Include(s => s.Program)
            .Include(s => s.Facilitators.Where(f => !f.IsDeleted))
            .Include(s => s.Enrollments.Where(e => !e.IsDeleted));

    public async Task<OrientationSession?> GetWithDetailsAsync(Guid id)
    {
        return await GetWithDetailsQuery()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public async Task<IEnumerable<OrientationSession>> GetByStatusAsync(OrientationSessionStatus status)
    {
        return await WithSummaryNavigations()
            .Where(s => s.Status == status && !s.IsDeleted)
            .OrderBy(s => s.ScheduledStartAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationSession>> GetUpcomingAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await WithSummaryNavigations()
            .Where(s => !s.IsDeleted
                        && s.Status != OrientationSessionStatus.Cancelled
                        && s.ScheduledStartAt != null
                        && s.ScheduledStartAt >= now
                        && s.ScheduledStartAt <= cutoff)
            .OrderBy(s => s.ScheduledStartAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationSession>> GetOpenForEnrollmentAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(s => !s.IsDeleted)
            .Where(OrientationSessionEnrolment.IsOpen(now))   // the one definition — the enrol check reads it too
            .OrderBy(s => s.ScheduledStartAt)
            .ToListAsync();
    }

    public async Task<int> GetEnrolledCountAsync(Guid sessionId)
    {
        return await _context.Set<EmployeeOrientation>()
            .CountAsync(e => e.SessionId == sessionId
                             && !e.IsDeleted
                             && OrientationEnrollmentStatuses.Occupying.Contains(e.EnrollmentStatus));
    }

    /// <summary>
    /// Seat counts for many sessions in one query, so a list screen does not fall back to 0 (an
    /// un-included collection is empty, not null, so a missing count renders as a confident zero).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> GetEnrolledCountsAsync(Guid tenantId, IEnumerable<Guid> sessionIds)
    {
        var ids = sessionIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        return await _context.Set<EmployeeOrientation>()
            .Where(e => e.TenantId == tenantId
                        && e.SessionId != null
                        && ids.Contains(e.SessionId!.Value)
                        && !e.IsDeleted
                        && OrientationEnrollmentStatuses.Occupying.Contains(e.EnrollmentStatus))
            .GroupBy(e => e.SessionId!.Value)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count);
    }
}

#endregion

#region Orientation Session Facilitator Repository

public class OrientationSessionFacilitatorRepository : GenericRepository<OrientationSessionFacilitator>, IOrientationSessionFacilitatorRepository
{
    public OrientationSessionFacilitatorRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationSessionFacilitator>> GetBySessionIdAsync(Guid sessionId)
    {
        return await _dbSet
            .Where(f => f.SessionId == sessionId && !f.IsDeleted)
            .OrderBy(f => f.Role)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationSessionFacilitator>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(f => f.Session)
            .Where(f => f.EmployeeId == employeeId && !f.IsDeleted)
            .ToListAsync();
    }
}

#endregion

#region Orientation Attendance Record Repository

public class OrientationAttendanceRecordRepository : GenericRepository<OrientationAttendanceRecord>, IOrientationAttendanceRecordRepository
{
    public OrientationAttendanceRecordRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Every attendance read must load Enrollment: the DTO's EmployeeId is read off it, and the
    /// employee-name hydrator keys on that id — so a read without it blanks both the id and the name.
    /// </summary>
    private IQueryable<OrientationAttendanceRecord> WithSummaryNavigations()
        => _dbSet.Include(a => a.Enrollment);

    public override async Task<OrientationAttendanceRecord?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

    public async Task<IEnumerable<OrientationAttendanceRecord>> GetByEnrollmentIdAsync(Guid enrollmentId)
    {
        return await WithSummaryNavigations()
            .Where(a => a.EnrollmentId == enrollmentId && !a.IsDeleted)
            .OrderBy(a => a.SessionDay)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationAttendanceRecord>> GetBySessionIdAsync(Guid sessionId)
    {
        return await WithSummaryNavigations()
            .Where(a => a.Enrollment.SessionId == sessionId && !a.IsDeleted)
            .OrderBy(a => a.SessionDay)
            .ToListAsync();
    }

    public async Task<OrientationAttendanceRecord?> GetByEnrollmentAndDayAsync(Guid enrollmentId, int sessionDay)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(a => a.EnrollmentId == enrollmentId && a.SessionDay == sessionDay && !a.IsDeleted);
    }
}

#endregion

// ============================================================================
// SECTION 3 — ENROLLMENT & PROGRESS
// ============================================================================

#region Employee Orientation Repository

public class EmployeeOrientationRepository : GenericRepository<EmployeeOrientation>, IEmployeeOrientationRepository
{
    public EmployeeOrientationRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// The single include set every enrollment list read routes through. The summary DTO renders both
    /// ProgramCode/ProgramTitle and SessionTitle; before this existed the by-program read included
    /// Session but not Program and the by-session read included Program but not Session, so each screen
    /// blanked a different column and the same record looked different depending on how you reached it.
    /// </summary>
    private IQueryable<EmployeeOrientation> WithSummaryNavigations()
        => _dbSet
            .Include(e => e.Program)
            .Include(e => e.Session);

    public override async Task<EmployeeOrientation?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
    }

    public override async Task<IEnumerable<EmployeeOrientation>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    /// <summary>
    /// Module / enrollment / completed counts for many programs in one query. List reads never include
    /// the enrollment collection (thousands of rows for a count of one), so without this the program
    /// list reports 0 enrollments for every program while the detail screen reports the truth.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, (int Enrolled, int Completed)>> GetProgramEnrollmentCountsAsync(
        Guid tenantId, IEnumerable<Guid> programIds)
    {
        var ids = programIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, (int, int)>();

        var rows = await _dbSet
            .Where(e => e.TenantId == tenantId && ids.Contains(e.ProgramId) && !e.IsDeleted)
            .GroupBy(e => e.ProgramId)
            .Select(g => new
            {
                ProgramId = g.Key,
                Enrolled = g.Count(),
                Completed = g.Count(e => e.CompletionStatus == OrientationCompletionStatus.Completed),
            })
            .ToListAsync();

        return rows.ToDictionary(r => r.ProgramId, r => (r.Enrolled, r.Completed));
    }

    public async Task<EmployeeOrientation?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(e => e.Program)
            .Include(e => e.Session)
            .Include(e => e.ContentProgress).ThenInclude(cp => cp.ContentItem)
            .Include(e => e.AssessmentResponses).ThenInclude(r => r.Question)
            .Include(e => e.AssessmentResponses).ThenInclude(r => r.SelectedOption)
            .Include(e => e.Acknowledgements)
            .Include(e => e.Feedbacks)
            .Include(e => e.AttendanceRecords)
            .Include(e => e.Certificates)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(e => e.EmployeeId == employeeId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetByProgramIdAsync(Guid programId)
    {
        return await WithSummaryNavigations()
            .Where(e => e.ProgramId == programId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetBySessionIdAsync(Guid sessionId)
    {
        return await WithSummaryNavigations()
            .Where(e => e.SessionId == sessionId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<EmployeeOrientation?> GetByEmployeeAndProgramAsync(Guid employeeId, Guid programId)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.ProgramId == programId && !e.IsDeleted);
    }

    public async Task<bool> ExistsForEmployeeAndProgramAsync(Guid employeeId, Guid programId)
    {
        return await _dbSet.AnyAsync(e => e.EmployeeId == employeeId && e.ProgramId == programId && !e.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetByCompletionStatusAsync(OrientationCompletionStatus status)
    {
        return await WithSummaryNavigations()
            .Where(e => e.CompletionStatus == status && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetByEnrollmentStatusAsync(OrientationEnrollmentStatus status)
    {
        return await WithSummaryNavigations()
            .Where(e => e.EnrollmentStatus == status && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetOverdueAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(e => !e.IsDeleted
                        && (e.CompletionStatus == OrientationCompletionStatus.Overdue
                            || (e.NextDueDate != null
                                && e.NextDueDate < now
                                && e.CompletionStatus != OrientationCompletionStatus.Completed
                                && e.CompletionStatus != OrientationCompletionStatus.Exempted)))
            .OrderBy(e => e.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetDueSoonAsync(int daysAhead = 7)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await WithSummaryNavigations()
            .Where(e => !e.IsDeleted
                        && e.NextDueDate != null
                        && e.NextDueDate >= now
                        && e.NextDueDate <= cutoff
                        && e.CompletionStatus != OrientationCompletionStatus.Completed
                        && e.CompletionStatus != OrientationCompletionStatus.Exempted)
            .OrderBy(e => e.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetWaitlistedForSessionAsync(Guid sessionId)
    {
        return await WithSummaryNavigations()
            .Where(e => e.SessionId == sessionId
                        && e.EnrollmentStatus == OrientationEnrollmentStatus.Waitlisted
                        && !e.IsDeleted)
            .OrderBy(e => e.WaitlistPosition)
            .ToListAsync();
    }

    public async Task<int> CountByProgramAndCompletionStatusAsync(Guid programId, OrientationCompletionStatus status)
    {
        return await _dbSet.CountAsync(e => e.ProgramId == programId && e.CompletionStatus == status && !e.IsDeleted);
    }
}

#endregion

#region Orientation Content Progress Repository

public class OrientationContentProgressRepository : GenericRepository<OrientationContentProgress>, IOrientationContentProgressRepository
{
    public OrientationContentProgressRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationContentProgress>> GetByEnrollmentIdAsync(Guid employeeOrientationId)
    {
        // Ordered by the curriculum sequence the author committed, not by whatever order the database
        // returns — this list is the participant's "work through these in order" checklist.
        return await _dbSet
            .Include(cp => cp.ContentItem).ThenInclude(ci => ci.Module)
            .Where(cp => cp.EmployeeOrientationId == employeeOrientationId && !cp.IsDeleted)
            .OrderBy(cp => cp.ContentItem.Module.SequenceOrder)
            .ThenBy(cp => cp.ContentItem.SequenceOrder)
            .ToListAsync();
    }

    public async Task<OrientationContentProgress?> GetByEnrollmentAndContentAsync(Guid employeeOrientationId, Guid contentItemId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(cp => cp.EmployeeOrientationId == employeeOrientationId
                                       && cp.ContentItemId == contentItemId
                                       && !cp.IsDeleted);
    }

    public async Task<int> CountCompletedForEnrollmentAsync(Guid employeeOrientationId)
    {
        return await _dbSet.CountAsync(cp => cp.EmployeeOrientationId == employeeOrientationId
                                             && cp.Status == OrientationContentProgressStatus.Completed
                                             && !cp.IsDeleted);
    }
}

#endregion

// ============================================================================
// SECTION 4 — ASSESSMENT
// ============================================================================

#region Orientation Assessment Question Repository

public class OrientationAssessmentQuestionRepository : GenericRepository<OrientationAssessmentQuestion>, IOrientationAssessmentQuestionRepository
{
    public OrientationAssessmentQuestionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationAssessmentQuestion>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(q => q.Options)
            .Where(q => q.ProgramId == programId && !q.IsDeleted)
            .OrderBy(q => q.SequenceOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationAssessmentQuestion>> GetActiveByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(q => q.Options)
            .Where(q => q.ProgramId == programId && q.IsActive && !q.IsDeleted)
            .OrderBy(q => q.SequenceOrder)
            .ToListAsync();
    }

    public async Task<OrientationAssessmentQuestion?> GetWithOptionsAsync(Guid id)
    {
        return await _dbSet
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);
    }

    public async Task<int> GetMaxSequenceOrderAsync(Guid programId)
    {
        return await _dbSet
            .Where(q => q.ProgramId == programId && !q.IsDeleted)
            .Select(q => (int?)q.SequenceOrder)
            .MaxAsync() ?? 0;
    }
}

#endregion

#region Orientation Assessment Option Repository

public class OrientationAssessmentOptionRepository : GenericRepository<OrientationAssessmentOption>, IOrientationAssessmentOptionRepository
{
    public OrientationAssessmentOptionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationAssessmentOption>> GetByQuestionIdAsync(Guid questionId)
    {
        return await _dbSet
            .Where(o => o.QuestionId == questionId && !o.IsDeleted)
            .OrderBy(o => o.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationAssessmentOption>> GetByQuestionIdsAsync(IEnumerable<Guid> questionIds)
    {
        var ids = questionIds.ToList();
        return await _dbSet
            .Where(o => ids.Contains(o.QuestionId) && !o.IsDeleted)
            .ToListAsync();
    }
}

#endregion

#region Orientation Assessment Response Repository

public class OrientationAssessmentResponseRepository : GenericRepository<OrientationAssessmentResponse>, IOrientationAssessmentResponseRepository
{
    public OrientationAssessmentResponseRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationAssessmentResponse>> GetByEnrollmentIdAsync(Guid employeeOrientationId)
    {
        return await _dbSet
            .Include(r => r.Question)
            .Include(r => r.SelectedOption)
            .Where(r => r.EmployeeOrientationId == employeeOrientationId && !r.IsDeleted)
            .OrderBy(r => r.Question.SequenceOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationAssessmentResponse>> GetByEnrollmentAndQuestionAsync(Guid employeeOrientationId, Guid questionId)
    {
        return await _dbSet
            .Include(r => r.SelectedOption)
            .Where(r => r.EmployeeOrientationId == employeeOrientationId
                        && r.QuestionId == questionId
                        && !r.IsDeleted)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SECTION 5 — COMPLETION ARTIFACTS
// ============================================================================

#region Orientation Acknowledgement Repository

public class OrientationAcknowledgementRepository : GenericRepository<OrientationAcknowledgement>, IOrientationAcknowledgementRepository
{
    public OrientationAcknowledgementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationAcknowledgement>> GetByEnrollmentIdAsync(Guid employeeOrientationId)
    {
        return await _dbSet
            .Where(a => a.EmployeeOrientationId == employeeOrientationId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationAcknowledgement>> GetUnsignedByEnrollmentAsync(Guid employeeOrientationId)
    {
        return await _dbSet
            .Where(a => a.EmployeeOrientationId == employeeOrientationId
                        && !a.IsDeleted
                        && (a.Status == OrientationAcknowledgementStatus.Pending
                            || a.Status == OrientationAcknowledgementStatus.Presented))
            .ToListAsync();
    }
}

#endregion

#region Orientation Feedback Repository

public class OrientationFeedbackRepository : GenericRepository<OrientationFeedback>, IOrientationFeedbackRepository
{
    public OrientationFeedbackRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OrientationFeedback>> GetByEnrollmentIdAsync(Guid employeeOrientationId)
    {
        return await _dbSet
            .Where(f => f.EmployeeOrientationId == employeeOrientationId && !f.IsDeleted)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationFeedback>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(f => f.EmployeeOrientation)
            .Where(f => f.EmployeeOrientation.ProgramId == programId && !f.IsDeleted)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync();
    }

    public async Task<double> GetAverageOverallRatingForProgramAsync(Guid programId)
    {
        var ratings = await _dbSet
            .Include(f => f.EmployeeOrientation)
            .Where(f => f.EmployeeOrientation.ProgramId == programId
                        && !f.IsDeleted
                        && f.OverallRating != null)
            .Select(f => (double)f.OverallRating!.Value)
            .ToListAsync();

        return ratings.Count == 0 ? 0 : ratings.Average();
    }
}

#endregion

#region Orientation Certificate Repository

public class OrientationCertificateRepository : GenericRepository<OrientationCertificate>, IOrientationCertificateRepository
{
    public OrientationCertificateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<OrientationCertificate?> GetByCertificateNumberAsync(string certificateNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber && !c.IsDeleted);
    }

    /// <summary>
    /// Counts soft-deleted certificates too. (TenantId, CertificateNumber) is UNIQUE and a soft delete
    /// does not release the number, so a check that only saw live rows would clear a number the database
    /// will still reject. A certificate serial is an audit identifier — once issued it is never reissued.
    /// </summary>
    public async Task<bool> CertificateNumberExistsAsync(Guid tenantId, string certificateNumber)
    {
        return await GetQueryableIncludingDeleted(c => c.TenantId == tenantId && c.CertificateNumber == certificateNumber)
            .AnyAsync();
    }

    /// <summary>
    /// The certificate DTO reads EmployeeId off the enrollment and ProgramTitle off the enrollment's
    /// program, and the name hydrator keys on that EmployeeId — so every certificate read needs both.
    /// </summary>
    private IQueryable<OrientationCertificate> WithSummaryNavigations()
        => _dbSet.Include(c => c.EmployeeOrientation).ThenInclude(e => e.Program);

    public override async Task<OrientationCertificate?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public async Task<IEnumerable<OrientationCertificate>> GetByEnrollmentIdAsync(Guid employeeOrientationId)
    {
        return await WithSummaryNavigations()
            .Where(c => c.EmployeeOrientationId == employeeOrientationId && !c.IsDeleted)
            .OrderByDescending(c => c.IssuedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationCertificate>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(c => c.EmployeeOrientation).ThenInclude(e => e.Program)
            .Where(c => c.EmployeeOrientation.EmployeeId == employeeId && !c.IsDeleted)
            .OrderByDescending(c => c.IssuedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationCertificate>> GetExpiringAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await _dbSet
            .Include(c => c.EmployeeOrientation).ThenInclude(e => e.Program)
            .Where(c => !c.IsDeleted
                        && c.Status == OrientationCertificateStatus.Active
                        && c.ExpiresAt != null
                        && c.ExpiresAt >= now
                        && c.ExpiresAt <= cutoff)
            .OrderBy(c => c.ExpiresAt)
            .ToListAsync();
    }
}

#endregion

#region Orientation Notification Repository

public class OrientationNotificationRepository : GenericRepository<OrientationNotification>, IOrientationNotificationRepository
{
    public OrientationNotificationRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>The notification DTO renders ProgramTitle, so every read loads the program.</summary>
    private IQueryable<OrientationNotification> WithSummaryNavigations()
        => _dbSet.Include(n => n.Program);

    public override async Task<OrientationNotification?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
    }

    public async Task<IEnumerable<OrientationNotification>> GetByRecipientAsync(Guid recipientEmployeeId, bool unreadOnly = false)
    {
        return await WithSummaryNavigations()
            .Where(n => n.RecipientEmployeeId == recipientEmployeeId
                        && !n.IsDeleted
                        && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid recipientEmployeeId)
    {
        return await _dbSet.CountAsync(n => n.RecipientEmployeeId == recipientEmployeeId && !n.IsRead && !n.IsDeleted);
    }

    public async Task<IEnumerable<OrientationNotification>> GetByEnrollmentIdAsync(Guid employeeOrientationId)
    {
        return await WithSummaryNavigations()
            .Where(n => n.EmployeeOrientationId == employeeOrientationId && !n.IsDeleted)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();
    }
}

#endregion
