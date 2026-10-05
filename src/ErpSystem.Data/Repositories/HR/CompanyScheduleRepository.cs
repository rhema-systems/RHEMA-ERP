using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

#region Company Event Repository

public class CompanyEventRepository : GenericRepository<CompanyEvent>, ICompanyEventRepository
{
    private readonly INumberSequenceService _sequences;

    public CompanyEventRepository(ApplicationDbContext context, INumberSequenceService sequences)
        : base(context)
    {
        _sequences = sequences;
    }

    /// <summary>
    /// Round 4, D7 (C-6). Year-scoped, and the probe sees SOFT-DELETED rows too — a deleted event
    /// still occupies its number, which is the whole point.
    /// </summary>
    public Task<string> GetNextEventNumberAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        return _sequences.NextUnusedAsync(
            "EVT",
            tenantId,
            year,
            format: value => $"EVT-{year}-{value:D5}",
            isTaken: number => _dbSet.IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.EventNumber == number, cancellationToken),
            highestIssued: async () => NumberSequenceExtensions.HighestIssued(
                await _dbSet.IgnoreQueryFilters()
                    .Where(e => e.TenantId == tenantId && e.EventNumber.StartsWith($"EVT-{year}-"))
                    .Select(e => e.EventNumber)
                    .ToListAsync(cancellationToken)),
            cancellationToken);
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
    private readonly INumberSequenceService _sequences;

    public MeetingRoomRepository(ApplicationDbContext context, INumberSequenceService sequences)
        : base(context)
    {
        _sequences = sequences;
    }

    /// <summary>
    /// Round 4, D7 (C-6). NOT year-scoped — a room code carries no year, so the sequence must not
    /// restart in January and hand RM-0001 to a second room.
    /// </summary>
    public Task<string> GetNextRoomCodeAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _sequences.NextUnusedAsync(
            "RM",
            tenantId,
            year: null,
            format: value => $"RM-{value:D4}",
            isTaken: code => _dbSet.IgnoreQueryFilters()
                .AnyAsync(r => r.TenantId == tenantId && r.RoomCode == code, cancellationToken),
            highestIssued: async () => NumberSequenceExtensions.HighestIssued(
                await _dbSet.IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId)
                    .Select(r => r.RoomCode)
                    .ToListAsync(cancellationToken)),
            cancellationToken);

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
    private readonly INumberSequenceService _sequences;

    public RoomBookingRepository(ApplicationDbContext context, INumberSequenceService sequences)
        : base(context)
    {
        _sequences = sequences;
    }

    /// <inheritdoc cref="ICompanyEventRepository.GetNextEventNumberAsync"/>
    public Task<string> GetNextBookingNumberAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        return _sequences.NextUnusedAsync(
            "BK",
            tenantId,
            year,
            format: value => $"BK-{year}-{value:D5}",
            isTaken: number => _dbSet.IgnoreQueryFilters()
                .AnyAsync(b => b.TenantId == tenantId && b.BookingNumber == number, cancellationToken),
            highestIssued: async () => NumberSequenceExtensions.HighestIssued(
                await _dbSet.IgnoreQueryFilters()
                    .Where(b => b.TenantId == tenantId && b.BookingNumber.StartsWith($"BK-{year}-"))
                    .Select(b => b.BookingNumber)
                    .ToListAsync(cancellationToken)),
            cancellationToken);
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

    // ⚠ Company-schedule final closure, lane 1: the six custom reads that stood here are gone. They
    // loaded every tenant's rows, matched ranges by containment rather than overlap, read "today" in
    // server time, knew nothing of a closure's type or of a yearly repeat, and ANDed site and
    // department (F-2, F-4, F-5, F-30). Closures are read through BusinessClosureRules and
    // IHrClosureCalendar, which have one answer to "which days" and "who is covered".
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
