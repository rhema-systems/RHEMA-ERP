using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// DTO for creating a leave request
/// </summary>
public class CreateLeaveRequestDto
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? RelieverEmployeeId { get; set; }
    public string? RelieverNotes { get; set; }
}

/// <summary>
/// DTO for leave request details
/// </summary>
public class LeaveRequestDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public bool IsPaidLeave { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalDays { get; set; }

    public DateTime RequestDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveStatus Status { get; set; }

    public Guid? RelieverEmployeeId { get; set; }
    public string? RelieverEmployeeName { get; set; }
    public string? RelieverNotes { get; set; }

    public Guid? ApprovedByEmployeeId { get; set; }
    public string? ApprovedByEmployeeName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ApprovalNotes { get; set; }

    public DateTime? RejectionDate { get; set; }
    public string? RejectionReason { get; set; }

    public Guid? LeavePlanId { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for employee leave balance
/// </summary>
public class LeaveBalanceDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal EntitledDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal CarriedOverDays { get; set; }
    public decimal AdjustmentDays { get; set; }
    public decimal AvailableDays { get; set; }
}

public class ApproveLeaveDto
{
    public Guid ApprovedBy { get; set; }
    public string? ApprovalNotes { get; set; }
}

public class RejectLeaveDto
{
    public string RejectionReason { get; set; } = string.Empty;
}

public class CloseLeaveDto
{
    public string? ClosureNotes { get; set; }
}

