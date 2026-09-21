using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class AttendanceMappingExtensions
{
    // ========================================================================
    // STAFF ATTENDANCE RECORD
    // ========================================================================

    #region StaffAttendanceRecord

    public static StaffAttendanceRecordDto ToDto(this StaffAttendanceRecord entity)
    {
        return new StaffAttendanceRecordDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            Date = entity.Date,
            CheckInTime = entity.CheckInTime,
            CheckOutTime = entity.CheckOutTime,
            WorkedHours = entity.WorkedHours,
            OvertimeHours = entity.OvertimeHours,
            Status = entity.Status,
            Notes = entity.Notes,
        };
    }

    public static StaffAttendanceRecordSummaryDto ToSummaryDto(this StaffAttendanceRecord entity)
    {
        return new StaffAttendanceRecordSummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            Date = entity.Date,
            CheckInTime = entity.CheckInTime,
            CheckOutTime = entity.CheckOutTime,
            WorkedHours = entity.WorkedHours,
            Status = entity.Status,
        };
    }

    public static StaffAttendanceRecord ToEntity(this CreateStaffAttendanceRecordDto dto, Guid tenantId, Guid userId)
    {
        return new StaffAttendanceRecord
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            Date = dto.Date,
            CheckInTime = dto.CheckInTime,
            CheckOutTime = dto.CheckOutTime,
            WorkedHours = dto.WorkedHours,
            OvertimeHours = dto.OvertimeHours,
            Status = dto.Status,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffAttendanceRecord entity, UpdateStaffAttendanceRecordDto dto, Guid userId)
    {
        entity.CheckInTime = dto.CheckInTime;
        entity.CheckOutTime = dto.CheckOutTime;
        entity.WorkedHours = dto.WorkedHours;
        entity.OvertimeHours = dto.OvertimeHours;
        entity.Status = dto.Status;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffAttendanceRecordSummaryDto> ToSummaryDtoList(this IEnumerable<StaffAttendanceRecord> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF DAILY ATTENDANCE
    // ========================================================================

    #region StaffDailyAttendance

    public static StaffDailyAttendanceDto ToDto(this StaffDailyAttendance entity)
    {
        return new StaffDailyAttendanceDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            AttendanceDate = entity.AttendanceDate,
            DayOfWeek = entity.DayOfWeek,
            WorkScheduleId = entity.WorkScheduleId,
            WorkScheduleName = entity.WorkSchedule?.ScheduleName,
            ScheduledStartTime = entity.ScheduledStartTime,
            ScheduledEndTime = entity.ScheduledEndTime,
            ScheduledWorkHours = entity.ScheduledWorkHours,
            ActualCheckInTime = entity.ActualCheckInTime,
            ActualCheckOutTime = entity.ActualCheckOutTime,
            ActualWorkHours = entity.ActualWorkHours,
            Status = entity.Status,
            StatusReason = entity.StatusReason,
            IsLate = entity.IsLate,
            LateMinutes = entity.LateMinutes,
            IsEarlyDeparture = entity.IsEarlyDeparture,
            EarlyDepartureMinutes = entity.EarlyDepartureMinutes,
            BreakStartTime = entity.BreakStartTime,
            BreakEndTime = entity.BreakEndTime,
            TotalBreakMinutes = entity.TotalBreakMinutes,
            IsOvertime = entity.IsOvertime,
            OvertimeHours = entity.OvertimeHours,
            OvertimeApproved = entity.OvertimeApproved,
            OvertimeApprovedById = entity.OvertimeApprovedById,
            OvertimeApprovedByName = entity.OvertimeApprovedBy?.FullName,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            CheckInLocation = entity.CheckInLocation,
            CheckOutLocation = entity.CheckOutLocation,
            CheckInLatitude = entity.CheckInLatitude,
            CheckInLongitude = entity.CheckInLongitude,
            CheckOutLatitude = entity.CheckOutLatitude,
            CheckOutLongitude = entity.CheckOutLongitude,
            CheckInLocationStatus = entity.CheckInLocationStatus,
            CheckOutLocationStatus = entity.CheckOutLocationStatus,
            CheckInGeofenceZoneId = entity.CheckInGeofenceZoneId,
            CheckInGeofenceZoneName = entity.CheckInGeofenceZone?.ZoneName,
            IsRemoteWork = entity.IsRemoteWork,
            RemoteWorkLocation = entity.RemoteWorkLocation,
            RemoteWorkRequestId = entity.RemoteWorkRequestId,
            CheckInDevice = entity.CheckInDevice,
            CheckInIpAddress = entity.CheckInIpAddress,
            CheckOutDevice = entity.CheckOutDevice,
            CheckOutIpAddress = entity.CheckOutIpAddress,
            LeaveRequestId = entity.LeaveRequestId,
            PublicHolidayId = entity.PublicHolidayId,
            PublicHolidayName = entity.PublicHoliday?.HolidayName,
            PayPeriodId = entity.PayPeriodId,
            PayPeriodName = entity.PayPeriod?.PeriodName,
            RequiresVerification = entity.RequiresVerification,
            IsVerified = entity.IsVerified,
            VerifiedDate = entity.VerifiedDate,
            VerifiedById = entity.VerifiedById,
            VerifiedByName = entity.VerifiedBy?.FullName,
            VerificationNotes = entity.VerificationNotes,
            HasException = entity.HasException,
            ExceptionReason = entity.ExceptionReason,
            ExceptionApproved = entity.ExceptionApproved,
            ExceptionApprovedById = entity.ExceptionApprovedById,
            ExceptionApprovedByName = entity.ExceptionApprovedBy?.FullName,
            Notes = entity.Notes,
            AttendanceLogs = entity.AttendanceLogs.Select(l => l.ToSummaryDto()).ToList(),
            Regularizations = entity.Regularizations.Select(r => r.ToSummaryDto()).ToList(),
        };
    }

    public static StaffDailyAttendanceSummaryDto ToSummaryDto(this StaffDailyAttendance entity)
    {
        return new StaffDailyAttendanceSummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            AttendanceDate = entity.AttendanceDate,
            DayOfWeek = entity.DayOfWeek,
            ActualCheckInTime = entity.ActualCheckInTime,
            ActualCheckOutTime = entity.ActualCheckOutTime,
            ActualWorkHours = entity.ActualWorkHours,
            Status = entity.Status,
            IsLate = entity.IsLate,
            LateMinutes = entity.LateMinutes,
            IsOvertime = entity.IsOvertime,
            OvertimeHours = entity.OvertimeHours,
            IsRemoteWork = entity.IsRemoteWork,
            HasException = entity.HasException,
            IsVerified = entity.IsVerified,
        };
    }

    public static StaffDailyAttendance ToEntity(this CreateStaffDailyAttendanceDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDailyAttendance
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            AttendanceDate = dto.AttendanceDate,
            DayOfWeek = dto.DayOfWeek,
            WorkScheduleId = dto.WorkScheduleId,
            ScheduledStartTime = dto.ScheduledStartTime,
            ScheduledEndTime = dto.ScheduledEndTime,
            ScheduledWorkHours = dto.ScheduledWorkHours,
            ActualCheckInTime = dto.ActualCheckInTime,
            ActualCheckOutTime = dto.ActualCheckOutTime,
            ActualWorkHours = dto.ActualWorkHours,
            Status = dto.Status,
            StatusReason = dto.StatusReason,
            IsLate = dto.IsLate,
            LateMinutes = dto.LateMinutes,
            IsEarlyDeparture = dto.IsEarlyDeparture,
            EarlyDepartureMinutes = dto.EarlyDepartureMinutes,
            BreakStartTime = dto.BreakStartTime,
            BreakEndTime = dto.BreakEndTime,
            TotalBreakMinutes = dto.TotalBreakMinutes,
            IsOvertime = dto.IsOvertime,
            OvertimeHours = dto.OvertimeHours,
            LocationId = dto.LocationId,
            CheckInLocation = dto.CheckInLocation,
            CheckOutLocation = dto.CheckOutLocation,
            CheckInLatitude = dto.CheckInLatitude,
            CheckInLongitude = dto.CheckInLongitude,
            CheckOutLatitude = dto.CheckOutLatitude,
            CheckOutLongitude = dto.CheckOutLongitude,
            CheckInGeofenceZoneId = dto.CheckInGeofenceZoneId,
            IsRemoteWork = dto.IsRemoteWork,
            RemoteWorkLocation = dto.RemoteWorkLocation,
            RemoteWorkRequestId = dto.RemoteWorkRequestId,
            CheckInDevice = dto.CheckInDevice,
            CheckInIpAddress = dto.CheckInIpAddress,
            CheckOutDevice = dto.CheckOutDevice,
            CheckOutIpAddress = dto.CheckOutIpAddress,
            LeaveRequestId = dto.LeaveRequestId,
            PublicHolidayId = dto.PublicHolidayId,
            PayPeriodId = dto.PayPeriodId,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDailyAttendance entity, UpdateStaffDailyAttendanceDto dto, Guid userId)
    {
        entity.ActualCheckInTime = dto.ActualCheckInTime;
        entity.ActualCheckOutTime = dto.ActualCheckOutTime;
        entity.ActualWorkHours = dto.ActualWorkHours;
        entity.Status = dto.Status;
        entity.StatusReason = dto.StatusReason;
        entity.IsLate = dto.IsLate;
        entity.LateMinutes = dto.LateMinutes;
        entity.IsEarlyDeparture = dto.IsEarlyDeparture;
        entity.EarlyDepartureMinutes = dto.EarlyDepartureMinutes;
        entity.BreakStartTime = dto.BreakStartTime;
        entity.BreakEndTime = dto.BreakEndTime;
        entity.TotalBreakMinutes = dto.TotalBreakMinutes;
        entity.IsOvertime = dto.IsOvertime;
        entity.OvertimeHours = dto.OvertimeHours;
        entity.LocationId = dto.LocationId;
        entity.IsRemoteWork = dto.IsRemoteWork;
        entity.RemoteWorkLocation = dto.RemoteWorkLocation;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffDailyAttendanceSummaryDto> ToSummaryDtoList(this IEnumerable<StaffDailyAttendance> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF ATTENDANCE LOG
    // ========================================================================

    #region StaffAttendanceLog

    public static StaffAttendanceLogDto ToDto(this StaffAttendanceLog entity)
    {
        return new StaffAttendanceLogDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            LogDateTime = entity.LogDateTime,
            LogType = entity.LogType,
            DeviceId = entity.DeviceId,
            DeviceSerialNumber = entity.DeviceSerialNumber,
            Location = entity.Location,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            IsProcessed = entity.IsProcessed,
            ProcessedDate = entity.ProcessedDate,
            AttendanceId = entity.AttendanceId,
            RawData = entity.RawData,
            VerificationLogs = entity.VerificationLogs.Select(v => v.ToDto()).ToList(),
        };
    }

    public static StaffAttendanceLogSummaryDto ToSummaryDto(this StaffAttendanceLog entity)
    {
        return new StaffAttendanceLogSummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            LogDateTime = entity.LogDateTime,
            LogType = entity.LogType,
            DeviceSerialNumber = entity.DeviceSerialNumber,
            IsProcessed = entity.IsProcessed,
            AttendanceId = entity.AttendanceId,
        };
    }

    public static StaffAttendanceLog ToEntity(this CreateStaffAttendanceLogDto dto, Guid tenantId, Guid userId)
    {
        return new StaffAttendanceLog
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            LogDateTime = dto.LogDateTime,
            LogType = dto.LogType,
            DeviceId = dto.DeviceId,
            DeviceSerialNumber = dto.DeviceSerialNumber,
            Location = dto.Location,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            RawData = dto.RawData,
            IsProcessed = false,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<StaffAttendanceLogSummaryDto> ToSummaryDtoList(this IEnumerable<StaffAttendanceLog> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // ATTENDANCE LOCATION VERIFICATION LOG
    // ========================================================================

    #region AttendanceLocationVerificationLog

    public static AttendanceLocationVerificationLogDto ToDto(this AttendanceLocationVerificationLog entity)
    {
        return new AttendanceLocationVerificationLogDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            AttendanceLogId = entity.AttendanceLogId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            VerificationDateTime = entity.VerificationDateTime,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            GeofenceZoneId = entity.GeofenceZoneId,
            GeofenceZoneName = entity.GeofenceZone?.ZoneName,
            DistanceFromZoneMetres = entity.DistanceFromZoneMetres,
            Status = entity.Status,
            Notes = entity.Notes,
        };
    }

    #endregion

    // ========================================================================
    // STAFF ATTENDANCE REGULARIZATION
    // ========================================================================

    #region StaffAttendanceRegularization

    public static StaffAttendanceRegularizationDto ToDto(this StaffAttendanceRegularization entity)
    {
        return new StaffAttendanceRegularizationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            RegularizationNumber = entity.RegularizationNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            AttendanceId = entity.AttendanceId,
            AttendanceDate = entity.AttendanceDate,
            RequestDate = entity.RequestDate,
            Type = entity.Type,
            RequestedCheckInTime = entity.RequestedCheckInTime,
            RequestedCheckOutTime = entity.RequestedCheckOutTime,
            Reason = entity.Reason,
            SupportingDocuments = entity.SupportingDocuments,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            ApprovalComments = entity.ApprovalComments,
            RejectedDate = entity.RejectedDate,
            RejectionReason = entity.RejectionReason,
            IsApplied = entity.IsApplied,
            AppliedDate = entity.AppliedDate,
        };
    }

    public static StaffAttendanceRegularizationSummaryDto ToSummaryDto(this StaffAttendanceRegularization entity)
    {
        return new StaffAttendanceRegularizationSummaryDto
        {
            Id = entity.Id,
            RegularizationNumber = entity.RegularizationNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            AttendanceDate = entity.AttendanceDate,
            Type = entity.Type,
            Status = entity.Status,
            RequestDate = entity.RequestDate,
            IsApplied = entity.IsApplied,
        };
    }

    public static StaffAttendanceRegularization ToEntity(this CreateStaffAttendanceRegularizationDto dto, Guid tenantId, Guid userId)
    {
        return new StaffAttendanceRegularization
        {
            TenantId = tenantId,
            RegularizationNumber = string.Empty, // generated by service
            EmployeeId = dto.EmployeeId,
            AttendanceId = dto.AttendanceId,
            RequestDate = DateTime.UtcNow,
            Type = dto.Type,
            RequestedCheckInTime = dto.RequestedCheckInTime,
            RequestedCheckOutTime = dto.RequestedCheckOutTime,
            Reason = dto.Reason,
            SupportingDocuments = dto.SupportingDocuments,
            Status = AttendanceRegularizationStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffAttendanceRegularization entity, UpdateStaffAttendanceRegularizationDto dto, Guid userId)
    {
        entity.RequestedCheckInTime = dto.RequestedCheckInTime;
        entity.RequestedCheckOutTime = dto.RequestedCheckOutTime;
        entity.Reason = dto.Reason;
        entity.SupportingDocuments = dto.SupportingDocuments;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffAttendanceRegularizationSummaryDto> ToSummaryDtoList(this IEnumerable<StaffAttendanceRegularization> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF MONTHLY ATTENDANCE SUMMARY
    // ========================================================================

    #region StaffMonthlyAttendanceSummary

    public static StaffMonthlyAttendanceSummaryDto ToDto(this StaffMonthlyAttendanceSummary entity)
    {
        return new StaffMonthlyAttendanceSummaryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            Year = entity.Year,
            Month = entity.Month,
            TotalWorkingDays = entity.TotalWorkingDays,
            DaysPresent = entity.DaysPresent,
            DaysAbsent = entity.DaysAbsent,
            DaysOnLeave = entity.DaysOnLeave,
            DaysLate = entity.DaysLate,
            DaysRemoteWork = entity.DaysRemoteWork,
            PublicHolidays = entity.PublicHolidays,
            Weekends = entity.Weekends,
            DaysHalfDay = entity.DaysHalfDay,
            TotalScheduledHours = entity.TotalScheduledHours,
            TotalWorkedHours = entity.TotalWorkedHours,
            TotalOvertimeHours = entity.TotalOvertimeHours,
            TotalUndertimeHours = entity.TotalUndertimeHours,
            TotalBreakHours = entity.TotalBreakHours,
            TotalLateMinutes = entity.TotalLateMinutes,
            NumberOfLateDays = entity.NumberOfLateDays,
            TotalEarlyDepartureMinutes = entity.TotalEarlyDepartureMinutes,
            NumberOfEarlyDepartureDays = entity.NumberOfEarlyDepartureDays,
            AttendancePercentage = entity.AttendancePercentage,
            PunctualityPercentage = entity.PunctualityPercentage,
            PayPeriodId = entity.PayPeriodId,
            PayPeriodName = entity.PayPeriod?.PeriodName,
            IsFinalized = entity.IsFinalized,
            FinalizedDate = entity.FinalizedDate,
            FinalizedById = entity.FinalizedById,
            FinalizedByName = entity.FinalizedBy?.FullName,
            Notes = entity.Notes,
        };
    }

    public static StaffMonthlyAttendanceSummarySummaryDto ToSummaryDto(this StaffMonthlyAttendanceSummary entity)
    {
        return new StaffMonthlyAttendanceSummarySummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            Year = entity.Year,
            Month = entity.Month,
            DaysPresent = entity.DaysPresent,
            DaysAbsent = entity.DaysAbsent,
            TotalWorkedHours = entity.TotalWorkedHours,
            TotalOvertimeHours = entity.TotalOvertimeHours,
            AttendancePercentage = entity.AttendancePercentage,
            PunctualityPercentage = entity.PunctualityPercentage,
            IsFinalized = entity.IsFinalized,
        };
    }

    public static void UpdateEntity(this StaffMonthlyAttendanceSummary entity, UpdateStaffMonthlyAttendanceSummaryDto dto, Guid userId)
    {
        entity.Notes = dto.Notes;
        if (dto.DaysPresent.HasValue) entity.DaysPresent = dto.DaysPresent.Value;
        if (dto.DaysAbsent.HasValue) entity.DaysAbsent = dto.DaysAbsent.Value;
        if (dto.DaysOnLeave.HasValue) entity.DaysOnLeave = dto.DaysOnLeave.Value;
        if (dto.TotalLateMinutes.HasValue) entity.TotalLateMinutes = dto.TotalLateMinutes.Value;
        if (dto.TotalOvertimeHours.HasValue) entity.TotalOvertimeHours = dto.TotalOvertimeHours.Value;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffMonthlyAttendanceSummarySummaryDto> ToSummaryDtoList(this IEnumerable<StaffMonthlyAttendanceSummary> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF BULK ATTENDANCE IMPORT
    // ========================================================================

    #region StaffBulkAttendanceImport

    public static StaffBulkAttendanceImportDto ToDto(this StaffBulkAttendanceImport entity)
    {
        return new StaffBulkAttendanceImportDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ImportReference = entity.ImportReference,
            ImportedById = entity.ImportedById,
            ImportedByName = entity.ImportedBy?.FullName ?? string.Empty,
            ImportDate = entity.ImportDate,
            SourceFileName = entity.SourceFileName,
            SourceType = entity.SourceType,
            TotalRows = entity.TotalRows,
            SuccessCount = entity.SuccessCount,
            FailureCount = entity.FailureCount,
            Status = entity.Status,
            ErrorSummary = entity.ErrorSummary,
            CompletedDate = entity.CompletedDate,
            Notes = entity.Notes,
            ImportRows = entity.ImportRows.Select(r => r.ToDto()).ToList(),
        };
    }

    public static StaffBulkAttendanceImportSummaryDto ToSummaryDto(this StaffBulkAttendanceImport entity)
    {
        return new StaffBulkAttendanceImportSummaryDto
        {
            Id = entity.Id,
            ImportReference = entity.ImportReference,
            ImportedByName = entity.ImportedBy?.FullName ?? string.Empty,
            ImportDate = entity.ImportDate,
            SourceFileName = entity.SourceFileName,
            SourceType = entity.SourceType,
            TotalRows = entity.TotalRows,
            SuccessCount = entity.SuccessCount,
            FailureCount = entity.FailureCount,
            Status = entity.Status,
            CompletedDate = entity.CompletedDate,
        };
    }

    public static StaffBulkAttendanceImport ToEntity(this CreateStaffBulkAttendanceImportDto dto, Guid tenantId, Guid userId)
    {
        return new StaffBulkAttendanceImport
        {
            TenantId = tenantId,
            ImportReference = string.Empty, // generated by service
            ImportedById = dto.ImportedById,
            ImportDate = DateTime.UtcNow,
            SourceFileName = dto.SourceFileName,
            SourceType = dto.SourceType,
            Status = AttendanceImportStatus.Pending,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<StaffBulkAttendanceImportSummaryDto> ToSummaryDtoList(this IEnumerable<StaffBulkAttendanceImport> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF BULK ATTENDANCE IMPORT ROW
    // ========================================================================

    #region StaffBulkAttendanceImportRow

    public static StaffBulkAttendanceImportRowDto ToDto(this StaffBulkAttendanceImportRow entity)
    {
        return new StaffBulkAttendanceImportRowDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ImportId = entity.ImportId,
            RowNumber = entity.RowNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            RawData = entity.RawData,
            AttendanceDate = entity.AttendanceDate,
            CheckInTime = entity.CheckInTime,
            CheckOutTime = entity.CheckOutTime,
            IsSuccess = entity.IsSuccess,
            ErrorMessage = entity.ErrorMessage,
            CreatedAttendanceId = entity.CreatedAttendanceId,
        };
    }

    #endregion

    // ========================================================================
    // WORK SCHEDULE
    // ========================================================================

    #region WorkSchedule

    public static WorkScheduleDto ToDto(this WorkSchedule entity)
    {
        return new WorkScheduleDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ScheduleName = entity.ScheduleName,
            Description = entity.Description,
            Type = entity.Type,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            StandardStartTime = entity.StandardStartTime,
            StandardEndTime = entity.StandardEndTime,
            StandardHoursPerDay = entity.StandardHoursPerDay,
            StandardHoursPerWeek = entity.StandardHoursPerWeek,
            HasFlexibleStartTime = entity.HasFlexibleStartTime,
            FlexibleStartTimeEarliest = entity.FlexibleStartTimeEarliest,
            FlexibleStartTimeLatest = entity.FlexibleStartTimeLatest,
            HasFlexibleEndTime = entity.HasFlexibleEndTime,
            FlexibleEndTimeEarliest = entity.FlexibleEndTimeEarliest,
            FlexibleEndTimeLatest = entity.FlexibleEndTimeLatest,
            HasCoreHours = entity.HasCoreHours,
            CoreHoursStart = entity.CoreHoursStart,
            CoreHoursEnd = entity.CoreHoursEnd,
            HasMandatoryBreak = entity.HasMandatoryBreak,
            BreakDurationMinutes = entity.BreakDurationMinutes,
            IsBreakPaid = entity.IsBreakPaid,
            WorksMonday = entity.WorksMonday,
            WorksTuesday = entity.WorksTuesday,
            WorksWednesday = entity.WorksWednesday,
            WorksThursday = entity.WorksThursday,
            WorksFriday = entity.WorksFriday,
            WorksSaturday = entity.WorksSaturday,
            WorksSunday = entity.WorksSunday,
            AllowsOvertime = entity.AllowsOvertime,
            OvertimeRequiresPreApproval = entity.OvertimeRequiresPreApproval,
            MaxOvertimeHoursPerDay = entity.MaxOvertimeHoursPerDay,
            MaxOvertimeHoursPerWeek = entity.MaxOvertimeHoursPerWeek,
            LateGracePeriodMinutes = entity.LateGracePeriodMinutes,
            EarlyDepartureGracePeriodMinutes = entity.EarlyDepartureGracePeriodMinutes,
            Shifts = entity.Shifts.Select(s => s.ToSummaryDto()).ToList(),
        };
    }

    public static WorkScheduleSummaryDto ToSummaryDto(this WorkSchedule entity)
    {
        return new WorkScheduleSummaryDto
        {
            Id = entity.Id,
            ScheduleName = entity.ScheduleName,
            Type = entity.Type,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            StandardStartTime = entity.StandardStartTime,
            StandardEndTime = entity.StandardEndTime,
            StandardHoursPerDay = entity.StandardHoursPerDay,
            StandardHoursPerWeek = entity.StandardHoursPerWeek,
            ShiftCount = entity.Shifts.Count,
        };
    }

    public static WorkSchedule ToEntity(this CreateWorkScheduleDto dto, Guid tenantId, Guid userId)
    {
        return new WorkSchedule
        {
            TenantId = tenantId,
            ScheduleName = dto.ScheduleName,
            Description = dto.Description,
            Type = dto.Type,
            IsDefault = dto.IsDefault,
            IsActive = dto.IsActive,
            StandardStartTime = dto.StandardStartTime,
            StandardEndTime = dto.StandardEndTime,
            StandardHoursPerDay = dto.StandardHoursPerDay,
            StandardHoursPerWeek = dto.StandardHoursPerWeek,
            HasFlexibleStartTime = dto.HasFlexibleStartTime,
            FlexibleStartTimeEarliest = dto.FlexibleStartTimeEarliest,
            FlexibleStartTimeLatest = dto.FlexibleStartTimeLatest,
            HasFlexibleEndTime = dto.HasFlexibleEndTime,
            FlexibleEndTimeEarliest = dto.FlexibleEndTimeEarliest,
            FlexibleEndTimeLatest = dto.FlexibleEndTimeLatest,
            HasCoreHours = dto.HasCoreHours,
            CoreHoursStart = dto.CoreHoursStart,
            CoreHoursEnd = dto.CoreHoursEnd,
            HasMandatoryBreak = dto.HasMandatoryBreak,
            BreakDurationMinutes = dto.BreakDurationMinutes,
            IsBreakPaid = dto.IsBreakPaid,
            WorksMonday = dto.WorksMonday,
            WorksTuesday = dto.WorksTuesday,
            WorksWednesday = dto.WorksWednesday,
            WorksThursday = dto.WorksThursday,
            WorksFriday = dto.WorksFriday,
            WorksSaturday = dto.WorksSaturday,
            WorksSunday = dto.WorksSunday,
            AllowsOvertime = dto.AllowsOvertime,
            OvertimeRequiresPreApproval = dto.OvertimeRequiresPreApproval,
            MaxOvertimeHoursPerDay = dto.MaxOvertimeHoursPerDay,
            MaxOvertimeHoursPerWeek = dto.MaxOvertimeHoursPerWeek,
            LateGracePeriodMinutes = dto.LateGracePeriodMinutes,
            EarlyDepartureGracePeriodMinutes = dto.EarlyDepartureGracePeriodMinutes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this WorkSchedule entity, UpdateWorkScheduleDto dto, Guid userId)
    {
        entity.ScheduleName = dto.ScheduleName;
        entity.Description = dto.Description;
        entity.Type = dto.Type;
        entity.IsDefault = dto.IsDefault;
        entity.IsActive = dto.IsActive;
        entity.StandardStartTime = dto.StandardStartTime;
        entity.StandardEndTime = dto.StandardEndTime;
        entity.StandardHoursPerDay = dto.StandardHoursPerDay;
        entity.StandardHoursPerWeek = dto.StandardHoursPerWeek;
        entity.HasFlexibleStartTime = dto.HasFlexibleStartTime;
        entity.FlexibleStartTimeEarliest = dto.FlexibleStartTimeEarliest;
        entity.FlexibleStartTimeLatest = dto.FlexibleStartTimeLatest;
        entity.HasFlexibleEndTime = dto.HasFlexibleEndTime;
        entity.FlexibleEndTimeEarliest = dto.FlexibleEndTimeEarliest;
        entity.FlexibleEndTimeLatest = dto.FlexibleEndTimeLatest;
        entity.HasCoreHours = dto.HasCoreHours;
        entity.CoreHoursStart = dto.CoreHoursStart;
        entity.CoreHoursEnd = dto.CoreHoursEnd;
        entity.HasMandatoryBreak = dto.HasMandatoryBreak;
        entity.BreakDurationMinutes = dto.BreakDurationMinutes;
        entity.IsBreakPaid = dto.IsBreakPaid;
        entity.WorksMonday = dto.WorksMonday;
        entity.WorksTuesday = dto.WorksTuesday;
        entity.WorksWednesday = dto.WorksWednesday;
        entity.WorksThursday = dto.WorksThursday;
        entity.WorksFriday = dto.WorksFriday;
        entity.WorksSaturday = dto.WorksSaturday;
        entity.WorksSunday = dto.WorksSunday;
        entity.AllowsOvertime = dto.AllowsOvertime;
        entity.OvertimeRequiresPreApproval = dto.OvertimeRequiresPreApproval;
        entity.MaxOvertimeHoursPerDay = dto.MaxOvertimeHoursPerDay;
        entity.MaxOvertimeHoursPerWeek = dto.MaxOvertimeHoursPerWeek;
        entity.LateGracePeriodMinutes = dto.LateGracePeriodMinutes;
        entity.EarlyDepartureGracePeriodMinutes = dto.EarlyDepartureGracePeriodMinutes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<WorkScheduleSummaryDto> ToSummaryDtoList(this IEnumerable<WorkSchedule> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // EMPLOYEE WORK SCHEDULE
    // ========================================================================

    #region EmployeeWorkSchedule

    public static EmployeeWorkScheduleDto ToDto(this EmployeeWorkSchedule entity)
    {
        return new EmployeeWorkScheduleDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            WorkScheduleId = entity.WorkScheduleId,
            WorkScheduleName = entity.WorkSchedule?.ScheduleName ?? string.Empty,
            WorkScheduleType = entity.WorkSchedule?.Type ?? default,
            EffectiveDate = entity.EffectiveDate,
            EndDate = entity.EndDate,
            IsCurrent = entity.IsCurrent,
            AssignmentReason = entity.AssignmentReason,
            AssignedById = entity.AssignedById,
            AssignedByName = entity.AssignedBy?.FullName,
        };
    }

    public static EmployeeWorkSchedule ToEntity(this AssignEmployeeWorkScheduleDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeWorkSchedule
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            WorkScheduleId = dto.WorkScheduleId,
            EffectiveDate = dto.EffectiveDate,
            EndDate = dto.EndDate,
            IsCurrent = true,
            AssignmentReason = dto.AssignmentReason,
            AssignedById = dto.AssignedById,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeWorkSchedule entity, UpdateEmployeeWorkScheduleDto dto, Guid userId)
    {
        entity.WorkScheduleId = dto.WorkScheduleId;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.EndDate = dto.EndDate;
        entity.AssignmentReason = dto.AssignmentReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeWorkScheduleDto> ToDtoList(this IEnumerable<EmployeeWorkSchedule> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // SHIFT DEFINITION
    // ========================================================================

    #region ShiftDefinition

    public static ShiftDefinitionDto ToDto(this ShiftDefinition entity)
    {
        return new ShiftDefinitionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            WorkScheduleId = entity.WorkScheduleId,
            WorkScheduleName = entity.WorkSchedule?.ScheduleName ?? string.Empty,
            ShiftName = entity.ShiftName,
            Description = entity.Description,
            Type = entity.Type,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            ShiftHours = entity.ShiftHours,
            IsNightShift = entity.IsNightShift,
            AttractsNightAllowance = entity.AttractsNightAllowance,
            AllowsOvertime = entity.AllowsOvertime,
            HasShiftDifferential = entity.HasShiftDifferential,
            ShiftDifferentialPercentage = entity.ShiftDifferentialPercentage,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
        };
    }

    public static ShiftDefinitionSummaryDto ToSummaryDto(this ShiftDefinition entity)
    {
        return new ShiftDefinitionSummaryDto
        {
            Id = entity.Id,
            WorkScheduleId = entity.WorkScheduleId,
            WorkScheduleName = entity.WorkSchedule?.ScheduleName ?? string.Empty,
            ShiftName = entity.ShiftName,
            Type = entity.Type,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            ShiftHours = entity.ShiftHours,
            IsNightShift = entity.IsNightShift,
            IsActive = entity.IsActive,
            DisplayOrder = entity.DisplayOrder,
        };
    }

    public static ShiftDefinition ToEntity(this CreateShiftDefinitionDto dto, Guid tenantId, Guid userId)
    {
        return new ShiftDefinition
        {
            TenantId = tenantId,
            WorkScheduleId = dto.WorkScheduleId,
            ShiftName = dto.ShiftName,
            Description = dto.Description,
            Type = dto.Type,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            ShiftHours = dto.ShiftHours,
            IsNightShift = dto.IsNightShift,
            AttractsNightAllowance = dto.AttractsNightAllowance,
            AllowsOvertime = dto.AllowsOvertime,
            HasShiftDifferential = dto.HasShiftDifferential,
            ShiftDifferentialPercentage = dto.ShiftDifferentialPercentage,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ShiftDefinition entity, UpdateShiftDefinitionDto dto, Guid userId)
    {
        entity.ShiftName = dto.ShiftName;
        entity.Description = dto.Description;
        entity.Type = dto.Type;
        entity.StartTime = dto.StartTime;
        entity.EndTime = dto.EndTime;
        entity.ShiftHours = dto.ShiftHours;
        entity.IsNightShift = dto.IsNightShift;
        entity.AttractsNightAllowance = dto.AttractsNightAllowance;
        entity.AllowsOvertime = dto.AllowsOvertime;
        entity.HasShiftDifferential = dto.HasShiftDifferential;
        entity.ShiftDifferentialPercentage = dto.ShiftDifferentialPercentage;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ShiftDefinitionSummaryDto> ToSummaryDtoList(this IEnumerable<ShiftDefinition> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // SHIFT ASSIGNMENT
    // ========================================================================

    #region ShiftAssignment

    public static ShiftAssignmentDto ToDto(this ShiftAssignment entity)
    {
        return new ShiftAssignmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            ShiftDefinitionId = entity.ShiftDefinitionId,
            ShiftName = entity.ShiftDefinition?.ShiftName ?? string.Empty,
            ShiftStartTime = entity.ShiftDefinition?.StartTime ?? default,
            ShiftEndTime = entity.ShiftDefinition?.EndTime ?? default,
            AssignmentDate = entity.AssignmentDate,
            EndDate = entity.EndDate,
            IsRecurring = entity.IsRecurring,
            RecurrencePattern = entity.RecurrencePattern,
            AssignedById = entity.AssignedById,
            AssignedByName = entity.AssignedBy?.FullName,
            Notes = entity.Notes,
        };
    }

    public static ShiftAssignment ToEntity(this CreateShiftAssignmentDto dto, Guid tenantId, Guid userId)
    {
        return new ShiftAssignment
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            ShiftDefinitionId = dto.ShiftDefinitionId,
            AssignmentDate = dto.AssignmentDate,
            EndDate = dto.EndDate,
            IsRecurring = dto.IsRecurring,
            RecurrencePattern = dto.RecurrencePattern,
            AssignedById = dto.AssignedById,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ShiftAssignment entity, UpdateShiftAssignmentDto dto, Guid userId)
    {
        entity.ShiftDefinitionId = dto.ShiftDefinitionId;
        entity.AssignmentDate = dto.AssignmentDate;
        entity.EndDate = dto.EndDate;
        entity.IsRecurring = dto.IsRecurring;
        entity.RecurrencePattern = dto.RecurrencePattern;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ShiftAssignmentDto> ToDtoList(this IEnumerable<ShiftAssignment> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // SHIFT ROTATION PLAN
    // ========================================================================

    #region ShiftRotationPlan

    public static ShiftRotationPlanDto ToDto(this ShiftRotationPlan entity)
    {
        return new ShiftRotationPlanDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PlanName = entity.PlanName,
            Description = entity.Description,
            RotationCycle = entity.RotationCycle,
            CycleLengthDays = entity.CycleLengthDays,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsActive = entity.IsActive,
            Notes = entity.Notes,
            Stages = entity.Stages.Select(s => s.ToDto()).ToList(),
            Members = entity.Members.Select(m => m.ToDto()).ToList(),
        };
    }

    public static ShiftRotationPlanSummaryDto ToSummaryDto(this ShiftRotationPlan entity)
    {
        return new ShiftRotationPlanSummaryDto
        {
            Id = entity.Id,
            PlanName = entity.PlanName,
            RotationCycle = entity.RotationCycle,
            CycleLengthDays = entity.CycleLengthDays,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsActive = entity.IsActive,
            StageCount = entity.Stages.Count,
            MemberCount = entity.Members.Count,
        };
    }

    public static ShiftRotationPlan ToEntity(this CreateShiftRotationPlanDto dto, Guid tenantId, Guid userId)
    {
        return new ShiftRotationPlan
        {
            TenantId = tenantId,
            PlanName = dto.PlanName,
            Description = dto.Description,
            RotationCycle = dto.RotationCycle,
            CycleLengthDays = dto.CycleLengthDays,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ShiftRotationPlan entity, UpdateShiftRotationPlanDto dto, Guid userId)
    {
        entity.PlanName = dto.PlanName;
        entity.Description = dto.Description;
        entity.EndDate = dto.EndDate;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ShiftRotationPlanSummaryDto> ToSummaryDtoList(this IEnumerable<ShiftRotationPlan> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // SHIFT ROTATION STAGE
    // ========================================================================

    #region ShiftRotationStage

    public static ShiftRotationStageDto ToDto(this ShiftRotationStage entity)
    {
        return new ShiftRotationStageDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ShiftRotationPlanId = entity.ShiftRotationPlanId,
            StageOrder = entity.StageOrder,
            ShiftDefinitionId = entity.ShiftDefinitionId,
            ShiftName = entity.ShiftDefinition?.ShiftName ?? string.Empty,
            ShiftStartTime = entity.ShiftDefinition?.StartTime ?? default,
            ShiftEndTime = entity.ShiftDefinition?.EndTime ?? default,
            DurationCycles = entity.DurationCycles,
            Label = entity.Label,
        };
    }

    public static ShiftRotationStage ToEntity(this CreateShiftRotationStageDto dto, Guid tenantId, Guid userId)
    {
        return new ShiftRotationStage
        {
            TenantId = tenantId,
            ShiftRotationPlanId = dto.ShiftRotationPlanId,
            StageOrder = dto.StageOrder,
            ShiftDefinitionId = dto.ShiftDefinitionId,
            DurationCycles = dto.DurationCycles,
            Label = dto.Label,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ShiftRotationStage entity, UpdateShiftRotationStageDto dto, Guid userId)
    {
        entity.StageOrder = dto.StageOrder;
        entity.ShiftDefinitionId = dto.ShiftDefinitionId;
        entity.DurationCycles = dto.DurationDays; // DurationDays mapped to DurationCycles
        entity.Label = dto.Notes;                 // Notes used as Label
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // SHIFT ROTATION MEMBER
    // ========================================================================

    #region ShiftRotationMember

    public static ShiftRotationMemberDto ToDto(this ShiftRotationMember entity)
    {
        return new ShiftRotationMemberDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ShiftRotationPlanId = entity.ShiftRotationPlanId,
            PlanName = entity.ShiftRotationPlan?.PlanName ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            TeamId = entity.TeamId,
            TeamName = entity.Team?.Name,
            CurrentStageOrder = entity.CurrentStageOrder,
            JoinDate = entity.JoinDate,
            ExitDate = entity.ExitDate,
            Notes = entity.Notes,
        };
    }

    public static ShiftRotationMember ToEntity(this AddShiftRotationMemberDto dto, Guid tenantId, Guid userId)
    {
        return new ShiftRotationMember
        {
            TenantId = tenantId,
            ShiftRotationPlanId = dto.ShiftRotationPlanId,
            EmployeeId = dto.EmployeeId,
            OrganizationUnitId = dto.OrganizationUnitId,
            TeamId = dto.TeamId,
            CurrentStageOrder = 1,
            JoinDate = dto.JoinDate,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ShiftRotationMember entity, UpdateShiftRotationMemberDto dto, Guid userId)
    {
        entity.CurrentStageOrder = dto.CurrentStageOrder;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // POSITION OVERTIME POLICY
    // ========================================================================

    #region PositionOvertimePolicy

    public static PositionOvertimePolicyDto ToDto(this PositionOvertimePolicy entity)
    {
        return new PositionOvertimePolicyDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            AllowanceType = entity.AllowanceType,
            IsEligible = entity.IsEligible,
            IsExempt = entity.IsExempt,
            ExemptionReason = entity.ExemptionReason,
            MaxHoursPerDay = entity.MaxHoursPerDay,
            MaxHoursPerWeek = entity.MaxHoursPerWeek,
            RequiresPreApproval = entity.RequiresPreApproval,
            EffectiveDate = entity.EffectiveDate,
            ExpiryDate = entity.ExpiryDate,
            Notes = entity.Notes,
        };
    }

    public static PositionOvertimePolicy ToEntity(this CreatePositionOvertimePolicyDto dto, Guid tenantId, Guid userId)
    {
        return new PositionOvertimePolicy
        {
            TenantId = tenantId,
            PositionId = dto.PositionId,
            AllowanceType = dto.AllowanceType,
            IsEligible = dto.IsEligible,
            IsExempt = dto.IsExempt,
            ExemptionReason = dto.ExemptionReason,
            MaxHoursPerDay = dto.MaxHoursPerDay,
            MaxHoursPerWeek = dto.MaxHoursPerWeek,
            RequiresPreApproval = dto.RequiresPreApproval,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this PositionOvertimePolicy entity, UpdatePositionOvertimePolicyDto dto, Guid userId)
    {
        entity.IsEligible = dto.IsEligible;
        entity.IsExempt = dto.IsExempt;
        entity.ExemptionReason = dto.ExemptionReason;
        entity.MaxHoursPerDay = dto.MaxHoursPerDay;
        entity.MaxHoursPerWeek = dto.MaxHoursPerWeek;
        entity.RequiresPreApproval = dto.RequiresPreApproval;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<PositionOvertimePolicyDto> ToDtoList(this IEnumerable<PositionOvertimePolicy> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // EMPLOYEE OVERTIME OVERRIDE
    // ========================================================================

    #region EmployeeOvertimeOverride

    public static EmployeeOvertimeOverrideDto ToDto(this EmployeeOvertimeOverride entity)
    {
        return new EmployeeOvertimeOverrideDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            PolicyId = entity.PolicyId,
            PolicyDescription = entity.Policy != null
                ? $"{entity.Policy.AllowanceType} – {entity.Policy.Position?.Title}"
                : null,
            AllowanceType = entity.AllowanceType,
            IsEligible = entity.IsEligible,
            IsExempt = entity.IsExempt,
            OverrideReason = entity.OverrideReason,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName ?? string.Empty,
            ApprovalDate = entity.ApprovalDate,
            EffectiveDate = entity.EffectiveDate,
            ExpiryDate = entity.ExpiryDate,
        };
    }

    public static EmployeeOvertimeOverride ToEntity(this CreateEmployeeOvertimeOverrideDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeOvertimeOverride
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            PolicyId = dto.PolicyId,
            AllowanceType = dto.AllowanceType,
            IsEligible = dto.IsEligible,
            IsExempt = dto.IsExempt,
            OverrideReason = dto.OverrideReason,
            ApprovedById = dto.ApprovedById,
            ApprovalDate = DateTime.UtcNow,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeOvertimeOverride entity, UpdateEmployeeOvertimeOverrideDto dto, Guid userId)
    {
        entity.IsEligible = dto.IsEligible;
        entity.IsExempt = dto.IsExempt;
        entity.OverrideReason = dto.OverrideReason;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeOvertimeOverrideDto> ToDtoList(this IEnumerable<EmployeeOvertimeOverride> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // STAFF OVERTIME REQUEST
    // ========================================================================

    #region StaffOvertimeRequest

    public static StaffOvertimeRequestDto ToDto(this StaffOvertimeRequest entity)
    {
        return new StaffOvertimeRequestDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            RequestNumber = entity.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            RequestDate = entity.RequestDate,
            OvertimeDate = entity.OvertimeDate,
            PlannedStartTime = entity.PlannedStartTime,
            PlannedEndTime = entity.PlannedEndTime,
            PlannedOvertimeHours = entity.PlannedOvertimeHours,
            Purpose = entity.Purpose,
            TaskDetails = entity.TaskDetails,
            Type = entity.Type,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            ApprovalComments = entity.ApprovalComments,
            RejectedDate = entity.RejectedDate,
            RejectionReason = entity.RejectionReason,
            ActualOvertimeHours = entity.ActualOvertimeHours,
            SupervisorConfirmedById = entity.SupervisorConfirmedById,
            SupervisorConfirmedByName = entity.SupervisorConfirmedBy?.FullName,
            SupervisorConfirmedDate = entity.SupervisorConfirmedDate,
            SupervisorNotes = entity.SupervisorNotes,
            AttendanceId = entity.AttendanceId,
        };
    }

    public static StaffOvertimeRequestSummaryDto ToSummaryDto(this StaffOvertimeRequest entity)
    {
        return new StaffOvertimeRequestSummaryDto
        {
            Id = entity.Id,
            RequestNumber = entity.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            OvertimeDate = entity.OvertimeDate,
            PlannedOvertimeHours = entity.PlannedOvertimeHours,
            ActualOvertimeHours = entity.ActualOvertimeHours,
            Type = entity.Type,
            Status = entity.Status,
            RequestDate = entity.RequestDate,
        };
    }

    public static StaffOvertimeRequest ToEntity(this CreateStaffOvertimeRequestDto dto, Guid tenantId, Guid userId)
    {
        return new StaffOvertimeRequest
        {
            TenantId = tenantId,
            RequestNumber = string.Empty, // generated by service
            EmployeeId = dto.EmployeeId,
            RequestDate = DateTime.UtcNow,
            OvertimeDate = dto.OvertimeDate,
            PlannedStartTime = dto.PlannedStartTime,
            PlannedEndTime = dto.PlannedEndTime,
            PlannedOvertimeHours = dto.PlannedOvertimeHours,
            Purpose = dto.Purpose,
            TaskDetails = dto.TaskDetails,
            Type = dto.Type,
            Status = OvertimeRequestStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffOvertimeRequest entity, UpdateStaffOvertimeRequestDto dto, Guid userId)
    {
        entity.PlannedOvertimeHours = dto.RequestedHours;
        entity.Purpose = dto.Reason;
        entity.TaskDetails = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffOvertimeRequestSummaryDto> ToSummaryDtoList(this IEnumerable<StaffOvertimeRequest> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // EMPLOYEE BIOMETRIC
    // ========================================================================

    #region EmployeeBiometric

    public static EmployeeBiometricDto ToDto(this EmployeeBiometric entity)
    {
        return new EmployeeBiometricDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            BiometricType = entity.BiometricType,
            BodyPart = entity.BodyPart,
            FingerPosition = entity.FingerPosition,
            TemplateFormat = entity.TemplateFormat,
            QualityScore = entity.QualityScore,
            DeviceId = entity.DeviceId,
            DeviceModel = entity.DeviceModel,
            EnrolledDate = entity.EnrolledDate,
            EnrolledById = entity.EnrolledById,
            EnrolledByName = entity.EnrolledBy?.FullName,
            IsActive = entity.IsActive,
            RevokedDate = entity.RevokedDate,
            RevokedReason = entity.RevokedReason,
        };
    }

    public static EmployeeBiometricSummaryDto ToSummaryDto(this EmployeeBiometric entity)
    {
        return new EmployeeBiometricSummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            BiometricType = entity.BiometricType,
            BodyPart = entity.BodyPart,
            FingerPosition = entity.FingerPosition,
            QualityScore = entity.QualityScore,
            EnrolledDate = entity.EnrolledDate,
            IsActive = entity.IsActive,
        };
    }

    public static EmployeeBiometric ToEntity(this EnrollBiometricDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeBiometric
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            BiometricType = dto.BiometricType,
            BodyPart = dto.BodyPart,
            FingerPosition = dto.FingerPosition,
            BiometricData = dto.BiometricData,
            TemplateFormat = dto.TemplateFormat,
            QualityScore = dto.QualityScore,
            DeviceId = dto.DeviceId,
            DeviceModel = dto.DeviceModel,
            EnrolledDate = DateTime.UtcNow,
            EnrolledById = dto.EnrolledById,
            IsActive = true,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeBiometric entity, UpdateEmployeeBiometricDto dto, Guid userId)
    {
        if (dto.BiometricData != null) entity.BiometricData = dto.BiometricData;
        entity.TemplateFormat = dto.TemplateFormat;
        entity.QualityScore = dto.QualityScore;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeBiometricSummaryDto> ToSummaryDtoList(this IEnumerable<EmployeeBiometric> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF ATTENDANCE DEVICE
    // ========================================================================

    #region StaffAttendanceDevice

    public static StaffAttendanceDeviceDto ToDto(this StaffAttendanceDevice entity)
    {
        return new StaffAttendanceDeviceDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DeviceId = entity.DeviceId,
            DeviceName = entity.DeviceName,
            DeviceModel = entity.DeviceModel,
            Manufacturer = entity.Manufacturer,
            FirmwareVersion = entity.FirmwareVersion,
            DeviceType = entity.DeviceType,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            LocationDescription = entity.LocationDescription,
            IpAddress = entity.IpAddress,
            Port = entity.Port,
            IsActive = entity.IsActive,
            LastSyncDate = entity.LastSyncDate,
            PendingSyncCount = entity.PendingSyncCount,
            Notes = entity.Notes,
        };
    }

    public static StaffAttendanceDeviceSummaryDto ToSummaryDto(this StaffAttendanceDevice entity)
    {
        return new StaffAttendanceDeviceSummaryDto
        {
            Id = entity.Id,
            DeviceId = entity.DeviceId,
            DeviceName = entity.DeviceName,
            DeviceType = entity.DeviceType,
            LocationName = entity.Location?.Name,
            LocationDescription = entity.LocationDescription,
            IsActive = entity.IsActive,
            LastSyncDate = entity.LastSyncDate,
            PendingSyncCount = entity.PendingSyncCount,
        };
    }

    public static StaffAttendanceDevice ToEntity(this CreateStaffAttendanceDeviceDto dto, Guid tenantId, Guid userId)
    {
        return new StaffAttendanceDevice
        {
            TenantId = tenantId,
            DeviceId = dto.DeviceId,
            DeviceName = dto.DeviceName,
            DeviceModel = dto.DeviceModel,
            Manufacturer = dto.Manufacturer,
            FirmwareVersion = dto.FirmwareVersion,
            DeviceType = dto.DeviceType,
            LocationId = dto.LocationId,
            LocationDescription = dto.LocationDescription,
            IpAddress = dto.IpAddress,
            Port = dto.Port,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffAttendanceDevice entity, UpdateStaffAttendanceDeviceDto dto, Guid userId)
    {
        entity.DeviceName = dto.DeviceName;
        entity.DeviceModel = dto.DeviceModel;
        entity.FirmwareVersion = dto.FirmwareVersion;
        entity.LocationId = dto.LocationId;
        entity.LocationDescription = dto.LocationDescription;
        entity.IpAddress = dto.IpAddress;
        entity.Port = dto.Port;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffAttendanceDeviceSummaryDto> ToSummaryDtoList(this IEnumerable<StaffAttendanceDevice> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // GEOFENCE ZONE
    // ========================================================================

    #region GeofenceZone

    public static GeofenceZoneDto ToDto(this GeofenceZone entity)
    {
        return new GeofenceZoneDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ZoneName = entity.ZoneName,
            Description = entity.Description,
            Shape = entity.Shape,
            CentreLatitude = entity.CentreLatitude,
            CentreLongitude = entity.CentreLongitude,
            RadiusMetres = entity.RadiusMetres,
            PolygonCoordinatesJson = entity.PolygonCoordinatesJson,
            SoftEnforcement = entity.SoftEnforcement,
            HardEnforcement = entity.HardEnforcement,
            IsActive = entity.IsActive,
            Notes = entity.Notes,
        };
    }

    public static GeofenceZoneSummaryDto ToSummaryDto(this GeofenceZone entity)
    {
        return new GeofenceZoneSummaryDto
        {
            Id = entity.Id,
            ZoneName = entity.ZoneName,
            Shape = entity.Shape,
            CentreLatitude = entity.CentreLatitude,
            CentreLongitude = entity.CentreLongitude,
            RadiusMetres = entity.RadiusMetres,
            PolygonCoordinatesJson = entity.PolygonCoordinatesJson,
            Description = entity.Description,
            Notes = entity.Notes,
            SoftEnforcement = entity.SoftEnforcement,
            HardEnforcement = entity.HardEnforcement,
            IsActive = entity.IsActive,
        };
    }

    public static GeofenceZone ToEntity(this CreateGeofenceZoneDto dto, Guid tenantId, Guid userId)
    {
        return new GeofenceZone
        {
            TenantId = tenantId,
            ZoneName = dto.ZoneName,
            Description = dto.Description,
            Shape = dto.Shape,
            CentreLatitude = dto.CentreLatitude,
            CentreLongitude = dto.CentreLongitude,
            RadiusMetres = dto.RadiusMetres,
            PolygonCoordinatesJson = dto.PolygonCoordinatesJson,
            SoftEnforcement = dto.SoftEnforcement,
            HardEnforcement = dto.HardEnforcement,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this GeofenceZone entity, UpdateGeofenceZoneDto dto, Guid userId)
    {
        entity.ZoneName = dto.ZoneName;
        entity.Description = dto.Description;
        entity.Shape = dto.Shape;
        entity.CentreLatitude = dto.CentreLatitude;
        entity.CentreLongitude = dto.CentreLongitude;
        entity.RadiusMetres = dto.RadiusMetres;
        entity.PolygonCoordinatesJson = dto.PolygonCoordinatesJson;
        entity.SoftEnforcement = dto.SoftEnforcement;
        entity.HardEnforcement = dto.HardEnforcement;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<GeofenceZoneSummaryDto> ToSummaryDtoList(this IEnumerable<GeofenceZone> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // REMOTE WORK REQUEST
    // ========================================================================

    #region RemoteWorkRequest

    public static RemoteWorkRequestDto ToDto(this RemoteWorkRequest entity)
    {
        return new RemoteWorkRequestDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            RequestNumber = entity.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            RequestDate = entity.RequestDate,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            RequestedDays = entity.RequestedDays,
            Reason = entity.Reason,
            RemoteLocation = entity.RemoteLocation,
            EquipmentConfirmed = entity.EquipmentConfirmed,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            ApprovalComments = entity.ApprovalComments,
            RejectedDate = entity.RejectedDate,
            RejectionReason = entity.RejectionReason,
        };
    }

    public static RemoteWorkRequestSummaryDto ToSummaryDto(this RemoteWorkRequest entity)
    {
        return new RemoteWorkRequestSummaryDto
        {
            Id = entity.Id,
            RequestNumber = entity.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            RequestedDays = entity.RequestedDays,
            Status = entity.Status,
            RequestDate = entity.RequestDate,
        };
    }

    public static RemoteWorkRequest ToEntity(this CreateRemoteWorkRequestDto dto, Guid tenantId, Guid userId)
    {
        return new RemoteWorkRequest
        {
            TenantId = tenantId,
            RequestNumber = string.Empty, // generated by service
            EmployeeId = dto.EmployeeId,
            RequestDate = DateTime.UtcNow,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            RequestedDays = dto.StartDate.DayNumber - dto.EndDate.DayNumber == 0
                ? 1
                : Math.Abs(dto.EndDate.DayNumber - dto.StartDate.DayNumber) + 1,
            Reason = dto.Reason,
            RemoteLocation = dto.RemoteLocation,
            EquipmentConfirmed = dto.EquipmentConfirmed,
            Status = RemoteWorkRequestStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this RemoteWorkRequest entity, UpdateRemoteWorkRequestDto dto, Guid userId)
    {
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.RemoteLocation = dto.WorkLocation;
        entity.Reason = dto.Reason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<RemoteWorkRequestSummaryDto> ToSummaryDtoList(this IEnumerable<RemoteWorkRequest> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // HOLIDAY CALENDAR
    // ========================================================================

    #region HolidayCalendar

    public static HolidayCalendarDto ToDto(this HolidayCalendar entity)
    {
        return new HolidayCalendarDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CalendarName = entity.CalendarName,
            Description = entity.Description,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name,
            Region = entity.Region,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            PublicHolidays = entity.PublicHolidays.Select(h => h.ToSummaryDto()).ToList(),
        };
    }

    public static HolidayCalendarSummaryDto ToSummaryDto(this HolidayCalendar entity)
    {
        return new HolidayCalendarSummaryDto
        {
            Id = entity.Id,
            CalendarName = entity.CalendarName,
            CountryName = entity.Country?.Name,
            Region = entity.Region,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            HolidayCount = entity.PublicHolidays.Count,
        };
    }

    public static HolidayCalendar ToEntity(this CreateHolidayCalendarDto dto, Guid tenantId, Guid userId)
    {
        return new HolidayCalendar
        {
            TenantId = tenantId,
            CalendarName = dto.CalendarName,
            Description = dto.Description,
            CountryId = dto.CountryId,
            Region = dto.Region,
            IsDefault = dto.IsDefault,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this HolidayCalendar entity, UpdateHolidayCalendarDto dto, Guid userId)
    {
        entity.CalendarName = dto.CalendarName;
        entity.Description = dto.Description;
        entity.CountryId = dto.CountryId;
        entity.Region = dto.Region;
        entity.IsDefault = dto.IsDefault;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<HolidayCalendarSummaryDto> ToSummaryDtoList(this IEnumerable<HolidayCalendar> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // PUBLIC HOLIDAY
    // ========================================================================

    #region PublicHoliday

    public static PublicHolidayDto ToDto(this PublicHoliday entity)
    {
        return new PublicHolidayDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            HolidayCalendarId = entity.HolidayCalendarId,
            CalendarName = entity.HolidayCalendar?.CalendarName ?? string.Empty,
            HolidayName = entity.HolidayName,
            Description = entity.Description,
            DateFrom = entity.DateFrom,
            DateTo = entity.DateTo,
            Year = entity.Year,
            ObservanceType = entity.ObservanceType,
            SubstitutionDate = entity.SubstitutionDate,
            AttractsHolidayPay = entity.AttractsHolidayPay,
            HolidayPayMultiplier = entity.HolidayPayMultiplier,
            IsRecurringAnnually = entity.IsRecurringAnnually,
            IsActive = entity.IsActive,
        };
    }

    public static PublicHolidaySummaryDto ToSummaryDto(this PublicHoliday entity)
    {
        return new PublicHolidaySummaryDto
        {
            Id = entity.Id,
            HolidayName = entity.HolidayName,
            DateFrom = entity.DateFrom,
            DateTo = entity.DateTo,
            Year = entity.Year,
            ObservanceType = entity.ObservanceType,
            AttractsHolidayPay = entity.AttractsHolidayPay,
            IsActive = entity.IsActive,
        };
    }

    public static PublicHoliday ToEntity(this CreatePublicHolidayDto dto, Guid tenantId, Guid userId)
    {
        return new PublicHoliday
        {
            TenantId = tenantId,
            HolidayCalendarId = dto.HolidayCalendarId,
            HolidayName = dto.HolidayName,
            Description = dto.Description,
            DateFrom = dto.DateFrom,
            DateTo = dto.DateTo,
            ObservanceType = dto.ObservanceType,
            SubstitutionDate = dto.SubstitutionDate,
            AttractsHolidayPay = dto.AttractsHolidayPay,
            HolidayPayMultiplier = dto.HolidayPayMultiplier,
            IsRecurringAnnually = dto.IsRecurringAnnually,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this PublicHoliday entity, UpdatePublicHolidayDto dto, Guid userId)
    {
        entity.HolidayName = dto.HolidayName;
        entity.Description = dto.Description;
        entity.DateFrom = dto.DateFrom;
        entity.DateTo = dto.DateTo;
        entity.ObservanceType = dto.ObservanceType;
        entity.SubstitutionDate = dto.SubstitutionDate;
        entity.AttractsHolidayPay = dto.AttractsHolidayPay;
        entity.HolidayPayMultiplier = dto.HolidayPayMultiplier;
        entity.IsRecurringAnnually = dto.IsRecurringAnnually;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<PublicHolidaySummaryDto> ToSummaryDtoList(this IEnumerable<PublicHoliday> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<PublicHolidayDto> ToDtoList(this IEnumerable<PublicHoliday> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // PAY PERIOD
    // ========================================================================

    #region PayPeriod

    public static PayPeriodDto ToDto(this PayPeriod entity)
    {
        return new PayPeriodDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PeriodName = entity.PeriodName,
            Type = entity.Type,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            ClosedDate = entity.ClosedDate,
            ClosedById = entity.ClosedById,
            ClosedByName = entity.ClosedBy?.FullName,
            ExportedDate = entity.ExportedDate,
            ExportedById = entity.ExportedById,
            ExportedByName = entity.ExportedBy?.FullName,
            Notes = entity.Notes,
            SummaryCount = entity.AttendanceSummaries.Count,
            ExportCount = entity.PayrollExports.Count,
        };
    }

    public static PayPeriodSummaryDto ToSummaryDto(this PayPeriod entity)
    {
        return new PayPeriodSummaryDto
        {
            Id = entity.Id,
            PeriodName = entity.PeriodName,
            Type = entity.Type,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            ClosedDate = entity.ClosedDate,
            ExportedDate = entity.ExportedDate,
        };
    }

    public static PayPeriod ToEntity(this CreatePayPeriodDto dto, Guid tenantId, Guid userId)
    {
        return new PayPeriod
        {
            TenantId = tenantId,
            PeriodName = dto.PeriodName,
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = PayPeriodStatus.Open,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this PayPeriod entity, UpdatePayPeriodDto dto, Guid userId)
    {
        entity.PeriodName = dto.PeriodName;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<PayPeriodSummaryDto> ToSummaryDtoList(this IEnumerable<PayPeriod> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF ATTENDANCE PAYROLL EXPORT
    // ========================================================================

    #region StaffAttendancePayrollExport

    public static StaffAttendancePayrollExportDto ToDto(this StaffAttendancePayrollExport entity)
    {
        return new StaffAttendancePayrollExportDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ExportReference = entity.ExportReference,
            PayPeriodId = entity.PayPeriodId,
            PayPeriodName = entity.PayPeriod?.PeriodName ?? string.Empty,
            ExportDate = entity.ExportDate,
            ExportedById = entity.ExportedById,
            ExportedByName = entity.ExportedBy?.FullName ?? string.Empty,
            TargetSystem = entity.TargetSystem,
            TotalEmployees = entity.TotalEmployees,
            TotalRecords = entity.TotalRecords,
            Status = entity.Status,
            ErrorDetails = entity.ErrorDetails,
            Notes = entity.Notes,
        };
    }

    public static StaffAttendancePayrollExportSummaryDto ToSummaryDto(this StaffAttendancePayrollExport entity)
    {
        return new StaffAttendancePayrollExportSummaryDto
        {
            Id = entity.Id,
            ExportReference = entity.ExportReference,
            PayPeriodName = entity.PayPeriod?.PeriodName ?? string.Empty,
            ExportDate = entity.ExportDate,
            ExportedByName = entity.ExportedBy?.FullName ?? string.Empty,
            TargetSystem = entity.TargetSystem,
            TotalEmployees = entity.TotalEmployees,
            TotalRecords = entity.TotalRecords,
            Status = entity.Status,
        };
    }

    public static StaffAttendancePayrollExport ToEntity(this CreateStaffAttendancePayrollExportDto dto, Guid tenantId, Guid userId)
    {
        return new StaffAttendancePayrollExport
        {
            TenantId = tenantId,
            ExportReference = string.Empty, // generated by service
            PayPeriodId = dto.PayPeriodId,
            ExportDate = DateTime.UtcNow,
            ExportedById = dto.ExportedById,
            TargetSystem = dto.TargetSystem,
            Status = PayrollExportStatus.Pending,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<StaffAttendancePayrollExportSummaryDto> ToSummaryDtoList(this IEnumerable<StaffAttendancePayrollExport> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF ATTENDANCE ALERT RULE
    // ========================================================================

    #region StaffAttendanceAlertRule

    public static StaffAttendanceAlertRuleDto ToDto(this StaffAttendanceAlertRule entity)
    {
        return new StaffAttendanceAlertRuleDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            RuleName = entity.RuleName,
            Description = entity.Description,
            TriggerType = entity.TriggerType,
            Severity = entity.Severity,
            ThresholdValue = entity.ThresholdValue,
            EvaluationWindowDays = entity.EvaluationWindowDays,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            NotifyByEmail = entity.NotifyByEmail,
            NotifyInApp = entity.NotifyInApp,
            NotifyRecipientsJson = entity.NotifyRecipientsJson,
            RequiresAcknowledgement = entity.RequiresAcknowledgement,
            IsActive = entity.IsActive,
            ActiveAlertCount = entity.Alerts.Count(a => a.Status == AttendanceAlertStatus.Active),
        };
    }

    public static StaffAttendanceAlertRuleSummaryDto ToSummaryDto(this StaffAttendanceAlertRule entity)
    {
        return new StaffAttendanceAlertRuleSummaryDto
        {
            Id = entity.Id,
            RuleName = entity.RuleName,
            TriggerType = entity.TriggerType,
            Severity = entity.Severity,
            ThresholdValue = entity.ThresholdValue,
            IsActive = entity.IsActive,
            ActiveAlertCount = entity.Alerts.Count(a => a.Status == AttendanceAlertStatus.Active),
        };
    }

    public static StaffAttendanceAlertRule ToEntity(this CreateStaffAttendanceAlertRuleDto dto, Guid tenantId, Guid userId)
    {
        return new StaffAttendanceAlertRule
        {
            TenantId = tenantId,
            RuleName = dto.RuleName,
            Description = dto.Description,
            TriggerType = dto.TriggerType,
            Severity = dto.Severity,
            ThresholdValue = dto.ThresholdValue,
            EvaluationWindowDays = dto.EvaluationWindowDays,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            EmployeeId = dto.EmployeeId,
            NotifyByEmail = dto.NotifyByEmail,
            NotifyInApp = dto.NotifyInApp,
            NotifyRecipientsJson = dto.NotifyRecipientsJson,
            RequiresAcknowledgement = dto.RequiresAcknowledgement,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffAttendanceAlertRule entity, UpdateStaffAttendanceAlertRuleDto dto, Guid userId)
    {
        entity.RuleName = dto.RuleName;
        entity.Description = dto.Description;
        entity.Severity = dto.Severity;
        entity.ThresholdValue = dto.ThresholdValue;
        entity.EvaluationWindowDays = dto.EvaluationWindowDays;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.PositionId = dto.PositionId;
        entity.EmployeeId = dto.EmployeeId;
        entity.NotifyByEmail = dto.NotifyByEmail;
        entity.NotifyInApp = dto.NotifyInApp;
        entity.NotifyRecipientsJson = dto.NotifyRecipientsJson;
        entity.RequiresAcknowledgement = dto.RequiresAcknowledgement;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffAttendanceAlertRuleSummaryDto> ToSummaryDtoList(this IEnumerable<StaffAttendanceAlertRule> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF ATTENDANCE ALERT
    // ========================================================================

    #region StaffAttendanceAlert

    public static StaffAttendanceAlertDto ToDto(this StaffAttendanceAlert entity)
    {
        return new StaffAttendanceAlertDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            AlertRuleId = entity.AlertRuleId,
            RuleName = entity.AlertRule?.RuleName ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            TriggerType = entity.TriggerType,
            Severity = entity.Severity,
            Status = entity.Status,
            TriggeredDate = entity.TriggeredDate,
            TriggerDescription = entity.TriggerDescription,
            TriggerValue = entity.TriggerValue,
            AcknowledgedById = entity.AcknowledgedById,
            AcknowledgedByName = entity.AcknowledgedBy?.FullName,
            AcknowledgedDate = entity.AcknowledgedDate,
            AcknowledgementNotes = entity.AcknowledgementNotes,
            ResolvedById = entity.ResolvedById,
            ResolvedByName = entity.ResolvedBy?.FullName,
            ResolvedDate = entity.ResolvedDate,
            ResolutionNotes = entity.ResolutionNotes,
            DismissedById = entity.DismissedById,
            DismissedByName = entity.DismissedBy?.FullName,
            DismissedDate = entity.DismissedDate,
            DismissalReason = entity.DismissalReason,
        };
    }

    public static StaffAttendanceAlertSummaryDto ToSummaryDto(this StaffAttendanceAlert entity)
    {
        return new StaffAttendanceAlertSummaryDto
        {
            Id = entity.Id,
            RuleName = entity.AlertRule?.RuleName ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            TriggerType = entity.TriggerType,
            Severity = entity.Severity,
            Status = entity.Status,
            TriggeredDate = entity.TriggeredDate,
            TriggerDescription = entity.TriggerDescription,
        };
    }

    public static IEnumerable<StaffAttendanceAlertSummaryDto> ToSummaryDtoList(this IEnumerable<StaffAttendanceAlert> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // CONSULTANT CLIENT
    // ========================================================================

    #region ConsultantClient

    public static ConsultantClientDto ToDto(this ConsultantClient entity)
    {
        return new ConsultantClientDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ClientName = entity.ClientName,
            ClientCode = entity.ClientCode,
            Industry = entity.Industry,
            Description = entity.Description,
            PrimaryContactName = entity.PrimaryContactName,
            PrimaryContactEmail = entity.PrimaryContactEmail,
            PrimaryContactPhone = entity.PrimaryContactPhone,
            AddressLine1 = entity.AddressLine1,
            AddressLine2 = entity.AddressLine2,
            City = entity.City,
            Region = entity.Region,
            PostalCode = entity.PostalCode,
            CountryId = entity.CountryId,
            FinanceCustomerId = entity.FinanceCustomerId,
            CountryName = entity.Country?.Name,
            BillingContactName = entity.BillingContactName,
            BillingContactEmail = entity.BillingContactEmail,
            BillingContactPhone = entity.BillingContactPhone,
            TaxIdentificationNumber = entity.TaxIdentificationNumber,
            Currency = entity.Currency,
            DefaultPaymentTermsDays = entity.DefaultPaymentTermsDays,
            IsActive = entity.IsActive,
            Notes = entity.Notes,
            ActiveEngagementCount = entity.Engagements.Count(e => e.Status == ClientEngagementStatus.Active),
            TimesheetCount = entity.Timesheets.Count,
            OutstandingInvoiceCount = entity.Invoices.Count(i =>
                i.Status != TimesheetInvoiceStatus.Paid && i.Status != TimesheetInvoiceStatus.Voided),
            Engagements = entity.Engagements.Select(e => e.ToSummaryDto()).ToList(),
        };
    }

    public static ConsultantClientSummaryDto ToSummaryDto(this ConsultantClient entity)
    {
        return new ConsultantClientSummaryDto
        {
            Id = entity.Id,
            ClientName = entity.ClientName,
            ClientCode = entity.ClientCode,
            PrimaryContactName = entity.PrimaryContactName,
            PrimaryContactEmail = entity.PrimaryContactEmail,
            City = entity.City,
            CountryName = entity.Country?.Name,
            Currency = entity.Currency,
            IsActive = entity.IsActive,
            ActiveEngagementCount = entity.Engagements.Count(e => e.Status == ClientEngagementStatus.Active),
        };
    }

    public static ConsultantClient ToEntity(this CreateConsultantClientDto dto, Guid tenantId, Guid userId)
    {
        return new ConsultantClient
        {
            TenantId = tenantId,
            ClientName = dto.ClientName,
            ClientCode = dto.ClientCode,
            Industry = dto.Industry,
            Description = dto.Description,
            PrimaryContactName = dto.PrimaryContactName,
            PrimaryContactEmail = dto.PrimaryContactEmail,
            PrimaryContactPhone = dto.PrimaryContactPhone,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            Region = dto.Region,
            PostalCode = dto.PostalCode,
            CountryId = dto.CountryId,
            FinanceCustomerId = dto.FinanceCustomerId,
            BillingContactName = dto.BillingContactName,
            BillingContactEmail = dto.BillingContactEmail,
            BillingContactPhone = dto.BillingContactPhone,
            TaxIdentificationNumber = dto.TaxIdentificationNumber,
            Currency = dto.Currency,
            DefaultPaymentTermsDays = dto.DefaultPaymentTermsDays,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ConsultantClient entity, UpdateConsultantClientDto dto, Guid userId)
    {
        entity.ClientName = dto.ClientName;
        entity.Industry = dto.Industry;
        entity.Description = dto.Description;
        entity.PrimaryContactName = dto.PrimaryContactName;
        entity.PrimaryContactEmail = dto.PrimaryContactEmail;
        entity.PrimaryContactPhone = dto.PrimaryContactPhone;
        entity.AddressLine1 = dto.AddressLine1;
        entity.AddressLine2 = dto.AddressLine2;
        entity.City = dto.City;
        entity.Region = dto.Region;
        entity.PostalCode = dto.PostalCode;
        entity.CountryId = dto.CountryId;
        entity.FinanceCustomerId = dto.FinanceCustomerId;
        entity.BillingContactName = dto.BillingContactName;
        entity.BillingContactEmail = dto.BillingContactEmail;
        entity.BillingContactPhone = dto.BillingContactPhone;
        entity.TaxIdentificationNumber = dto.TaxIdentificationNumber;
        entity.Currency = dto.Currency;
        entity.DefaultPaymentTermsDays = dto.DefaultPaymentTermsDays;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ConsultantClientSummaryDto> ToSummaryDtoList(this IEnumerable<ConsultantClient> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // CLIENT ENGAGEMENT
    // ========================================================================

    #region ClientEngagement

    public static ClientEngagementDto ToDto(this ClientEngagement entity)
    {
        return new ClientEngagementDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EngagementCode = entity.EngagementCode,
            Title = entity.Title,
            Description = entity.Description,
            ClientId = entity.ClientId,
            ClientName = entity.Client?.ClientName ?? string.Empty,
            ClientCode = entity.Client?.ClientCode ?? string.Empty,
            ConsultantId = entity.ConsultantId,
            ConsultantName = entity.Consultant?.FullName ?? string.Empty,
            ConsultantNumber = entity.Consultant?.EmployeeNumber ?? string.Empty,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            HourlyRate = entity.HourlyRate,
            Currency = entity.Currency,
            BillingCycle = entity.BillingCycle,
            MaxHoursPerWeek = entity.MaxHoursPerWeek,
            ContractValue = entity.ContractValue,
            PurchaseOrderNumber = entity.PurchaseOrderNumber,
            Status = entity.Status,
            Notes = entity.Notes,
            TimesheetCount = entity.Timesheets.Count,
            Timesheets = entity.Timesheets.Select(t => t.ToSummaryDto()).ToList(),
        };
    }

    public static ClientEngagementSummaryDto ToSummaryDto(this ClientEngagement entity)
    {
        return new ClientEngagementSummaryDto
        {
            Id = entity.Id,
            EngagementCode = entity.EngagementCode,
            Title = entity.Title,
            ClientId = entity.ClientId,
            ClientName = entity.Client?.ClientName ?? string.Empty,
            ConsultantId = entity.ConsultantId,
            ConsultantName = entity.Consultant?.FullName ?? string.Empty,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            HourlyRate = entity.HourlyRate,
            Currency = entity.Currency,
            BillingCycle = entity.BillingCycle,
            Status = entity.Status,
        };
    }

    public static ClientEngagement ToEntity(this CreateClientEngagementDto dto, Guid tenantId, Guid userId)
    {
        return new ClientEngagement
        {
            TenantId = tenantId,
            EngagementCode = dto.EngagementCode,
            Title = dto.Title,
            Description = dto.Description,
            ClientId = dto.ClientId,
            ConsultantId = dto.ConsultantId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            HourlyRate = dto.HourlyRate,
            Currency = dto.Currency,
            BillingCycle = dto.BillingCycle,
            MaxHoursPerWeek = dto.MaxHoursPerWeek,
            ContractValue = dto.ContractValue,
            PurchaseOrderNumber = dto.PurchaseOrderNumber,
            Status = dto.Status,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ClientEngagement entity, UpdateClientEngagementDto dto, Guid userId)
    {
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.EndDate = dto.EndDate;
        entity.HourlyRate = dto.HourlyRate;
        entity.Currency = dto.Currency;
        entity.BillingCycle = dto.BillingCycle;
        entity.MaxHoursPerWeek = dto.MaxHoursPerWeek;
        entity.ContractValue = dto.ContractValue;
        entity.PurchaseOrderNumber = dto.PurchaseOrderNumber;
        entity.Status = dto.Status;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ClientEngagementSummaryDto> ToSummaryDtoList(this IEnumerable<ClientEngagement> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // CONSULTANT TIMESHEET
    // ========================================================================

    #region ConsultantTimesheet

    public static ConsultantTimesheetDto ToDto(this ConsultantTimesheet entity)
    {
        return new ConsultantTimesheetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TimesheetNumber = entity.TimesheetNumber,
            ConsultantId = entity.ConsultantId,
            ConsultantName = entity.Consultant?.FullName ?? string.Empty,
            ConsultantNumber = entity.Consultant?.EmployeeNumber ?? string.Empty,
            ClientId = entity.ClientId,
            ClientName = entity.Client?.ClientName ?? string.Empty,
            ClientCode = entity.Client?.ClientCode ?? string.Empty,
            EngagementId = entity.EngagementId,
            EngagementCode = entity.Engagement?.EngagementCode,
            EngagementTitle = entity.Engagement?.Title,
            PeriodStartDate = entity.PeriodStartDate,
            PeriodEndDate = entity.PeriodEndDate,
            TotalHours = entity.TotalHours,
            Status = entity.Status,
            SubmittedDate = entity.SubmittedDate,
            Notes = entity.Notes,
            Entries = entity.Entries.Select(e => e.ToDto()).ToList(),
            Confirmations = entity.Confirmations.Select(c => c.ToDto()).ToList(),
        };
    }

    public static ConsultantTimesheetSummaryDto ToSummaryDto(this ConsultantTimesheet entity)
    {
        return new ConsultantTimesheetSummaryDto
        {
            Id = entity.Id,
            TimesheetNumber = entity.TimesheetNumber,
            ConsultantId = entity.ConsultantId,
            ConsultantName = entity.Consultant?.FullName ?? string.Empty,
            ClientId = entity.ClientId,
            ClientName = entity.Client?.ClientName ?? string.Empty,
            PeriodStartDate = entity.PeriodStartDate,
            PeriodEndDate = entity.PeriodEndDate,
            TotalHours = entity.TotalHours,
            Status = entity.Status,
            SubmittedDate = entity.SubmittedDate,
        };
    }

    public static ConsultantTimesheet ToEntity(this CreateConsultantTimesheetDto dto, Guid tenantId, Guid userId)
    {
        return new ConsultantTimesheet
        {
            TenantId = tenantId,
            TimesheetNumber = string.Empty, // generated by service
            ConsultantId = dto.ConsultantId,
            ClientId = dto.ClientId,
            EngagementId = dto.EngagementId,
            PeriodStartDate = dto.PeriodStartDate,
            PeriodEndDate = dto.PeriodEndDate,
            TotalHours = 0,
            Status = TimesheetStatus.Draft,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ConsultantTimesheet entity, UpdateConsultantTimesheetDto dto, Guid userId)
    {
        entity.EngagementId = dto.EngagementId;
        entity.PeriodStartDate = dto.PeriodStartDate;
        entity.PeriodEndDate = dto.PeriodEndDate;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ConsultantTimesheetSummaryDto> ToSummaryDtoList(this IEnumerable<ConsultantTimesheet> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // CONSULTANT TIMESHEET ENTRY
    // ========================================================================

    #region ConsultantTimesheetEntry

    public static ConsultantTimesheetEntryDto ToDto(this ConsultantTimesheetEntry entity)
    {
        return new ConsultantTimesheetEntryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TimesheetId = entity.TimesheetId,
            TimesheetNumber = entity.Timesheet?.TimesheetNumber ?? string.Empty,
            WorkDate = entity.WorkDate,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            BreakMinutes = entity.BreakMinutes,
            TotalHours = entity.TotalHours,
            ActivitySummary = entity.ActivitySummary,
            Location = entity.Location,
            Notes = entity.Notes,
        };
    }

    public static ConsultantTimesheetEntry ToEntity(this CreateConsultantTimesheetEntryDto dto, Guid tenantId, Guid userId)
    {
        var totalHours = (decimal)(dto.EndTime - dto.StartTime).TotalHours - dto.BreakMinutes / 60m;
        return new ConsultantTimesheetEntry
        {
            TenantId = tenantId,
            TimesheetId = dto.TimesheetId,
            WorkDate = dto.WorkDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            BreakMinutes = dto.BreakMinutes,
            TotalHours = totalHours < 0 ? 0 : totalHours,
            ActivitySummary = dto.ActivitySummary,
            Location = dto.Location,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ConsultantTimesheetEntry entity, UpdateConsultantTimesheetEntryDto dto, Guid userId)
    {
        var totalHours = (decimal)(dto.EndTime - dto.StartTime).TotalHours - dto.BreakMinutes / 60m;
        entity.WorkDate = dto.WorkDate;
        entity.StartTime = dto.StartTime;
        entity.EndTime = dto.EndTime;
        entity.BreakMinutes = dto.BreakMinutes;
        entity.TotalHours = totalHours < 0 ? 0 : totalHours;
        entity.ActivitySummary = dto.ActivitySummary;
        entity.Location = dto.Location;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ConsultantTimesheetEntryDto> ToDtoList(this IEnumerable<ConsultantTimesheetEntry> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // CLIENT TIMESHEET CONFIRMATION
    // ========================================================================

    #region ClientTimesheetConfirmation

    public static ClientTimesheetConfirmationDto ToDto(this ClientTimesheetConfirmation entity)
    {
        return new ClientTimesheetConfirmationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TimesheetId = entity.TimesheetId,
            TimesheetNumber = entity.Timesheet?.TimesheetNumber ?? string.Empty,
            ClientContactEmail = entity.ClientContactEmail,
            ClientContactName = entity.ClientContactName,
            TokenExpiryDate = entity.TokenExpiryDate,
            SentDate = entity.SentDate,
            SentById = entity.SentById,
            SentByName = entity.SentBy?.FullName ?? string.Empty,
            Status = entity.Status,
            ViewedDate = entity.ViewedDate,
            ConfirmedDate = entity.ConfirmedDate,
            RejectedDate = entity.RejectedDate,
            ClientNotes = entity.ClientNotes,
            ResendCount = entity.ResendCount,
        };
    }

    public static ClientTimesheetConfirmation ToEntity(this SendTimesheetConfirmationDto dto, Guid tenantId, Guid userId)
    {
        return new ClientTimesheetConfirmation
        {
            TenantId = tenantId,
            TimesheetId = dto.TimesheetId,
            ClientContactEmail = dto.ClientContactEmail,
            ClientContactName = dto.ClientContactName,
            ConfirmationToken = Guid.NewGuid(),
            TokenExpiryDate = dto.TokenExpiryDate,
            SentDate = DateTime.UtcNow,
            SentById = dto.SentById,
            Status = TimesheetConfirmationStatus.Sent,
            ResendCount = 0,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<ClientTimesheetConfirmationDto> ToDtoList(this IEnumerable<ClientTimesheetConfirmation> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // TIMESHEET INVOICE
    // ========================================================================

    #region TimesheetInvoice

    public static TimesheetInvoiceDto ToDto(this TimesheetInvoice entity)
    {
        return new TimesheetInvoiceDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            InvoiceNumber = entity.InvoiceNumber,
            ClientId = entity.ClientId,
            ClientName = entity.Client?.ClientName ?? string.Empty,
            ClientCode = entity.Client?.ClientCode ?? string.Empty,
            ConsultantId = entity.ConsultantId,
            ConsultantName = entity.Consultant?.FullName ?? string.Empty,
            BillingPeriodStart = entity.BillingPeriodStart,
            BillingPeriodEnd = entity.BillingPeriodEnd,
            TotalHours = entity.TotalHours,
            HourlyRate = entity.HourlyRate,
            SubTotal = entity.SubTotal,
            TaxPercentage = entity.TaxPercentage,
            TaxAmount = entity.TaxAmount,
            TotalAmount = entity.TotalAmount,
            Currency = entity.Currency,
            Status = entity.Status,
            IssuedDate = entity.IssuedDate,
            DueDate = entity.DueDate,
            PaidDate = entity.PaidDate,
            Notes = entity.Notes,
            LinkedTimesheets = entity.LinkedTimesheets.Select(l => l.ToDto()).ToList(),
        };
    }

    public static TimesheetInvoiceSummaryDto ToSummaryDto(this TimesheetInvoice entity)
    {
        return new TimesheetInvoiceSummaryDto
        {
            Id = entity.Id,
            InvoiceNumber = entity.InvoiceNumber,
            ClientId = entity.ClientId,
            ClientName = entity.Client?.ClientName ?? string.Empty,
            ConsultantId = entity.ConsultantId,
            ConsultantName = entity.Consultant?.FullName ?? string.Empty,
            BillingPeriodStart = entity.BillingPeriodStart,
            BillingPeriodEnd = entity.BillingPeriodEnd,
            TotalHours = entity.TotalHours,
            TotalAmount = entity.TotalAmount,
            Currency = entity.Currency,
            Status = entity.Status,
            DueDate = entity.DueDate,
            PaidDate = entity.PaidDate,
        };
    }

    public static TimesheetInvoice ToEntity(this CreateTimesheetInvoiceDto dto, Guid tenantId, Guid userId)
    {
        var subTotal = 0m; // computed after linking timesheets
        var taxAmount = subTotal * dto.TaxPercentage / 100;
        return new TimesheetInvoice
        {
            TenantId = tenantId,
            InvoiceNumber = string.Empty, // generated by service
            ClientId = dto.ClientId,
            ConsultantId = dto.ConsultantId,
            BillingPeriodStart = dto.BillingPeriodStart,
            BillingPeriodEnd = dto.BillingPeriodEnd,
            TotalHours = 0,
            HourlyRate = dto.HourlyRate,
            SubTotal = subTotal,
            TaxPercentage = dto.TaxPercentage,
            TaxAmount = taxAmount,
            TotalAmount = subTotal + taxAmount,
            Currency = dto.Currency,
            Status = TimesheetInvoiceStatus.Draft,
            DueDate = dto.DueDate,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TimesheetInvoice entity, UpdateTimesheetInvoiceDto dto, Guid userId)
    {
        entity.IssuedDate = dto.InvoiceDate;
        entity.DueDate = dto.DueDate;
        entity.TotalAmount = dto.TotalAmount;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TimesheetInvoiceSummaryDto> ToSummaryDtoList(this IEnumerable<TimesheetInvoice> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TIMESHEET INVOICE LINK
    // ========================================================================

    #region TimesheetInvoiceLink

    public static TimesheetInvoiceLinkDto ToDto(this TimesheetInvoiceLink entity)
    {
        return new TimesheetInvoiceLinkDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            InvoiceId = entity.InvoiceId,
            InvoiceNumber = entity.Invoice?.InvoiceNumber ?? string.Empty,
            TimesheetId = entity.TimesheetId,
            TimesheetNumber = entity.Timesheet?.TimesheetNumber ?? string.Empty,
            TimesheetPeriodStart = entity.Timesheet?.PeriodStartDate ?? default,
            TimesheetPeriodEnd = entity.Timesheet?.PeriodEndDate ?? default,
            Hours = entity.Hours,
            Amount = entity.Amount,
        };
    }

    public static TimesheetInvoiceLink ToEntity(this AddTimesheetToInvoiceDto dto, Guid tenantId, Guid userId)
    {
        return new TimesheetInvoiceLink
        {
            TenantId = tenantId,
            InvoiceId = dto.InvoiceId,
            TimesheetId = dto.TimesheetId,
            Hours = 0,   // computed by service after loading timesheet
            Amount = 0,  // computed by service after loading invoice rate
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<TimesheetInvoiceLinkDto> ToDtoList(this IEnumerable<TimesheetInvoiceLink> entities)
        => entities.Select(e => e.ToDto());

    #endregion
}
