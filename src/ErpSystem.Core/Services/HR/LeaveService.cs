using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Application.Extensions;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service for leave management operations
/// </summary>
public class LeaveService : ILeaveService
{
    private readonly ILeaveRepository _leaveRepository;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepository;
    private readonly IGenericRepository<LeaveBalance> _leaveBalanceRepository;
    private readonly IGenericRepository<PublicHoliday> _holidayRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveService> _logger;

    public LeaveService(
            ILeaveRepository leaveRepository,
            IGenericRepository<LeaveType> leaveTypeRepository,
            IGenericRepository<LeaveBalance> leaveBalanceRepository,
            IGenericRepository<PublicHoliday> holidayRepository,
            IEmployeeRepository employeeRepository,
            ILogger<LeaveService> logger,
            IUnitOfWork unitOfWork)
    {
        _leaveRepository = leaveRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _leaveBalanceRepository = leaveBalanceRepository;
        _holidayRepository = holidayRepository;
        _employeeRepository = employeeRepository;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    #region Leave CRUD operations

    public async Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestDto dto)
    {
        // Validate employee exists
        var employee = await _employeeRepository.GetByIdAsync(dto.EmployeeId);
        if (employee == null)
        {
            throw new ArgumentException($"Employee with ID '{dto.EmployeeId}' not found.");
        }

        // Validate leave type exists
        var leaveType = await _leaveTypeRepository.GetByIdAsync(dto.LeaveTypeId);
        if (leaveType == null)
        {
            throw new ArgumentException($"Leave type with ID '{dto.LeaveTypeId}' not found.");
        }

        // Validate dates
        if (dto.StartDate < DateTime.Today)
        {
            throw new InvalidOperationException("Leave start date cannot be in the past.");
        }

        if (dto.EndDate < dto.StartDate)
        {
            throw new InvalidOperationException("Leave end date must be after or equal to start date.");
        }

        // Check for conflicting leave
        var hasConflict = await _leaveRepository.HasConflictingLeaveAsync(dto.EmployeeId, dto.StartDate, dto.EndDate, null);

        if (hasConflict)
        {
            throw new InvalidOperationException("Employee already has a leave request for this period.");
        }

        // Calculate total days
        var totalDays = await CalculateLeaveDaysAsync(dto.StartDate, dto.EndDate, leaveType);

        // Validate against balance
        var currentYear = dto.StartDate.Year;
        var balance = await _leaveBalanceRepository.FirstOrDefaultAsync(
            lb => lb.EmployeeId == dto.EmployeeId &&
                  lb.LeaveTypeId == dto.LeaveTypeId &&
                  lb.Year == currentYear);

        if (balance == null)
        {
            // Initialize balance for the year
            balance = new LeaveBalance
            {
                EmployeeId = dto.EmployeeId,
                LeaveTypeId = dto.LeaveTypeId,
                Year = currentYear,
                EntitledDays = leaveType.DefaultDaysPerYear,
                UsedDays = 0,
                CarriedOverDays = 0,
                AdjustmentDays = 0
            };
            
            await _leaveBalanceRepository.AddAsync(balance);
        }

        if (balance.AvailableDays < totalDays)
        {
            throw new InvalidOperationException($"Insufficient leave balance. Available: {balance.AvailableDays} days, Requested: {totalDays} days");
        }

        // Validate reliever if provided
        if (dto.RelieverEmployeeId.HasValue)
        {
            await ValidateRelieverAsync(dto.RelieverEmployeeId.Value, dto.EmployeeId, dto.StartDate, dto.EndDate);
        }
        else if (employee.ManagerId.HasValue)
        {
            // Auto-suggest reliever based on reporting structure
            dto.RelieverEmployeeId = await SuggestRelieverAsync(employee.ManagerId.Value, dto.StartDate, dto.EndDate);
        }

        // Create request
        // var request = _mapper.Map<LeaveRequest>(dto);
        var request = dto.ToEntity();
        request.RequestNumber = await GenerateRequestNumberAsync();
        request.RequestDate = DateTime.UtcNow;
        request.TotalDays = totalDays;
        request.Status = LeaveStatus.Pending;

        await _leaveRepository.AddAsync(request);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave request created: {requestNumber}", request.RequestNumber);

        return await GetLeaveRequestByIdAsync(request.Id);
    }

    public async Task<LeaveRequestDto> ApproveLeaveAsync(Guid id, ApproveLeaveDto dto)
    {
        var request = await _leaveRepository.GetByIdAsync(id, lr => lr.Employee, lr => lr.LeaveType);

        if (request == null)
        {
            throw new ArgumentException($"Leave request with ID '{id}' not found.");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot approve leave request with status '{request.Status}'.");
        }

        // Validate approver exists
        var approver = await _employeeRepository.GetByIdAsync(dto.ApprovedBy);
        if (approver == null)
        {
            throw new ArgumentException($"Approver with ID '{dto.ApprovedBy}' not found.");
        }

        // Update request
        request.Status = LeaveStatus.Approved;
        request.ApprovedByEmployeeId = dto.ApprovedBy;
        request.ApprovalDate = DateTime.UtcNow;
        request.ApprovalNotes = dto.ApprovalNotes;

        // Update leave balance
        var balance = await _leaveBalanceRepository.FirstOrDefaultAsync(
            lb => lb.EmployeeId == request.EmployeeId &&
                  lb.LeaveTypeId == request.LeaveTypeId &&
                  lb.Year == request.StartDate.Year);

        if (balance != null)
        {
            balance.UsedDays += request.TotalDays;
        }

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave request approved: {requestNumber}", request.RequestNumber);

        // TODO: Send notification to employee
        // await _notificationService.SendLeaveApprovedNotificationAsync(request);

        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<LeaveRequestDto> RejectLeaveAsync(Guid id, RejectLeaveDto dto)
    {
        var request = await _leaveRepository.GetByIdAsync(id);

        if (request == null)
        {
            throw new ArgumentException($"Leave request with ID '{id}' not found.");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot reject leave request with status '{request.Status}'.");
        }

        request.Status = LeaveStatus.Rejected;
        request.RejectionDate = DateTime.UtcNow;
        request.RejectionReason = dto.RejectionReason;

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave request rejected: {requestNumber}", request.RequestNumber);

        // TODO: Send notification to employee
        // await _notificationService.SendLeaveRejectedNotificationAsync(request);

        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<LeaveRequestDto> GetLeaveRequestByIdAsync(Guid id)
    {
        var request = await _leaveRepository
            .GetQueryable()
            .Include(la => la.Employee)
            .Include(la => la.LeaveType)
            .Include(la => la.RelieverEmployee)
            .Include(la => la.ApprovedByEmployee)
            .FirstOrDefaultAsync(la => la.Id == id);

        if (request == null)
        {
            throw new ArgumentException($"Leave request with ID '{id}' not found.");
        }

        // return _mapper.Map<LeaveRequestDto>(request);
        return request.ToDto();
    }

    public async Task<LeaveRequestDto?> GetLeaveRequestByNumberAsync(string requestNumber)
    {
        var request = await _leaveRepository.GetByRequestNumberAsync(requestNumber);
        // return request == null ? null : _mapper.Map<LeaveRequestDto>(request);
        return request?.ToDto();
    }

    public async Task<PagedResult<LeaveRequestDto>> GetEmployeeLeaveHistoryAsync(Guid employeeId, int year, int pageNumber, int pageSize)
    {
        var query = _leaveRepository
            .GetQueryable()
            .Include(la => la.LeaveType)
            .Include(la => la.RelieverEmployee)
            .Where(la => la.EmployeeId == employeeId && la.StartDate.Year == year);

        var totalCount = await query.CountAsync();

        var requests = await query
            .OrderByDescending(la => la.RequestDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // var requestDtos = _mapper.Map<List<LeaveRequestDto>>(requests);
        var requestDtos = requests.ToDtoList();

        return new PagedResult<LeaveRequestDto>
        {
            Items = requestDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<LeaveRequestDto>> GetPendingApprovalsAsync(Guid managerId, int pageNumber, int pageSize)
    {
        var requests = await _leaveRepository.GetPendingApprovalsForManagerAsync(managerId);

        var totalCount = requests.Count();
        var pagedrequests = requests
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // var requestDtos = _mapper.Map<List<LeaveRequestDto>>(pagedrequests);
        var requestDtos = pagedrequests.ToDtoList();

        return new PagedResult<LeaveRequestDto>
        {
            Items = requestDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<LeaveBalanceDto>> GetEmployeeLeaveBalancesAsync(Guid employeeId, int year)
    {
        var balances = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Where(lb => lb.EmployeeId == employeeId && lb.Year == year)
            .ToListAsync();

        // return _mapper.Map<IEnumerable<LeaveBalanceDto>>(balances);
        return balances.ToDtoList();
    }

    public async Task<bool> CancelLeaveRequestAsync(Guid id, string cancellationReason)
    {
        var request = await _leaveRepository.GetByIdAsync(id);

        if (request == null)
        {
            throw new ArgumentException($"Leave request with ID '{id}' not found.");
        }

        if (request.Status == LeaveStatus.Cancelled || request.Status == LeaveStatus.Closed)
        {
            throw new InvalidOperationException($"Cannot cancel leave request with status '{request.Status}'.");
        }

        // Refund leave balance if was approved
        if (request.Status == LeaveStatus.Approved)
        {
            var balance = await _leaveBalanceRepository.FirstOrDefaultAsync(
                lb => lb.EmployeeId == request.EmployeeId &&
                      lb.LeaveTypeId == request.LeaveTypeId &&
                      lb.Year == request.StartDate.Year);

            if (balance != null)
            {
                balance.UsedDays -= request.TotalDays;
            }
        }

        request.Status = LeaveStatus.Cancelled;
        request.CancellationDate = DateTime.UtcNow;
        request.CancellationReason = cancellationReason;

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<LeaveRequestDto> CloseLeaveRequestAsync(Guid id, CloseLeaveDto dto)
    {
        var request = await _leaveRepository.GetByIdAsync(id);

        if (request == null)
        {
            throw new ArgumentException($"Leave request with ID '{id}' not found.");
        }

        if (request.Status != LeaveStatus.Approved)
        {
            throw new InvalidOperationException("Only approved leave requests can be closed.");
        }

        if (request.EndDate > DateTime.Today)
        {
            throw new InvalidOperationException("Cannot close leave request before end date.");
        }

        request.Status = LeaveStatus.Closed;
        request.ClosureDate = DateTime.UtcNow;
        request.ClosureNotes = dto.ClosureNotes;

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        return await GetLeaveRequestByIdAsync(id);
    }

    #endregion Leave CRUD operations

    #region Private helper methods

    private async Task<decimal> CalculateLeaveDaysAsync(DateTime startDate, DateTime endDate, LeaveType leaveType)
    {
        decimal totalDays = 0;
        var start = DateOnly.FromDateTime(startDate);
        var end = DateOnly.FromDateTime(endDate);
        var currentDate = startDate;

        var holidays = await _holidayRepository.FindAsync(h => h.Date >= start && h.Date <= end);
        var holidayDates = holidays.Select(h => h.Date).ToHashSet();

        while (currentDate <= endDate)
        {
            bool isWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday;
            bool isHoliday = holidayDates.Contains(DateOnly.FromDateTime(currentDate));

            bool countDay = true;

            if (!leaveType.CountWeekendsAsLeave && isWeekend)
            {
                countDay = false;
            }

            if (!leaveType.CountHolidaysAsLeave && isHoliday)
            {
                countDay = false;
            }

            if (countDay)
            {
                totalDays++;
            }

            currentDate = currentDate.AddDays(1);
        }

        return totalDays;
    }

    private async Task ValidateRelieverAsync(Guid relieverId, Guid employeeId, DateTime startDate, DateTime endDate)
    {
        if (relieverId == employeeId)
        {
            throw new InvalidOperationException("Employee cannot be their own reliever.");
        }

        var reliever = await _employeeRepository.GetByIdAsync(relieverId);
        if (reliever == null)
        {
            throw new ArgumentException($"Reliever with ID '{relieverId}' not found.");
        }

        if (reliever.StaffStatus != StaffStatus.Active)
        {
            throw new InvalidOperationException("Reliever must be an active employee.");
        }

        var hasConflict = await _leaveRepository.RelieverHasConflictAsync(relieverId, startDate, endDate);

        if (hasConflict)
        {
            throw new InvalidOperationException("Selected reliever is not available during the requested period.");
        }
    }

    private async Task<Guid?> SuggestRelieverAsync(Guid managerId, DateTime startDate, DateTime endDate)
    {
        // Check if manager is available
        var managerHasConflict = await _leaveRepository.RelieverHasConflictAsync(managerId, startDate, endDate);

        return managerHasConflict ? null : managerId;
    }

    private async Task<string> GenerateRequestNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"LV{year}";

        var lastrequest = await _leaveRepository
            .GetQueryable()
            .Where(lr => lr.RequestNumber.StartsWith(prefix))
            .OrderByDescending(lr => lr.RequestNumber)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastrequest != null)
        {
            var lastNumberStr = lastrequest.RequestNumber.Substring(prefix.Length);
            if (int.TryParse(lastNumberStr, out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"{prefix}{nextNumber:D6}";
    }

    #endregion Private helper methods
}