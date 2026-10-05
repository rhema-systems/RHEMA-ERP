using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Company Event Repository

/// <remarks>
/// ⚠ Lane 2a removed the nine custom reads: none filtered by tenant (F-30), the range read wanted
/// containment rather than overlap, the upcoming read used the server's local date, and three had no
/// caller. The event service builds its lists on its own tenant-scoped query.
/// </remarks>
public interface ICompanyEventRepository : IGenericRepository<CompanyEvent>
{
    /// <summary>
    /// The next event number, from the shared sequence and probed against the table before it is
    /// used (round 4, D7 — company-schedule defect C-6).
    /// </summary>
    /// <remarks>
    /// &#9888; This was <c>COUNT(*) + 1</c> over live rows. Soft-deleted rows are excluded from that
    /// count, so deleting an event FREED its number and the next create took it — and the index was
    /// not unique, so nothing complained and two events quietly shared a reference. Every other HR
    /// generator was hardened against exactly this; these three were missed.
    /// </remarks>
    Task<string> GetNextEventNumberAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

#endregion Company Event Repository

#region Event Participant Repository

/// <remarks>
/// ⚠ Lane 2d removed the custom reads: none filtered by tenant (F-30), and the event service was their
/// only caller. It reads guests, attendance and tasks on its own tenant-scoped queries.
/// </remarks>
public interface IEventParticipantRepository : IGenericRepository<EventParticipant>
{
}

#endregion Event Participant Repository

#region Event Attendance Repository

/// <remarks>
/// ⚠ Lane 2d removed the custom reads: none filtered by tenant (F-30), and the event service was their
/// only caller. It reads guests, attendance and tasks on its own tenant-scoped queries.
/// </remarks>
public interface IEventAttendanceRepository : IGenericRepository<EventAttendance>
{
}

#endregion Event Attendance Repository

#region Event Attachment Repository

public interface IEventAttachmentRepository : IGenericRepository<EventAttachment>
{
    Task<IEnumerable<EventAttachment>> GetByEventIdAsync(Guid eventId);
    Task<IEnumerable<EventAttachment>> GetByTypeAsync(Guid eventId, EventAttachmentType type);
}

#endregion Event Attachment Repository

#region Event Task Repository

/// <remarks>
/// ⚠ Lane 2d removed the custom reads: none filtered by tenant (F-30), and the event service was their
/// only caller. It reads guests, attendance and tasks on its own tenant-scoped queries.
/// </remarks>
public interface IEventTaskRepository : IGenericRepository<EventTask>
{
}

#endregion Event Task Repository

#region Meeting Room Repository

public interface IMeetingRoomRepository : IGenericRepository<MeetingRoom>
{
    Task<MeetingRoom?> GetByRoomCodeAsync(string roomCode);
    Task<IEnumerable<MeetingRoom>> GetByLocationAsync(Guid locationId);
    Task<IEnumerable<MeetingRoom>> GetAvailableRoomsAsync(DateTime startDateTime, DateTime endDateTime, int? minCapacity = null);
    Task<IEnumerable<MeetingRoom>> GetActiveRoomsAsync();
    Task<IEnumerable<MeetingRoom>> GetBookableRoomsAsync();

    /// <inheritdoc cref="ICompanyEventRepository.GetNextEventNumberAsync"/>
    Task<string> GetNextRoomCodeAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

#endregion Meeting Room Repository

#region Room Booking Repository

public interface IRoomBookingRepository : IGenericRepository<RoomBooking>
{
    Task<RoomBooking?> GetByBookingNumberAsync(string bookingNumber);
    Task<IEnumerable<RoomBooking>> GetByRoomIdAsync(Guid roomId);
    Task<IEnumerable<RoomBooking>> GetByBookerAsync(Guid bookedById);
    Task<IEnumerable<RoomBooking>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<RoomBooking>> GetByStatusAsync(BookingStatus status);
    Task<bool> HasConflictingBookingAsync(Guid roomId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null);
    Task<IEnumerable<RoomBooking>> GetPendingApprovalsAsync();

    /// <inheritdoc cref="ICompanyEventRepository.GetNextEventNumberAsync"/>
    Task<string> GetNextBookingNumberAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

#endregion Room Booking Repository

#region Company Milestone Repository

public interface ICompanyMilestoneRepository : IGenericRepository<CompanyMilestone>
{
    Task<IEnumerable<CompanyMilestone>> GetByCategoryAsync(MilestoneCategory category);
    Task<IEnumerable<CompanyMilestone>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<CompanyMilestone>> GetUpcomingMilestonesAsync(int daysAhead = 90);
    Task<IEnumerable<CompanyMilestone>> GetRecurringMilestonesAsync();
}

#endregion Company Milestone Repository

#region Business Closure Repository

public interface IBusinessClosureRepository : IGenericRepository<BusinessClosure>
{
    // Lane 1: no custom reads — closures are read through BusinessClosureRules and IHrClosureCalendar.
}

#endregion Business Closure Repository

#region Fiscal Year Repository

public interface IFiscalYearRepository : IGenericRepository<FiscalYear>
{
    Task<FiscalYear?> GetByYearAsync(int year);
    Task<FiscalYear?> GetCurrentFiscalYearAsync();
    Task<IEnumerable<FiscalYear>> GetByStatusAsync(FiscalYearStatus status);
    Task<FiscalYear?> GetFiscalYearForDateAsync(DateTime date);
}

#endregion Fiscal Year Repository

#region Fiscal Period Repository

public interface IFiscalPeriodRepository : IGenericRepository<FiscalPeriod>
{
    Task<IEnumerable<FiscalPeriod>> GetByFiscalYearIdAsync(Guid fiscalYearId);
    Task<FiscalPeriod?> GetByPeriodNumberAsync(Guid fiscalYearId, int periodNumber);
    Task<FiscalPeriod?> GetCurrentPeriodAsync();
    Task<FiscalPeriod?> GetPeriodForDateAsync(DateTime date);
    Task<IEnumerable<FiscalPeriod>> GetOpenPeriodsAsync();
}

#endregion Fiscal Period Repository
