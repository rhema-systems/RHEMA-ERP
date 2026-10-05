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
}

#endregion Event Participant Repository

#region Event Attendance Repository

public class EventAttendanceRepository : GenericRepository<EventAttendance>, IEventAttendanceRepository
{
    public EventAttendanceRepository(ApplicationDbContext context) : base(context)
    {
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

    // ⚠ Lane 3a (F-30, F-15, C-28): the by-code, by-site, available, active and bookable reads are gone — none had a
    // tenant. The room service reads on its own tenant-scoped query.
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

    // ⚠ Lane 3a (F-30): the by-number, room, booker, range, status, pending and clash reads are gone — none had a
    // tenant. The booking service reads on its own tenant-scoped query.
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
