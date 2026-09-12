using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Common;
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
    private const string EntityType = "LeaveRequest";

    private readonly ILeaveRepository _leaveRepository;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepository;
    private readonly IGenericRepository<LeaveBalance> _leaveBalanceRepository;
    private readonly IGenericRepository<LeaveEncashment> _leaveEncashmentRepository;
    private readonly IGenericRepository<LeaveAdjustment> _leaveAdjustmentRepository;
    private readonly IGenericRepository<EmployeeReliever> _employeeRelieverRepository;
    private readonly IGenericRepository<PublicHoliday> _holidayRepository;
    private readonly IGenericRepository<LeaveRequestAttachment> _attachmentRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILeaveTypeService _leaveTypeService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveService> _logger;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILeaveBalanceRecalculationService _recalculationService;
    private readonly ILeaveEntitlementService _entitlementService;
    private readonly IDateTimeProvider _clock;
    private readonly INumberSequenceService _numberSequence;

    /// <summary>
    /// Sequence key for leave request numbers. Year-bucketed: the printed number is
    /// <c>LV{year}{seq:D6}</c> and restarts each January, so the counter must too.
    /// </summary>
    private const string LeaveRequestSequenceKey = "LEAVE-REQ";

    public LeaveService(
            ILeaveRepository leaveRepository,
            IGenericRepository<LeaveType> leaveTypeRepository,
            IGenericRepository<LeaveBalance> leaveBalanceRepository,
            IGenericRepository<LeaveEncashment> leaveEncashmentRepository,
            IGenericRepository<LeaveAdjustment> leaveAdjustmentRepository,
            IGenericRepository<EmployeeReliever> employeeRelieverRepository,
            IGenericRepository<PublicHoliday> holidayRepository,
            IGenericRepository<LeaveRequestAttachment> attachmentRepository,
            IEmployeeRepository employeeRepository,
            ILeaveTypeService leaveTypeService,
            ILogger<LeaveService> logger,
            IUnitOfWork unitOfWork,
            IWorkflowIntegrationService workflowIntegrationService,
            IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
            ICurrentUserService currentUserService,
            ILeaveBalanceRecalculationService recalculationService,
            ILeaveEntitlementService entitlementService,
            IDateTimeProvider clock,
            INumberSequenceService numberSequence)
    {
        _leaveRepository = leaveRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _leaveBalanceRepository = leaveBalanceRepository;
        _leaveEncashmentRepository = leaveEncashmentRepository;
        _leaveAdjustmentRepository = leaveAdjustmentRepository;
        _employeeRelieverRepository = employeeRelieverRepository;
        _holidayRepository = holidayRepository;
        _attachmentRepository = attachmentRepository;
        _employeeRepository = employeeRepository;
        _leaveTypeService = leaveTypeService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserService = currentUserService;
        _recalculationService = recalculationService;
        _entitlementService = entitlementService;
        _numberSequence = numberSequence;
        _clock = clock;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    // A leave request owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<LeaveRequest> GetOwnedLeaveRequestAsync(Guid id)
    {
        var entity = await _leaveRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave request with ID '{id}' not found.");
        return entity;
    }

    private async Task<LeaveBalance> GetOwnedLeaveBalanceAsync(Guid id)
    {
        var entity = await _leaveBalanceRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave balance with ID '{id}' not found.");
        return entity;
    }

    private async Task<LeaveAdjustment> GetOwnedLeaveAdjustmentAsync(Guid id)
    {
        var entity = await _leaveAdjustmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave adjustment with ID '{id}' not found.");
        return entity;
    }

    private async Task<LeaveRequestAttachment> GetOwnedAttachmentAsync(Guid id)
    {
        var entity = await _attachmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave request attachment '{id}' not found.");
        return entity;
    }

    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var entity = await _employeeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return entity;
    }

    private async Task<LeaveType> GetOwnedLeaveTypeAsync(Guid id)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave type with ID '{id}' not found.");
        return entity;
    }

    private async Task<bool> HasConflictingLeaveAsync(
        Guid employeeId, DateOnly startDate, DateOnly endDate, Guid? excludeRequestId = null)
    {
        var tenantId = GetTenantId();
        var query = _leaveRepository.GetQueryable()
            .Where(la => la.TenantId == tenantId &&
                        la.EmployeeId == employeeId &&
                        (la.Status == LeaveStatus.Approved || la.Status == LeaveStatus.Pending) &&
                        (la.StartDate <= endDate && la.EndDate >= startDate));

        if (excludeRequestId.HasValue)
            query = query.Where(la => la.Id != excludeRequestId.Value);

        return await query.AnyAsync();
    }

    private async Task<bool> RelieverHasConflictAsync(Guid relieverId, DateOnly startDate, DateOnly endDate)
    {
        var tenantId = GetTenantId();

        var relieverOnLeave = await _leaveRepository.GetQueryable()
            .AnyAsync(la => la.TenantId == tenantId &&
                           la.EmployeeId == relieverId &&
                           la.Status == LeaveStatus.Approved &&
                           la.StartDate <= endDate &&
                           la.EndDate >= startDate);

        if (relieverOnLeave)
            return true;

        return await _leaveRepository.GetQueryable()
            .AnyAsync(la => la.TenantId == tenantId &&
                           la.RelieverEmployeeId == relieverId &&
                           la.Status == LeaveStatus.Approved &&
                           la.StartDate <= endDate &&
                           la.EndDate >= startDate);
    }

    #region Leave CRUD operations

    public async Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestDto dto)
    {
        // Validate employee exists
        var employee = await GetOwnedEmployeeAsync(dto.EmployeeId);

        // Validate leave type exists
        var leaveType = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);

        // Check that the employee is eligible for this leave type (gender / org / position rules)
        var isEligible = await _leaveTypeService.IsEmployeeEligibleAsync(dto.LeaveTypeId, dto.EmployeeId);
        if (!isEligible)
        {
            throw new InvalidOperationException(
                "This employee does not meet the eligibility criteria for the selected leave type.");
        }

        // Service-access gate: e.g. annual leave requires 1 year of service before it can be taken.
        var isAccessible = await _entitlementService.IsAccessibleAsync(dto.EmployeeId, dto.LeaveTypeId, dto.StartDate);
        if (!isAccessible)
        {
            throw new InvalidOperationException(
                "This employee has not yet completed the minimum service period required to take this leave type.");
        }

        // Validate dates
        if (dto.StartDate < _clock.TodayUtc)
        {
            throw new InvalidOperationException("Leave start date cannot be in the past.");
        }

        if (dto.EndDate < dto.StartDate)
        {
            throw new InvalidOperationException("Leave end date must be after or equal to start date.");
        }

        // Minimum-notice rule: the request must be filed at least N days before it starts.
        // Skipped for drafts so an employee can save a draft any time.
        if (!dto.SaveAsDraft && leaveType.MinDaysNotice is int minNotice && minNotice > 0)
        {
            var noticeDays = dto.StartDate.DayNumber - _clock.TodayUtc.DayNumber;
            if (noticeDays < minNotice)
                throw new InvalidOperationException(
                    $"This leave type requires at least {minNotice} day(s) notice. Only {noticeDays} day(s) given.");
        }

        // Check for conflicting leave
        var hasConflict = await HasConflictingLeaveAsync(dto.EmployeeId, dto.StartDate, dto.EndDate, null);

        if (hasConflict)
        {
            throw new InvalidOperationException("Employee already has a leave request for this period.");
        }

        // Calculate total days
        var totalDays = await CalculateLeaveDaysAsync(dto.StartDate, dto.EndDate, leaveType);

        // Validate against the ACCRUED balance (you cannot take more than has accrued to date).
        // We do NOT persist the balance here — the recalculation service (called inside the
        // transaction below) creates and populates it.
        var currentYear = dto.StartDate.Year;
        var tenantId = GetTenantId();
        var balance = await _leaveBalanceRepository.FirstOrDefaultAsync(
            lb => lb.TenantId == tenantId &&
                  lb.EmployeeId == dto.EmployeeId &&
                  lb.LeaveTypeId == dto.LeaveTypeId &&
                  lb.Year == currentYear);

        var accruedAvailable = await GetAccruedAvailableDaysAsync(
            dto.EmployeeId, dto.LeaveTypeId, dto.LeaveSubTypeId, currentYear, balance);
        if (accruedAvailable < totalDays)
        {
            throw new InvalidOperationException(
                $"Insufficient accrued leave balance. Available: {accruedAvailable} days, Requested: {totalDays} days");
        }

        // Validate explicitly-chosen relievers (hard-fail on clash, as before).
        if (dto.RelieverEmployeeId.HasValue)
            await ValidateRelieverAsync(dto.RelieverEmployeeId.Value, dto.EmployeeId, dto.StartDate, dto.EndDate);
        if (dto.SecondRelieverEmployeeId.HasValue)
            await ValidateRelieverAsync(dto.SecondRelieverEmployeeId.Value, dto.EmployeeId, dto.StartDate, dto.EndDate);

        // Auto-fill empty reliever slots from the employee's pre-defined relievers (by priority),
        // skipping any that are unavailable for the requested period.
        await AutoFillRelieversAsync(dto);

        // Last resort: suggest the manager as primary reliever if still none.
        if (!dto.RelieverEmployeeId.HasValue && employee.ManagerId.HasValue)
            dto.RelieverEmployeeId = await SuggestRelieverAsync(employee.ManagerId.Value, dto.StartDate, dto.EndDate);

        // Reliever requirement: this leave type cannot be submitted without a reliever once all
        // auto-fill/suggestion attempts are exhausted. Drafts are exempt so they can be saved early.
        if (!dto.SaveAsDraft && leaveType.RequiresReliever && !dto.RelieverEmployeeId.HasValue)
            throw new InvalidOperationException(
                "This leave type requires a reliever, but none could be assigned. Please select a reliever.");

        var request = dto.ToEntity();
        request.TenantId = tenantId;
        request.RequestDate = _clock.UtcNow;
        request.TotalDays = totalDays;
        request.Status = dto.SaveAsDraft ? LeaveStatus.Draft : LeaveStatus.Pending;

        // Persist the request and recalculate its balance in one transaction so a recalc
        // failure cannot leave an orphaned request with a stale balance. The unique index on
        // (TenantId, RequestNumber) guards against duplicate numbers under concurrency; on a
        // collision we regenerate the number and retry.
        const int maxAttempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            request.RequestNumber = await GenerateRequestNumberAsync();
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await _leaveRepository.AddAsync(request);
                    await _unitOfWork.SaveChangesAsync(ct);
                    await _recalculationService.RecalculateAsync(dto.EmployeeId, dto.LeaveTypeId, currentYear);
                });
                break;
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts && IsDuplicateRequestNumber(ex))
            {
                // Another request grabbed this number first; detach the failed insert and retry.
                _unitOfWork.ClearChangeTracker();
                _logger.LogWarning("Duplicate leave request number on attempt {Attempt}; regenerating.", attempt);
            }
        }

        _logger.LogInformation("Leave request created: {requestNumber}", request.RequestNumber);

        return await GetLeaveRequestByIdAsync(request.Id);
    }

    public async Task<LeaveRequestDto> UpdateDraftAsync(Guid id, CreateLeaveRequestDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status != LeaveStatus.Draft)
            throw new InvalidOperationException("Only draft leave requests can be updated.");

        // Determine what has changed so we recalculate balance only when necessary.
        bool typeChanged  = request.LeaveTypeId != dto.LeaveTypeId;
        bool datesChanged = request.StartDate   != dto.StartDate || request.EndDate != dto.EndDate;
        bool needsBalanceRecalc = typeChanged || datesChanged;

        // If the leave type changed, re-validate eligibility.
        LeaveType leaveType;
        if (typeChanged)
        {
            leaveType = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);

            var isEligible = await _leaveTypeService.IsEmployeeEligibleAsync(dto.LeaveTypeId, request.EmployeeId);
            if (!isEligible)
                throw new InvalidOperationException(
                    "This employee does not meet the eligibility criteria for the selected leave type.");

            var isAccessible = await _entitlementService.IsAccessibleAsync(request.EmployeeId, dto.LeaveTypeId, dto.StartDate);
            if (!isAccessible)
                throw new InvalidOperationException(
                    "This employee has not yet completed the minimum service period required to take this leave type.");
        }
        else
        {
            leaveType = await GetOwnedLeaveTypeAsync(request.LeaveTypeId);
        }

        if (dto.EndDate < dto.StartDate)
            throw new InvalidOperationException("Leave end date must be after or equal to start date.");

        // Check for conflicting leave (exclude this request itself).
        if (datesChanged)
        {
            var hasConflict = await HasConflictingLeaveAsync(
                request.EmployeeId, dto.StartDate, dto.EndDate, id);
            if (hasConflict)
                throw new InvalidOperationException("Employee already has a leave request for this period.");
        }

        var totalDays = await CalculateLeaveDaysAsync(dto.StartDate, dto.EndDate, leaveType);

        // Validate balance only when dates or type have changed. A missing balance means the
        // employee still has their full entitlement; the recalculation service persists it later.
        if (needsBalanceRecalc)
        {
            var currentYear = dto.StartDate.Year;
            var tenantId = GetTenantId();
            var balance = await _leaveBalanceRepository.FirstOrDefaultAsync(
                lb => lb.TenantId == tenantId &&
                      lb.EmployeeId == request.EmployeeId &&
                      lb.LeaveTypeId == dto.LeaveTypeId &&
                      lb.Year == currentYear);

            var accruedAvailable = await GetAccruedAvailableDaysAsync(
                request.EmployeeId, dto.LeaveTypeId, dto.LeaveSubTypeId, currentYear, balance);
            // Accrued availability + days already reserved by this draft = real headroom.
            var headroom = accruedAvailable + request.TotalDays;
            if (headroom < totalDays)
                throw new InvalidOperationException(
                    $"Insufficient accrued leave balance. Available: {headroom} days, Requested: {totalDays} days");
        }

        // Validate reliever if provided.
        if (dto.RelieverEmployeeId.HasValue)
        {
            await ValidateRelieverAsync(dto.RelieverEmployeeId.Value, request.EmployeeId, dto.StartDate, dto.EndDate);
        }

        // Store the old type/year so we can recalculate the previously-affected balance too.
        var oldLeaveTypeId = request.LeaveTypeId;
        var oldYear        = request.StartDate.Year;

        // Apply all mutable fields.
        request.LeaveTypeId        = dto.LeaveTypeId;
        request.LeaveSubTypeId     = dto.LeaveSubTypeId;
        request.StartDate          = dto.StartDate;
        request.EndDate            = dto.EndDate;
        request.TotalDays          = totalDays;
        request.Reason             = dto.Reason;
        request.RelieverEmployeeId = dto.RelieverEmployeeId;
        request.RelieverNotes      = dto.RelieverNotes;
        request.HandoverNotes      = dto.HandoverNotes;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);

            if (needsBalanceRecalc)
            {
                var newYear = dto.StartDate.Year;
                // Recalculate the new balance bucket.
                await _recalculationService.RecalculateAsync(request.EmployeeId, dto.LeaveTypeId, newYear);
                // If the leave type or year changed, also fix up the old bucket.
                if (typeChanged || oldYear != newYear)
                    await _recalculationService.RecalculateAsync(request.EmployeeId, oldLeaveTypeId, oldYear);
            }
        });

        _logger.LogInformation("Draft leave request updated: {requestId}", id);
        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<bool> SubmitForApprovalAsync(Guid id)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status != LeaveStatus.Pending && request.Status != LeaveStatus.Draft)
            throw new InvalidOperationException("Only Pending or Draft leave requests can be submitted for approval.");

        var userId = GetCurrentUserId();

        // Auto-approval: leave types configured with RequiresApproval = false skip the workflow
        // entirely and are approved on submission (balance deducted via the recalc, same as a
        // normal approval). Used for low-risk types (e.g. short casual leave).
        var leaveType = await GetOwnedLeaveTypeAsync(request.LeaveTypeId);
        if (!leaveType.RequiresApproval)
        {
            request.Status = LeaveStatus.Approved;
            request.ApprovedById = userId == Guid.Empty ? null : userId;
            request.ApprovedDate = _clock.UtcNow;
            request.RejectionReason = null;

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _leaveRepository.UpdateAsync(request);
                await _unitOfWork.SaveChangesAsync(ct);
                await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
            });

            _logger.LogInformation("Leave request auto-approved (approval not required): {requestNumber}", request.RequestNumber);
            return true;
        }

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, id);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start approval workflow.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(request, workflowResult.Outcome, userId);

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave request submitted for approval: {requestNumber}", request.RequestNumber);
        return true;
    }

    public async Task<LeaveRequestDto> ApproveLeaveAsync(Guid id, ApproveLeaveDto dto)
    {
        var tenantId = GetTenantId();
        var request = await _leaveRepository
            .GetQueryable()
            .Include(lr => lr.Employee)
            .Include(lr => lr.LeaveType)
            .FirstOrDefaultAsync(lr => lr.Id == id && lr.TenantId == tenantId)
            ?? throw new ArgumentException($"Leave request with ID '{id}' not found.");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var comments = dto.Comments ?? dto.ApprovalNotes;
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Approve", comments);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(request, workflowResult.Outcome, userId);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after approval outcome
            await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
        });

        _logger.LogInformation("Leave request approved: {requestNumber}", request.RequestNumber);
        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<LeaveRequestDto> RejectLeaveAsync(Guid id, RejectLeaveDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var rejectionText = !string.IsNullOrWhiteSpace(dto.RejectionReason) ? dto.RejectionReason : "Rejected";
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Reject", rejectionText);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process rejection.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(request, workflowResult.Outcome, userId, rejectionText);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after rejection
            await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
        });

        _logger.LogInformation("Leave request rejected: {requestNumber}", request.RequestNumber);
        return await GetLeaveRequestByIdAsync(id);
    }

    public async Task<LeaveRequestDto> GetLeaveRequestByIdAsync(Guid id)
    {
        var tenantId = GetTenantId();
        var request = await _leaveRepository
            .GetQueryable()
            .Include(la => la.Employee)
            .Include(la => la.LeaveType)
            .Include(la => la.LeaveSubType)
            .Include(la => la.RelieverEmployee)
            .Include(la => la.SecondRelieverEmployee)
            .FirstOrDefaultAsync(la => la.Id == id && la.TenantId == tenantId);

        if (request == null)
        {
            throw new ArgumentException($"Leave request with ID '{id}' not found.");
        }

        // return _mapper.Map<LeaveRequestDto>(request);
        return request.ToDto();
    }

    public async Task<LeaveRequestDto?> GetLeaveRequestByNumberAsync(string requestNumber)
    {
        var tenantId = GetTenantId();
        var request = await _leaveRepository
            .GetQueryable()
            .Include(lr => lr.Employee)
            .Include(lr => lr.LeaveType)
            .Include(lr => lr.LeaveSubType)
            .Include(lr => lr.RelieverEmployee)
            .Include(lr => lr.SecondRelieverEmployee)
            .FirstOrDefaultAsync(lr => lr.TenantId == tenantId && lr.RequestNumber == requestNumber);
        return request?.ToDto();
    }

    public async Task<PagedResult<LeaveRequestDto>> GetEmployeeLeaveHistoryAsync(Guid employeeId, int year, int pageNumber, int pageSize)
    {
        var tenantId = GetTenantId();
        var query = _leaveRepository
            .GetQueryable()
            .Include(la => la.LeaveType)
            .Include(la => la.RelieverEmployee)
            .Include(la => la.SecondRelieverEmployee)
            .Where(la => la.TenantId == tenantId && la.EmployeeId == employeeId && la.StartDate.Year == year);

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
        var tenantId = GetTenantId();
        var requests = (await _leaveRepository.GetPendingApprovalsForManagerAsync(managerId))
            .Where(r => r.TenantId == tenantId);

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
        var tenantId = GetTenantId();
        var balances = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.Employee)
            .Where(lb => lb.TenantId == tenantId && lb.EmployeeId == employeeId && lb.Year == year)
            .ToListAsync();

        var dtos = balances.ToDtoList();
        foreach (var (balance, dto) in balances.Zip(dtos))
        {
            dto.AccruedToDateDays = await _entitlementService.GetAccruedAsOfAsync(
                balance.EmployeeId, balance.LeaveTypeId, balance.LeaveSubTypeId, balance.Year);
        }

        return dtos;
    }

    public async Task<IEnumerable<LeaveBalanceDto>> GetAllLeaveBalancesAsync(int year, Guid? employeeId, Guid? leaveTypeId)
    {
        var tenantId = GetTenantId();
        var query = _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.LeaveSubType)
            .Include(lb => lb.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Where(lb => lb.TenantId == tenantId && lb.Year == year);

        if (employeeId.HasValue)
            query = query.Where(lb => lb.EmployeeId == employeeId.Value);

        if (leaveTypeId.HasValue)
            query = query.Where(lb => lb.LeaveTypeId == leaveTypeId.Value);

        var balances = await query
            .OrderBy(lb => lb.Employee.LastName)
            .ThenBy(lb => lb.Employee.FirstName)
            .ThenBy(lb => lb.LeaveType.Name)
            .ToListAsync();

        return balances.ToDtoList();
    }

    public async Task<IEnumerable<MandatoryLeaveComplianceDto>> GetMandatoryLeaveComplianceAsync(int year)
    {
        // One pass over the balances for leave types flagged mandatory-to-take. Compliance is
        // measured against the entitlement the employee is required to use within the year:
        // taken (UsedDays) ≥ entitled = Compliant; pending covers the gap = Scheduled; else Outstanding.
        var tenantId = GetTenantId();
        var balances = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.Employee)
            .Where(lb => lb.TenantId == tenantId
                      && lb.Year == year
                      && lb.LeaveType.MandatoryAnnualLeave
                      && lb.LeaveType.IsActive
                      && !lb.Employee.IsDeleted)
            .OrderBy(lb => lb.Employee.LastName)
            .ThenBy(lb => lb.Employee.FirstName)
            .ThenBy(lb => lb.LeaveType.Name)
            .ToListAsync();

        return balances.Select(lb =>
        {
            var entitled = lb.EntitledDays + lb.CarriedOverDays;
            var taken = lb.UsedDays;
            var scheduled = lb.PendingDays;
            var outstanding = Math.Max(0m, entitled - taken - scheduled);

            string status = taken >= entitled ? "Compliant"
                          : (taken + scheduled) >= entitled ? "Scheduled"
                          : "Outstanding";

            return new MandatoryLeaveComplianceDto
            {
                EmployeeId = lb.EmployeeId,
                EmployeeName = lb.Employee?.FullName ?? string.Empty,
                LeaveTypeId = lb.LeaveTypeId,
                LeaveTypeName = lb.LeaveType?.Name ?? string.Empty,
                Year = year,
                EntitledDays = entitled,
                TakenDays = taken,
                ScheduledDays = scheduled,
                OutstandingDays = outstanding,
                Status = status
            };
        }).ToList();
    }

    public async Task<LeaveBalanceDetailDto?> GetLeaveBalanceDetailAsync(Guid balanceId)
    {
        var tenantId = GetTenantId();
        var balance = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.LeaveSubType)
            .Include(lb => lb.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .FirstOrDefaultAsync(lb => lb.Id == balanceId && lb.TenantId == tenantId);

        if (balance == null) return null;

        var requests = await _leaveRepository
            .GetQueryable()
            .Include(r => r.LeaveType)
            .Include(r => r.RelieverEmployee)
            .Include(r => r.SecondRelieverEmployee)
            .Where(r => r.TenantId == tenantId
                     && r.EmployeeId == balance.EmployeeId
                     && r.LeaveTypeId == balance.LeaveTypeId
                     && r.StartDate.Year == balance.Year)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();

        var encashments = await _leaveEncashmentRepository
            .GetQueryable()
            .Include(e => e.Employee)
            .Include(e => e.ProcessedByEmployee)
            .Include(e => e.LeaveRequest).ThenInclude(lr => lr.LeaveType)
            .Where(e => e.TenantId == tenantId
                     && e.EmployeeId == balance.EmployeeId
                     && e.LeaveTypeId == balance.LeaveTypeId
                     && e.Year == balance.Year)
            .OrderByDescending(e => e.ProcessedDate)
            .ToListAsync();

        var adjustments = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .Where(a => a.TenantId == tenantId && a.LeaveBalanceId == balanceId)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync();

        var dto = new LeaveBalanceDetailDto
        {
            Id                   = balance.Id,
            EmployeeId           = balance.EmployeeId,
            EmployeeName         = balance.Employee?.FullName ?? string.Empty,
            OrganizationUnitName = balance.Employee?.OrganizationUnit?.Name,
            LeaveTypeId          = balance.LeaveTypeId,
            LeaveTypeName        = balance.LeaveType?.Name ?? string.Empty,
            LeaveSubTypeId       = balance.LeaveSubTypeId,
            LeaveSubTypeName     = balance.LeaveSubType?.SubTypeName,
            Year                 = balance.Year,
            EntitledDays         = balance.EntitledDays,
            AccruedToDateDays    = await _entitlementService.GetAccruedAsOfAsync(
                                       balance.EmployeeId, balance.LeaveTypeId, balance.LeaveSubTypeId, balance.Year),
            UsedDays             = balance.UsedDays,
            PendingDays          = balance.PendingDays,
            CarriedOverDays      = balance.CarriedOverDays,
            AdjustmentDays       = balance.AdjustmentDays,
            EncashedDays         = balance.EncashedDays,
            AvailableDays        = balance.AvailableDays,
            AllowCashConversion  = balance.LeaveType?.AllowCashConversion ?? false,
            Requests             = requests.ToDtoList(),
            Encashments          = encashments.ToDtoList(),
            Adjustments          = adjustments.ToDtoList()
        };

        return dto;
    }

    public async Task<bool> CancelLeaveRequestAsync(Guid id, string cancellationReason)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status == LeaveStatus.Cancelled || request.Status == LeaveStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot cancel leave request with status '{request.Status}'.");
        }

        var employeeId   = request.EmployeeId;
        var leaveTypeId  = request.LeaveTypeId;
        var year         = request.StartDate.Year;

        request.Status = LeaveStatus.Cancelled;
        request.CancellationDate = _clock.UtcNow;
        request.CancellationReason = cancellationReason;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after cancellation
            await _recalculationService.RecalculateAsync(employeeId, leaveTypeId, year);
        });

        return true;
    }

    public async Task<LeaveRequestDto> CloseLeaveRequestAsync(Guid id, CloseLeaveDto dto)
    {
        var request = await GetOwnedLeaveRequestAsync(id);

        if (request.Status != LeaveStatus.Approved)
        {
            throw new InvalidOperationException("Only approved leave requests can be closed.");
        }

        if (request.EndDate > _clock.TodayUtc)
        {
            throw new InvalidOperationException("Cannot close leave request before end date.");
        }

        request.Status = LeaveStatus.Completed;
        request.ClosureDate = _clock.UtcNow;
        request.ClosureNotes = dto.ClosureNotes;

        await _leaveRepository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        return await GetLeaveRequestByIdAsync(id);
    }

    #endregion Leave CRUD operations

    #region Private helper methods

    private async Task<decimal> CalculateLeaveDaysAsync(DateOnly startDate, DateOnly endDate, LeaveType leaveType)
    {
        decimal totalDays = 0;
        var currentDate = startDate;

        // Match any holiday that overlaps the requested range (not only holidays fully inside it),
        // e.g. a Dec 30 – Jan 2 holiday must be counted for a Jan 1 – 5 leave request.
        var tenantId = GetTenantId();
        var holidays = await _holidayRepository.FindAsync(h =>
            h.TenantId == tenantId && h.DateFrom <= endDate && h.DateTo >= startDate);
        var holidayDates = new HashSet<DateOnly>();
        foreach (var holiday in holidays)
        {
            for (var date = holiday.DateFrom; date <= holiday.DateTo; date = date.AddDays(1))
            {
                holidayDates.Add(date);
            }
        }

        while (currentDate <= endDate)
        {
            bool isWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday;
            bool isHoliday = holidayDates.Contains(currentDate);

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

    private async Task ValidateRelieverAsync(Guid relieverId, Guid employeeId, DateOnly startDate, DateOnly endDate)
    {
        if (relieverId == employeeId)
        {
            throw new InvalidOperationException("Employee cannot be their own reliever.");
        }

        var reliever = await GetOwnedEmployeeAsync(relieverId);

        if (reliever.StaffStatus != StaffStatus.Active)
        {
            throw new InvalidOperationException("Reliever must be an active employee.");
        }

        var hasConflict = await RelieverHasConflictAsync(relieverId, startDate, endDate);

        if (hasConflict)
        {
            throw new InvalidOperationException("Selected reliever is not available during the requested period.");
        }
    }

    /// <summary>
    /// Best-effort default reliever used only when the requester did not pick one. Currently
    /// proposes the employee's manager when they are free, otherwise returns null (no reliever
    /// auto-assigned — it stays optional unless the leave type requires one). This is superseded
    /// in Phase 3 by pre-defined relievers configured on the employee profile.
    /// </summary>
    private async Task<Guid?> SuggestRelieverAsync(Guid managerId, DateOnly startDate, DateOnly endDate)
    {
        var managerHasConflict = await RelieverHasConflictAsync(managerId, startDate, endDate);

        return managerHasConflict ? null : managerId;
    }

    /// <summary>
    /// Fills any empty reliever slot on the request from the employee's pre-defined relievers
    /// (priority 1 → primary, priority 2 → second), skipping relievers who are themselves on
    /// leave during the period. Explicit selections are left untouched.
    /// </summary>
    private async Task AutoFillRelieversAsync(CreateLeaveRequestDto dto)
    {
        if (dto.RelieverEmployeeId.HasValue && dto.SecondRelieverEmployeeId.HasValue)
            return;

        var tenantId = GetTenantId();
        var predefined = await _employeeRelieverRepository
            .GetQueryable()
            .Where(r => r.TenantId == tenantId && r.EmployeeId == dto.EmployeeId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        foreach (var pr in predefined)
        {
            if (pr.RelieverEmployeeId == dto.EmployeeId) continue;
            if (pr.RelieverEmployeeId == dto.RelieverEmployeeId || pr.RelieverEmployeeId == dto.SecondRelieverEmployeeId)
                continue;

            // Don't auto-assign a reliever who is unavailable (the user can still override on the form).
            var clash = await RelieverHasConflictAsync(pr.RelieverEmployeeId, dto.StartDate, dto.EndDate);
            if (clash) continue;

            if (!dto.RelieverEmployeeId.HasValue)
                dto.RelieverEmployeeId = pr.RelieverEmployeeId;
            else if (!dto.SecondRelieverEmployeeId.HasValue)
                dto.SecondRelieverEmployeeId = pr.RelieverEmployeeId;

            if (dto.RelieverEmployeeId.HasValue && dto.SecondRelieverEmployeeId.HasValue)
                break;
        }
    }

    /// <summary>
    /// Days the employee can actually take right now for a leave type/year: accrued-to-date plus
    /// carry-over and adjustments, less used/pending/encashed. For leave types without an accrual
    /// policy this equals the normal full-entitlement availability (no behavior change), and it
    /// respects a manual <c>EntitledDays</c> override on an existing balance.
    /// </summary>
    private async Task<decimal> GetAccruedAvailableDaysAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, LeaveBalance? balance)
    {
        var snapshot = await _entitlementService.GetSnapshotAsync(employeeId, leaveTypeId, leaveSubTypeId, year);

        var baseEntitled = balance?.EntitledDays ?? snapshot.AnnualEntitledDays;
        var effectiveAccrued = snapshot.HasAccrualPolicy ? snapshot.AccruedToDateDays : baseEntitled;

        var carried  = balance?.CarriedOverDays ?? 0m;
        var adjust   = balance?.AdjustmentDays  ?? 0m;
        var used     = balance?.UsedDays        ?? 0m;
        var pending  = balance?.PendingDays     ?? 0m;
        var encashed = balance?.EncashedDays    ?? 0m;

        return effectiveAccrued + carried + adjust - used - pending - encashed;
    }

    private static bool IsDuplicateRequestNumber(DbUpdateException ex)
    {
        // Provider-agnostic: the unique index name (IX_LeaveRequests_TenantId_RequestNumber)
        // and/or the column name appear in the violation message across SQL Server / Postgres.
        var message = (ex.InnerException?.Message ?? ex.Message);
        return message.Contains("RequestNumber", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Issues the next leave request number, <c>LV{year}{seq:D6}</c>, from the tenant's atomic
    /// number sequence.
    /// </summary>
    /// <remarks>
    /// <para>Finish-plan lane 4 (2026-09-01). This used to be a max+1 scan over the LIVE rows:
    /// order by number, take the last, add one. Two things were wrong with that. Under concurrency
    /// two creates could read the same maximum (the unique index caught it and the retry loop
    /// below re-ran the same scan). And a soft-deleted request vanished from the scan while its
    /// number stayed in the unique index, so the next create was handed a number already taken —
    /// every retry recomputed the same collision and the create failed. Training, recruitment and
    /// staff numbering had already moved to <see cref="INumberSequenceService"/> for exactly this.</para>
    ///
    /// <para><b>Seeding.</b> Numbers already in the table were never issued by the counter. The first
    /// time the counter is used for a year it is advanced past the highest number on record for that
    /// year — deleted rows included, because those numbers are still in the index — so it keeps
    /// issuing after the numbers already in the wild rather than restarting at 000001. The seed
    /// runs only while the counter reads zero, so it costs one scan per tenant per year, not one per
    /// create. <see cref="INumberSequenceService.AdvanceToAtLeastAsync"/> only ever moves forward, so
    /// two concurrent first creates cannot lower it.</para>
    /// </remarks>
    private async Task<string> GenerateRequestNumberAsync()
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"LV{year}";
        var tenantId = GetTenantId();

        if (await _numberSequence.PeekAsync(LeaveRequestSequenceKey, year) == 0)
        {
            // GetQueryableIncludingDeleted: the repository applies its soft-delete filter as a plain
            // Where, which IgnoreQueryFilters cannot remove — and a deleted request's number is
            // still in the unique index.
            var existing = await _leaveRepository
                .GetQueryableIncludingDeleted(lr => lr.TenantId == tenantId && lr.RequestNumber.StartsWith(prefix))
                .Select(lr => lr.RequestNumber)
                .ToListAsync();

            long highest = 0;
            foreach (var number in existing)
            {
                if (number.Length > prefix.Length
                    && long.TryParse(number.AsSpan(prefix.Length), out var value)
                    && value > highest)
                {
                    highest = value;
                }
            }

            if (highest > 0)
                await _numberSequence.AdvanceToAtLeastAsync(LeaveRequestSequenceKey, highest, year);
        }

        var next = await _numberSequence.NextAsync(LeaveRequestSequenceKey, year);
        return $"{prefix}{next:D6}";
    }

    private Guid GetCurrentUserId()
    {
        return Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// The employee the token belongs to, for columns that are Employee foreign keys (an
    /// adjustment's <c>PerformedBy</c>). Not the user id: a user id is never an employee id, and the
    /// column's constraint refuses it.
    /// </summary>
    /// <remarks>
    /// Finish-plan lane 4 (2026-09-01). The screen sent the login's user id and the service accepted
    /// it, so no adjustment raised from the desk had ever been saved (the table was empty). The
    /// house rule from <c>HrControllerBase.TryGetEmployeeWriteContext</c>: an actor column names an
    /// employee, and an unlinked account cannot be that actor.
    /// </remarks>
    private Guid RequireActingEmployeeId(string purpose)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty)
            return me;
        throw new InvalidOperationException(
            $"{purpose} requires your user account to be linked to an employee record. Please contact your administrator.");
    }

    #endregion Private helper methods

    #region Leave Adjustment methods

    public async Task<LeaveAdjustmentDto> AddAdjustmentAsync(CreateLeaveAdjustmentDto dto)
    {
        var tenantId = GetTenantId();
        var balance = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.LeaveType)
            .Include(lb => lb.Employee)
            .FirstOrDefaultAsync(lb => lb.Id == dto.LeaveBalanceId && lb.TenantId == tenantId)
            ?? throw new ArgumentException($"Leave balance with ID '{dto.LeaveBalanceId}' not found.");

        var adjustment = new LeaveAdjustment
        {
            TenantId       = tenantId,
            LeaveBalanceId = dto.LeaveBalanceId,
            EmployeeId     = balance.EmployeeId,
            LeaveTypeId    = balance.LeaveTypeId,
            LeaveSubTypeId = balance.LeaveSubTypeId,
            Year           = balance.Year,
            Days           = dto.Days,
            ReasonCodeId   = dto.ReasonCodeId,
            Reason         = dto.Reason,
            AdjustmentDate = _clock.UtcNow,
            PerformedBy    = RequireActingEmployeeId("Recording a leave adjustment")
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveAdjustmentRepository.AddAsync(adjustment);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after adjustment
            await _recalculationService.RecalculateAsync(balance.EmployeeId, balance.LeaveTypeId, balance.Year);
        });

        // Reload to get navigation props
        var created = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .FirstOrDefaultAsync(a => a.Id == adjustment.Id && a.TenantId == tenantId)
            ?? adjustment;

        return created.ToDto();
    }

    public async Task<IEnumerable<LeaveAdjustmentDto>> GetAdjustmentsAsync(Guid balanceId)
    {
        await GetOwnedLeaveBalanceAsync(balanceId);

        var tenantId = GetTenantId();
        var adjustments = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .Where(a => a.TenantId == tenantId && a.LeaveBalanceId == balanceId)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync();

        return adjustments.ToDtoList();
    }

    public async Task<IEnumerable<LeaveAdjustmentDto>> GetAllAdjustmentsAsync(
        int year, Guid? employeeId, Guid? leaveTypeId, string? search)
    {
        var tenantId = GetTenantId();
        var query = _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .Where(a => a.TenantId == tenantId && a.Year == year);

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);

        if (leaveTypeId.HasValue)
            query = query.Where(a => a.LeaveTypeId == leaveTypeId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.Reason.Contains(search));

        var adjustments = await query
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync();

        return adjustments.ToDtoList();
    }

    public async Task<LeaveAdjustmentDto?> GetAdjustmentByIdAsync(Guid id)
    {
        var tenantId = GetTenantId();
        var adjustment = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);

        return adjustment?.ToDto();
    }

    public async Task<LeaveAdjustmentDto> CreateStandaloneAdjustmentAsync(CreateLeaveAdjustmentStandaloneDto dto)
    {
        var leaveType = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        var tenantId = GetTenantId();

        var balance = await _leaveBalanceRepository
            .GetQueryable()
            .FirstOrDefaultAsync(lb =>
                lb.TenantId == tenantId &&
                lb.EmployeeId  == dto.EmployeeId  &&
                lb.LeaveTypeId == dto.LeaveTypeId &&
                lb.Year        == dto.Year);

        if (balance == null)
        {
            balance = new LeaveBalance
            {
                TenantId        = tenantId,
                EmployeeId      = dto.EmployeeId,
                LeaveTypeId     = dto.LeaveTypeId,
                LeaveSubTypeId  = dto.LeaveSubTypeId,
                Year            = dto.Year,
                EntitledDays    = leaveType.DefaultDaysPerYear,
                CarriedOverDays = 0,
                UsedDays        = 0,
                PendingDays     = 0,
                AdjustmentDays  = 0
            };
            await _leaveBalanceRepository.AddAsync(balance);
            await _unitOfWork.SaveChangesAsync();
        }

        // Stamped from the token, never from the payload — see RequireActingEmployeeId. The old
        // fallback here was GetCurrentUserId(), a USER id, which the Employee constraint refuses.
        var performedBy = RequireActingEmployeeId("Recording a leave adjustment");

        var adjustment = new LeaveAdjustment
        {
            TenantId       = tenantId,
            LeaveBalanceId = balance.Id,
            EmployeeId     = dto.EmployeeId,
            LeaveTypeId    = dto.LeaveTypeId,
            LeaveSubTypeId = dto.LeaveSubTypeId,
            Year           = dto.Year,
            Days           = dto.Days,
            ReasonCodeId   = dto.ReasonCodeId,
            Reason         = dto.Reason,
            AdjustmentDate = dto.AdjustmentDate?.ToUniversalTime() ?? _clock.UtcNow,
            PerformedBy    = performedBy
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveAdjustmentRepository.AddAsync(adjustment);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(dto.EmployeeId, dto.LeaveTypeId, dto.Year);
        });

        var created = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .FirstOrDefaultAsync(a => a.Id == adjustment.Id && a.TenantId == tenantId)
            ?? adjustment;

        _logger.LogInformation(
            "Standalone adjustment created: employee={EmployeeId} type={LeaveTypeId} year={Year} days={Days}",
            dto.EmployeeId, dto.LeaveTypeId, dto.Year, dto.Days);

        return created.ToDto();
    }

    public async Task<LeaveAdjustmentDto> UpdateAdjustmentAsync(Guid id, UpdateLeaveAdjustmentDto dto)
    {
        var adjustment = await GetOwnedLeaveAdjustmentAsync(id);

        if (dto.Days == 0)
            throw new InvalidOperationException("Adjustment days cannot be zero.");

        adjustment.Days           = dto.Days;
        adjustment.ReasonCodeId   = dto.ReasonCodeId;
        adjustment.Reason         = dto.Reason;
        if (dto.AdjustmentDate.HasValue)
            adjustment.AdjustmentDate = dto.AdjustmentDate.Value.ToUniversalTime();

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveAdjustmentRepository.UpdateAsync(adjustment);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(adjustment.EmployeeId, adjustment.LeaveTypeId, adjustment.Year);
        });

        var tenantId = GetTenantId();
        var updated = await _leaveAdjustmentRepository
            .GetQueryable()
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.Employee)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveType)
            .Include(a => a.LeaveBalance).ThenInclude(lb => lb.LeaveSubType)
            .Include(a => a.PerformedByEmployee)
            .Include(a => a.ReasonCode)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId)
            ?? adjustment;

        _logger.LogInformation("Leave adjustment updated: id={AdjustmentId}", id);
        return updated.ToDto();
    }

    public async Task DeleteAdjustmentAsync(Guid adjustmentId)
    {
        var adjustment = await GetOwnedLeaveAdjustmentAsync(adjustmentId);

        var balanceForDelete = await GetOwnedLeaveBalanceAsync(adjustment.LeaveBalanceId);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _leaveAdjustmentRepository.DeleteAsync(adjustment);
            await _unitOfWork.SaveChangesAsync(ct);
            // Recalculate balance from source data after adjustment deletion
            await _recalculationService.RecalculateAsync(balanceForDelete.EmployeeId, balanceForDelete.LeaveTypeId, balanceForDelete.Year);
        });
    }

    #endregion Leave Adjustment methods

    #region Leave Request Attachment methods

    public async Task<LeaveRequestAttachmentDto> UploadAttachmentAsync(
        Guid leaveRequestId, Guid uploadedBy, string fileName, string filePath,
        string? contentType, long? fileSizeBytes,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null)
    {
        var request = await GetOwnedLeaveRequestAsync(leaveRequestId);

        var attachment = new LeaveRequestAttachment
        {
            LeaveRequestId = leaveRequestId,
            FileName       = fileName,
            FilePath       = filePath,
            ContentType    = contentType,
            FileSizeBytes  = fileSizeBytes,
            UploadedDate   = _clock.UtcNow,
            UploadedBy     = uploadedBy,
            TenantId       = request.TenantId,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId   = documentRecordId,
            DocumentVersionId  = documentVersionId
        };

        await _attachmentRepository.AddAsync(attachment);
        await _unitOfWork.SaveChangesAsync();

        // Reload with navigation for name
        var saved = await GetOwnedAttachmentAsync(attachment.Id);
        return saved.ToDto();
    }

    public async Task<IEnumerable<LeaveRequestAttachmentDto>> GetAttachmentsAsync(Guid leaveRequestId)
    {
        await GetOwnedLeaveRequestAsync(leaveRequestId);

        var tenantId = GetTenantId();
        var all = await _attachmentRepository
            .GetQueryable()
            .Include(a => a.UploadedByEmployee)
            .Where(a => a.TenantId == tenantId && a.LeaveRequestId == leaveRequestId)
            .ToListAsync();
        return all.Select(a => a.ToDto()).ToList();
    }

    public async Task<LeaveRequestAttachmentDto?> GetAttachmentByIdAsync(Guid attachmentId)
    {
        var attachment = await _attachmentRepository
            .GetQueryable()
            .Include(a => a.UploadedByEmployee)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TenantId == GetTenantId());
        return attachment?.ToDto();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId)
    {
        var tenantId = GetTenantId();
        var attachment = await _attachmentRepository
            .GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TenantId == tenantId);
        if (attachment == null) return false;

        await _attachmentRepository.DeleteAsync(attachment);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    #endregion Leave Request Attachment methods
}