using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// GEOFENCE ZONE SERVICE
// ============================================================================

#region Geofence Zone Service

public interface IGeofenceZoneService
{
    Task<GeofenceZoneDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<GeofenceZoneSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<GeofenceZoneSummaryDto>> GetActiveZonesAsync(CancellationToken ct = default);
    Task<IEnumerable<GeofenceZoneSummaryDto>> GetByLocationIdAsync(Guid locationId, CancellationToken ct = default);
    Task<PagedResult<GeofenceZoneSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<GeofenceZoneDto> CreateAsync(CreateGeofenceZoneDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<GeofenceZoneDto> UpdateAsync(UpdateGeofenceZoneDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// REMOTE WORK REQUEST SERVICE
// ============================================================================

#region Remote Work Request Service

public interface IRemoteWorkRequestService
{
    Task<RemoteWorkRequestDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RemoteWorkRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken ct = default);
    Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetByStatusAsync(RemoteWorkRequestStatus status, CancellationToken ct = default);
    Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default);
    Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<PagedResult<RemoteWorkRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<RemoteWorkRequestDto> CreateAsync(CreateRemoteWorkRequestDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<RemoteWorkRequestDto> UpdateAsync(UpdateRemoteWorkRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<RemoteWorkRequestDto> ApproveAsync(Guid requestId, string? comments, Guid userId, CancellationToken ct = default);
    Task<RemoteWorkRequestDto> RejectAsync(Guid requestId, string rejectionReason, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// HOLIDAY CALENDAR SERVICE
// ============================================================================

#region Holiday Calendar Service

public interface IHolidayCalendarService
{
    Task<HolidayCalendarDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<HolidayCalendarDto?> GetDefaultCalendarAsync(CancellationToken ct = default);
    Task<IEnumerable<HolidayCalendarSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<HolidayCalendarSummaryDto>> GetActiveCalendarsAsync(CancellationToken ct = default);
    Task<HolidayCalendarDto> GetWithHolidaysAsync(Guid id, int? year = null, CancellationToken ct = default);
    Task<PagedResult<HolidayCalendarSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<HolidayCalendarDto> CreateAsync(CreateHolidayCalendarDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<HolidayCalendarDto> UpdateAsync(UpdateHolidayCalendarDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // Public holiday sub-operations
    Task<PublicHolidayDto> AddHolidayAsync(CreatePublicHolidayDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidaySummaryDto>> GetHolidaysAsync(Guid calendarId, int? year = null, CancellationToken ct = default);
    Task<PublicHolidayDto> GetHolidayByIdAsync(Guid holidayId, CancellationToken ct = default);
    Task<PublicHolidayDto> UpdateHolidayAsync(UpdatePublicHolidayDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteHolidayAsync(Guid holidayId, CancellationToken ct = default);
}

#endregion

// ============================================================================
// PUBLIC HOLIDAY SERVICE (queries + calendar-scoped writes via HolidayCalendarService)
// ============================================================================

#region Public Holiday Service (standalone)

public interface IPublicHolidayService
{
    Task<PublicHolidayDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidayDto>> GetByCalendarIdAsync(Guid calendarId, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidayDto>> GetByCalendarAndYearAsync(Guid calendarId, int year, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidayDto>> GetByYearAsync(int year, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidayDto>> GetInRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidayDto>> GetRecurringHolidaysAsync(Guid calendarId, CancellationToken ct = default);

    Task<PublicHolidayDto> CreateAsync(CreatePublicHolidayDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<PublicHolidayDto> UpdateAsync(UpdatePublicHolidayDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// PAY PERIOD SERVICE
// ============================================================================

#region Pay Period Service

public interface IPayPeriodService
{
    Task<PayPeriodDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PayPeriodDto?> GetCurrentOpenPeriodAsync(CancellationToken ct = default);
    Task<PayPeriodDto?> GetPeriodCoveringDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IEnumerable<PayPeriodSummaryDto>> GetByStatusAsync(PayPeriodStatus status, CancellationToken ct = default);
    Task<IEnumerable<PayPeriodSummaryDto>> GetByTypeAsync(PayPeriodType type, CancellationToken ct = default);
    Task<PayPeriodDto> GetWithSummariesAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<PayPeriodSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<PayPeriodDto> CreateAsync(CreatePayPeriodDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<PayPeriodDto> UpdateAsync(UpdatePayPeriodDto dto, Guid userId, CancellationToken ct = default);
    Task<PayPeriodDto> CloseAsync(Guid periodId, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// STAFF ATTENDANCE PAYROLL EXPORT SERVICE
// ============================================================================

#region Staff Attendance Payroll Export Service

public interface IStaffAttendancePayrollExportService
{
    Task<StaffAttendancePayrollExportDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffAttendancePayrollExportDto?> GetByExportReferenceAsync(string exportReference, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendancePayrollExportSummaryDto>> GetByPayPeriodIdAsync(Guid payPeriodId, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendancePayrollExportSummaryDto>> GetByStatusAsync(PayrollExportStatus status, CancellationToken ct = default);
    Task<StaffAttendancePayrollExportDto?> GetLatestSuccessfulExportForPeriodAsync(Guid payPeriodId, CancellationToken ct = default);
    Task<PagedResult<StaffAttendancePayrollExportSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    /// <summary>Generates and dispatches the export batch for the given pay period.</summary>
    Task<StaffAttendancePayrollExportDto> ExportAsync(Guid payPeriodId, string? targetSystem, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion
