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

    public async Task<IEnumerable<OrientationCategory>> GetRootCategoriesAsync()
    {
        return await _dbSet
            .Include(c => c.SubCategories)
            .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationCategory>> GetByParentAsync(Guid? parentCategoryId)
    {
        return await _dbSet
            .Where(c => c.ParentCategoryId == parentCategoryId && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationCategory>> GetActiveAsync()
    {
        return await _dbSet
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<OrientationCategory?> GetWithSubCategoriesAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }
}

#endregion

#region Orientation Program Repository

public class OrientationProgramRepository : GenericRepository<OrientationProgram>, IOrientationProgramRepository
{
    public OrientationProgramRepository(ApplicationDbContext context) : base(context) { }

    public async Task<OrientationProgram?> GetByProgramCodeAsync(string programCode)
    {
        return await _dbSet
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.ProgramCode == programCode && !p.IsDeleted);
    }

    public async Task<bool> ProgramCodeExistsAsync(string programCode, Guid? excludeId = null)
    {
        return await _dbSet.AnyAsync(p => p.ProgramCode == programCode && !p.IsDeleted
                                          && (excludeId == null || p.Id != excludeId));
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
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationProgram>> GetByCategoryAsync(Guid categoryId)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationProgram>> GetByTypeAsync(OrientationProgramType programType)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.ProgramType == programType && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationProgram>> GetActiveProgramsAsync()
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.Status == OrientationProgramStatus.Active && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationProgram>> GetByOwnerOrganizationUnitAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.OwnerOrganizationUnitId == organizationUnitId && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<int> GetMaxProgramCodeSequenceAsync(string prefix)
    {
        var codes = await _dbSet
            .Where(p => p.ProgramCode.StartsWith(prefix))
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

    private static readonly OrientationEnrollmentStatus[] ActiveEnrollmentStatuses =
    {
        OrientationEnrollmentStatus.PendingConfirmation,
        OrientationEnrollmentStatus.Confirmed,
        OrientationEnrollmentStatus.Active,
        OrientationEnrollmentStatus.Completed,
    };

    public async Task<OrientationSession?> GetBySessionCodeAsync(string sessionCode)
    {
        return await _dbSet
            .Include(s => s.Program)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode && !s.IsDeleted);
    }

    public async Task<bool> SessionCodeExistsAsync(string sessionCode, Guid? excludeId = null)
    {
        return await _dbSet.AnyAsync(s => s.SessionCode == sessionCode && !s.IsDeleted
                                          && (excludeId == null || s.Id != excludeId));
    }

    public async Task<IEnumerable<OrientationSession>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => s.ProgramId == programId && !s.IsDeleted)
            .OrderByDescending(s => s.ScheduledStartAt)
            .ToListAsync();
    }

    public async Task<OrientationSession?> GetWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.Program)
            .Include(s => s.Facilitators)
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public async Task<IEnumerable<OrientationSession>> GetByStatusAsync(OrientationSessionStatus status)
    {
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => s.Status == status && !s.IsDeleted)
            .OrderBy(s => s.ScheduledStartAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationSession>> GetUpcomingAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await _dbSet
            .Include(s => s.Program)
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
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => !s.IsDeleted
                        && s.Status == OrientationSessionStatus.EnrollmentOpen
                        && (s.EnrollmentDeadlineAt == null || s.EnrollmentDeadlineAt >= now))
            .OrderBy(s => s.ScheduledStartAt)
            .ToListAsync();
    }

    public async Task<int> GetEnrolledCountAsync(Guid sessionId)
    {
        return await _context.Set<EmployeeOrientation>()
            .CountAsync(e => e.SessionId == sessionId
                             && !e.IsDeleted
                             && ActiveEnrollmentStatuses.Contains(e.EnrollmentStatus));
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

    public async Task<IEnumerable<OrientationAttendanceRecord>> GetByEnrollmentIdAsync(Guid enrollmentId)
    {
        return await _dbSet
            .Where(a => a.EnrollmentId == enrollmentId && !a.IsDeleted)
            .OrderBy(a => a.SessionDay)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrientationAttendanceRecord>> GetBySessionIdAsync(Guid sessionId)
    {
        return await _dbSet
            .Include(a => a.Enrollment)
            .Where(a => a.Enrollment.SessionId == sessionId && !a.IsDeleted)
            .OrderBy(a => a.SessionDay)
            .ToListAsync();
    }

    public async Task<OrientationAttendanceRecord?> GetByEnrollmentAndDayAsync(Guid enrollmentId, int sessionDay)
    {
        return await _dbSet
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
        return await _dbSet
            .Include(e => e.Program)
            .Include(e => e.Session)
            .Where(e => e.EmployeeId == employeeId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(e => e.Session)
            .Where(e => e.ProgramId == programId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetBySessionIdAsync(Guid sessionId)
    {
        return await _dbSet
            .Include(e => e.Program)
            .Where(e => e.SessionId == sessionId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<EmployeeOrientation?> GetByEmployeeAndProgramAsync(Guid employeeId, Guid programId)
    {
        return await _dbSet
            .Include(e => e.Program)
            .Include(e => e.Session)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.ProgramId == programId && !e.IsDeleted);
    }

    public async Task<bool> ExistsForEmployeeAndProgramAsync(Guid employeeId, Guid programId)
    {
        return await _dbSet.AnyAsync(e => e.EmployeeId == employeeId && e.ProgramId == programId && !e.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetByCompletionStatusAsync(OrientationCompletionStatus status)
    {
        return await _dbSet
            .Include(e => e.Program)
            .Where(e => e.CompletionStatus == status && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetByEnrollmentStatusAsync(OrientationEnrollmentStatus status)
    {
        return await _dbSet
            .Include(e => e.Program)
            .Where(e => e.EnrollmentStatus == status && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOrientation>> GetOverdueAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(e => e.Program)
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
        return await _dbSet
            .Include(e => e.Program)
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
        return await _dbSet
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
        return await _dbSet
            .Include(cp => cp.ContentItem)
            .Where(cp => cp.EmployeeOrientationId == employeeOrientationId && !cp.IsDeleted)
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
        return await _dbSet
            .Include(c => c.EmployeeOrientation).ThenInclude(e => e.Program)
            .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber && !c.IsDeleted);
    }

    public async Task<bool> CertificateNumberExistsAsync(string certificateNumber)
    {
        return await _dbSet.AnyAsync(c => c.CertificateNumber == certificateNumber && !c.IsDeleted);
    }

    public async Task<IEnumerable<OrientationCertificate>> GetByEnrollmentIdAsync(Guid employeeOrientationId)
    {
        return await _dbSet
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

    public async Task<IEnumerable<OrientationNotification>> GetByRecipientAsync(Guid recipientEmployeeId, bool unreadOnly = false)
    {
        return await _dbSet
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
        return await _dbSet
            .Where(n => n.EmployeeOrientationId == employeeOrientationId && !n.IsDeleted)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();
    }
}

#endregion
