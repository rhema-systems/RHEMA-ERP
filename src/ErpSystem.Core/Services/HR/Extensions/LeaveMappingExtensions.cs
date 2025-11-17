using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;

namespace ErpSystem.Application.Extensions
{
    /// <summary>
    /// Manual mapping extensions for Leave entities and DTOs
    /// </summary>
    public static class LeaveMappingExtensions
    {
        // ===== CREATE DTO TO ENTITY =====
        public static LeaveRequest ToEntity(this CreateLeaveRequestDto dto)
        {
            return new LeaveRequest
            {
                EmployeeId = dto.EmployeeId,
                LeaveTypeId = dto.LeaveTypeId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Reason = dto.Reason,
                RelieverEmployeeId = dto.RelieverEmployeeId,
                RelieverNotes = dto.RelieverNotes
            };
        }

        // ===== ENTITY TO DTO =====
        public static LeaveRequestDto ToDto(this LeaveRequest entity)
        {
            return new LeaveRequestDto
            {
                Id = entity.Id,
                RequestNumber = entity.RequestNumber,
                EmployeeId = entity.EmployeeId,
                EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
                EmployeeName = entity.Employee?.FullName ?? string.Empty,
                LeaveTypeId = entity.LeaveTypeId,
                LeaveTypeName = entity.LeaveType?.Name ?? string.Empty,
                IsPaidLeave = entity.LeaveType?.IsPaid ?? false,
                StartDate = entity.StartDate,
                EndDate = entity.EndDate,
                TotalDays = entity.TotalDays,
                RequestDate = entity.RequestDate,
                Reason = entity.Reason,
                Status = entity.Status,
                RelieverEmployeeId = entity.RelieverEmployeeId,
                RelieverEmployeeName = entity.RelieverEmployee?.FullName,
                RelieverNotes = entity.RelieverNotes,
                ApprovedByEmployeeId = entity.ApprovedByEmployeeId,
                ApprovedByEmployeeName = entity.ApprovedByEmployee?.FullName,
                ApprovalDate = entity.ApprovalDate,
                ApprovalNotes = entity.ApprovalNotes,
                RejectionDate = entity.RejectionDate,
                RejectionReason = entity.RejectionReason,
                CreatedAt = entity.CreatedAt
            };
        }

        // ===== LEAVE BALANCE MAPPINGS =====
        public static LeaveBalanceDto ToDto(this LeaveBalance entity)
        {
            return new LeaveBalanceDto
            {
                Id = entity.Id,
                EmployeeId = entity.EmployeeId,
                LeaveTypeId = entity.LeaveTypeId,
                LeaveTypeName = entity.LeaveType?.Name ?? string.Empty,
                Year = entity.Year,
                EntitledDays = entity.EntitledDays,
                UsedDays = entity.UsedDays,
                CarriedOverDays = entity.CarriedOverDays,
                AdjustmentDays = entity.AdjustmentDays,
                AvailableDays = entity.AvailableDays
            };
        }

        // ===== COLLECTION MAPPINGS =====
        public static List<LeaveRequestDto> ToDtoList(this IEnumerable<LeaveRequest> entities)
        {
            return entities.Select(e => e.ToDto()).ToList();
        }

        public static List<LeaveBalanceDto> ToDtoList(this IEnumerable<LeaveBalance> entities)
        {
            return entities.Select(e => e.ToDto()).ToList();
        }
    }
}