using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

/// <summary>
/// Service interface for leave management
/// </summary>
public interface ILeaveService
{
    Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestDto dto);
    Task<LeaveRequestDto> ApproveLeaveAsync(Guid id, ApproveLeaveDto dto);
    Task<LeaveRequestDto> RejectLeaveAsync(Guid id, RejectLeaveDto dto);
    Task<LeaveRequestDto> GetLeaveRequestByIdAsync(Guid id);
    Task<LeaveRequestDto?> GetLeaveRequestByNumberAsync(string applicationNumber);
    Task<PagedResult<LeaveRequestDto>> GetEmployeeLeaveHistoryAsync(Guid employeeId, int year, int pageNumber, int pageSize);
    Task<PagedResult<LeaveRequestDto>> GetPendingApprovalsAsync(Guid managerId, int pageNumber, int pageSize);
    Task<IEnumerable<LeaveBalanceDto>> GetEmployeeLeaveBalancesAsync(Guid employeeId, int year);
    Task<bool> CancelLeaveRequestAsync(Guid id, string cancellationReason);
    Task<LeaveRequestDto> CloseLeaveRequestAsync(Guid id, CloseLeaveDto dto);
}