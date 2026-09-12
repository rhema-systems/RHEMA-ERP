using ErpSystem.Application.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class LeaveEncashmentService : ILeaveEncashmentService
{
    private const string EntityType = "LeaveEncashment";

    private readonly IGenericRepository<LeaveEncashment> _encashmentRepository;
    private readonly ILeaveRepository _leaveRepository;
    private readonly IGenericRepository<LeaveBalance> _balanceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveEncashmentService> _logger;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILeaveBalanceRecalculationService _recalculationService;
    private readonly IEmolumentService _emolumentService;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepository;
    private readonly IDateTimeProvider _clock;

    public LeaveEncashmentService(
        IGenericRepository<LeaveEncashment> encashmentRepository,
        ILeaveRepository leaveRepository,
        IGenericRepository<LeaveBalance> balanceRepository,
        IUnitOfWork unitOfWork,
        ILogger<LeaveEncashmentService> logger,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ICurrentUserService currentUserService,
        ILeaveBalanceRecalculationService recalculationService,
        IEmolumentService emolumentService,
        IGenericRepository<LeaveType> leaveTypeRepository,
        IDateTimeProvider clock)
    {
        _encashmentRepository = encashmentRepository;
        _leaveRepository = leaveRepository;
        _balanceRepository = balanceRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserService = currentUserService;
        _recalculationService = recalculationService;
        _emolumentService = emolumentService;
        _leaveTypeRepository = leaveTypeRepository;
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

    // An encashment owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<LeaveEncashment> GetOwnedEncashmentAsync(Guid id)
    {
        var entity = await _encashmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave encashment '{id}' not found.");
        return entity;
    }

    private async Task<LeaveRequest> GetOwnedLeaveRequestAsync(Guid id)
    {
        var entity = await _leaveRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave request '{id}' not found.");
        return entity;
    }

    private async Task<LeaveType> GetOwnedLeaveTypeAsync(Guid id)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave type '{id}' not found.");
        return entity;
    }

    public async Task<LeaveEncashmentDto> RequestEncashmentAsync(CreateLeaveEncashmentDto dto)
    {
        var leaveRequest = await GetOwnedLeaveRequestAsync(dto.LeaveRequestId);

        if (leaveRequest.Encashment != null)
            throw new InvalidOperationException("This leave request has already been encashed.");

        // The leave type must permit cash conversion before any encashment can be requested.
        var leaveType = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        if (!leaveType.AllowCashConversion)
            throw new InvalidOperationException("This leave type does not allow cash conversion (encashment).");

        var tenantId = GetTenantId();
        var balance = await _balanceRepository.FirstOrDefaultAsync(
            b => b.TenantId == tenantId &&
                 b.EmployeeId == dto.EmployeeId &&
                 b.LeaveTypeId == dto.LeaveTypeId &&
                 b.Year == dto.Year);

        if (balance == null)
            throw new InvalidOperationException("No leave balance found for the specified year and leave type.");

        if (balance.AvailableDays < dto.DaysEncashed)
            throw new InvalidOperationException(
                $"Insufficient balance. Available: {balance.AvailableDays} days, Requested: {dto.DaysEncashed} days.");

        // Derive the payout from the employee's emoluments (basic + linked allowances) per the
        // leave type's rate policy — the server is the source of truth. Fall back to the supplied
        // amount only if the derivation yields nothing (e.g. no rate configured).
        var asOf = leaveRequest.StartDate;
        var dailyRate = await _emolumentService.GetEncashmentDailyRateAsync(dto.EmployeeId, dto.LeaveTypeId, asOf);
        var computedAmount = Math.Round(dailyRate * dto.DaysEncashed, 2);
        var amountPaid = computedAmount > 0 ? computedAmount : dto.AmountPaid;

        var entity = new LeaveEncashment
        {
            TenantId = tenantId,
            LeaveRequestId = dto.LeaveRequestId,
            EmployeeId = dto.EmployeeId,
            LeaveTypeId = dto.LeaveTypeId,
            Year = dto.Year,
            DaysEncashed = dto.DaysEncashed,
            AmountPaid = amountPaid,
            Notes = dto.Notes,
            Status = LeaveEncashmentStatus.Submitted
        };

        // Create and submit in ONE transaction: a workflow submit that throws (e.g. no active
        // definition for the tenant) must roll the insert back too, or the caller gets an error
        // while an orphaned Submitted row survives — and every retry then refuses with
        // "already been encashed". Found live in area 25 slice 4.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _encashmentRepository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);

            var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, entity.Id);

            if (workflowResult.ExecutionResult.Success)
            {
                var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
                adapter.ApplySubmitOutcome(entity, workflowResult.Outcome, GetCurrentUserId());
                await _encashmentRepository.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        });

        _logger.LogInformation("Leave encashment requested for employee {employeeId}: {days} days",
            dto.EmployeeId, dto.DaysEncashed);

        return (await GetWithIncludes(entity.Id))!.ToDto();
    }

    public async Task<LeaveEncashmentDto> ApproveEncashmentAsync(Guid id)
    {
        var entity = await GetOwnedEncashmentAsync(id);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Approve", null);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, userId);

        await _encashmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave encashment {id} approved", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeaveEncashmentDto> RejectEncashmentAsync(Guid id, string reason)
    {
        var entity = await GetOwnedEncashmentAsync(id);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var rejectionText = !string.IsNullOrWhiteSpace(reason) ? reason : "Rejected";
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Reject", rejectionText);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process rejection.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, userId, rejectionText);

        await _encashmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave encashment {id} rejected", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeaveEncashmentDto> MarkAsProcessedAsync(Guid id, ProcessLeaveEncashmentDto dto)
    {
        var entity = await GetOwnedEncashmentAsync(id);

        if (entity.Status != LeaveEncashmentStatus.Approved)
            throw new InvalidOperationException("Only approved encashments can be marked as processed.");

        entity.Status = LeaveEncashmentStatus.Processed;
        entity.ProcessedDate = _clock.UtcNow;
        entity.ProcessedByEmployeeId = dto.ProcessedByEmployeeId;
        entity.PaymentReference = dto.PaymentReference;

        // Mark processed and recalculate the balance in one transaction. The balance's
        // EncashedDays is derived from processed encashments by the recalculation service —
        // the single source of truth — so we never mutate UsedDays directly here.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _encashmentRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            await _recalculationService.RecalculateAsync(entity.EmployeeId, entity.LeaveTypeId, entity.Year);
        });

        _logger.LogInformation("Leave encashment {id} marked as processed with reference {ref}",
            id, dto.PaymentReference);

        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<IEnumerable<LeaveEncashmentDto>> GetEmployeeEncashmentsAsync(Guid employeeId, int year)
    {
        var tenantId = GetTenantId();
        var items = await _encashmentRepository
            .GetQueryable()
            .Include(e => e.Employee)
            .Include(e => e.ProcessedByEmployee)
            .Include(e => e.LeaveRequest).ThenInclude(r => r!.LeaveType)
            .Where(e => e.TenantId == tenantId && e.EmployeeId == employeeId && e.Year == year)
            .OrderByDescending(e => e.ProcessedDate)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<IEnumerable<LeaveEncashmentDto>> GetAllEncashmentsAsync(
        int year,
        Guid?     employeeId  = null,
        Guid?     leaveTypeId = null,
        DateTime? from        = null,
        DateTime? to          = null,
        string?   search      = null)
    {
        var tenantId = GetTenantId();
        var query = _encashmentRepository
            .GetQueryable()
            .Include(e => e.Employee)
            .Include(e => e.ProcessedByEmployee)
            .Include(e => e.LeaveRequest).ThenInclude(r => r!.LeaveType)
            .Where(e => e.TenantId == tenantId && e.Year == year);

        if (employeeId.HasValue)  query = query.Where(e => e.EmployeeId  == employeeId.Value);
        if (leaveTypeId.HasValue) query = query.Where(e => e.LeaveTypeId == leaveTypeId.Value);
        if (from.HasValue)        query = query.Where(e => e.ProcessedDate != null && e.ProcessedDate >= from.Value);
        if (to.HasValue)          query = query.Where(e => e.ProcessedDate != null && e.ProcessedDate <= to.Value.AddDays(1));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                (e.PaymentReference != null && e.PaymentReference.Contains(term)) ||
                e.Employee.FirstName.Contains(term) ||
                e.Employee.LastName.Contains(term));
        }

        var items = await query.OrderByDescending(e => e.ProcessedDate).ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveEncashmentDto> GetByIdAsync(Guid id)
    {
        var entity = await GetWithIncludes(id);
        if (entity == null)
            throw new ArgumentException($"Leave encashment '{id}' not found.");
        return entity.ToDto();
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;

    private async Task<LeaveEncashment?> GetWithIncludes(Guid id)
    {
        var tenantId = GetTenantId();
        return await _encashmentRepository
            .GetQueryable()
            .Include(e => e.Employee)
            .Include(e => e.ProcessedByEmployee)
            .Include(e => e.LeaveRequest).ThenInclude(r => r!.LeaveType)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);
    }
}
