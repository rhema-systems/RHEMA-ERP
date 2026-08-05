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

    public async Task<TrainingVendor?> GetByVendorCodeAsync(string vendorCode)
    {
        return await _dbSet
            .Include(v => v.Trainers)
            .FirstOrDefaultAsync(v => v.VendorCode == vendorCode && !v.IsDeleted);
    }

    public async Task<IEnumerable<TrainingVendor>> GetByVendorTypeAsync(TrainingVendorType type)
    {
        return await _dbSet
            .Where(v => v.VendorType == type && !v.IsDeleted)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetActiveVendorsAsync()
    {
        return await _dbSet
            .Where(v => v.IsActive && !v.IsBlacklisted && !v.IsDeleted)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetPreferredVendorsAsync()
    {
        return await _dbSet
            .Where(v => v.IsPreferred && v.IsActive && !v.IsDeleted)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetBlacklistedVendorsAsync()
    {
        return await _dbSet
            .Where(v => v.IsBlacklisted && !v.IsDeleted)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetWithExpiredAccreditationAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(v => !v.IsDeleted && v.AccreditationExpiryDate != null && v.AccreditationExpiryDate < now)
            .OrderBy(v => v.AccreditationExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingVendor>> GetWithExpiringAccreditationAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await _dbSet
            .Where(v => !v.IsDeleted
                     && v.AccreditationExpiryDate != null
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

    public async Task<TrainerProfile?> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.EmployeeId == employeeId && !t.IsDeleted);
    }

    public async Task<IEnumerable<TrainerProfile>> GetByVendorIdAsync(Guid vendorId)
    {
        return await _dbSet
            .Include(t => t.Vendor)
            .Where(t => t.VendorId == vendorId && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainerProfile>> GetActiveTrainersAsync()
    {
        return await _dbSet
            .Where(t => t.IsActive && !t.IsDeleted)
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
        return await _dbSet
            .Include(t => t.Availability)
            .Where(t => t.IsActive && !t.IsDeleted
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

    public async Task<TrainingSchedule?> GetByScheduleNumberAsync(string scheduleNumber)
    {
        return await _dbSet
            .Include(s => s.Program)
            .FirstOrDefaultAsync(s => s.ScheduleNumber == scheduleNumber && !s.IsDeleted);
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(s => s.TrainerProfile)
            .Include(s => s.Vendor)
            .Where(s => s.ProgramId == programId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByStatusAsync(ScheduleStatus status)
    {
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => s.Status == status && !s.IsDeleted)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetUpcomingSchedulesAsync(int daysAhead = 90)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await _dbSet
            .Include(s => s.Program)
            .Include(s => s.TrainerProfile)
            .Where(s => !s.IsDeleted
                     && s.StartDate >= now
                     && s.StartDate <= cutoff
                     && (s.Status == ScheduleStatus.Planned || s.Status == ScheduleStatus.RegistrationOpen))
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetCurrentlyRunningAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(s => s.Program)
            .Include(s => s.TrainerProfile)
            .Where(s => !s.IsDeleted
                     && s.StartDate <= now
                     && s.EndDate >= now
                     && s.Status == ScheduleStatus.InProgress)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByTrainerProfileIdAsync(Guid trainerProfileId)
    {
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => s.TrainerProfileId == trainerProfileId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByVendorIdAsync(Guid vendorId)
    {
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => s.VendorId == vendorId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetByBudgetIdAsync(Guid budgetId)
    {
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => s.TrainingBudgetId == budgetId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => s.Status == ScheduleStatus.Planned && !s.IsDeleted)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetWithRegistrationOpenAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(s => s.Program)
            .Where(s => !s.IsDeleted
                     && s.RegistrationOpenDate <= now
                     && s.RegistrationCloseDate >= now)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSchedule>> GetWithAvailableSlotsAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(s => s.Program)
            .Include(s => s.Nominations)
            .Where(s => !s.IsDeleted
                     && s.RegistrationOpenDate <= now
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

    public async Task<IEnumerable<TrainingSession>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await _dbSet
            .Where(s => s.ScheduleId == scheduleId && !s.IsDeleted)
            .OrderBy(s => s.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingSession>> GetByDateAsync(DateTime date)
    {
        return await _dbSet
            .Include(s => s.Schedule).ThenInclude(sc => sc.Program)
            .Where(s => !s.IsDeleted && s.Date.Date == date.Date)
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

    public async Task<TrainingNomination?> GetByNominationNumberAsync(string nominationNumber)
    {
        return await _dbSet
            .Include(n => n.Employee)
            .Include(n => n.Schedule).ThenInclude(s => s.Program)
            .FirstOrDefaultAsync(n => n.NominationNumber == nominationNumber && !n.IsDeleted);
    }

    public async Task<IEnumerable<TrainingNomination>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(n => n.Employee)
            .Where(n => n.ScheduleId == scheduleId && !n.IsDeleted)
            .OrderBy(n => n.Employee.LastName)
            .ThenBy(n => n.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(n => n.EmployeeId == employeeId && !n.IsDeleted)
            .OrderByDescending(n => n.Schedule.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetByStatusAsync(NominationStatus status)
    {
        return await _dbSet
            .Include(n => n.Employee)
            .Include(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(n => n.Status == status && !n.IsDeleted)
            .OrderBy(n => n.Schedule.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetPendingSupervisorApprovalAsync()
    {
        return await _dbSet
            .Include(n => n.Employee)
            .Include(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(n => n.Status == NominationStatus.SupervisorReview && !n.IsDeleted)
            .OrderBy(n => n.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetPendingHrApprovalAsync()
    {
        return await _dbSet
            .Include(n => n.Employee)
            .Include(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(n => n.Status == NominationStatus.HrReview && !n.IsDeleted)
            .OrderBy(n => n.NominationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetConfirmedForScheduleAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(n => n.Employee)
            .Where(n => n.ScheduleId == scheduleId
                     && n.Status == NominationStatus.Confirmed
                     && !n.IsDeleted)
            .OrderBy(n => n.Employee.LastName)
            .ThenBy(n => n.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNomination>> GetByNeedsAssessmentIdAsync(Guid assessmentId)
    {
        return await _dbSet
            .Include(n => n.Employee)
            .Include(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(n => n.TrainingNeedsAssessmentId == assessmentId && !n.IsDeleted)
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

    public async Task<TrainingCompletion?> GetByNominationIdAsync(Guid nominationId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.NominationId == nominationId && !c.IsDeleted);
    }

    public async Task<IEnumerable<TrainingCompletion>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(c => c.Nomination).ThenInclude(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(c => c.EmployeeId == employeeId && !c.IsDeleted)
            .OrderByDescending(c => c.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCompletion>> GetByStatusAsync(TrainingCompletionStatus status)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Nomination).ThenInclude(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(c => c.Status == status && !c.IsDeleted)
            .OrderByDescending(c => c.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCompletion>> GetPassedCompletionsForProgramAsync(Guid programId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Nomination).ThenInclude(n => n.Schedule)
            .Where(c => !c.IsDeleted
                     && c.IsPassed
                     && c.Nomination.Schedule.ProgramId == programId)
            .OrderByDescending(c => c.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCompletion>> GetPendingManagerVerificationAsync()
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Nomination).ThenInclude(n => n.Schedule).ThenInclude(s => s.Program)
            .Where(c => !c.IsDeleted && !c.IsVerifiedByManager)
            .OrderBy(c => c.CompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCompletion>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Nomination)
            .Where(c => !c.IsDeleted && c.Nomination.ScheduleId == scheduleId)
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

    public async Task<IEnumerable<TrainingAttendance>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.ScheduleId == scheduleId && !a.IsDeleted)
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingAttendance>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Schedule).ThenInclude(s => s.Program)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.AttendanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingAttendance>> GetByScheduleAndDateAsync(Guid scheduleId, DateTime date)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.ScheduleId == scheduleId
                     && a.AttendanceDate.Date == date.Date
                     && !a.IsDeleted)
            .OrderBy(a => a.Employee.LastName)
            .ThenBy(a => a.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingAttendance>> GetAbsenteesForScheduleAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.ScheduleId == scheduleId && !a.IsPresent && !a.IsDeleted)
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

    public async Task<IEnumerable<TrainingFeedback>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(f => f.Employee)
            .Where(f => f.ScheduleId == scheduleId && !f.IsDeleted)
            .OrderBy(f => f.Employee.LastName)
            .ThenBy(f => f.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingFeedback>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(f => f.Schedule).ThenInclude(s => s.Program)
            .Where(f => f.EmployeeId == employeeId && !f.IsDeleted)
            .OrderByDescending(f => f.FeedbackDate)
            .ToListAsync();
    }
}

#endregion

#region Training Follow-Up Assessment Repository

public class TrainingFollowUpAssessmentRepository : GenericRepository<TrainingFollowUpAssessment>, ITrainingFollowUpAssessmentRepository
{
    public TrainingFollowUpAssessmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TrainingFollowUpAssessment>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.ScheduleId == scheduleId && !a.IsDeleted)
            .OrderBy(a => a.AssessmentType)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingFollowUpAssessment>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Schedule).ThenInclude(s => s.Program)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.AssessmentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingFollowUpAssessment>> GetByAssessmentTypeAsync(Guid scheduleId, TrainingAssessmentType assessmentType)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.ScheduleId == scheduleId
                     && a.AssessmentType == assessmentType
                     && !a.IsDeleted)
            .OrderBy(a => a.Employee.LastName)
            .ThenBy(a => a.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingFollowUpAssessment>> GetPendingManagerObservationAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Schedule).ThenInclude(s => s.Program)
            .Include(a => a.Manager)
            .Where(a => !a.IsDeleted && a.ManagerSubmittedDate == null)
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

    public async Task<TrainingCertificate?> GetByCertificateNumberAsync(string certificateNumber)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Program)
            .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber && !c.IsDeleted);
    }

    public async Task<IEnumerable<TrainingCertificate>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(c => c.Program)
            .Where(c => c.EmployeeId == employeeId && !c.IsDeleted)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => c.ProgramId == programId && !c.IsDeleted)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetByStatusAsync(CertificateStatus status)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Program)
            .Where(c => c.Status == status && !c.IsDeleted)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetActiveAsync()
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Program)
            .Where(c => c.Status == CertificateStatus.Active && !c.IsDeleted)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetExpiredAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Program)
            .Where(c => !c.IsDeleted && c.ExpiryDate != null && c.ExpiryDate < now)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetExpiringAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Program)
            .Where(c => !c.IsDeleted
                     && c.ExpiryDate != null
                     && c.ExpiryDate >= now
                     && c.ExpiryDate <= cutoff)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingCertificate>> GetRenewalsForCertificateAsync(Guid previousCertificateId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Program)
            .Where(c => c.PreviousCertificateId == previousCertificateId && c.IsRenewal && !c.IsDeleted)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }
}

#endregion

#region Employee Certificate Repository

public class EmployeeCertificateRepository : GenericRepository<EmployeeCertificate>, IEmployeeCertificateRepository
{
    public EmployeeCertificateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeCertificate>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(c => c.VerifiedBy)
            .Where(c => c.EmployeeId == employeeId && !c.IsDeleted)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCertificate>> GetUnverifiedAsync()
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => !c.IsVerified && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCertificate>> GetExpiredAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => !c.IsDeleted && c.ExpiryDate != null && c.ExpiryDate < now)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCertificate>> GetExpiringAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => !c.IsDeleted
                     && c.ExpiryDate != null
                     && c.ExpiryDate >= now
                     && c.ExpiryDate <= cutoff)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeCertificate>> GetByStatusAsync(CertificateStatus status)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => c.Status == status && !c.IsDeleted)
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

    public async Task<ComplianceTrainingRequirement?> GetByRequirementCodeAsync(string requirementCode)
    {
        return await _dbSet
            .Include(r => r.Program)
            .FirstOrDefaultAsync(r => r.RequirementCode == requirementCode && !r.IsDeleted);
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetActiveAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(r => r.Program)
            .Where(r => r.IsActive
                     && !r.IsDeleted
                     && r.EffectiveDate <= now
                     && (r.ExpiryDate == null || r.ExpiryDate > now))
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Where(r => r.ProgramId == programId && !r.IsDeleted)
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetByOrganizationLevelAsync(Guid orgLevelId)
    {
        return await _dbSet
            .Include(r => r.Program)
            .Where(r => r.OrganizationLevelId == orgLevelId && !r.IsDeleted)
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetByOrganizationUnitAsync(Guid orgUnitId)
    {
        return await _dbSet
            .Include(r => r.Program)
            .Where(r => r.OrganizationUnitId == orgUnitId && !r.IsDeleted)
            .OrderBy(r => r.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirement>> GetByPositionAsync(Guid positionId)
    {
        return await _dbSet
            .Include(r => r.Program)
            .Where(r => r.PositionId == positionId && !r.IsDeleted)
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

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.Requirement).ThenInclude(req => req.Program)
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderBy(r => r.Requirement.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetByRequirementIdAsync(Guid requirementId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.RequirementId == requirementId && !r.IsDeleted)
            .OrderBy(r => r.Employee.LastName)
            .ThenBy(r => r.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetByStatusAsync(ComplianceStatus status)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Requirement)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderBy(r => r.Employee.LastName)
            .ThenBy(r => r.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetNonCompliantAsync()
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Requirement)
            .Where(r => !r.IsDeleted
                     && r.Status != ComplianceStatus.Compliant
                     && !r.IsExempt)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetOverdueAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Requirement)
            .Where(r => !r.IsDeleted
                     && !r.IsExempt
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
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Requirement)
            .Where(r => !r.IsDeleted
                     && !r.IsExempt
                     && r.NextDueDate != null
                     && r.NextDueDate >= now
                     && r.NextDueDate <= cutoff)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeComplianceRecord>> GetExemptAsync()
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Requirement)
            .Where(r => r.IsExempt && !r.IsDeleted)
            .OrderBy(r => r.Employee.LastName)
            .ThenBy(r => r.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<EmployeeComplianceRecord?> GetEmployeeRecordAsync(Guid employeeId, Guid requirementId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Requirement).ThenInclude(req => req.Program)
            .Include(r => r.ExemptedBy)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId
                                    && r.RequirementId == requirementId
                                    && !r.IsDeleted);
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

    public async Task<TrainingPlan?> GetByPlanNumberAsync(string planNumber)
    {
        return await _dbSet
            .Include(p => p.OrganizationUnit)
            .FirstOrDefaultAsync(p => p.PlanNumber == planNumber && !p.IsDeleted);
    }

    public async Task<IEnumerable<TrainingPlan>> GetByYearAsync(int year)
    {
        return await _dbSet
            .Include(p => p.OrganizationUnit)
            .Where(p => p.Year == year && !p.IsDeleted)
            .OrderBy(p => p.OrganizationUnit!.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlan>> GetByStatusAsync(TrainingPlanStatus status)
    {
        return await _dbSet
            .Include(p => p.OrganizationUnit)
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderByDescending(p => p.Year)
            .ThenBy(p => p.OrganizationUnit!.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlan>> GetByOrganizationUnitAsync(Guid orgUnitId)
    {
        return await _dbSet
            .Include(p => p.OrganizationUnit)
            .Where(p => p.OrganizationUnitId == orgUnitId && !p.IsDeleted)
            .OrderByDescending(p => p.Year)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingPlan>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Include(p => p.OrganizationUnit)
            .Where(p => p.Status == TrainingPlanStatus.PendingApproval && !p.IsDeleted)
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
            .Include(i => i.Program)
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

    public async Task<IEnumerable<TrainingNeedsAssessment>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.Year)
            .ToListAsync();
    }

    public async Task<TrainingNeedsAssessment?> GetByEmployeeAndYearAsync(Guid employeeId, int year)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.IdentifiedBy)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId
                                    && a.Year == year
                                    && !a.IsDeleted);
    }

    public async Task<IEnumerable<TrainingNeedsAssessment>> GetByYearAsync(int year)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.Year == year && !a.IsDeleted)
            .OrderBy(a => a.Employee.LastName)
            .ThenBy(a => a.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNeedsAssessment>> GetUnfulfilledAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => !a.TrainingProvided && !a.IsDeleted)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.Year)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingNeedsAssessment>> GetByPriorityAsync(TrainingPriority priority)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.Priority == priority && !a.IsDeleted)
            .OrderByDescending(a => a.Year)
            .ThenBy(a => a.Employee.LastName)
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

    public async Task<IEnumerable<TrainingWaitlist>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(w => w.Employee)
            .Where(w => w.ScheduleId == scheduleId && !w.IsDeleted)
            .OrderBy(w => w.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(w => w.Schedule).ThenInclude(s => s.Program)
            .Where(w => w.EmployeeId == employeeId && !w.IsDeleted)
            .OrderBy(w => w.Schedule.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetActiveWaitlistAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(w => w.Employee)
            .Where(w => w.ScheduleId == scheduleId
                     && w.Status == TrainingWaitlistStatus.Active
                     && !w.IsDeleted)
            .OrderBy(w => w.Position)
            .ToListAsync();
    }

    public async Task<TrainingWaitlist?> GetNextInQueueAsync(Guid scheduleId)
    {
        return await _dbSet
            .Include(w => w.Employee)
            .Where(w => w.ScheduleId == scheduleId
                     && w.Status == TrainingWaitlistStatus.Active
                     && !w.IsDeleted)
            .OrderBy(w => w.Position)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetByStatusAsync(Guid scheduleId, TrainingWaitlistStatus status)
    {
        return await _dbSet
            .Include(w => w.Employee)
            .Where(w => w.ScheduleId == scheduleId && w.Status == status && !w.IsDeleted)
            .OrderBy(w => w.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetOfferedAsync(Guid scheduleId)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(w => w.Employee)
            .Where(w => w.ScheduleId == scheduleId
                     && w.Status == TrainingWaitlistStatus.Offered
                     && w.OfferExpiryDate != null
                     && w.OfferExpiryDate >= now
                     && !w.IsDeleted)
            .OrderBy(w => w.OfferExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingWaitlist>> GetExpiredOffersAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(w => w.Employee)
            .Include(w => w.Schedule).ThenInclude(s => s.Program)
            .Where(w => !w.IsDeleted
                     && w.Status == TrainingWaitlistStatus.Offered
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

    public async Task<TrainingRequest?> GetByRequestNumberAsync(string requestNumber)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber && !r.IsDeleted);
    }

    public async Task<IEnumerable<TrainingRequest>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.LinkedProgram)
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingRequest>> GetByStatusAsync(TrainingRequestStatus status)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.LinkedProgram)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderBy(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingRequest>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.Status == TrainingRequestStatus.Submitted && !r.IsDeleted)
            .OrderBy(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingRequest>> GetLinkedToProgramAsync(Guid programId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.LinkedProgram)
            .Where(r => r.LinkedProgramId == programId && !r.IsDeleted)
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

    public async Task<IEnumerable<LearningPath>> GetByStatusAsync(LearningPathStatus status)
    {
        return await _dbSet
            .Where(lp => lp.Status == status && !lp.IsDeleted)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetActiveAsync()
    {
        return await _dbSet
            .Where(lp => lp.Status == LearningPathStatus.Active && !lp.IsDeleted)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetByOrganizationLevelAsync(Guid orgLevelId)
    {
        return await _dbSet
            .Where(lp => lp.OrganizationLevelId == orgLevelId && !lp.IsDeleted)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetByOrganizationUnitAsync(Guid orgUnitId)
    {
        return await _dbSet
            .Where(lp => lp.OrganizationUnitId == orgUnitId && !lp.IsDeleted)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetByPositionAsync(Guid positionId)
    {
        return await _dbSet
            .Where(lp => lp.PositionId == positionId && !lp.IsDeleted)
            .OrderBy(lp => lp.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPath>> GetWithCertificateAsync()
    {
        return await _dbSet
            .Where(lp => lp.ProvidesCertificate && !lp.IsDeleted)
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
            .FirstOrDefaultAsync(lp => lp.Id == id && !lp.IsDeleted);
    }
}

#endregion

#region Learning Path Program Repository

public class LearningPathProgramRepository : GenericRepository<LearningPathProgram>, ILearningPathProgramRepository
{
    public LearningPathProgramRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<LearningPathProgram>> GetByLearningPathIdAsync(Guid learningPathId)
    {
        return await _dbSet
            .Include(p => p.Program)
            .Where(p => p.LearningPathId == learningPathId && !p.IsDeleted)
            .OrderBy(p => p.SequenceOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPathProgram>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(p => p.LearningPath)
            .Where(p => p.ProgramId == programId && !p.IsDeleted)
            .OrderBy(p => p.LearningPath.Name)
            .ToListAsync();
    }
}

#endregion

#region Learning Path Skill Repository

public class LearningPathSkillRepository : GenericRepository<LearningPathSkill>, ILearningPathSkillRepository
{
    public LearningPathSkillRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<LearningPathSkill>> GetByLearningPathIdAsync(Guid learningPathId)
    {
        return await _dbSet
            .Include(s => s.Skill)
            .Where(s => s.LearningPathId == learningPathId && !s.IsDeleted)
            .OrderBy(s => s.Skill.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<LearningPathSkill>> GetBySkillIdAsync(Guid skillId)
    {
        return await _dbSet
            .Include(s => s.LearningPath)
            .Where(s => s.SkillId == skillId && !s.IsDeleted)
            .OrderBy(s => s.LearningPath.Name)
            .ToListAsync();
    }
}

#endregion

#region Employee Learning Path Repository

public class EmployeeLearningPathRepository : GenericRepository<EmployeeLearningPath>, IEmployeeLearningPathRepository
{
    public EmployeeLearningPathRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeLearningPath>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(e => e.LearningPath)
            .Where(e => e.EmployeeId == employeeId && !e.IsDeleted)
            .OrderBy(e => e.LearningPath.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetByLearningPathIdAsync(Guid learningPathId)
    {
        return await _dbSet
            .Include(e => e.Employee)
            .Where(e => e.LearningPathId == learningPathId && !e.IsDeleted)
            .OrderBy(e => e.Employee.LastName)
            .ThenBy(e => e.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetActiveEnrollmentsAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(e => e.LearningPath)
            .Where(e => e.EmployeeId == employeeId && !e.IsCompleted && !e.IsDeleted)
            .OrderBy(e => e.TargetCompletionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetCompletedAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(e => e.LearningPath)
            .Where(e => e.EmployeeId == employeeId && e.IsCompleted && !e.IsDeleted)
            .OrderByDescending(e => e.ActualCompletionDate)
            .ToListAsync();
    }

    public async Task<EmployeeLearningPath?> GetEnrollmentAsync(Guid employeeId, Guid learningPathId)
    {
        return await _dbSet
            .Include(e => e.LearningPath)
            .Include(e => e.AssignedBy)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId
                                    && e.LearningPathId == learningPathId
                                    && !e.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeLearningPath>> GetOverdueAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(e => e.Employee)
            .Include(e => e.LearningPath)
            .Where(e => !e.IsDeleted
                     && !e.IsCompleted
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
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
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

    public async Task<IEnumerable<EmployeeLearningPathStep>> GetByEmployeeLearningPathIdAsync(Guid enrollmentId)
    {
        return await _dbSet
            .Include(s => s.LearningPathProgram).ThenInclude(lpp => lpp.Program)
            .Where(s => s.EmployeeLearningPathId == enrollmentId && !s.IsDeleted)
            .OrderBy(s => s.LearningPathProgram.SequenceOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPathStep>> GetIncompleteStepsAsync(Guid enrollmentId)
    {
        return await _dbSet
            .Include(s => s.LearningPathProgram).ThenInclude(lpp => lpp.Program)
            .Where(s => s.EmployeeLearningPathId == enrollmentId && !s.IsCompleted && !s.IsDeleted)
            .OrderBy(s => s.LearningPathProgram.SequenceOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeLearningPathStep>> GetCompletedStepsAsync(Guid enrollmentId)
    {
        return await _dbSet
            .Include(s => s.LearningPathProgram).ThenInclude(lpp => lpp.Program)
            .Where(s => s.EmployeeLearningPathId == enrollmentId && s.IsCompleted && !s.IsDeleted)
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

    public async Task<IEnumerable<MentoringProgram>> GetActiveAsync()
    {
        return await _dbSet
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringProgram>> GetByCoordinatorAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(p => p.CoordinatedBy)
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
}

#endregion

#region Mentoring Pair Repository

public class MentoringPairRepository : GenericRepository<MentoringPair>, IMentoringPairRepository
{
    public MentoringPairRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MentoringPair>> GetByProgramIdAsync(Guid programId)
    {
        return await _dbSet
            .Include(p => p.Mentor)
            .Include(p => p.Mentee)
            .Where(p => p.ProgramId == programId && !p.IsDeleted)
            .OrderBy(p => p.Mentor.LastName)
            .ThenBy(p => p.Mentee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringPair>> GetByMentorIdAsync(Guid mentorId)
    {
        return await _dbSet
            .Include(p => p.Mentee)
            .Include(p => p.Program)
            .Where(p => p.MentorId == mentorId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringPair>> GetByMenteeIdAsync(Guid menteeId)
    {
        return await _dbSet
            .Include(p => p.Mentor)
            .Include(p => p.Program)
            .Where(p => p.MenteeId == menteeId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringPair>> GetByStatusAsync(MentoringStatus status)
    {
        return await _dbSet
            .Include(p => p.Mentor)
            .Include(p => p.Mentee)
            .Include(p => p.Program)
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderBy(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringPair>> GetActiveAsync()
    {
        return await _dbSet
            .Include(p => p.Mentor)
            .Include(p => p.Mentee)
            .Include(p => p.Program)
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
            .Include(p => p.Mentor)
            .Include(p => p.Mentee)
            .Include(p => p.Sessions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

#region Mentoring Session Repository

public class MentoringSessionRepository : GenericRepository<MentoringSession>, IMentoringSessionRepository
{
    public MentoringSessionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MentoringSession>> GetByPairIdAsync(Guid pairId)
    {
        return await _dbSet
            .Where(s => s.PairId == pairId && !s.IsDeleted)
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringSession>> GetByDateRangeAsync(Guid pairId, DateTime from, DateTime to)
    {
        return await _dbSet
            .Where(s => s.PairId == pairId
                     && !s.IsDeleted
                     && s.SessionDate >= from
                     && s.SessionDate <= to)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentoringSession>> GetMissedSessionsAsync(Guid pairId)
    {
        return await _dbSet
            .Where(s => s.PairId == pairId
                     && !s.IsDeleted
                     && (!s.AttendedByMentor || !s.AttendedByMentee))
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();
    }
}

#endregion
