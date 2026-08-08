using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Company Event Service

public class CompanyEventService : ICompanyEventService
{
    private readonly ICompanyEventRepository _eventRepository;
    private readonly IEventParticipantRepository _participantRepository;
    private readonly IEventAttendanceRepository _attendanceRepository;
    private readonly IEventAttachmentRepository _attachmentRepository;
    private readonly IEventTaskRepository _taskRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompanyEventService> _logger;

    public CompanyEventService(
        ICompanyEventRepository eventRepository,
        IEventParticipantRepository participantRepository,
        IEventAttendanceRepository attendanceRepository,
        IEventAttachmentRepository attachmentRepository,
        IEventTaskRepository taskRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CompanyEventService> logger)
    {
        _eventRepository = eventRepository;
        _participantRepository = participantRepository;
        _attendanceRepository = attendanceRepository;
        _attachmentRepository = attachmentRepository;
        _taskRepository = taskRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<CompanyEvent> GetOwnedEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Include(e => e.Station)
            .Include(e => e.ApprovedBy)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Company event with ID '{id}' not found.");
        return entity;
    }

    public async Task<CompanyEventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<CompanyEventDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Include(e => e.Station)
            .Include(e => e.ApprovedBy)
            .Include(e => e.Participants).ThenInclude(p => p.Employee)
            .Include(e => e.AttendanceRecords).ThenInclude(a => a.Employee)
            .Include(e => e.Attachments)
            .Include(e => e.Tasks).ThenInclude(t => t.AssignedTo)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Company event with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<CompanyEventDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<CompanyEventDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Where(e => e.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CompanyEventDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _eventRepository.GetByDateRangeAsync(startDate, endDate))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByOrganizerAsync(Guid organizerId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _eventRepository.GetByOrganizerAsync(organizerId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _eventRepository.GetByDepartmentAsync(departmentId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByStatusAsync(EventStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _eventRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByCategoryAsync(EventCategory category, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _eventRepository.GetByCategoryAsync(category))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetUpcomingEventsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _eventRepository.GetUpcomingEventsAsync(daysAhead))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<CompanyEventDto> CreateAsync(CreateCompanyEventDto createDto, Guid organizerId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.OrganizerId = organizerId;
        entity.EventNumber = await GenerateEventNumberAsync(tenantId, cancellationToken);
        entity.Status = EventStatus.Scheduled;

        await _eventRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event created: {EventNumber}", entity.EventNumber);

        return entity.ToDto();
    }

    public async Task<CompanyEventDto> UpdateAsync(UpdateCompanyEventDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Include(e => e.Station)
            .FirstOrDefaultAsync(e => e.Id == updateDto.Id && e.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Company event with ID '{updateDto.Id}' not found.");

        updateDto.UpdateEntity(entity);

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event updated: {EventNumber}", entity.EventNumber);

        return entity.ToDto();
    }

    public async Task<bool> ApproveEventAsync(Guid eventId, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(eventId, cancellationToken);

        entity.ApprovedById = approvedById;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.Status = EventStatus.Confirmed;

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event approved: {EventNumber}", entity.EventNumber);

        return true;
    }

    public async Task<bool> CancelEventAsync(CancelEventDto cancelDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(cancelDto.EventId, cancellationToken);

        entity.IsCancelled = true;
        entity.CancellationDate = DateTime.UtcNow;
        entity.CancellationReason = cancelDto.CancellationReason;
        entity.Status = EventStatus.Cancelled;

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event cancelled: {EventNumber}", entity.EventNumber);

        return true;
    }

    public async Task<bool> RescheduleEventAsync(RescheduleEventDto rescheduleDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(rescheduleDto.EventId, cancellationToken);

        entity.IsRescheduled = true;
        entity.RescheduledDate = DateTime.UtcNow;
        entity.RescheduleReason = rescheduleDto.RescheduleReason;
        entity.StartDate = rescheduleDto.NewStartDate;
        entity.StartTime = rescheduleDto.NewStartTime;
        entity.EndDate = rescheduleDto.NewEndDate;
        entity.EndTime = rescheduleDto.NewEndTime;

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event rescheduled: {EventNumber}", entity.EventNumber);

        return true;
    }

    public async Task<bool> CompleteEventAsync(CompleteEventDto completeDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(completeDto.EventId, cancellationToken);

        entity.Status = EventStatus.Completed;
        entity.ActualStartTime = completeDto.ActualStartTime;
        entity.ActualEndTime = completeDto.ActualEndTime;
        entity.ActualAttendance = completeDto.ActualAttendance;
        entity.OutcomeSummary = completeDto.OutcomeSummary;

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event completed: {EventNumber}", entity.EventNumber);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(id, cancellationToken);

        await _eventRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event deleted: {Id}", id);

        return true;
    }

    #region Participant Operations

    public async Task<EventParticipantDto> AddParticipantAsync(CreateEventParticipantDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(createDto.EventId, cancellationToken);

        if (createDto.EmployeeId.HasValue)
        {
            var isAlreadyParticipant = await _participantRepository.IsParticipantAsync(createDto.EventId, createDto.EmployeeId.Value);
            if (isAlreadyParticipant)
                throw new InvalidOperationException("Employee is already a participant in this event");
        }

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.InvitationStatus = InvitationStatus.Sent;
        entity.InvitationSentDate = DateTime.UtcNow;

        await _participantRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _participantRepository.GetQueryable()
            .Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == entity.Id && p.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Participant added to event: {EventId}", createDto.EventId);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<EventParticipantDto>> GetParticipantsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var entities = (await _participantRepository.GetByEventIdAsync(eventId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<bool> RespondToInvitationAsync(RespondToEventInvitationDto responseDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _participantRepository.GetByIdAsync(responseDto.ParticipantId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Participant not found");

        entity.InvitationStatus = responseDto.Response;
        entity.ResponseDate = DateTime.UtcNow;
        entity.ResponseComments = responseDto.ResponseComments;

        await _participantRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Invitation response recorded: {ParticipantId}", responseDto.ParticipantId);

        return true;
    }

    public async Task<bool> RemoveParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _participantRepository.GetByIdAsync(participantId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Participant not found");

        await _participantRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Participant removed: {ParticipantId}", participantId);

        return true;
    }

    #endregion

    #region Attendance Operations

    public async Task<EventAttendanceDto> MarkAttendanceAsync(MarkEventAttendanceDto markDto, Guid markedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(markDto.EventId, cancellationToken);

        var existingAttendance = await _attendanceRepository.GetByEventAndEmployeeAsync(markDto.EventId, markDto.EmployeeId);

        if (existingAttendance != null)
        {
            if (existingAttendance.TenantId != tenantId)
                throw new ArgumentException("Attendance record not found");

            existingAttendance.Attended = markDto.Attended;
            existingAttendance.CheckInTime = markDto.CheckInTime ?? DateTime.UtcNow;
            existingAttendance.AbsenceReason = markDto.AbsenceReason;
            existingAttendance.Notes = markDto.Notes;
            existingAttendance.MarkedById = markedById;

            await _attendanceRepository.UpdateAsync(existingAttendance);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return existingAttendance.ToDto();
        }

        var entity = markDto.ToEntity();
        entity.TenantId = tenantId;
        entity.MarkedById = markedById;
        entity.CheckInTime = markDto.CheckInTime ?? (markDto.Attended ? DateTime.UtcNow : null);

        await _attendanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _attendanceRepository.GetQueryable()
            .Include(a => a.Employee)
            .Include(a => a.MarkedBy)
            .FirstOrDefaultAsync(a => a.Id == entity.Id && a.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Attendance marked for event: {EventId}, Employee: {EmployeeId}", markDto.EventId, markDto.EmployeeId);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<EventAttendanceDto>> GetAttendanceAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var entities = (await _attendanceRepository.GetByEventIdAsync(eventId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<bool> CheckOutAsync(CheckOutEventDto checkOutDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attendanceRepository.GetByIdAsync(checkOutDto.AttendanceId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Attendance record not found");

        entity.CheckOutTime = DateTime.UtcNow;
        entity.Notes = checkOutDto.Notes ?? entity.Notes;

        await _attendanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Check-out recorded: {AttendanceId}", checkOutDto.AttendanceId);

        return true;
    }

    #endregion

    #region Attachment Operations

    public async Task<EventAttachmentDto> AddAttachmentAsync(CreateEventAttachmentDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(createDto.EventId, cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.UploadDate = DateTime.UtcNow;

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment added to event: {EventId}", createDto.EventId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<EventAttachmentDto>> GetAttachmentsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var entities = (await _attachmentRepository.GetByEventIdAsync(eventId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository.GetByIdAsync(attachmentId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Attachment not found");

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment deleted: {AttachmentId}", attachmentId);

        return true;
    }

    #endregion

    #region Task Operations

    public async Task<EventTaskDto> AddTaskAsync(CreateEventTaskDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(createDto.EventId, cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.Status = EventTaskStatus.NotStarted;

        await _taskRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _taskRepository.GetQueryable()
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == entity.Id && t.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Task added to event: {EventId}", createDto.EventId);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<EventTaskDto>> GetTasksAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var entities = (await _taskRepository.GetByEventIdAsync(eventId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<EventTaskDto> UpdateTaskAsync(UpdateEventTaskDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _taskRepository.GetQueryable()
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == updateDto.Id && t.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Task not found");

        updateDto.UpdateEntity(entity);

        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Event task updated: {TaskId}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> CompleteTaskAsync(CompleteEventTaskDto completeDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _taskRepository.GetByIdAsync(completeDto.TaskId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Task not found");

        entity.Status = EventTaskStatus.Completed;
        entity.CompletionDate = DateTime.UtcNow;
        entity.CompletionNotes = completeDto.CompletionNotes;

        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Event task completed: {TaskId}", completeDto.TaskId);

        return true;
    }

    public async Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _taskRepository.GetByIdAsync(taskId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Task not found");

        await _taskRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Event task deleted: {TaskId}", taskId);

        return true;
    }

    #endregion

    #region Helper Methods

    private async Task<string> GenerateEventNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _eventRepository.GetQueryable()
            .CountAsync(e => e.TenantId == tenantId && e.CreatedAt.Year == year, cancellationToken);

        return $"EVT-{year}-{(count + 1):D5}";
    }

    #endregion
}

#endregion Company Event Service

#region Meeting Room Service

public class MeetingRoomService : IMeetingRoomService
{
    private readonly IMeetingRoomRepository _roomRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MeetingRoomService> _logger;

    public MeetingRoomService(
        IMeetingRoomRepository roomRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<MeetingRoomService> logger)
    {
        _roomRepository = roomRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<MeetingRoom> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _roomRepository.GetQueryable()
            .Include(r => r.Station)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Meeting room with ID '{id}' not found.");
        return entity;
    }

    public async Task<MeetingRoomDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<MeetingRoomDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _roomRepository.GetQueryable()
            .Include(r => r.Station)
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<MeetingRoomDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _roomRepository.GetQueryable()
            .Include(r => r.Station)
            .Where(r => r.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(r => r.RoomName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MeetingRoomDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<MeetingRoomSummaryDto>> GetByStationAsync(Guid stationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _roomRepository.GetByStationAsync(stationId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MeetingRoomSummaryDto>> GetAvailableRoomsAsync(DateTime startDateTime, DateTime endDateTime, int? minCapacity = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _roomRepository.GetAvailableRoomsAsync(startDateTime, endDateTime, minCapacity))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MeetingRoomSummaryDto>> GetActiveRoomsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _roomRepository.GetActiveRoomsAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<MeetingRoomDto> CreateAsync(CreateMeetingRoomDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        if (string.IsNullOrEmpty(entity.RoomCode))
        {
            entity.RoomCode = await GenerateRoomCodeAsync(tenantId, cancellationToken);
        }
        else
        {
            var codeExists = await _roomRepository.GetQueryable()
                .AnyAsync(r => r.TenantId == tenantId && r.RoomCode == entity.RoomCode, cancellationToken);
            if (codeExists)
                throw new InvalidOperationException($"Room code '{entity.RoomCode}' already exists for this tenant.");
        }

        await _roomRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Meeting room created: {RoomCode}", entity.RoomCode);

        return entity.ToDto();
    }

    public async Task<MeetingRoomDto> UpdateAsync(UpdateMeetingRoomDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _roomRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Meeting room updated: {RoomCode}", entity.RoomCode);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        await _roomRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Meeting room deleted: {Id}", id);

        return true;
    }

    private async Task<string> GenerateRoomCodeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var count = await _roomRepository.GetQueryable()
            .CountAsync(r => r.TenantId == tenantId, cancellationToken);
        return $"RM-{(count + 1):D4}";
    }
}

#endregion Meeting Room Service

#region Room Booking Service

public class RoomBookingService : IRoomBookingService
{
    private readonly IRoomBookingRepository _bookingRepository;
    private readonly IMeetingRoomRepository _roomRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RoomBookingService> _logger;

    public RoomBookingService(
        IRoomBookingRepository bookingRepository,
        IMeetingRoomRepository roomRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<RoomBookingService> logger)
    {
        _bookingRepository = bookingRepository;
        _roomRepository = roomRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<RoomBooking> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _bookingRepository.GetQueryable()
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Include(b => b.ApprovedBy)
            .Include(b => b.Event)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Room booking with ID '{id}' not found.");
        return entity;
    }

    private async Task<bool> HasConflictingBookingAsync(Guid tenantId, Guid roomId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        var query = _bookingRepository.GetQueryable()
            .Where(b => b.TenantId == tenantId &&
                        b.RoomId == roomId &&
                        !b.IsCancelled &&
                        b.Status != BookingStatus.Cancelled &&
                        b.StartDateTime < endDateTime &&
                        b.EndDateTime > startDateTime);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<RoomBookingDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<RoomBookingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _bookingRepository.GetQueryable()
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Where(b => b.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<RoomBookingDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _bookingRepository.GetQueryable()
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Where(b => b.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.StartDateTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RoomBookingDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetByRoomIdAsync(roomId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetByBookerAsync(Guid bookedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetByBookerAsync(bookedById))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetByDateRangeAsync(startDate, endDate))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetByStatusAsync(BookingStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetPendingApprovalsAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<RoomBookingDto> CreateAsync(CreateRoomBookingDto createDto, Guid bookedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var room = await _roomRepository.GetByIdAsync(createDto.RoomId);
        if (room == null || room.TenantId != tenantId)
            throw new ArgumentException("Meeting room not found");

        if (!room.IsBookable)
            throw new InvalidOperationException("This room is not available for booking");

        var hasConflict = await HasConflictingBookingAsync(
            tenantId, createDto.RoomId, createDto.StartDateTime, createDto.EndDateTime, cancellationToken: cancellationToken);

        if (hasConflict)
            throw new InvalidOperationException("There is a conflicting booking for this time slot");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.BookedById = bookedById;
        entity.BookingDate = DateTime.UtcNow;
        entity.BookingNumber = await GenerateBookingNumberAsync(tenantId, cancellationToken);
        entity.Status = room.RequiresApproval ? BookingStatus.Tentative : BookingStatus.Confirmed;

        await _bookingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking created: {BookingNumber}", entity.BookingNumber);

        return entity.ToDto();
    }

    public async Task<RoomBookingDto> UpdateAsync(UpdateRoomBookingDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _bookingRepository.GetQueryable()
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .FirstOrDefaultAsync(b => b.Id == updateDto.Id && b.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Room booking with ID '{updateDto.Id}' not found.");

        var hasConflict = await HasConflictingBookingAsync(
            tenantId, entity.RoomId, updateDto.StartDateTime, updateDto.EndDateTime, updateDto.Id, cancellationToken);

        if (hasConflict)
            throw new InvalidOperationException("There is a conflicting booking for this time slot");

        updateDto.UpdateEntity(entity);

        await _bookingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking updated: {BookingNumber}", entity.BookingNumber);

        return entity.ToDto();
    }

    public async Task<bool> ApproveBookingAsync(Guid bookingId, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(bookingId, cancellationToken);

        entity.ApprovedById = approvedById;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.Status = BookingStatus.Confirmed;

        await _bookingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking approved: {BookingNumber}", entity.BookingNumber);

        return true;
    }

    public async Task<bool> CancelBookingAsync(CancelRoomBookingDto cancelDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(cancelDto.BookingId, cancellationToken);

        entity.IsCancelled = true;
        entity.CancellationDate = DateTime.UtcNow;
        entity.CancellationReason = cancelDto.CancellationReason;
        entity.Status = BookingStatus.Cancelled;

        await _bookingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking cancelled: {BookingNumber}", entity.BookingNumber);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        await _bookingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking deleted: {Id}", id);

        return true;
    }

    private async Task<string> GenerateBookingNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _bookingRepository.GetQueryable()
            .CountAsync(b => b.TenantId == tenantId && b.BookingDate.Year == year, cancellationToken);

        return $"BK-{year}-{(count + 1):D5}";
    }
}

#endregion Room Booking Service

#region Company Milestone Service

public class CompanyMilestoneService : ICompanyMilestoneService
{
    private readonly ICompanyMilestoneRepository _milestoneRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompanyMilestoneService> _logger;

    public CompanyMilestoneService(
        ICompanyMilestoneRepository milestoneRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CompanyMilestoneService> logger)
    {
        _milestoneRepository = milestoneRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<CompanyMilestone> GetOwnedAsync(Guid id)
    {
        var entity = await _milestoneRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Company milestone with ID '{id}' not found.");
        return entity;
    }

    public async Task<CompanyMilestoneDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<CompanyMilestoneDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<CompanyMilestoneDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _milestoneRepository.GetQueryable().Where(m => m.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.MilestoneDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CompanyMilestoneDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<CompanyMilestoneDto>> GetByCategoryAsync(MilestoneCategory category, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetByCategoryAsync(category))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<CompanyMilestoneDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetByDateRangeAsync(startDate, endDate))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<CompanyMilestoneDto>> GetUpcomingMilestonesAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetUpcomingMilestonesAsync(daysAhead))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<CompanyMilestoneDto> CreateAsync(CreateCompanyMilestoneDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await _milestoneRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company milestone created: {Title}", entity.Title);

        return entity.ToDto();
    }

    public async Task<CompanyMilestoneDto> UpdateAsync(UpdateCompanyMilestoneDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        updateDto.UpdateEntity(entity);

        await _milestoneRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company milestone updated: {Title}", entity.Title);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _milestoneRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company milestone deleted: {Id}", id);

        return true;
    }
}

#endregion Company Milestone Service

#region Business Closure Service

public class BusinessClosureService : IBusinessClosureService
{
    private readonly IBusinessClosureRepository _closureRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BusinessClosureService> _logger;

    public BusinessClosureService(
        IBusinessClosureRepository closureRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<BusinessClosureService> logger)
    {
        _closureRepository = closureRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<BusinessClosure> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _closureRepository.GetQueryable()
            .Include(c => c.Station)
            .Include(c => c.Department)
            .Include(c => c.AnnouncedBy)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Business closure with ID '{id}' not found.");
        return entity;
    }

    public async Task<BusinessClosureDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<BusinessClosureDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _closureRepository.GetQueryable()
            .Include(c => c.Station)
            .Include(c => c.Department)
            .Include(c => c.AnnouncedBy)
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<BusinessClosureDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _closureRepository.GetQueryable()
            .Include(c => c.Station)
            .Include(c => c.Department)
            .Include(c => c.AnnouncedBy)
            .Where(c => c.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BusinessClosureDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<BusinessClosureDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _closureRepository.GetByDateRangeAsync(startDate, endDate))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<BusinessClosureDto>> GetByTypeAsync(ClosureType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _closureRepository.GetByTypeAsync(type))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<BusinessClosureDto>> GetByStationAsync(Guid stationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _closureRepository.GetByStationAsync(stationId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<BusinessClosureDto>> GetUpcomingClosuresAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _closureRepository.GetUpcomingClosuresAsync(daysAhead))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<bool> IsClosureDateAsync(DateTime date, Guid? stationId = null, Guid? departmentId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _closureRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && c.StartDate <= date && c.EndDate >= date);

        if (stationId.HasValue)
            query = query.Where(c => c.AffectsAllStations || c.StationId == stationId.Value);

        if (departmentId.HasValue)
            query = query.Where(c => c.AffectsAllStations || c.DepartmentId == departmentId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<BusinessClosureDto> CreateAsync(CreateBusinessClosureDto createDto, Guid announcedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.AnnouncedById = announcedById;
        entity.AnnouncementDate = DateTime.UtcNow;

        await _closureRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business closure created: {Title}", entity.Title);

        return entity.ToDto();
    }

    public async Task<BusinessClosureDto> UpdateAsync(UpdateBusinessClosureDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _closureRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business closure updated: {Title}", entity.Title);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        await _closureRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business closure deleted: {Id}", id);

        return true;
    }
}

#endregion Business Closure Service

#region Fiscal Year Service

public class FiscalYearService : IFiscalYearService
{
    private readonly IFiscalYearRepository _fiscalYearRepository;
    private readonly IFiscalPeriodRepository _periodRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FiscalYearService> _logger;

    public FiscalYearService(
        IFiscalYearRepository fiscalYearRepository,
        IFiscalPeriodRepository periodRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<FiscalYearService> logger)
    {
        _fiscalYearRepository = fiscalYearRepository;
        _periodRepository = periodRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<FiscalYear> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _fiscalYearRepository.GetQueryable()
            .Include(fy => fy.Periods)
            .FirstOrDefaultAsync(fy => fy.Id == id && fy.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Fiscal year with ID '{id}' not found.");
        return entity;
    }

    private async Task<FiscalPeriod> GetOwnedPeriodAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _periodRepository.GetQueryable()
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Fiscal period not found");
        return entity;
    }

    public async Task<FiscalYearDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<FiscalYearDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<FiscalYearDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _fiscalYearRepository.GetQueryable()
            .Include(fy => fy.Periods)
            .Where(fy => fy.TenantId == tenantId)
            .OrderByDescending(fy => fy.Year)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<FiscalYearDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _fiscalYearRepository.GetQueryable()
            .Include(fy => fy.Periods)
            .Where(fy => fy.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(fy => fy.Year)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FiscalYearDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<FiscalYearDto?> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _fiscalYearRepository.GetByYearAsync(year);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<FiscalYearDto?> GetCurrentFiscalYearAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _fiscalYearRepository.GetCurrentFiscalYearAsync();
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<FiscalYearDto>> GetByStatusAsync(FiscalYearStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _fiscalYearRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<FiscalYearDto> CreateAsync(CreateFiscalYearDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var existingYear = await _fiscalYearRepository.GetQueryable()
            .FirstOrDefaultAsync(fy => fy.TenantId == tenantId && fy.Year == createDto.Year, cancellationToken);
        if (existingYear != null)
            throw new InvalidOperationException($"Fiscal year {createDto.Year} already exists");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.Status = FiscalYearStatus.Active;

        if (createDto.IsCurrent)
        {
            await ClearCurrentFiscalYearAsync(tenantId, cancellationToken);
        }

        await _fiscalYearRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal year created: {Year}", entity.Year);

        return entity.ToDto();
    }

    public async Task<FiscalYearDto> UpdateAsync(UpdateFiscalYearDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);

        if (updateDto.IsCurrent && !entity.IsCurrent)
        {
            await ClearCurrentFiscalYearAsync(tenantId, cancellationToken);
        }

        updateDto.UpdateEntity(entity);

        await _fiscalYearRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal year updated: {Year}", entity.Year);

        return entity.ToDto();
    }

    public async Task<bool> SetAsCurrentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync(id, cancellationToken);

        await ClearCurrentFiscalYearAsync(tenantId, cancellationToken);

        entity.IsCurrent = true;

        await _fiscalYearRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal year set as current: {Year}", entity.Year);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        await _fiscalYearRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal year deleted: {Id}", id);

        return true;
    }

    #region Period Operations

    public async Task<FiscalPeriodDto> AddPeriodAsync(CreateFiscalPeriodDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAsync(createDto.FiscalYearId, cancellationToken);

        var existingPeriod = await _periodRepository.GetByPeriodNumberAsync(createDto.FiscalYearId, createDto.PeriodNumber);
        if (existingPeriod != null && existingPeriod.TenantId == tenantId)
            throw new InvalidOperationException($"Period number {createDto.PeriodNumber} already exists for this fiscal year");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await _periodRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _periodRepository.GetQueryable()
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.Id == entity.Id && p.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Fiscal period added: {PeriodNumber}", createDto.PeriodNumber);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<FiscalPeriodDto>> GetPeriodsAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAsync(fiscalYearId, cancellationToken);
        var entities = (await _periodRepository.GetByFiscalYearIdAsync(fiscalYearId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<FiscalPeriodDto> UpdatePeriodAsync(UpdateFiscalPeriodDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPeriodAsync(updateDto.Id, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _periodRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal period updated: {PeriodNumber}", entity.PeriodNumber);

        return entity.ToDto();
    }

    public async Task<bool> ClosePeriodAsync(CloseFiscalPeriodDto closeDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPeriodAsync(closeDto.PeriodId, cancellationToken);

        entity.IsClosed = true;
        entity.ClosedDate = DateTime.UtcNow;

        await _periodRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal period closed: {PeriodId}", closeDto.PeriodId);

        return true;
    }

    public async Task<bool> DeletePeriodAsync(Guid periodId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPeriodAsync(periodId, cancellationToken);

        await _periodRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal period deleted: {PeriodId}", periodId);

        return true;
    }

    #endregion

    #region Helper Methods

    private async Task ClearCurrentFiscalYearAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var currentYears = await _fiscalYearRepository.GetQueryable()
            .Where(fy => fy.TenantId == tenantId && fy.IsCurrent)
            .ToListAsync(cancellationToken);

        foreach (var currentYear in currentYears)
        {
            currentYear.IsCurrent = false;
            await _fiscalYearRepository.UpdateAsync(currentYear);
        }
    }

    #endregion
}

#endregion Fiscal Year Service
