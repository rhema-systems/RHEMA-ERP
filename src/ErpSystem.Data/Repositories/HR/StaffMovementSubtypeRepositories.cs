using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF PROMOTION REPOSITORY
// ============================================================================

#region Staff Promotion Repository

public class StaffPromotionRepository : GenericRepository<StaffPromotion>, IStaffPromotionRepository
{
    public StaffPromotionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffPromotion?> GetByMovementIdAsync(Guid movementId)
    {
        return await _dbSet
            .Include(p => p.Movement).ThenInclude(m => m.Employee)
            .Include(p => p.Movement).ThenInclude(m => m.CurrentPosition)
            .Include(p => p.Movement).ThenInclude(m => m.NewPosition)
            .Include(p => p.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Include(p => p.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Include(p => p.Movement).ThenInclude(m => m.CurrentSalaryGrade)
            .Include(p => p.Movement).ThenInclude(m => m.NewSalaryGrade)
            .FirstOrDefaultAsync(p => p.MovementId == movementId && !p.IsDeleted);
    }

    public async Task<IEnumerable<StaffPromotion>> GetByTypeAsync(StaffPromotionType type)
    {
        return await _dbSet
            .Include(p => p.Movement).ThenInclude(m => m.Employee)
            .Include(p => p.Movement).ThenInclude(m => m.NewPosition)
            .Include(p => p.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Where(p => p.Type == type && !p.IsDeleted)
            .OrderByDescending(p => p.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffPromotion>> GetActiveActingPromotionsAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(p => p.Movement).ThenInclude(m => m.Employee)
            .Include(p => p.Movement).ThenInclude(m => m.NewPosition)
            .Include(p => p.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Where(p => p.IsActingPromotion
                     && !p.IsDeleted
                     && p.ActingPeriodEndDate != null
                     && p.ActingPeriodEndDate >= today)
            .OrderBy(p => p.ActingPeriodEndDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF TRANSFER REPOSITORY
// ============================================================================

#region Staff Transfer Repository

public class StaffTransferRepository : GenericRepository<StaffTransfer>, IStaffTransferRepository
{
    public StaffTransferRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTransfer?> GetByMovementIdAsync(Guid movementId)
    {
        return await _dbSet
            .Include(t => t.Movement).ThenInclude(m => m.Employee)
            .Include(t => t.Movement).ThenInclude(m => m.CurrentPosition)
            .Include(t => t.Movement).ThenInclude(m => m.NewPosition)
            .Include(t => t.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Include(t => t.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Include(t => t.Movement).ThenInclude(m => m.CurrentLocation)
            .Include(t => t.Movement).ThenInclude(m => m.NewLocation)
            .FirstOrDefaultAsync(t => t.MovementId == movementId && !t.IsDeleted);
    }

    public async Task<IEnumerable<StaffTransfer>> GetByTypeAsync(StaffTransferType type)
    {
        return await _dbSet
            .Include(t => t.Movement).ThenInclude(m => m.Employee)
            .Include(t => t.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Include(t => t.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Where(t => t.Type == type && !t.IsDeleted)
            .OrderByDescending(t => t.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTransfer>> GetByReasonCategoryAsync(
        StaffTransferReasonCategory reasonCategory)
    {
        return await _dbSet
            .Include(t => t.Movement).ThenInclude(m => m.Employee)
            .Include(t => t.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Include(t => t.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Where(t => t.ReasonCategory == reasonCategory && !t.IsDeleted)
            .OrderByDescending(t => t.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTransfer>> GetInterCompanyTransfersAsync()
    {
        return await _dbSet
            .Include(t => t.Movement).ThenInclude(m => m.Employee)
            .Include(t => t.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Include(t => t.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Where(t => t.IsInterCompany && !t.IsDeleted)
            .OrderByDescending(t => t.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTransfer>> GetRelocationTransfersAsync()
    {
        return await _dbSet
            .Include(t => t.Movement).ThenInclude(m => m.Employee)
            .Include(t => t.Movement).ThenInclude(m => m.CurrentLocation)
            .Include(t => t.Movement).ThenInclude(m => m.NewLocation)
            .Where(t => t.RequiresRelocation && !t.IsDeleted)
            .OrderByDescending(t => t.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTransfer>> GetInTransitionAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(t => t.Movement).ThenInclude(m => m.Employee)
            .Include(t => t.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Include(t => t.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Where(t => !t.IsDeleted
                     && t.TransitionStartDate != null
                     && t.TransitionEndDate != null
                     && t.TransitionStartDate <= today
                     && t.TransitionEndDate >= today)
            .OrderBy(t => t.TransitionEndDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DEMOTION REPOSITORY
// ============================================================================

#region Staff Demotion Repository

public class StaffDemotionRepository : GenericRepository<StaffDemotion>, IStaffDemotionRepository
{
    public StaffDemotionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffDemotion?> GetByMovementIdAsync(Guid movementId)
    {
        return await _dbSet
            .Include(d => d.Movement).ThenInclude(m => m.Employee)
            .Include(d => d.Movement).ThenInclude(m => m.CurrentPosition)
            .Include(d => d.Movement).ThenInclude(m => m.NewPosition)
            .Include(d => d.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Include(d => d.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Include(d => d.Movement).ThenInclude(m => m.CurrentSalaryGrade)
            .Include(d => d.Movement).ThenInclude(m => m.NewSalaryGrade)
            .FirstOrDefaultAsync(d => d.MovementId == movementId && !d.IsDeleted);
    }

    public async Task<IEnumerable<StaffDemotion>> GetDisciplinaryDemotionsAsync()
    {
        return await _dbSet
            .Include(d => d.Movement).ThenInclude(m => m.Employee)
            .Include(d => d.Movement).ThenInclude(m => m.NewPosition)
            .Include(d => d.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Where(d => d.IsDisciplinaryAction && !d.IsDeleted)
            .OrderByDescending(d => d.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDemotion>> GetPerformanceRelatedDemotionsAsync()
    {
        return await _dbSet
            .Include(d => d.Movement).ThenInclude(m => m.Employee)
            .Include(d => d.Movement).ThenInclude(m => m.NewPosition)
            .Where(d => d.IsPerformanceRelated && !d.IsDeleted)
            .OrderByDescending(d => d.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDemotion>> GetWithPendingAppealsAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(d => d.Movement).ThenInclude(m => m.Employee)
            .Include(d => d.Movement).ThenInclude(m => m.CurrentPosition)
            .Include(d => d.Movement).ThenInclude(m => m.NewPosition)
            .Where(d => !d.IsDeleted
                     && d.RightToAppeal
                     && d.EmployeeResponse == null
                     && (d.AppealDeadline == null || d.AppealDeadline >= today))
            .OrderBy(d => d.AppealDeadline)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDemotion>> GetByDisciplinaryActionAsync(Guid disciplinaryActionId)
    {
        return await _dbSet
            .Include(d => d.Movement).ThenInclude(m => m.Employee)
            .Include(d => d.Movement).ThenInclude(m => m.NewPosition)
            .Where(d => d.DisciplinaryActionId == disciplinaryActionId && !d.IsDeleted)
            .OrderByDescending(d => d.Movement.EffectiveDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF SECONDMENT REPOSITORY
// ============================================================================

#region Staff Secondment Repository

public class StaffSecondmentRepository : GenericRepository<StaffSecondment>, IStaffSecondmentRepository
{
    public StaffSecondmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffSecondment?> GetByMovementIdAsync(Guid movementId)
    {
        return await _dbSet
            .Include(s => s.Movement).ThenInclude(m => m.Employee)
            .Include(s => s.Movement).ThenInclude(m => m.CurrentPosition)
            .Include(s => s.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Include(s => s.Movement).ThenInclude(m => m.NewPosition)
            .Include(s => s.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .FirstOrDefaultAsync(s => s.MovementId == movementId && !s.IsDeleted);
    }

    public async Task<IEnumerable<StaffSecondment>> GetByTypeAsync(StaffSecondmentType type)
    {
        return await _dbSet
            .Include(s => s.Movement).ThenInclude(m => m.Employee)
            .Include(s => s.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Where(s => s.Type == type && !s.IsDeleted)
            .OrderByDescending(s => s.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffSecondment>> GetExternalSecondmentsAsync()
    {
        return await _dbSet
            .Include(s => s.Movement).ThenInclude(m => m.Employee)
            .Include(s => s.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Where(s => s.IsExternal && !s.IsDeleted)
            .OrderByDescending(s => s.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffSecondment>> GetByHostOrganizationAsync(string hostOrganization)
    {
        return await _dbSet
            .Include(s => s.Movement).ThenInclude(m => m.Employee)
            .Include(s => s.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Where(s => !s.IsDeleted
                     && s.HostOrganization != null
                     && s.HostOrganization.Contains(hostOrganization))
            .OrderByDescending(s => s.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffSecondment>> GetEndingSoonAsync(int daysAhead = 30)
    {
        var today  = DateTime.UtcNow;
        var cutoff = today.AddDays(daysAhead);
        return await _dbSet
            .Include(s => s.Movement).ThenInclude(m => m.Employee)
            .Include(s => s.Movement).ThenInclude(m => m.CurrentOrganizationUnit)
            .Include(s => s.Movement).ThenInclude(m => m.NewOrganizationUnit)
            .Where(s => !s.IsDeleted
                     && !s.Movement.ReturnProcessed
                     && s.EndDate >= today
                     && s.EndDate <= cutoff)
            .OrderBy(s => s.EndDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF ACTING APPOINTMENT REPOSITORY
// ============================================================================

#region Staff Acting Appointment Repository

public class StaffActingAppointmentRepository
    : GenericRepository<StaffActingAppointment>, IStaffActingAppointmentRepository
{
    public StaffActingAppointmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffActingAppointment?> GetByAppointmentNumberAsync(string appointmentNumber)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.ActingPosition)
            .Include(a => a.ActingForEmployee)
            .FirstOrDefaultAsync(a => a.AppointmentNumber == appointmentNumber && !a.IsDeleted);
    }

    public async Task<StaffActingAppointment?> GetWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.ActingPosition)
            .Include(a => a.ActingForEmployee)
            .Include(a => a.Movement)
                .ThenInclude(m => m!.CurrentOrganizationUnit)
            .Include(a => a.Movement)
                .ThenInclude(m => m!.NewOrganizationUnit)
            .Include(a => a.Movement)
                .ThenInclude(m => m!.CurrentSalaryGrade)
            .Include(a => a.Movement)
                .ThenInclude(m => m!.NewSalaryGrade)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

    public async Task<IEnumerable<StaffActingAppointment>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.ActingPosition)
            .Include(a => a.ActingForEmployee)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffActingAppointment>> GetByStatusAsync(StaffActingStatus status)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.ActingPosition)
            .Include(a => a.ActingForEmployee)
            .Where(a => a.Status == status && !a.IsDeleted)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffActingAppointment>> GetActiveAppointmentsAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.ActingPosition)
            .Include(a => a.ActingForEmployee)
            .Where(a => a.Status == StaffActingStatus.Active && !a.IsDeleted)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffActingAppointment>> GetByActingPositionAsync(Guid positionId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.ActingPosition)
            .Include(a => a.ActingForEmployee)
            .Where(a => a.ActingPositionId == positionId && !a.IsDeleted)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffActingAppointment>> GetExpiringAppointmentsAsync(int daysAhead = 14)
    {
        var today  = DateTime.UtcNow;
        var cutoff = today.AddDays(daysAhead);
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.ActingPosition)
            .Where(a => a.Status == StaffActingStatus.Active
                     && !a.IsDeleted
                     && a.EndDate != null
                     && a.EndDate >= today
                     && a.EndDate <= cutoff)
            .OrderBy(a => a.EndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffActingAppointment>> GetConvertedToPermanentAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.ActingPosition)
            .Where(a => a.ConvertedToPermanent && !a.IsDeleted)
            .OrderByDescending(a => a.ConversionDate)
            .ToListAsync();
    }

    public async Task<StaffActingAppointment?> GetByOriginatingMovementAsync(Guid movementId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.ActingPosition)
            .Include(a => a.ActingForEmployee)
            .FirstOrDefaultAsync(a => a.MovementId == movementId && !a.IsDeleted);
    }
}

#endregion

// ============================================================================
// EMPLOYEE CAREER PATH REPOSITORY
// ============================================================================

#region Employee Career Path Repository

public class EmployeeCareerPathRepository
    : GenericRepository<EmployeeCareerPath>, IEmployeeCareerPathRepository
{
    public EmployeeCareerPathRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeCareerPath>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(cp => cp.Position)
            .Include(cp => cp.OrganizationUnit)
            .Include(cp => cp.OrganizationLevel)
            .Include(cp => cp.SalaryGrade)
            .Include(cp => cp.SalaryLevel)
            .Include(cp => cp.SalaryNotch)
            .Where(cp => cp.EmployeeId == employeeId && !cp.IsDeleted)
            .OrderBy(cp => cp.StartDate)
            .ToListAsync();
    }

    public async Task<EmployeeCareerPath?> GetCurrentPositionAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(cp => cp.Position)
            .Include(cp => cp.OrganizationUnit)
            .Include(cp => cp.OrganizationLevel)
            .Include(cp => cp.Location)
            .Include(cp => cp.LocationLevel)
            .Include(cp => cp.SalaryGrade)
            .Include(cp => cp.SalaryLevel)
            .Include(cp => cp.SalaryNotch)
            .FirstOrDefaultAsync(cp => cp.EmployeeId == employeeId
                                    && cp.IsCurrent
                                    && !cp.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeCareerPath>> GetByOrganizationUnitIdAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .Include(cp => cp.Employee)
            .Include(cp => cp.Position)
            .Include(cp => cp.OrganizationUnit)
            .Where(cp => cp.OrganizationUnitId == organizationUnitId && !cp.IsDeleted)
            .OrderByDescending(cp => cp.StartDate)
            .ToListAsync();
    }

    public async Task<EmployeeCareerPath?> GetByMovementIdAsync(Guid movementId)
    {
        return await _dbSet
            .Include(cp => cp.Employee)
            .Include(cp => cp.Position)
            .Include(cp => cp.OrganizationUnit)
            .Include(cp => cp.SalaryGrade)
            .FirstOrDefaultAsync(cp => cp.MovementId == movementId && !cp.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeCareerPath>> GetBySalaryGradeIdAsync(Guid salaryGradeId)
    {
        return await _dbSet
            .Include(cp => cp.Employee)
            .Include(cp => cp.Position)
            .Include(cp => cp.OrganizationUnit)
            .Include(cp => cp.SalaryGrade)
            .Include(cp => cp.SalaryLevel)
            .Include(cp => cp.SalaryNotch)
            .Where(cp => cp.SalaryGradeId == salaryGradeId && !cp.IsDeleted)
            .OrderByDescending(cp => cp.StartDate)
            .ToListAsync();
    }

    public async Task<EmployeeCareerPath?> GetWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(cp => cp.Employee)
            .Include(cp => cp.Position)
            .Include(cp => cp.OrganizationUnit)
            .Include(cp => cp.OrganizationLevel)
            .Include(cp => cp.Location)
            .Include(cp => cp.LocationLevel)
            .Include(cp => cp.SalaryGrade)
            .Include(cp => cp.SalaryLevel)
            .Include(cp => cp.SalaryNotch)
            .Include(cp => cp.Movement)
            .FirstOrDefaultAsync(cp => cp.Id == id && !cp.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeCareerPath>> GetCurrentOccupantsForUnitAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .Include(cp => cp.Employee)
            .Include(cp => cp.Position)
            .Include(cp => cp.SalaryGrade)
            .Where(cp => cp.OrganizationUnitId == organizationUnitId
                      && cp.IsCurrent
                      && !cp.IsDeleted)
            .OrderBy(cp => cp.Employee.LastName)
            .ThenBy(cp => cp.Employee.FirstName)
            .ToListAsync();
    }
}

#endregion
