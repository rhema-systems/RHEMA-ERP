using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class OrientationSessionService : IOrientationSessionService
{
    private readonly IOrientationSessionRepository _sessionRepository;
    private readonly IOrientationProgramRepository _programRepository;
    private readonly IOrientationSessionFacilitatorRepository _facilitatorRepository;
    private readonly IOrientationAttendanceRecordRepository _attendanceRepository;
    private readonly IEmployeeOrientationRepository _enrollmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrientationSessionService> _logger;

    public OrientationSessionService(
        IOrientationSessionRepository sessionRepository,
        IOrientationProgramRepository programRepository,
        IOrientationSessionFacilitatorRepository facilitatorRepository,
        IOrientationAttendanceRecordRepository attendanceRepository,
        IEmployeeOrientationRepository enrollmentRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrientationSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _programRepository = programRepository;
        _facilitatorRepository = facilitatorRepository;
        _attendanceRepository = attendanceRepository;
        _enrollmentRepository = enrollmentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ====================================================================
    // QUERIES
    // ====================================================================

    public async Task<OrientationSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetWithDetailsAsync(id)
            ?? throw new ArgumentException($"Orientation session with ID '{id}' not found.");
        var dto = entity.ToDto();
        var map = await _unitOfWork.ResolveEmployeesAsync(dto.Facilitators.Select(f => f.EmployeeId));
        dto.Facilitators.FillNames(map);
        return dto;
    }

    public async Task<OrientationSessionDto?> GetBySessionCodeAsync(string sessionCode, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetBySessionCodeAsync(sessionCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<OrientationSessionSummaryDto>> GetByProgramIdAsync(Guid programId, CancellationToken cancellationToken = default)
        => (await _sessionRepository.GetByProgramIdAsync(programId)).ToSummaryDtoList();

    public async Task<IEnumerable<OrientationSessionSummaryDto>> GetByStatusAsync(OrientationSessionStatus status, CancellationToken cancellationToken = default)
        => (await _sessionRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<OrientationSessionSummaryDto>> GetUpcomingAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _sessionRepository.GetUpcomingAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<OrientationSessionSummaryDto>> GetOpenForEnrollmentAsync(CancellationToken cancellationToken = default)
        => (await _sessionRepository.GetOpenForEnrollmentAsync()).ToSummaryDtoList();

    // ====================================================================
    // CRUD + LIFECYCLE
    // ====================================================================

    public async Task<OrientationSessionDto> CreateAsync(CreateOrientationSessionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (!await _programRepository.ExistsAsync(p => p.Id == createDto.ProgramId && !p.IsDeleted))
            throw new ArgumentException($"Orientation program with ID '{createDto.ProgramId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        entity.SessionCode = string.IsNullOrWhiteSpace(createDto.SessionCode)
            ? await GenerateSessionCodeAsync(cancellationToken)
            : createDto.SessionCode.Trim();

        if (await _sessionRepository.SessionCodeExistsAsync(entity.SessionCode))
            throw new InvalidOperationException($"Session code '{entity.SessionCode}' is already in use.");

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation session created: {Code}", entity.SessionCode);

        return (await _sessionRepository.GetWithDetailsAsync(entity.Id))!.ToDto();
    }

    public async Task<OrientationSessionDto> UpdateAsync(UpdateOrientationSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(updateDto.Id)
            ?? throw new ArgumentException($"Orientation session with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _sessionRepository.GetWithDetailsAsync(entity.Id))!.ToDto();
    }

    public async Task<bool> ChangeStatusAsync(ChangeOrientationSessionStatusDto changeDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(changeDto.SessionId)
            ?? throw new ArgumentException($"Orientation session with ID '{changeDto.SessionId}' not found.");

        entity.Status = changeDto.NewStatus;
        if (changeDto.NewStatus == OrientationSessionStatus.InProgress && entity.ActualStartAt == null)
            entity.ActualStartAt = DateTime.UtcNow;
        if (changeDto.NewStatus == OrientationSessionStatus.Completed && entity.ActualEndAt == null)
            entity.ActualEndAt = DateTime.UtcNow;

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation session {Code} status changed to {Status}", entity.SessionCode, changeDto.NewStatus);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Orientation session with ID '{id}' not found.");

        var enrolledCount = await _sessionRepository.GetEnrolledCountAsync(id);
        if (enrolledCount > 0)
            throw new InvalidOperationException("Cannot delete a session that has active enrollments. Cancel it instead.");

        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // FACILITATORS
    // ====================================================================

    public async Task<OrientationSessionFacilitatorDto> AddFacilitatorAsync(CreateOrientationSessionFacilitatorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (!await _sessionRepository.ExistsAsync(s => s.Id == createDto.SessionId && !s.IsDeleted))
            throw new ArgumentException($"Orientation session with ID '{createDto.SessionId}' not found.");

        if (createDto.EmployeeId == null && string.IsNullOrWhiteSpace(createDto.ExternalFacilitatorName))
            throw new InvalidOperationException("A facilitator must reference an employee or supply an external facilitator name.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _facilitatorRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationSessionFacilitatorDto>> GetFacilitatorsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var list = (await _facilitatorRepository.GetBySessionIdAsync(sessionId)).Select(f => f.ToDto()).ToList();
        var map = await _unitOfWork.ResolveEmployeesAsync(list.Select(f => f.EmployeeId));
        list.FillNames(map);
        return list;
    }

    public async Task<OrientationSessionFacilitatorDto> UpdateFacilitatorAsync(UpdateOrientationSessionFacilitatorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _facilitatorRepository.GetByIdAsync(updateDto.Id)
            ?? throw new ArgumentException($"Orientation session facilitator with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _facilitatorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemoveFacilitatorAsync(Guid facilitatorId, CancellationToken cancellationToken = default)
    {
        var entity = await _facilitatorRepository.GetByIdAsync(facilitatorId)
            ?? throw new ArgumentException($"Orientation session facilitator with ID '{facilitatorId}' not found.");

        await _facilitatorRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // ATTENDANCE
    // ====================================================================

    public async Task<IEnumerable<OrientationAttendanceRecordDto>> MarkAttendanceAsync(MarkOrientationAttendanceDto markDto, Guid markedByUserId, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(markDto.SessionId)
            ?? throw new ArgumentException($"Orientation session with ID '{markDto.SessionId}' not found.");

        var results = new List<OrientationAttendanceRecord>();

        foreach (var entry in markDto.Entries)
        {
            var record = await _attendanceRepository.GetByEnrollmentAndDayAsync(entry.EnrollmentId, markDto.SessionDay);

            if (record == null)
            {
                record = new OrientationAttendanceRecord
                {
                    TenantId = session.TenantId,
                    EnrollmentId = entry.EnrollmentId,
                    SessionDay = markDto.SessionDay,
                    CreatedBy = markedByUserId.ToString(),
                };
                ApplyAttendanceEntry(record, entry, markedByUserId);
                await _attendanceRepository.AddAsync(record);
            }
            else
            {
                ApplyAttendanceEntry(record, entry, markedByUserId);
                record.UpdatedAt = DateTime.UtcNow;
                record.UpdatedBy = markedByUserId.ToString();
                await _attendanceRepository.UpdateAsync(record);
            }

            results.Add(record);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return results.Select(r => r.ToDto());
    }

    public async Task<IEnumerable<OrientationAttendanceRecordDto>> GetAttendanceForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => await HydrateAttendanceAsync((await _attendanceRepository.GetBySessionIdAsync(sessionId)).Select(a => a.ToDto()).ToList());

    public async Task<IEnumerable<OrientationAttendanceRecordDto>> GetAttendanceForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
        => await HydrateAttendanceAsync((await _attendanceRepository.GetByEnrollmentIdAsync(enrollmentId)).Select(a => a.ToDto()).ToList());

    private async Task<List<OrientationAttendanceRecordDto>> HydrateAttendanceAsync(List<OrientationAttendanceRecordDto> list)
    {
        var ids = list.Select(a => a.EmployeeId).Concat(list.Select(a => a.MarkedByEmployeeId));
        var map = await _unitOfWork.ResolveEmployeesAsync(ids);
        list.FillNames(map);
        return list;
    }

    // ====================================================================
    // HELPERS
    // ====================================================================

    private static void ApplyAttendanceEntry(OrientationAttendanceRecord record, OrientationAttendanceEntryDto entry, Guid markedByUserId)
    {
        record.AttendanceStatus = entry.AttendanceStatus;
        record.CheckInAt = entry.CheckInAt;
        record.CheckOutAt = entry.CheckOutAt;
        record.MarkedLate = entry.MarkedLate;
        record.AbsenceReason = entry.AbsenceReason;
        record.MarkedByEmployeeId = markedByUserId;

        record.AttendedMinutes = entry.CheckInAt.HasValue && entry.CheckOutAt.HasValue
            ? (int)Math.Max(0, (entry.CheckOutAt.Value - entry.CheckInAt.Value).TotalMinutes)
            : record.AttendedMinutes;
    }

    private async Task<string> GenerateSessionCodeAsync(CancellationToken cancellationToken)
    {
        var prefix = $"OSN-{DateTime.UtcNow.Year}-";
        var next = (await _sessionRepository.CountAsync()) + 1;
        var code = $"{prefix}{next:D4}";
        while (await _sessionRepository.SessionCodeExistsAsync(code))
        {
            next++;
            code = $"{prefix}{next:D4}";
        }
        return code;
    }
}
