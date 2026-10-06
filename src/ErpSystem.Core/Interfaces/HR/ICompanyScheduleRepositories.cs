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

/// <remarks>
/// ⚠ Lane 3a (F-30, F-15, C-28): the room reads here — by code, by site, available, active, bookable — loaded every
/// tenant's rows and filtered in memory, and the availability read's booked-room subquery had no tenant at all. Two had
/// no caller. The room service builds its lists on its own tenant-scoped query, as the event service does since lane 2.
/// </remarks>
public interface IMeetingRoomRepository : IGenericRepository<MeetingRoom>
{
    /// <inheritdoc cref="ICompanyEventRepository.GetNextEventNumberAsync"/>
    Task<string> GetNextRoomCodeAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

#endregion Meeting Room Repository

#region Room Booking Repository

/// <remarks>
/// ⚠ Lane 3a (F-30): the booking reads here — by number, room, booker, range, status, pending, and a clash check — had
/// no tenant, and the range read wanted containment, not overlap. The booking service reads on its own tenant-scoped
/// query.
/// </remarks>
public interface IRoomBookingRepository : IGenericRepository<RoomBooking>
{
    /// <inheritdoc cref="ICompanyEventRepository.GetNextEventNumberAsync"/>
    Task<string> GetNextBookingNumberAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

#endregion Room Booking Repository

#region Company Milestone Repository

/// <remarks>
/// ⚠ Lane 4a: its four custom reads are gone — they loaded every tenant's rows, compared the milestone's own date only (a
/// yearly one was never upcoming after its first year) and read "today" in server time. The service reads milestones
/// through its own tenant-scoped query and <c>CompanyMilestoneRules</c>.
/// </remarks>
public interface ICompanyMilestoneRepository : IGenericRepository<CompanyMilestone>
{
}

#endregion Company Milestone Repository

#region Business Closure Repository

public interface IBusinessClosureRepository : IGenericRepository<BusinessClosure>
{
    // Lane 1: no custom reads — closures are read through BusinessClosureRules and IHrClosureCalendar.
}

#endregion Business Closure Repository

// ⚠ Company-schedule final closure lane 4b (D-6): HR's fiscal-year and fiscal-period repositories are retired with HR's own
// calendar — they read every tenant's rows (F-3). HR reads Finance's calendar (IHrFiscalCalendar).
