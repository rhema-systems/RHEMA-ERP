using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// VENDOR & TRAINER REPOSITORIES
// ============================================================================

#region Training Vendor Repository

public class TrainingVendorRepository : GenericRepository<TrainingVendor>, ITrainingVendorRepository
{
    public TrainingVendorRepository(ApplicationDbContext context) : base(context) { }

    // TrainingVendorSummaryDto derives TotalTrainersCount from the Trainers collection. An
    // un-included collection is empty rather than null, so the list's "Trainers" column silently
    // read 0 on every row instead of failing. Centralised so the reads cannot drift apart again.
    private IQueryable<TrainingVendor> WithSummaryNavigations()
        => _dbSet
            .Include(v => v.Trainers)
            .Where(v => !v.IsDeleted);

    public override async Task<IEnumerable<TrainingVendor>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<TrainingVendor?> GetByVendorCodeAsync(string vendorCode)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(v => v.VendorCode == vendorCode);
    }

    public async Task<IEnumerable<TrainingVendor>> GetByVendorTypeAsync(TrainingVendorType type)
    {
        return await WithSummaryNavigations()
            .Where(v => v.VendorType == type)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetActiveVendorsAsync()
    {
        return await WithSummaryNavigations()
            .Where(v => v.IsActive && !v.IsBlacklisted)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetPreferredVendorsAsync()
    {
        return await WithSummaryNavigations()
            .Where(v => v.IsPreferred && v.IsActive)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetBlacklistedVendorsAsync()
    {
        return await WithSummaryNavigations()
            .Where(v => v.IsBlacklisted)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetWithExpiredAccreditationAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(v => v.AccreditationExpiryDate != null && v.AccreditationExpiryDate < now)
            .OrderBy(v => v.AccreditationExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetWithExpiringAccreditationAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await WithSummaryNavigations()
            .Where(v => v.AccreditationExpiryDate != null
                     && v.AccreditationExpiryDate >= now
                     && v.AccreditationExpiryDate <= cutoff)
            .OrderBy(v => v.AccreditationExpiryDate)
            .ToListAsync();
    }

    public async Task<TrainingVendor?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(v => v.Trainers).ThenInclude(t => t.TrainerSkills).ThenInclude(s => s.Skill)
            .Include(v => v.Trainers).ThenInclude(t => t.Availability)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
    }
}

#endregion

#region Trainer Profile Repository

public class TrainerProfileRepository : GenericRepository<TrainerProfile>, ITrainerProfileRepository
{
    public TrainerProfileRepository(ApplicationDbContext context) : base(context) { }

    // TrainerProfileSummaryDto renders EmployeeName and VendorName, so every read that feeds it must
    // carry both navigations. Centralised here so the reads cannot drift apart again — the list
    // screen's "Source" column was blank on every row because GetAllAsync/GetActiveTrainersAsync
    // loaded neither, while the detail screen beside it showed the name correctly.
    private IQueryable<TrainerProfile> WithSummaryNavigations()
        => _dbSet
            .Include(t => t.Employee)
            .Include(t => t.Vendor)
            .Where(t => !t.IsDeleted);

    public override async Task<IEnumerable<TrainerProfile>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<TrainerProfile?> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(t => t.EmployeeId == employeeId);
    }

    public async Task<IEnumerable<TrainerProfile>> GetByVendorIdAsync(Guid vendorId)
    {
        return await WithSummaryNavigations()
            .Where(t => t.VendorId == vendorId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainerProfile>> GetActiveTrainersAsync()
    {
        return await WithSummaryNavigations()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<TrainerProfile?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.Employee)
            .Include(t => t.Vendor)
            .Include(t => t.TrainerSkills).ThenInclude(s => s.Skill)
            .Include(t => t.Availability)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    public async Task<IEnumerable<TrainerProfile>> GetAvailableForDateRangeAsync(DateTime from, DateTime to)
    {
        return await WithSummaryNavigations()
            .Include(t => t.Availability)
            .Where(t => t.IsActive
                     && t.Availability.Any(a => a.IsAvailable
                                             && a.FromDate <= from
                                             && a.ToDate >= to))
            .OrderBy(t => t.Name)
            .ToListAsync();
    }
}

#endregion

#region Trainer Skill Repository

public class TrainerSkillRepository : GenericRepository<TrainerSkill>, ITrainerSkillRepository
{
    public TrainerSkillRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainerSkill>> GetByTrainerProfileIdAsync(Guid trainerProfileId)
    {
        return await _dbSet
            .Include(s => s.Skill)
            .Where(s => s.TrainerProfileId == trainerProfileId && !s.IsDeleted)
            .OrderBy(s => s.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainerSkill>> GetBySkillIdAsync(Guid skillId)
    {
        return await _dbSet
            .Include(s => s.TrainerProfile).ThenInclude(t => t.Employee)
            .Include(s => s.TrainerProfile).ThenInclude(t => t.Vendor)
            .Where(s => s.SkillId == skillId && !s.IsDeleted)
            .ToListAsync();
    }
}

#endregion

#region Trainer Availability Repository

public class TrainerAvailabilityRepository : GenericRepository<TrainerAvailability>, ITrainerAvailabilityRepository
{
    public TrainerAvailabilityRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainerAvailability>> GetByTrainerProfileIdAsync(Guid trainerProfileId)
    {
        return await _dbSet
            .Where(a => a.TrainerProfileId == trainerProfileId && !a.IsDeleted)
            .OrderBy(a => a.FromDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainerAvailability>> GetBlockedPeriodsAsync(Guid trainerProfileId)
    {
        return await _dbSet
            .Where(a => a.TrainerProfileId == trainerProfileId && !a.IsAvailable && !a.IsDeleted)
            .OrderBy(a => a.FromDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// TRAINING PROGRAM REPOSITORIES
// ============================================================================

#region Training Program Repository

public class TrainingProgramRepository : GenericRepository<TrainingProgram>, ITrainingProgramRepository
{
    public TrainingProgramRepository(ApplicationDbContext context) : base(context) { }

    public async Task<TrainingProgram?> GetByProgramCodeAsync(string programCode)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.ProgramCode == programCode && !p.IsDeleted);
    }

    public async Task<IEnumerable<TrainingProgram>> GetByCategoryAsync(Guid categoryOptionId)
    {
        return await _dbSet
            .Include(p => p.CategoryOption)
            .Include(p => p.ProgramGroup)
            .Where(p => p.CategoryOptionId == categoryOptionId && !p.IsDeleted)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingProgram>> GetByTypeAsync(TrainingType type)
    {
        return await _dbSet
            .Where(p => p.Type == type && !p.IsDeleted)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingProgram>> GetActiveAsync()
    {
        return await _dbSet
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingProgram>> GetWithCertificateAsync()
    {
        return await _dbSet
            .Where(p => p.ProvidesCertificate && !p.IsDeleted)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingProgram>> GetRequiringApprovalAsync()
    {
        return await _dbSet
            .Where(p => p.RequiresApproval && !p.IsDeleted)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();
    }

    public async Task<TrainingProgram?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.CategoryOption)
            .Include(p => p.ProgramGroup)
            .Include(p => p.Competencies).ThenInclude(c => c.Competency)
            .Include(p => p.Skills).ThenInclude(s => s.Skill)
            .Include(p => p.Materials)
            .Include(p => p.Schedules)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

#region Training Material Repository

public class TrainingMaterialRepository : GenericRepository<TrainingMaterial>, ITrainingMaterialRepository
{
    public TrainingMaterialRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainingMaterial>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Where(m => m.ProgramId == programId && !m.IsDeleted)
            .OrderBy(m => m.MaterialName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingMaterial>> GetPublicMaterialsAsync(Guid programId)
    {
        return await _dbSet
            .Where(m => m.ProgramId == programId && m.IsPublic && !m.IsDeleted)
            .OrderBy(m => m.MaterialName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingMaterial>> GetActiveByProgramAsync(Guid programId)
    {
        return await _dbSet
            .Where(m => m.ProgramId == programId && m.IsActive && !m.IsDeleted)
            .OrderBy(m => m.MaterialName)
            .ToListAsync();
    }
}

#endregion

#region Training Program Competency Repository

public class TrainingProgramCompetencyRepository : GenericRepository<TrainingProgramCompetency>, ITrainingProgramCompetencyRepository
{
    public TrainingProgramCompetencyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainingProgramCompetency>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(c => c.Competency)
            .Where(c => c.ProgramId == programId && !c.IsDeleted)
            .OrderBy(c => c.Competency.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingProgramCompetency>> GetByCompetencyIdAsync(Guid competencyId)
    {
        return await _dbSet
            .Include(c => c.Program)
            .Where(c => c.CompetencyId == competencyId && !c.IsDeleted)
            .OrderBy(c => c.Program.ProgramName)
            .ToListAsync();
    }
}

#endregion

#region Training Program Skill Repository

public class TrainingProgramSkillRepository : GenericRepository<TrainingProgramSkill>, ITrainingProgramSkillRepository
{
    public TrainingProgramSkillRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainingProgramSkill>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(s => s.Skill)
            .Where(s => s.ProgramId == programId && !s.IsDeleted)
            .OrderBy(s => s.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingProgramSkill>> GetBySkillIdAsync(Guid skillId)
    {
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => s.SkillId == skillId && !s.IsDeleted)
            .OrderBy(s => s.Program.ProgramName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SCHEDULE & SESSION REPOSITORIES
// ============================================================================

#region Training Schedule Repository

public class TrainingScheduleRepository : GenericRepository<TrainingSchedule>, ITrainingScheduleRepository
{
    public TrainingScheduleRepository(ApplicationDbContext context) : base(context) { }

    // TrainingScheduleSummaryDto reads ProgramCode/ProgramName, TrainerName, VendorName and derives
    // ConfirmedParticipantsCount from the Nominations collection. Every read below fed that summary
    // with a different subset — most carried Program alone, so the Trainer/Vendor columns were blank
    // and the confirmed-seat count read 0 on every list except the paged one. Centralised so the
    // reads cannot drift apart again.
    private IQueryable<TrainingSchedule> WithSummaryNavigations()
        => _dbSet
            .Include(s => s.Program)
            .Include(s => s.TrainerProfile)
            .Include(s => s.Vendor)
            .Include(s => s.Nominations)
            .Where(s => !s.IsDeleted);

    public override async Task<IEnumerable<TrainingSchedule>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<TrainingSchedule?> GetByScheduleNumberAsync(string scheduleNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(s => s.ScheduleNumber == scheduleNumber);
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByProgramIdAsync(Guid programId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.ProgramId == programId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByStatusAsync(ScheduleStatus status)
    {
        return await WithSummaryNavigations()
            .Where(s => s.Status == status)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetUpcomingSchedulesAsync(int daysAhead = 90)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await WithSummaryNavigations()
            .Where(s => s.StartDate >= now
                     && s.StartDate <= cutoff
                     && (s.Status == ScheduleStatus.Planned || s.Status == ScheduleStatus.RegistrationOpen))
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetCurrentlyRunningAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(s => s.StartDate <= now
                     && s.EndDate >= now
                     && s.Status == ScheduleStatus.InProgress)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByTrainerProfileIdAsync(Guid trainerProfileId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.TrainerProfileId == trainerProfileId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByVendorIdAsync(Guid vendorId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.VendorId == vendorId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByBudgetIdAsync(Guid budgetId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.TrainingBudgetId == budgetId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetPendingApprovalAsync()
    {
        return await WithSummaryNavigations()
            .Where(s => s.Status == ScheduleStatus.Planned)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetWithRegistrationOpenAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(s => s.RegistrationOpenDate <= now
                     && s.RegistrationCloseDate >= now)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetWithAvailableSlotsAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(s => s.RegistrationOpenDate <= now
                     && s.RegistrationCloseDate >= now
                     && s.Nominations.Count(n => n.Status == NominationStatus.Confirmed && !n.IsDeleted) < s.MaxParticipants)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<TrainingSchedule?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.Program)
            .Include(s => s.TrainerProfile).ThenInclude(t => t!.Employee)
            .Include(s => s.Vendor)
            .Include(s => s.Sessions)
            .Include(s => s.Nominations).ThenInclude(n => n.Employee)
            .Include(s => s.Attendance).ThenInclude(a => a.Employee)
            .Include(s => s.Feedbacks).ThenInclude(f => f.Employee)
            .Include(s => s.TrainingBudget)
            .Include(s => s.ApprovedBy)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }
}

#endregion

#region Training Session Repository

public class TrainingSessionRepository : GenericRepository<TrainingSession>, ITrainingSessionRepository
{
    public TrainingSessionRepository(ApplicationDbContext context) : base(context) { }

    // TrainingSessionDto reads Schedule.ScheduleNumber, which the by-schedule read did not load.
    private IQueryable<TrainingSession> WithSummaryNavigations()
        => _dbSet
            .Include(s => s.Schedule).ThenInclude(sc => sc.Program)
            .Where(s => !s.IsDeleted);

    public async Task<IEnumerable<TrainingSession>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.ScheduleId == scheduleId)
            .OrderBy(s => s.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSession>> GetByDateAsync(DateTime date)
    {
        return await WithSummaryNavigations()
            .Where(s => s.Date.Date == date.Date)
            .OrderBy(s => s.Date)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// NOMINATION & COMPLETION REPOSITORIES
// ============================================================================

#region Training Nomination Repository

public class TrainingNominationRepository : GenericRepository<TrainingNomination>, ITrainingNominationRepository
{
    public TrainingNominationRepository(ApplicationDbContext context) : base(context) { }

    // TrainingNominationSummaryDto reads Schedule.Program.ProgramName, Schedule.StartDate and the
    // employee's name/number. The by-schedule reads carried Employee only, so ProgramName and
    // TrainingStartDate were blank on a schedule's own nominee list — the screen where they matter
    // most. Centralised so the reads cannot drift apart again.
    private IQueryable<TrainingNomination> WithSummaryNavigations()
        => _dbSet
            .Include(n => n.Employee)
            .Include(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(n => !n.IsDeleted);

    public override async Task<IEnumerable<TrainingNomination>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderByDescending(n => n.NominationDate)
            .ToListAsync();
    }

    public async Task<TrainingNomination?> GetByNominationNumberAsync(string nominationNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(n => n.NominationNumber == nominationNumber);
    }

    public async Task<IEnumerable<TrainingNomination>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(n => n.ScheduleId == scheduleId)
            .OrderBy(n => n.Employee.LastName)
            .ThenBy(n => n.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(n => n.EmployeeId == employeeId)
            .OrderByDescending(n => n.Schedule.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetByStatusAsync(NominationStatus status)
    {
        return await WithSummaryNavigations()
            .Where(n => n.Status == status)
            .OrderBy(n => n.Schedule.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetPendingSupervisorApprovalAsync()
    {
        return await WithSummaryNavigations()
            .Where(n => n.Status == NominationStatus.SupervisorReview)
            .OrderBy(n => n.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetPendingHrApprovalAsync()
    {
        return await WithSummaryNavigations()
            .Where(n => n.Status == NominationStatus.HrReview)
            .OrderBy(n => n.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetConfirmedForScheduleAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(n => n.ScheduleId == scheduleId
                     && n.Status == NominationStatus.Confirmed)
            .OrderBy(n => n.Employee.LastName)
            .ThenBy(n => n.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetByNeedsAssessmentIdAsync(Guid assessmentId)
    {
        return await WithSummaryNavigations()
            .Where(n => n.TrainingNeedsAssessmentId == assessmentId)
            .OrderBy(n => n.NominationDate)
            .ToListAsync();
    }

    public async Task<TrainingNomination?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(n => n.Schedule).ThenInclude(s => s.Program)
            .Include(n => n.Employee)
            .Include(n => n.NominatedBy)
            .Include(n => n.SupervisorApprovedBy)
            .Include(n => n.HrApprovedBy)
            .Include(n => n.CompletionRecord)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
    }
}

#endregion

#region Training Completion Repository

public class TrainingCompletionRepository : GenericRepository<TrainingCompletion>, ITrainingCompletionRepository
{
    public TrainingCompletionRepository(ApplicationDbContext context) : base(context) { }

    // TrainingCompletionDto reads NominationNumber, the employee's name/number and
    // Nomination.Schedule.Program.ProgramName. Each read below carried a different half of that
    // chain — by-nomination had no Nomination at all, by-employee had no Employee, and two stopped
    // at Schedule so ProgramName was blank. Centralised so the reads cannot drift apart again.
    private IQueryable<TrainingCompletion> WithSummaryNavigations()
        => _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Nomination).ThenInclude(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(c => !c.IsDeleted);

    // GetOwnedCompletionAsync routed through the generic GetByIdAsync, so the by-id read and every
    // write response returned a blank nomination number, employee and programme.
    public override async Task<TrainingCompletion?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<TrainingCompletion?> GetByNominationIdAsync(Guid nominationId)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(c => c.NominationId == nominationId);
    }

    public async Task<IEnumerable<TrainingCompletion>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCompletion>> GetByStatusAsync(TrainingCompletionStatus status)
    {
        return await WithSummaryNavigations()
            .Where(c => c.Status == status)
            .OrderByDescending(c => c.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCompletion>> GetPassedCompletionsForProgramAsync(Guid programId)
    {
        return await WithSummaryNavigations()
            .Where(c => c.IsPassed
                     && c.Nomination.Schedule.ProgramId == programId)
            .OrderByDescending(c => c.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCompletion>> GetPendingManagerVerificationAsync()
    {
        return await WithSummaryNavigations()
            .Where(c => !c.IsVerifiedByManager)
            .OrderBy(c => c.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCompletion>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(c => c.Nomination.ScheduleId == scheduleId)
            .OrderBy(c => c.Employee.LastName)
            .ThenBy(c => c.Employee.FirstName)
            .ToListAsync();
    }
}

#endregion

#region Training Attendance Repository

public class TrainingAttendanceRepository : GenericRepository<TrainingAttendance>, ITrainingAttendanceRepository
{
    public TrainingAttendanceRepository(ApplicationDbContext context) : base(context) { }

    // TrainingAttendanceDto reads ScheduleNumber, ProgramName, the employee's name/number,
    // NominationNumber and MarkedByName. No read below carried more than half of those — the
    // register screen showed blank schedule/programme and never showed who marked the row.
    private IQueryable<TrainingAttendance> WithSummaryNavigations()
        => _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Schedule).ThenInclude(s => s.Program)
            .Include(a => a.Nomination)
            .Include(a => a.MarkedBy)
            .Where(a => !a.IsDeleted);


    // Untracked: a tracked re-read returns the identity-map instance with its stale navigations.
    public async Task<TrainingAttendance?> GetByIdWithNavigationsAsync(Guid id)
    {
        return await WithSummaryNavigations().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<TrainingAttendance>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(a => a.ScheduleId == scheduleId)
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingAttendance>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.AttendanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingAttendance>> GetByScheduleAndDateAsync(Guid scheduleId, DateTime date)
    {
        return await WithSummaryNavigations()
            .Where(a => a.ScheduleId == scheduleId
                     && a.AttendanceDate.Date == date.Date)
            .OrderBy(a => a.Employee.LastName)
            .ThenBy(a => a.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingAttendance>> GetAbsenteesForScheduleAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(a => a.ScheduleId == scheduleId && !a.IsPresent)
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }
}

#endregion

#region Training Feedback Repository

public class TrainingFeedbackRepository : GenericRepository<TrainingFeedback>, ITrainingFeedbackRepository
{
    public TrainingFeedbackRepository(ApplicationDbContext context) : base(context) { }

    // TrainingFeedbackDto reads ScheduleNumber, ProgramName and the respondent's name — the
    // by-schedule read carried Employee alone and the by-employee read carried Schedule alone.
    private IQueryable<TrainingFeedback> WithSummaryNavigations()
        => _dbSet
            .Include(f => f.Employee)
            .Include(f => f.Schedule).ThenInclude(s => s.Program)
            .Where(f => !f.IsDeleted);


    // Untracked: a tracked re-read returns the identity-map instance with its stale navigations.
    public async Task<TrainingFeedback?> GetByIdWithNavigationsAsync(Guid id)
    {
        return await WithSummaryNavigations().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<IEnumerable<TrainingFeedback>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(f => f.ScheduleId == scheduleId)
            .OrderBy(f => f.Employee.LastName)
            .ThenBy(f => f.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingFeedback>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(f => f.EmployeeId == employeeId)
            .OrderByDescending(f => f.FeedbackDate)
            .ToListAsync();
    }
}

#endregion

#region Training Follow-Up Assessment Repository

public class TrainingFollowUpAssessmentRepository : GenericRepository<TrainingFollowUpAssessment>, ITrainingFollowUpAssessmentRepository
{
    public TrainingFollowUpAssessmentRepository(ApplicationDbContext context) : base(context) { }

    // TrainingFollowUpAssessmentDto reads ScheduleNumber, ProgramName, the employee's name and
    // ManagerName. Only GetPendingManagerObservationAsync carried the full set; the other three
    // each dropped something. This is the shape that read correctly on one screen and blank on the
    // next, which is what makes it hard to spot.
    private IQueryable<TrainingFollowUpAssessment> WithSummaryNavigations()
        => _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Schedule).ThenInclude(s => s.Program)
            .Include(a => a.Manager)
            .Where(a => !a.IsDeleted);


    // Untracked: a tracked re-read returns the identity-map instance with its stale navigations.
    public async Task<TrainingFollowUpAssessment?> GetByIdWithNavigationsAsync(Guid id)
    {
        return await WithSummaryNavigations().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<TrainingFollowUpAssessment>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(a => a.ScheduleId == scheduleId)
            .OrderBy(a => a.AssessmentType)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingFollowUpAssessment>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.AssessmentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingFollowUpAssessment>> GetByAssessmentTypeAsync(Guid scheduleId, TrainingAssessmentType assessmentType)
    {
        return await WithSummaryNavigations()
            .Where(a => a.ScheduleId == scheduleId
                     && a.AssessmentType == assessmentType)
            .OrderBy(a => a.Employee.LastName)
            .ThenBy(a => a.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingFollowUpAssessment>> GetPendingManagerObservationAsync()
    {
        return await WithSummaryNavigations()
            .Where(a => a.ManagerSubmittedDate == null)
            .OrderBy(a => a.AssessmentDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// CERTIFICATE REPOSITORIES
// ============================================================================

#region Training Certificate Repository

public class TrainingCertificateRepository : GenericRepository<TrainingCertificate>, ITrainingCertificateRepository
{
    public TrainingCertificateRepository(ApplicationDbContext context) : base(context) { }

    // TrainingCertificateDto reads the nomination number, the employee's name/number, the programme
    // name, the superseded certificate's number and who issued it. Every read below carried a
    // different subset — the by-employee one had no Employee at all, so "my certificates" showed a
    // blank holder. Centralised so the reads cannot drift apart again.
    private IQueryable<TrainingCertificate> WithSummaryNavigations()
        => _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Program)
            .Include(c => c.Nomination)
            .Include(c => c.PreviousCertificate)
            .Include(c => c.IssuedBy)
            .Where(c => !c.IsDeleted);

    // The service's owned-entity fetch used the generic GetByIdAsync, which loads nothing — blanking
    // the by-id read and every write response that re-reads through it.
    public override async Task<TrainingCertificate?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(c => c.Id == id);
    }

    public override async Task<IEnumerable<TrainingCertificate>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<TrainingCertificate?> GetByCertificateNumberAsync(string certificateNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber);
    }

    public async Task<IEnumerable<TrainingCertificate>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetByProgramIdAsync(Guid programId)
    {
        return await WithSummaryNavigations()
            .Where(c => c.ProgramId == programId)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetByStatusAsync(CertificateStatus status)
    {
        return await WithSummaryNavigations()
            .Where(c => c.Status == status)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetActiveAsync()
    {
        return await WithSummaryNavigations()
            .Where(c => c.Status == CertificateStatus.Active)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetExpiredAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(c => c.ExpiryDate != null && c.ExpiryDate < now)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetExpiringAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await WithSummaryNavigations()
            .Where(c => c.ExpiryDate != null
                     && c.ExpiryDate >= now
                     && c.ExpiryDate <= cutoff)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetRenewalsForCertificateAsync(Guid previousCertificateId)
    {
        return await WithSummaryNavigations()
            .Where(c => c.PreviousCertificateId == previousCertificateId && c.IsRenewal)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }
}

#endregion

#region Employee Certificate Repository

public class EmployeeCertificateRepository : GenericRepository<EmployeeCertificate>, IEmployeeCertificateRepository
{
    public EmployeeCertificateRepository(ApplicationDbContext context) : base(context) { }

    // EmployeeCertificateDto reads the holder's name/number and who verified it. The reads split
    // those between them — by-employee carried VerifiedBy but no Employee, the rest the other way
    // round — so each list blanked whichever half it was missing.
    private IQueryable<EmployeeCertificate> WithSummaryNavigations()
        => _dbSet
            .Include(c => c.Employee)
            .Include(c => c.VerifiedBy)
            .Where(c => !c.IsDeleted);

    // GetOwnedAsync used the generic GetByIdAsync, so the by-id read and every write response
    // (create, update, verify) came back with a blank holder and verifier.
    public override async Task<EmployeeCertificate?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(c => c.Id == id);
    }

    public override async Task<IEnumerable<EmployeeCertificate>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    // Untracked: a tracked re-read returns the identity-map instance with its stale navigations.
    public async Task<EmployeeCertificate?> GetByIdWithNavigationsAsync(Guid id)
    {
        return await WithSummaryNavigations().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<EmployeeCertificate>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCertificate>> GetUnverifiedAsync()
    {
        return await WithSummaryNavigations()
            .Where(c => !c.IsVerified)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCertificate>> GetExpiredAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(c => c.ExpiryDate != null && c.ExpiryDate < now)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCertificate>> GetExpiringAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await WithSummaryNavigations()
            .Where(c => c.ExpiryDate != null
                     && c.ExpiryDate >= now
                     && c.ExpiryDate <= cutoff)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCertificate>> GetByStatusAsync(CertificateStatus status)
    {
        return await WithSummaryNavigations()
            .Where(c => c.Status == status)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// COMPLIANCE REPOSITORIES
// ============================================================================

#region Compliance Training Requirement Repository

public class ComplianceTrainingRequirementRepository : GenericRepository<ComplianceTrainingRequirement>, IComplianceTrainingRequirementRepository
{
    public ComplianceTrainingRequirementRepository(ApplicationDbContext context) : base(context) { }

    // The requirement summary derives TotalAssignedEmployees and ComplianceRate from the
    // EmployeeRecords collection, and names the programme and the scope (level / unit / position).
    // Nothing loaded EmployeeRecords except GetWithFullDetailsAsync, so the compliance rate — the
    // one number this screen exists for — read 0% on every row of every list.
    private IQueryable<ComplianceTrainingRequirement> WithSummaryNavigations()
        => _dbSet
            .Include(r => r.Program)
            .Include(r => r.OrganizationLevel)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Position)
            .Include(r => r.EmployeeRecords)
            .Where(r => !r.IsDeleted);

    // GetOwnedRequirementAsync used the generic GetByIdAsync, blanking the by-id read and every
    // write response that re-reads through it.
    public override async Task<ComplianceTrainingRequirement?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(r => r.Id == id);
    }

    public override async Task<IEnumerable<ComplianceTrainingRequirement>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<ComplianceTrainingRequirement?> GetByRequirementCodeAsync(string requirementCode)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(r => r.RequirementCode == requirementCode);
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetActiveAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(r => r.IsActive
                     && r.EffectiveDate <= now
                     && (r.ExpiryDate == null || r.ExpiryDate > now))
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetByProgramIdAsync(Guid programId)
    {
        return await WithSummaryNavigations()
            .Where(r => r.ProgramId == programId)
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetByOrganizationLevelAsync(Guid orgLevelId)
    {
        return await WithSummaryNavigations()
            .Where(r => r.OrganizationLevelId == orgLevelId)
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetByOrganizationUnitAsync(Guid orgUnitId)
    {
        return await WithSummaryNavigations()
            .Where(r => r.OrganizationUnitId == orgUnitId)
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetByPositionAsync(Guid positionId)
    {
        return await WithSummaryNavigations()
            .Where(r => r.PositionId == positionId)
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<ComplianceTrainingRequirement?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(r => r.Program)
            .Include(r => r.OrganizationLevel)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Position)
            .Include(r => r.EmployeeRecords).ThenInclude(e => e.Employee)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }
}

#endregion

#region Employee Compliance Record Repository

public class EmployeeComplianceRecordRepository : GenericRepository<EmployeeComplianceRecord>, IEmployeeComplianceRecordRepository
{
    public EmployeeComplianceRecordRepository(ApplicationDbContext context) : base(context) { }

    // EmployeeComplianceRecordDto reads the employee's name/number, the requirement's code/name, the
    // programme behind it, the nomination that fulfilled it and who granted an exemption. The reads
    // each carried a fragment: by-employee had no Employee, by-requirement had no Requirement, and
    // none stopped at Requirement without .ThenInclude(Program) so ProgramName was blank.
    // ⚠ GetExemptAsync did not load ExemptedBy — on the one list whose entire purpose is showing who
    // granted the exemption.
    private IQueryable<EmployeeComplianceRecord> WithSummaryNavigations()
        => _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Requirement).ThenInclude(req => req.Program)
            .Include(r => r.FulfillingNomination)
            .Include(r => r.ExemptedBy)
            .Where(r => !r.IsDeleted);

    // GetOwnedRecordAsync used the generic GetByIdAsync, blanking the by-id read and the write
    // responses that re-read through it.
    public override async Task<EmployeeComplianceRecord?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(r => r.Id == id);
    }

    public override async Task<IEnumerable<EmployeeComplianceRecord>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    // Untracked: exemption sets ExemptedById, so a tracked re-read still maps a null exempter.
    public async Task<EmployeeComplianceRecord?> GetByIdWithNavigationsAsync(Guid id)
    {
        return await WithSummaryNavigations().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(r => r.EmployeeId == employeeId)
            .OrderBy(r => r.Requirement.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetByRequirementIdAsync(Guid requirementId)
    {
        return await WithSummaryNavigations()
            .Where(r => r.RequirementId == requirementId)
            .OrderBy(r => r.Employee.LastName)
            .ThenBy(r => r.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetByStatusAsync(ComplianceStatus status)
    {
        return await WithSummaryNavigations()
            .Where(r => r.Status == status)
            .OrderBy(r => r.Employee.LastName)
            .ThenBy(r => r.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetNonCompliantAsync()
    {
        return await WithSummaryNavigations()
            .Where(r => r.Status != ComplianceStatus.Compliant
                     && !r.IsExempt)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetOverdueAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(r => !r.IsExempt
                     && r.NextDueDate != null
                     && r.NextDueDate < now
                     && r.Status != ComplianceStatus.Compliant)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetExpiringAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await WithSummaryNavigations()
            .Where(r => !r.IsExempt
                     && r.NextDueDate != null
                     && r.NextDueDate >= now
                     && r.NextDueDate <= cutoff)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetExemptAsync()
    {
        return await WithSummaryNavigations()
            .Where(r => r.IsExempt)
            .OrderBy(r => r.Employee.LastName)
            .ThenBy(r => r.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<EmployeeComplianceRecord?> GetEmployeeRecordAsync(Guid employeeId, Guid requirementId)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId
                                    && r.RequirementId == requirementId
                                   );
    }
}

#endregion

// ============================================================================
// BUDGET REPOSITORIES
// ============================================================================

#region Training Budget Repository

public class TrainingBudgetRepository : GenericRepository<TrainingBudget>, ITrainingBudgetRepository
{
    public TrainingBudgetRepository(ApplicationDbContext context) : base(context) { }

    // Single source of tenant + soft-delete scoping so no query below can forget either.
    private IQueryable<TrainingBudget> OwnedBy(Guid tenantId)
        => _dbSet.Where(b => b.TenantId == tenantId && !b.IsDeleted);

    public async Task<TrainingBudget?> GetForTenantAsync(Guid id, Guid tenantId)
    {
        return await OwnedBy(tenantId).FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<TrainingBudget?> GetByBudgetCodeAsync(string budgetCode, Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .FirstOrDefaultAsync(b => b.BudgetCode == budgetCode);
    }

    public async Task<IEnumerable<TrainingBudget>> GetAllForTenantAsync(Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(b => b.OrganizationUnit)
            .OrderByDescending(b => b.Year)
            .ThenBy(b => b.Quarter)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingBudget>> GetByYearAsync(int year, Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(b => b.OrganizationUnit)
            .Where(b => b.Year == year)
            .OrderBy(b => b.Quarter)
            .ThenBy(b => b.OrganizationUnit!.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingBudget>> GetByYearAndQuarterAsync(int year, int? quarter, Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(b => b.OrganizationUnit)
            .Where(b => b.Year == year && b.Quarter == quarter)
            .OrderBy(b => b.OrganizationUnit!.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingBudget>> GetByOrganizationUnitAsync(Guid orgUnitId, Guid tenantId)
    {
        return await OwnedBy(tenantId)
            // The summary DTO renders OrganizationUnitName; every sibling read here includes it and
            // this one did not, so the unit column was blank on this filtered list alone.
            .Include(b => b.OrganizationUnit)
            .Where(b => b.OrganizationUnitId == orgUnitId)
            .OrderByDescending(b => b.Year)
            .ThenBy(b => b.Quarter)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingBudget>> GetByStatusAsync(TrainingBudgetStatus status, Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(b => b.OrganizationUnit)
            .Where(b => b.Status == status)
            .OrderByDescending(b => b.Year)
            .ThenBy(b => b.Quarter)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingBudget>> GetApprovedAsync(Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(b => b.OrganizationUnit)
            .Where(b => b.Status == TrainingBudgetStatus.Approved)
            .OrderByDescending(b => b.Year)
            .ThenBy(b => b.Quarter)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingBudget>> GetWithExceededBudgetAsync(Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(b => b.OrganizationUnit)
            .Where(b => b.SpentAmount > b.AllocatedAmount)
            .OrderByDescending(b => b.Year)
            .ToListAsync();
    }

    public async Task<TrainingBudget?> GetWithFullDetailsAsync(Guid id, Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(b => b.OrganizationLevel)
            .Include(b => b.OrganizationUnit)
            .Include(b => b.ApprovedBy)
            .Include(b => b.Transactions).ThenInclude(t => t.RecordedBy)
            .Include(b => b.Schedules).ThenInclude(s => s.Program)
            .FirstOrDefaultAsync(b => b.Id == id);
    }
}

#endregion

#region Training Budget Transaction Repository

public class TrainingBudgetTransactionRepository : GenericRepository<TrainingBudgetTransaction>, ITrainingBudgetTransactionRepository
{
    public TrainingBudgetTransactionRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<TrainingBudgetTransaction> OwnedBy(Guid tenantId)
        => _dbSet.Where(t => t.TenantId == tenantId && !t.IsDeleted);

    public async Task<IEnumerable<TrainingBudgetTransaction>> GetByBudgetIdAsync(Guid budgetId, Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(t => t.Budget)
            .Include(t => t.Schedule)
            .Include(t => t.RecordedBy)
            .Where(t => t.BudgetId == budgetId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingBudgetTransaction>> GetByScheduleIdAsync(Guid scheduleId, Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(t => t.Budget)
            .Include(t => t.RecordedBy)
            .Where(t => t.ScheduleId == scheduleId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingBudgetTransaction>> GetByDateRangeAsync(Guid budgetId, DateTime from, DateTime to, Guid tenantId)
    {
        return await OwnedBy(tenantId)
            .Include(t => t.Budget)
            .Include(t => t.Schedule)
            .Include(t => t.RecordedBy)
            .Where(t => t.BudgetId == budgetId
                     && t.TransactionDate >= from
                     && t.TransactionDate <= to)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// TRAINING PLAN REPOSITORIES
// ============================================================================

#region Training Plan Repository

public class TrainingPlanRepository : GenericRepository<TrainingPlan>, ITrainingPlanRepository
{
    public TrainingPlanRepository(ApplicationDbContext context) : base(context) { }

    // TrainingPlanSummaryDto renders OrganizationUnitName and derives TotalItemsCount /
    // CompletedItemsCount from the Items collection. An un-included collection is empty, not null,
    // so the counts read "0 / 0" and the completion percentage reads "—" on every row instead of
    // failing loudly. Centralised so the reads cannot drift apart again.
    private IQueryable<TrainingPlan> WithSummaryNavigations()
        => _dbSet
            .Include(p => p.OrganizationUnit)
            .Include(p => p.Items)
            .Where(p => !p.IsDeleted);

    public override async Task<IEnumerable<TrainingPlan>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderByDescending(p => p.Year)
            .ThenBy(p => p.PlanNumber)
            .ToListAsync();
    }

    public async Task<TrainingPlan?> GetByPlanNumberAsync(string planNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(p => p.PlanNumber == planNumber);
    }

    public async Task<IEnumerable<TrainingPlan>> GetByYearAsync(int year)
    {
        return await WithSummaryNavigations()
            .Where(p => p.Year == year)
            .OrderBy(p => p.OrganizationUnit!.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlan>> GetByStatusAsync(TrainingPlanStatus status)
    {
        return await WithSummaryNavigations()
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.Year)
            .ThenBy(p => p.OrganizationUnit!.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlan>> GetByOrganizationUnitAsync(Guid orgUnitId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.OrganizationUnitId == orgUnitId)
            .OrderByDescending(p => p.Year)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlan>> GetPendingApprovalAsync()
    {
        return await WithSummaryNavigations()
            .Where(p => p.Status == TrainingPlanStatus.PendingApproval)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<TrainingPlan?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.OrganizationLevel)
            .Include(p => p.OrganizationUnit)
            .Include(p => p.ApprovedBy)
            .Include(p => p.Items).ThenInclude(i => i.Program)
            .Include(p => p.Items).ThenInclude(i => i.FulfilledBySchedule)
            .Include(p => p.BudgetLines)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

#region Training Plan Item Repository

public class TrainingPlanItemRepository : GenericRepository<TrainingPlanItem>, ITrainingPlanItemRepository
{
    public TrainingPlanItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainingPlanItem>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(i => i.Plan)
            .Include(i => i.Program)
            .Include(i => i.FulfilledBySchedule)
            .Where(i => i.PlanId == planId && !i.IsDeleted)
            .OrderBy(i => i.Quarter)
            .ThenBy(i => i.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlanItem>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(i => i.Plan).ThenInclude(p => p.OrganizationUnit)
            .Where(i => i.ProgramId == programId && !i.IsDeleted)
            .OrderByDescending(i => i.Plan.Year)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlanItem>> GetCompletedItemsAsync(Guid planId)
    {
        return await _dbSet
            .Include(i => i.Program)
            .Include(i => i.FulfilledBySchedule)
            .Where(i => i.PlanId == planId && i.IsCompleted && !i.IsDeleted)
            .OrderBy(i => i.Quarter)
            .ThenBy(i => i.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlanItem>> GetPendingItemsAsync(Guid planId)
    {
        return await _dbSet
            .Include(i => i.Program)
            .Where(i => i.PlanId == planId && !i.IsCompleted && !i.IsDeleted)
            .OrderBy(i => i.Quarter)
            .ThenBy(i => i.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlanItem>> GetByQuarterAsync(Guid planId, int quarter)
    {
        return await _dbSet
            .Include(i => i.Program)
            .Where(i => i.PlanId == planId && i.Quarter == quarter && !i.IsDeleted)
            .OrderBy(i => i.PlannedStartDate)
            .ToListAsync();
    }
}

#endregion

#region Training Plan Budget Line Repository

public class TrainingPlanBudgetLineRepository : GenericRepository<TrainingPlanBudgetLine>, ITrainingPlanBudgetLineRepository
{
    public TrainingPlanBudgetLineRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainingPlanBudgetLine>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(b => b.Plan)
            .Where(b => b.PlanId == planId && !b.IsDeleted)
            .OrderBy(b => b.Category)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// TRAINING NEEDS ASSESSMENT REPOSITORIES
// ============================================================================

#region Training Needs Assessment Repository

public class TrainingNeedsAssessmentRepository : GenericRepository<TrainingNeedsAssessment>, ITrainingNeedsAssessmentRepository
{
    public TrainingNeedsAssessmentRepository(ApplicationDbContext context) : base(context) { }

    // TrainingNeedsAssessmentSummaryDto renders EmployeeName/EmployeeNumber AND counts the two child
    // collections (RecommendedProgramsCount, SkillGapsCount). An un-included collection is an empty
    // one, not null, so the counts silently read 0 rather than failing — every list read that feeds
    // the summary needs all three. Centralised so they cannot drift apart again.
    private IQueryable<TrainingNeedsAssessment> WithSummaryNavigations()
        => _dbSet
            .Include(a => a.Employee)
            .Include(a => a.RecommendedPrograms)
            .Include(a => a.SkillGaps)
            .Where(a => !a.IsDeleted);

    public override async Task<IEnumerable<TrainingNeedsAssessment>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderByDescending(a => a.Year)
            .ThenBy(a => a.Employee!.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNeedsAssessment>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.Year)
            .ToListAsync();
    }

    public async Task<TrainingNeedsAssessment?> GetByEmployeeAndYearAsync(Guid employeeId, int year)
    {
        return await WithSummaryNavigations()
            .Include(a => a.IdentifiedBy)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId
                                    && a.Year == year);
    }

    public async Task<IEnumerable<TrainingNeedsAssessment>> GetByYearAsync(int year)
    {
        return await WithSummaryNavigations()
            .Where(a => a.Year == year)
            .OrderBy(a => a.Employee!.LastName)
            .ThenBy(a => a.Employee!.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNeedsAssessment>> GetUnfulfilledAsync()
    {
        return await WithSummaryNavigations()
            .Where(a => !a.TrainingProvided)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.Year)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNeedsAssessment>> GetByPriorityAsync(TrainingPriority priority)
    {
        return await WithSummaryNavigations()
            .Where(a => a.Priority == priority)
            .OrderByDescending(a => a.Year)
            .ThenBy(a => a.Employee!.LastName)
            .ToListAsync();
    }

    public async Task<TrainingNeedsAssessment?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.IdentifiedBy)
            .Include(a => a.RecommendedPrograms).ThenInclude(p => p.Program)
            .Include(a => a.SkillGaps).ThenInclude(s => s.Skill)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }
}

#endregion

#region Training Needs Assessment Program Repository

public class TrainingNeedsAssessmentProgramRepository : GenericRepository<TrainingNeedsAssessmentProgram>, ITrainingNeedsAssessmentProgramRepository
{
    public TrainingNeedsAssessmentProgramRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainingNeedsAssessmentProgram>> GetByAssessmentIdAsync(Guid assessmentId)
    {
        return await _dbSet
            .Include(p => p.Program)
            .Where(p => p.AssessmentId == assessmentId && !p.IsDeleted)
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.Program.ProgramName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentProgram>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(p => p.Assessment).ThenInclude(a => a.Employee)
            .Where(p => p.ProgramId == programId && !p.IsDeleted)
            .OrderByDescending(p => p.Assessment.Year)
            .ToListAsync();
    }
}

#endregion

#region Training Needs Assessment Skill Repository

public class TrainingNeedsAssessmentSkillRepository : GenericRepository<TrainingNeedsAssessmentSkill>, ITrainingNeedsAssessmentSkillRepository
{
    public TrainingNeedsAssessmentSkillRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainingNeedsAssessmentSkill>> GetByAssessmentIdAsync(Guid assessmentId)
    {
        return await _dbSet
            .Include(s => s.Skill)
            .Where(s => s.AssessmentId == assessmentId && !s.IsDeleted)
            .OrderBy(s => s.GapPriority)
            .ThenBy(s => s.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSkill>> GetBySkillIdAsync(Guid skillId)
    {
        return await _dbSet
            .Include(s => s.Assessment).ThenInclude(a => a.Employee)
            .Where(s => s.SkillId == skillId && !s.IsDeleted)
            .OrderByDescending(s => s.Assessment.Year)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// WAITLIST & REQUEST REPOSITORIES
// ============================================================================

#region Training Waitlist Repository

public class TrainingWaitlistRepository : GenericRepository<TrainingWaitlist>, ITrainingWaitlistRepository
{
    public TrainingWaitlistRepository(ApplicationDbContext context) : base(context) { }

    // TrainingWaitlistDto reads ScheduleNumber, ProgramName, the employee's name/number and
    // CreatedNominationNumber (the nomination a promoted entry produced). Nothing loaded Nomination
    // at all, so the "promoted to" column was blank on every row even after a successful promotion.
    private IQueryable<TrainingWaitlist> WithSummaryNavigations()
        => _dbSet
            .Include(w => w.Employee)
            .Include(w => w.Schedule).ThenInclude(s => s.Program)
            .Include(w => w.Nomination)
            .Where(w => !w.IsDeleted);

    // The service's GetOwnedAsync routed through the generic GetByIdAsync, which loads nothing — so
    // the by-id read AND every write response that re-reads through it came back with a blank
    // schedule, programme and employee while the list beside it was correct.
    public override async Task<TrainingWaitlist?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(w => w.ScheduleId == scheduleId)
            .OrderBy(w => w.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(w => w.EmployeeId == employeeId)
            .OrderBy(w => w.Schedule.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetActiveWaitlistAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(w => w.ScheduleId == scheduleId
                     && w.Status == TrainingWaitlistStatus.Active)
            .OrderBy(w => w.Position)
            .ToListAsync();
    }

    public async Task<TrainingWaitlist?> GetNextInQueueAsync(Guid scheduleId)
    {
        return await WithSummaryNavigations()
            .Where(w => w.ScheduleId == scheduleId
                     && w.Status == TrainingWaitlistStatus.Active)
            .OrderBy(w => w.Position)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetByStatusAsync(Guid scheduleId, TrainingWaitlistStatus status)
    {
        return await WithSummaryNavigations()
            .Where(w => w.ScheduleId == scheduleId && w.Status == status)
            .OrderBy(w => w.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetOfferedAsync(Guid scheduleId)
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(w => w.ScheduleId == scheduleId
                     && w.Status == TrainingWaitlistStatus.Offered
                     && w.OfferExpiryDate != null
                     && w.OfferExpiryDate >= now)
            .OrderBy(w => w.OfferExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetExpiredOffersAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(w => w.Status == TrainingWaitlistStatus.Offered
                     && w.OfferExpiryDate != null
                     && w.OfferExpiryDate < now)
            .OrderBy(w => w.OfferExpiryDate)
            .ToListAsync();
    }
}

#endregion

#region Training Request Repository

public class TrainingRequestRepository : GenericRepository<TrainingRequest>, ITrainingRequestRepository
{
    public TrainingRequestRepository(ApplicationDbContext context) : base(context) { }

    // TrainingRequestSummaryDto reads EmployeeName and LinkedProgramName. The by-employee read
    // dropped Employee and the approvals queue dropped LinkedProgram, so each blanked one column.
    private IQueryable<TrainingRequest> WithSummaryNavigations()
        => _dbSet
            .Include(r => r.Employee)
            .Include(r => r.LinkedProgram)
            .Where(r => !r.IsDeleted);

    // Same gap as the waitlist repository: GetOwnedAsync used the generic GetByIdAsync, blanking the
    // detail read and every write response (submit, approve, reject, link-to-programme).
    public override async Task<TrainingRequest?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(r => r.Id == id);
    }

    public override async Task<IEnumerable<TrainingRequest>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<TrainingRequest?> GetByRequestNumberAsync(string requestNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber);
    }

    public async Task<IEnumerable<TrainingRequest>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingRequest>> GetByStatusAsync(TrainingRequestStatus status)
    {
        return await WithSummaryNavigations()
            .Where(r => r.Status == status)
            .OrderBy(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingRequest>> GetPendingApprovalAsync()
    {
        return await WithSummaryNavigations()
            .Where(r => r.Status == TrainingRequestStatus.Submitted)
            .OrderBy(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingRequest>> GetLinkedToProgramAsync(Guid programId)
    {
        return await WithSummaryNavigations()
            .Where(r => r.LinkedProgramId == programId)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// LEARNING PATH REPOSITORIES
// ============================================================================

#region Learning Path Repository

public class LearningPathRepository : GenericRepository<LearningPath>, ILearningPathRepository
{
    public LearningPathRepository(ApplicationDbContext context) : base(context) { }

    // LearningPathSummaryDto names the unit and position and derives TotalProgramsCount from the
    // Programs collection. Not one list read below carried a single include, so every path showed a
    // blank scope and "0 programmes" — on a record whose entire point is being a sequence of
    // programmes. Centralised so the reads cannot drift apart again.
    private IQueryable<LearningPath> WithSummaryNavigations()
        => _dbSet
            .Include(lp => lp.OrganizationLevel)
            .Include(lp => lp.OrganizationUnit)
            .Include(lp => lp.Position)
            .Include(lp => lp.Programs)
            .Include(lp => lp.Enrollments)
            .Where(lp => !lp.IsDeleted);

    // The service's owned-path fetch used the generic GetByIdAsync, which loads nothing.
    public override async Task<LearningPath?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(lp => lp.Id == id);
    }

    public override async Task<IEnumerable<LearningPath>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetByStatusAsync(LearningPathStatus status)
    {
        return await WithSummaryNavigations()
            .Where(lp => lp.Status == status)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetActiveAsync()
    {
        return await WithSummaryNavigations()
            .Where(lp => lp.Status == LearningPathStatus.Active)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetByOrganizationLevelAsync(Guid orgLevelId)
    {
        return await WithSummaryNavigations()
            .Where(lp => lp.OrganizationLevelId == orgLevelId)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetByOrganizationUnitAsync(Guid orgUnitId)
    {
        return await WithSummaryNavigations()
            .Where(lp => lp.OrganizationUnitId == orgUnitId)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetByPositionAsync(Guid positionId)
    {
        return await WithSummaryNavigations()
            .Where(lp => lp.PositionId == positionId)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetWithCertificateAsync()
    {
        return await WithSummaryNavigations()
            .Where(lp => lp.ProvidesCertificate)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<LearningPath?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(lp => lp.OrganizationLevel)
            .Include(lp => lp.OrganizationUnit)
            .Include(lp => lp.Position)
            .Include(lp => lp.Programs).ThenInclude(p => p.Program)
            .Include(lp => lp.Programs).ThenInclude(p => p.PrerequisitePathProgram)
            .Include(lp => lp.TargetSkills).ThenInclude(s => s.Skill)
            .Include(lp => lp.Enrollments).ThenInclude(e => e.Employee)
            .FirstOrDefaultAsync(lp => lp.Id == id);
    }
}

#endregion

#region Learning Path Program Repository

public class LearningPathProgramRepository : GenericRepository<LearningPathProgram>, ILearningPathProgramRepository
{
    public LearningPathProgramRepository(ApplicationDbContext context) : base(context) { }

    // LearningPathProgramDto names the path, the programme, and the *prerequisite* programme —
    // which is the whole point of a sequenced path. The by-path read carried Program alone, so the
    // prerequisite column was blank on exactly the screen that defines the sequence.
    private IQueryable<LearningPathProgram> WithSummaryNavigations()
        => _dbSet
            .Include(p => p.LearningPath)
            .Include(p => p.Program)
            .Include(p => p.PrerequisitePathProgram).ThenInclude(pre => pre!.Program)
            .Where(p => !p.IsDeleted);

    public override async Task<LearningPathProgram?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(p => p.Id == id);
    }

    // Untracked: a changed prerequisite leaves the identity-map instance mapping the previous one.
    public async Task<LearningPathProgram?> GetByIdWithNavigationsAsync(Guid id)
    {
        return await WithSummaryNavigations().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<LearningPathProgram>> GetByLearningPathIdAsync(Guid learningPathId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.LearningPathId == learningPathId)
            .OrderBy(p => p.SequenceOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPathProgram>> GetByProgramIdAsync(Guid programId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.ProgramId == programId)
            .OrderBy(p => p.LearningPath.Name)
            .ToListAsync();
    }
}

#endregion

#region Learning Path Skill Repository

public class LearningPathSkillRepository : GenericRepository<LearningPathSkill>, ILearningPathSkillRepository
{
    public LearningPathSkillRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<LearningPathSkill> WithSummaryNavigations()
        => _dbSet
            .Include(s => s.LearningPath)
            .Include(s => s.Skill)
            .Where(s => !s.IsDeleted);

    public override async Task<LearningPathSkill?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<LearningPathSkill>> GetByLearningPathIdAsync(Guid learningPathId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.LearningPathId == learningPathId)
            .OrderBy(s => s.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPathSkill>> GetBySkillIdAsync(Guid skillId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.SkillId == skillId)
            .OrderBy(s => s.LearningPath.Name)
            .ToListAsync();
    }
}

#endregion

#region Employee Learning Path Repository

public class EmployeeLearningPathRepository : GenericRepository<EmployeeLearningPath>, IEmployeeLearningPathRepository
{
    public EmployeeLearningPathRepository(ApplicationDbContext context) : base(context) { }

    // EmployeeLearningPathDto names the learner, the path and who assigned it. The reads split those
    // between them — by-employee had no Employee, by-path had no LearningPath — so each list blanked
    // whichever half it was missing. Centralised so they cannot drift apart again.
    private IQueryable<EmployeeLearningPath> WithSummaryNavigations()
        => _dbSet
            .Include(e => e.Employee)
            .Include(e => e.LearningPath)
            .Include(e => e.AssignedBy)
            .Where(e => !e.IsDeleted);

    // The service's owned-enrollment fetch used the generic GetByIdAsync, which loads nothing.
    public override async Task<EmployeeLearningPath?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(e => e.EmployeeId == employeeId)
            .OrderBy(e => e.LearningPath.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetByLearningPathIdAsync(Guid learningPathId)
    {
        return await WithSummaryNavigations()
            .Where(e => e.LearningPathId == learningPathId)
            .OrderBy(e => e.Employee.LastName)
            .ThenBy(e => e.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetActiveEnrollmentsAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(e => e.EmployeeId == employeeId && !e.IsCompleted)
            .OrderBy(e => e.TargetCompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetCompletedAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(e => e.EmployeeId == employeeId && e.IsCompleted)
            .OrderByDescending(e => e.ActualCompletionDate)
            .ToListAsync();
    }

    public async Task<EmployeeLearningPath?> GetEnrollmentAsync(Guid employeeId, Guid learningPathId)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId
                                    && e.LearningPathId == learningPathId
                                   );
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetOverdueAsync()
    {
        var now = DateTime.UtcNow;
        return await WithSummaryNavigations()
            .Where(e => !e.IsCompleted
                     && e.TargetCompletionDate != null
                     && e.TargetCompletionDate < now)
            .OrderBy(e => e.TargetCompletionDate)
            .ToListAsync();
    }

    public async Task<EmployeeLearningPath?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(e => e.Employee)
            .Include(e => e.LearningPath)
            .Include(e => e.AssignedBy)
            .Include(e => e.Steps).ThenInclude(s => s.LearningPathProgram).ThenInclude(lpp => lpp.Program)
            .Include(e => e.Steps).ThenInclude(s => s.LearningPathProgram).ThenInclude(lpp => lpp.PrerequisitePathProgram).ThenInclude(pre => pre!.Program)
            .Include(e => e.Steps).ThenInclude(s => s.Nomination)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Include(e => e.Employee).ThenInclude(emp => emp.OrganizationUnit)
            .Include(e => e.Employee).ThenInclude(emp => emp.Position)
            .Include(e => e.LearningPath)
            .Include(e => e.AssignedBy)
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.Employee.LastName)
            .ThenBy(e => e.Employee.FirstName)
            .ToListAsync();
    }
}

#endregion

#region Employee Learning Path Step Repository

public class EmployeeLearningPathStepRepository : GenericRepository<EmployeeLearningPathStep>, IEmployeeLearningPathStepRepository
{
    public EmployeeLearningPathStepRepository(ApplicationDbContext context) : base(context) { }

    // EmployeeLearningPathStepDto names the programme, its prerequisite, and the nomination that
    // evidences completion. The three list reads stopped at Program, so a learner's step list showed
    // no prerequisite (the thing that says why a step is locked) and no nomination.
    private IQueryable<EmployeeLearningPathStep> WithSummaryNavigations()
        => _dbSet
            .Include(s => s.LearningPathProgram).ThenInclude(lpp => lpp.Program)
            .Include(s => s.LearningPathProgram).ThenInclude(lpp => lpp.PrerequisitePathProgram).ThenInclude(pre => pre!.Program)
            .Include(s => s.Nomination)
            .Where(s => !s.IsDeleted);

    public override async Task<EmployeeLearningPathStep?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations().FirstOrDefaultAsync(s => s.Id == id);
    }

    // Untracked: linking a nomination leaves the identity-map instance mapping a null one.
    public async Task<EmployeeLearningPathStep?> GetByIdWithNavigationsAsync(Guid id)
    {
        return await WithSummaryNavigations().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<EmployeeLearningPathStep>> GetByEmployeeLearningPathIdAsync(Guid enrollmentId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.EmployeeLearningPathId == enrollmentId)
            .OrderBy(s => s.LearningPathProgram.SequenceOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPathStep>> GetIncompleteStepsAsync(Guid enrollmentId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.EmployeeLearningPathId == enrollmentId && !s.IsCompleted)
            .OrderBy(s => s.LearningPathProgram.SequenceOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPathStep>> GetCompletedStepsAsync(Guid enrollmentId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.EmployeeLearningPathId == enrollmentId && s.IsCompleted)
            .OrderBy(s => s.LearningPathProgram.SequenceOrder)
            .ToListAsync();
    }

    public async Task<EmployeeLearningPathStep?> GetStepWithContextAsync(Guid stepId)
    {
        return await _dbSet
            // Program + active materials
            .Include(s => s.LearningPathProgram)
                .ThenInclude(lpp => lpp.Program)
                    .ThenInclude(p => p.Materials.Where(m => m.IsActive))
            // Prerequisite
            .Include(s => s.LearningPathProgram)
                .ThenInclude(lpp => lpp.PrerequisitePathProgram)
                    .ThenInclude(pre => pre!.Program)
            // Enrollment context + sibling steps for lock logic
            .Include(s => s.EmployeeLearningPath)
                .ThenInclude(elp => elp.LearningPath)
            // The learner, so the page can name whose step this is when it is not the caller's.
            .Include(s => s.EmployeeLearningPath)
                .ThenInclude(elp => elp.Employee)
            .Include(s => s.EmployeeLearningPath)
                .ThenInclude(elp => elp.Steps)
                    .ThenInclude(sib => sib.LearningPathProgram)
            // Nomination + completion record
            .Include(s => s.Nomination)
                .ThenInclude(n => n!.CompletionRecord)
            .FirstOrDefaultAsync(s => s.Id == stepId && !s.IsDeleted);
    }
}

#endregion

// ============================================================================
// MENTORING REPOSITORIES
// ============================================================================

#region Mentoring Program Repository

public class MentoringProgramRepository : GenericRepository<MentoringProgram>, IMentoringProgramRepository
{
    public MentoringProgramRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Everything <c>MentoringProgramSummaryDto</c> renders: the coordinator's name, and the pairs the
    /// two count columns are derived from.
    ///
    /// Pairs matter as much as the coordinator here — an un-included collection is empty rather than
    /// null, so the mapper's <c>Pairs.Count</c> produced a confident <b>0</b> on every row instead of
    /// anything that looked broken.
    /// </summary>
    private IQueryable<MentoringProgram> WithSummaryNavigations()
        => _dbSet
            .Include(p => p.CoordinatedBy)
            .Include(p => p.Pairs);

    public override async Task<MentoringProgram?> GetByIdAsync(Guid id)
        => await WithSummaryNavigations().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    public override async Task<IEnumerable<MentoringProgram>> GetAllAsync()
        => await WithSummaryNavigations()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();

    public async Task<IEnumerable<MentoringProgram>> GetActiveAsync()
    {
        return await WithSummaryNavigations()
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringProgram>> GetByCoordinatorAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.CoordinatedById == employeeId && !p.IsDeleted)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();
    }

    public async Task<MentoringProgram?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.CoordinatedBy)
            .Include(p => p.Pairs).ThenInclude(pair => pair.Mentor)
            .Include(p => p.Pairs).ThenInclude(pair => pair.Mentee)
            .Include(p => p.Pairs).ThenInclude(pair => pair.Sessions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<MentoringProgram?> GetWithFullDetailsUntrackedAsync(Guid id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.CoordinatedBy)
            .Include(p => p.Pairs).ThenInclude(pair => pair.Mentor)
            .Include(p => p.Pairs).ThenInclude(pair => pair.Mentee)
            .Include(p => p.Pairs).ThenInclude(pair => pair.Sessions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

#region Mentoring Pair Repository

public class MentoringPairRepository : GenericRepository<MentoringPair>, IMentoringPairRepository
{
    public MentoringPairRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Everything <c>MentoringPairSummaryDto</c> renders: both names, the programme, and the sessions
    /// behind <c>TotalSessionsCount</c>.
    ///
    /// Each list below previously included a different subset, so the same pair rendered differently
    /// depending on which screen asked. The by-mentor list omitted <c>Mentor</c> and the by-mentee list
    /// omitted <c>Mentee</c> — meaning the blank column was always the person whose list you were
    /// looking at.
    /// </summary>
    private IQueryable<MentoringPair> WithSummaryNavigations()
        => _dbSet
            .Include(p => p.Program)
            .Include(p => p.Mentor)
            .Include(p => p.Mentee)
            .Include(p => p.Sessions);

    public override async Task<MentoringPair?> GetByIdAsync(Guid id)
        => await WithSummaryNavigations().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    public override async Task<IEnumerable<MentoringPair>> GetAllAsync()
        => await WithSummaryNavigations()
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

    public async Task<IEnumerable<MentoringPair>> GetByProgramIdAsync(Guid programId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.ProgramId == programId && !p.IsDeleted)
            .OrderBy(p => p.Mentor.LastName)
            .ThenBy(p => p.Mentee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringPair>> GetByMentorIdAsync(Guid mentorId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.MentorId == mentorId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringPair>> GetByMenteeIdAsync(Guid menteeId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.MenteeId == menteeId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringPair>> GetByStatusAsync(MentoringStatus status)
    {
        return await WithSummaryNavigations()
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderBy(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringPair>> GetActiveAsync()
    {
        return await WithSummaryNavigations()
            .Where(p => p.Status == MentoringStatus.Active && !p.IsDeleted)
            .OrderBy(p => p.Mentor.LastName)
            .ThenBy(p => p.Mentee.LastName)
            .ToListAsync();
    }

    public async Task<MentoringPair?> GetActivePairAsync(Guid mentorId, Guid menteeId)
    {
        return await _dbSet
            .Include(p => p.Program)
            .FirstOrDefaultAsync(p => p.MentorId == mentorId
                                    && p.MenteeId == menteeId
                                    && p.Status == MentoringStatus.Active
                                    && !p.IsDeleted);
    }

    public async Task<MentoringPair?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Program)
            // MentorPosition/MenteePosition come off Position.Title, so stopping at the employee left
            // both position fields blank on the detail screen.
            .Include(p => p.Mentor).ThenInclude(m => m.Position)
            .Include(p => p.Mentee).ThenInclude(m => m.Position)
            .Include(p => p.Sessions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<MentoringPair?> GetWithFullDetailsUntrackedAsync(Guid id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.Program)
            .Include(p => p.Mentor).ThenInclude(m => m.Position)
            .Include(p => p.Mentee).ThenInclude(m => m.Position)
            .Include(p => p.Sessions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

#region Mentoring Session Repository

public class MentoringSessionRepository : GenericRepository<MentoringSession>, IMentoringSessionRepository
{
    public MentoringSessionRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// <c>MentoringSessionDto</c> names the mentor, the mentee and the programme, and every one of
    /// those resolves through <c>Pair</c> — which no session read loaded at all. The session log
    /// therefore rendered three blank columns on every row, on the one screen where they are the point.
    /// </summary>
    private IQueryable<MentoringSession> WithSummaryNavigations()
        => _dbSet
            .Include(s => s.Pair).ThenInclude(p => p.Mentor)
            .Include(s => s.Pair).ThenInclude(p => p.Mentee)
            .Include(s => s.Pair).ThenInclude(p => p.Program);

    public override async Task<MentoringSession?> GetByIdAsync(Guid id)
        => await WithSummaryNavigations().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

    public override async Task<IEnumerable<MentoringSession>> GetAllAsync()
        => await WithSummaryNavigations()
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();

    public async Task<IEnumerable<MentoringSession>> GetByPairIdAsync(Guid pairId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.PairId == pairId && !s.IsDeleted)
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringSession>> GetByDateRangeAsync(Guid pairId, DateTime from, DateTime to)
    {
        return await WithSummaryNavigations()
            .Where(s => s.PairId == pairId
                     && !s.IsDeleted
                     && s.SessionDate >= from
                     && s.SessionDate <= to)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringSession>> GetMissedSessionsAsync(Guid pairId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.PairId == pairId
                     && !s.IsDeleted
                     && (!s.AttendedByMentor || !s.AttendedByMentee))
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<MentoringSession?> GetWithPairUntrackedAsync(Guid id)
        => await _dbSet
            .AsNoTracking()
            .Include(s => s.Pair).ThenInclude(p => p.Mentor)
            .Include(s => s.Pair).ThenInclude(p => p.Mentee)
            .Include(s => s.Pair).ThenInclude(p => p.Program)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
}

#endregion
