using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

#region Company Event Repository

public class CompanyEventRepository : GenericRepository<CompanyEvent>, ICompanyEventRepository
{
    public CompanyEventRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<CompanyEvent?> GetByEventNumberAsync(string eventNumber)
    {
        return await _dbSet
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Include(e => e.SiteLocation)
            .Include(e => e.ApprovedBy)
            .FirstOrDefaultAsync(e => e.EventNumber == eventNumber);
    }

    public async Task<IEnumerable<CompanyEvent>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Where(e => e.StartDate >= startDate && e.EndDate <= endDate)
            .OrderBy(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyEvent>> GetByOrganizerAsync(Guid organizerId)
    {
        return await _dbSet
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Where(e => e.OrganizerId == organizerId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyEvent>> GetByDepartmentAsync(Guid departmentId)
    {
        return await _dbSet
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Where(e => e.DepartmentId == departmentId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyEvent>> GetByStatusAsync(EventStatus status)
    {
        return await _dbSet
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyEvent>> GetByCategoryAsync(EventCategory category)
    {
        return await _dbSet
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Where(e => e.Category == category)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyEvent>> GetUpcomingEventsAsync(int daysAhead = 30)
    {
        var today = DateTime.Today;
        var futureDate = today.AddDays(daysAhead);

        return await _dbSet
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Where(e => e.StartDate >= today && e.StartDate <= futureDate && !e.IsCancelled)
            .OrderBy(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyEvent>> GetActiveEventsAsync(DateTime? asOfDate = null)
    {
        var targetDate = asOfDate ?? DateTime.Today;

        return await _dbSet
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Where(e => e.StartDate <= targetDate && e.EndDate >= targetDate && !e.IsCancelled)
            .OrderBy(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<bool> HasConflictingEventAsync(Guid organizerId, DateTime startDate, DateTime endDate, Guid? excludeEventId = null)
    {
        var query = _dbSet
            .Where(e => e.OrganizerId == organizerId &&
                       !e.IsCancelled &&
                       e.StartDate <= endDate &&
                       e.EndDate >= startDate);

        if (excludeEventId.HasValue)
        {
            query = query.Where(e => e.Id != excludeEventId.Value);
        }

        return await query.AnyAsync();
    }
}

#endregion Company Event Repository

#region Event Participant Repository

public class EventParticipantRepository : GenericRepository<EventParticipant>, IEventParticipantRepository
{
    public EventParticipantRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EventParticipant>> GetByEventIdAsync(Guid eventId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Where(p => p.EventId == eventId)
            .OrderBy(p => p.Role)
            .ThenBy(p => p.Employee != null ? p.Employee.FirstName : p.ExternalParticipantName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EventParticipant>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(p => p.Event)
            .Where(p => p.EmployeeId == employeeId)
            .OrderByDescending(p => p.Event.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EventParticipant>> GetByInvitationStatusAsync(Guid eventId, InvitationStatus status)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Where(p => p.EventId == eventId && p.InvitationStatus == status)
            .ToListAsync();
    }

    public async Task<bool> IsParticipantAsync(Guid eventId, Guid employeeId)
    {
        return await _dbSet.AnyAsync(p => p.EventId == eventId && p.EmployeeId == employeeId);
    }
}

#endregion Event Participant Repository

#region Event Attendance Repository

public class EventAttendanceRepository : GenericRepository<EventAttendance>, IEventAttendanceRepository
{
    public EventAttendanceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EventAttendance>> GetByEventIdAsync(Guid eventId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.MarkedBy)
            .Where(a => a.EventId == eventId)
            .OrderBy(a => a.Employee.FirstName)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EventAttendance>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Event)
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.Event.StartDate)
            .ToListAsync();
    }

    public async Task<EventAttendance?> GetByEventAndEmployeeAsync(Guid eventId, Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.MarkedBy)
            .FirstOrDefaultAsync(a => a.EventId == eventId && a.EmployeeId == employeeId);
    }

    public async Task<int> GetAttendanceCountAsync(Guid eventId)
    {
        return await _dbSet.CountAsync(a => a.EventId == eventId && a.Attended);
    }
}

#endregion Event Attendance Repository

#region Event Attachment Repository

public class EventAttachmentRepository : GenericRepository<EventAttachment>, IEventAttachmentRepository
{
    public EventAttachmentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EventAttachment>> GetByEventIdAsync(Guid eventId)
    {
        return await _dbSet
            .Where(a => a.EventId == eventId)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EventAttachment>> GetByTypeAsync(Guid eventId, EventAttachmentType type)
    {
        return await _dbSet
            .Where(a => a.EventId == eventId && a.Type == type)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync();
    }
}

#endregion Event Attachment Repository

#region Event Task Repository

public class EventTaskRepository : GenericRepository<EventTask>, IEventTaskRepository
{
    public EventTaskRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EventTask>> GetByEventIdAsync(Guid eventId)
    {
        return await _dbSet
            .Include(t => t.AssignedTo)
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<EventTask>> GetByAssigneeAsync(Guid assignedToId)
    {
        return await _dbSet
            .Include(t => t.Event)
            .Where(t => t.AssignedToId == assignedToId)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EventTask>> GetByStatusAsync(Guid eventId, EventTaskStatus status)
    {
        return await _dbSet
            .Include(t => t.AssignedTo)
            .Where(t => t.EventId == eventId && t.Status == status)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EventTask>> GetPendingTasksAsync(Guid? assignedToId = null)
    {
        var query = _dbSet
            .Include(t => t.Event)
            .Include(t => t.AssignedTo)
            .Where(t => t.Status == EventTaskStatus.NotStarted || t.Status == EventTaskStatus.InProgress);

        if (assignedToId.HasValue)
        {
            query = query.Where(t => t.AssignedToId == assignedToId.Value);
        }

        return await query.OrderBy(t => t.DueDate).ToListAsync();
    }

    public async Task<IEnumerable<EventTask>> GetOverdueTasksAsync()
    {
        var today = DateTime.Today;

        return await _dbSet
            .Include(t => t.Event)
            .Include(t => t.AssignedTo)
            .Where(t => t.DueDate < today && t.Status != EventTaskStatus.Completed && t.Status != EventTaskStatus.Cancelled)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }
}

#endregion Event Task Repository

#region Meeting Room Repository

public class MeetingRoomRepository : GenericRepository<MeetingRoom>, IMeetingRoomRepository
{
    public MeetingRoomRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<MeetingRoom?> GetByRoomCodeAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.SiteLocation)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    public async Task<IEnumerable<MeetingRoom>> GetByLocationAsync(Guid locationId)
    {
        return await _dbSet
            .Include(r => r.SiteLocation)
            .Where(r => r.LocationId == locationId)
            .OrderBy(r => r.RoomName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MeetingRoom>> GetAvailableRoomsAsync(DateTime startDateTime, DateTime endDateTime, int? minCapacity = null)
    {
        var bookedRoomIds = await _context.Set<RoomBooking>()
            .Where(b => b.Status != BookingStatus.Cancelled &&
                       !b.IsCancelled &&
                       b.StartDateTime < endDateTime &&
                       b.EndDateTime > startDateTime)
            .Select(b => b.RoomId)
            .ToListAsync();

        var query = _dbSet
            .Include(r => r.SiteLocation)
            .Where(r => r.IsActive && r.IsBookable && !bookedRoomIds.Contains(r.Id));

        if (minCapacity.HasValue)
        {
            query = query.Where(r => r.Capacity >= minCapacity.Value);
        }

        return await query.OrderBy(r => r.RoomName).ToListAsync();
    }

    public async Task<IEnumerable<MeetingRoom>> GetActiveRoomsAsync()
    {
        return await _dbSet
            .Include(r => r.SiteLocation)
            .Where(r => r.IsActive)
            .OrderBy(r => r.RoomName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MeetingRoom>> GetBookableRoomsAsync()
    {
        return await _dbSet
            .Include(r => r.SiteLocation)
            .Where(r => r.IsActive && r.IsBookable)
            .OrderBy(r => r.RoomName)
            .ToListAsync();
    }
}

#endregion Meeting Room Repository

#region Room Booking Repository

public class RoomBookingRepository : GenericRepository<RoomBooking>, IRoomBookingRepository
{
    public RoomBookingRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<RoomBooking?> GetByBookingNumberAsync(string bookingNumber)
    {
        return await _dbSet
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Include(b => b.ApprovedBy)
            .Include(b => b.Event)
            .FirstOrDefaultAsync(b => b.BookingNumber == bookingNumber);
    }

    public async Task<IEnumerable<RoomBooking>> GetByRoomIdAsync(Guid roomId)
    {
        return await _dbSet
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Where(b => b.RoomId == roomId)
            .OrderByDescending(b => b.StartDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<RoomBooking>> GetByBookerAsync(Guid bookedById)
    {
        return await _dbSet
            .Include(b => b.Room)
            .Where(b => b.BookedById == bookedById)
            .OrderByDescending(b => b.StartDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<RoomBooking>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Where(b => b.StartDateTime >= startDate && b.EndDateTime <= endDate)
            .OrderBy(b => b.StartDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<RoomBooking>> GetByStatusAsync(BookingStatus status)
    {
        return await _dbSet
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Where(b => b.Status == status)
            .OrderByDescending(b => b.StartDateTime)
            .ToListAsync();
    }

    public async Task<bool> HasConflictingBookingAsync(Guid roomId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null)
    {
        var query = _dbSet
            .Where(b => b.RoomId == roomId &&
                       !b.IsCancelled &&
                       b.Status != BookingStatus.Cancelled &&
                       b.StartDateTime < endDateTime &&
                       b.EndDateTime > startDateTime);

        if (excludeBookingId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBookingId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<RoomBooking>> GetPendingApprovalsAsync()
    {
        return await _dbSet
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Where(b => b.Status == BookingStatus.Tentative && !b.IsCancelled)
            .OrderBy(b => b.StartDateTime)
            .ToListAsync();
    }
}

#endregion Room Booking Repository

#region Company Milestone Repository

public class CompanyMilestoneRepository : GenericRepository<CompanyMilestone>, ICompanyMilestoneRepository
{
    public CompanyMilestoneRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<CompanyMilestone>> GetByCategoryAsync(MilestoneCategory category)
    {
        return await _dbSet
            .Where(m => m.Category == category)
            .OrderBy(m => m.MilestoneDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyMilestone>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(m => m.MilestoneDate >= startDate && m.MilestoneDate <= endDate)
            .OrderBy(m => m.MilestoneDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyMilestone>> GetUpcomingMilestonesAsync(int daysAhead = 90)
    {
        var today = DateTime.Today;
        var futureDate = today.AddDays(daysAhead);

        return await _dbSet
            .Where(m => m.MilestoneDate >= today && m.MilestoneDate <= futureDate)
            .OrderBy(m => m.MilestoneDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyMilestone>> GetRecurringMilestonesAsync()
    {
        return await _dbSet
            .Where(m => m.IsRecurringAnnually)
            .OrderBy(m => m.MilestoneDate)
            .ToListAsync();
    }
}

#endregion Company Milestone Repository

#region Business Closure Repository

public class BusinessClosureRepository : GenericRepository<BusinessClosure>, IBusinessClosureRepository
{
    public BusinessClosureRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<BusinessClosure>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Include(c => c.SiteLocation)
            .Include(c => c.Department)
            .Include(c => c.AnnouncedBy)
            .Where(c => c.StartDate >= startDate && c.EndDate <= endDate)
            .OrderBy(c => c.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessClosure>> GetByTypeAsync(ClosureType type)
    {
        return await _dbSet
            .Include(c => c.SiteLocation)
            .Include(c => c.Department)
            .Where(c => c.Type == type)
            .OrderBy(c => c.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessClosure>> GetByLocationAsync(Guid locationId)
    {
        return await _dbSet
            .Include(c => c.SiteLocation)
            .Include(c => c.Department)
            .Where(c => c.AffectsAllStations || c.LocationId == locationId)
            .OrderBy(c => c.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessClosure>> GetByDepartmentAsync(Guid departmentId)
    {
        return await _dbSet
            .Include(c => c.SiteLocation)
            .Include(c => c.Department)
            .Where(c => c.AffectsAllStations || c.DepartmentId == departmentId)
            .OrderBy(c => c.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessClosure>> GetUpcomingClosuresAsync(int daysAhead = 30)
    {
        var today = DateTime.Today;
        var futureDate = today.AddDays(daysAhead);

        return await _dbSet
            .Include(c => c.SiteLocation)
            .Include(c => c.Department)
            .Where(c => c.StartDate >= today && c.StartDate <= futureDate)
            .OrderBy(c => c.StartDate)
            .ToListAsync();
    }

    public async Task<bool> IsClosureDateAsync(DateTime date, Guid? locationId = null, Guid? departmentId = null)
    {
        var query = _dbSet.Where(c => c.StartDate <= date && c.EndDate >= date);

        if (locationId.HasValue)
        {
            query = query.Where(c => c.AffectsAllStations || c.LocationId == locationId.Value);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(c => c.AffectsAllStations || c.DepartmentId == departmentId.Value);
        }

        return await query.AnyAsync();
    }
}

#endregion Business Closure Repository

#region Fiscal Year Repository

public class FiscalYearRepository : GenericRepository<FiscalYear>, IFiscalYearRepository
{
    public FiscalYearRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<FiscalYear?> GetByYearAsync(int year)
    {
        return await _dbSet
            .Include(fy => fy.Periods)
            .FirstOrDefaultAsync(fy => fy.Year == year);
    }

    public async Task<FiscalYear?> GetCurrentFiscalYearAsync()
    {
        return await _dbSet
            .Include(fy => fy.Periods)
            .FirstOrDefaultAsync(fy => fy.IsCurrent);
    }

    public async Task<IEnumerable<FiscalYear>> GetByStatusAsync(FiscalYearStatus status)
    {
        return await _dbSet
            .Include(fy => fy.Periods)
            .Where(fy => fy.Status == status)
            .OrderByDescending(fy => fy.Year)
            .ToListAsync();
    }

    public async Task<FiscalYear?> GetFiscalYearForDateAsync(DateTime date)
    {
        return await _dbSet
            .Include(fy => fy.Periods)
            .FirstOrDefaultAsync(fy => fy.StartDate <= date && fy.EndDate >= date);
    }
}

#endregion Fiscal Year Repository

#region Fiscal Period Repository

public class FiscalPeriodRepository : GenericRepository<FiscalPeriod>, IFiscalPeriodRepository
{
    public FiscalPeriodRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<FiscalPeriod>> GetByFiscalYearIdAsync(Guid fiscalYearId)
    {
        return await _dbSet
            .Include(p => p.FiscalYear)
            .Where(p => p.FiscalYearId == fiscalYearId)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync();
    }

    public async Task<FiscalPeriod?> GetByPeriodNumberAsync(Guid fiscalYearId, int periodNumber)
    {
        return await _dbSet
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.FiscalYearId == fiscalYearId && p.PeriodNumber == periodNumber);
    }

    public async Task<FiscalPeriod?> GetCurrentPeriodAsync()
    {
        var today = DateTime.Today;

        return await _dbSet
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.StartDate <= today && p.EndDate >= today && !p.IsClosed);
    }

    public async Task<FiscalPeriod?> GetPeriodForDateAsync(DateTime date)
    {
        return await _dbSet
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date);
    }

    public async Task<IEnumerable<FiscalPeriod>> GetOpenPeriodsAsync()
    {
        return await _dbSet
            .Include(p => p.FiscalYear)
            .Where(p => !p.IsClosed)
            .OrderBy(p => p.FiscalYear.Year)
            .ThenBy(p => p.PeriodNumber)
            .ToListAsync();
    }
}

#endregion Fiscal Period Repository
