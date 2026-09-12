using ErpSystem.Application.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class LeavePlanService : ILeavePlanService
{
    private const string EntityType = "LeavePlan";

    private readonly ILeavePlanRepository _leavePlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeavePlanService> _logger;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserService _currentUserService;

    public LeavePlanService(
        ILeavePlanRepository leavePlanRepository,
        IUnitOfWork unitOfWork,
        ILogger<LeavePlanService> logger,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ICurrentUserService currentUserService)
    {
        _leavePlanRepository = leavePlanRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserService = currentUserService;
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

    // A leave plan owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<LeavePlan> GetOwnedLeavePlanAsync(Guid id)
    {
        var entity = await _leavePlanRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave plan '{id}' not found.");
        return entity;
    }

    private async Task<bool> HasConflictingPlanAsync(
        Guid employeeId, DateOnly startDate, DateOnly endDate, Guid? excludePlanId = null)
    {
        var tenantId = GetTenantId();
        var query = _leavePlanRepository.GetQueryable().Where(p =>
            p.TenantId == tenantId &&
            p.EmployeeId == employeeId &&
            p.Status != LeavePlanStatus.Cancelled &&
            p.Status != LeavePlanStatus.Rejected &&
            p.StartDate <= endDate &&
            p.EndDate >= startDate);

        if (excludePlanId.HasValue)
            query = query.Where(p => p.Id != excludePlanId.Value);

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<LeavePlanDto>> GetByEmployeeAndYearAsync(Guid employeeId, int year)
    {
        var tenantId = GetTenantId();
        var items = await _leavePlanRepository
            .GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.LeaveType)
            .Include(p => p.LeaveSubType)
            .Include(p => p.RelieverEmployee)
            .Include(p => p.PlannedByEmployee)
            .Where(p => p.TenantId == tenantId && p.EmployeeId == employeeId && p.Year == year)
            .OrderBy(p => p.StartDate)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<IEnumerable<LeavePlanDto>> GetByYearAsync(int year)
    {
        var tenantId = GetTenantId();
        var items = await _leavePlanRepository
            .GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.LeaveType)
            .Include(p => p.LeaveSubType)
            .Include(p => p.RelieverEmployee)
            .Include(p => p.PlannedByEmployee)
            .Include(p => p.OrganizationUnit)
            .Where(p => p.TenantId == tenantId && p.Year == year)
            .OrderBy(p => p.StartDate)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeavePlanDto> GetByIdAsync(Guid id)
    {
        var entity = await GetWithIncludes(id);
        if (entity == null)
            throw new ArgumentException($"Leave plan '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<LeavePlanDto> CreateLeavePlanAsync(CreateLeavePlanDto dto)
    {
        if (dto.EndDate < dto.StartDate)
            throw new InvalidOperationException("End date must be after or equal to start date.");

        var hasConflict = await HasConflictingPlanAsync(dto.EmployeeId, dto.StartDate, dto.EndDate);
        if (hasConflict)
            throw new InvalidOperationException("Employee already has a leave plan for this period.");

        var entity = dto.ToEntity();
        entity.TenantId = GetTenantId();
        await _leavePlanRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Leave plan created for employee {employeeId}", dto.EmployeeId);
        return (await GetWithIncludes(entity.Id))!.ToDto();
    }

    public async Task<LeavePlanDto> UpdateLeavePlanAsync(Guid id, CreateLeavePlanDto dto)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        if (entity.Status != LeavePlanStatus.Draft)
            throw new InvalidOperationException("Only draft leave plans can be edited.");

        if (dto.EndDate < dto.StartDate)
            throw new InvalidOperationException("End date must be after or equal to start date.");

        var hasConflict = await HasConflictingPlanAsync(dto.EmployeeId, dto.StartDate, dto.EndDate, id);
        if (hasConflict)
            throw new InvalidOperationException("Employee already has a leave plan for this period.");

        entity.EmployeeId = dto.EmployeeId;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.PositionId = dto.PositionId;
        entity.LeaveTypeId = dto.LeaveTypeId;
        entity.LeaveSubTypeId = dto.LeaveSubTypeId;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.RelieverId = dto.RelieverId;
        entity.SecondRelieverId = dto.SecondRelieverId;
        entity.Notes = dto.Notes;
        entity.PlannedBy = dto.PlannedBy;
        entity.Year = dto.Year;

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeavePlanDto> SubmitLeavePlanAsync(Guid id)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        if (entity.Status != LeavePlanStatus.Draft)
            throw new InvalidOperationException("Only draft leave plans can be submitted.");

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, id);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start approval workflow.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(entity, workflowResult, GetCurrentUserId());

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} submitted for approval", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeavePlanDto> ApproveLeavePlanAsync(Guid id)
    {
        var entity = await GetOwnedLeavePlanAsync(id);

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

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} approved", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeavePlanDto> RejectLeavePlanAsync(Guid id, string reason)
    {
        var entity = await GetOwnedLeavePlanAsync(id);

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

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} rejected", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeavePlanDto> SuggestChangesAsync(Guid id, SuggestLeavePlanChangesDto dto)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        if (entity.Status != LeavePlanStatus.Submitted)
            throw new InvalidOperationException("Only submitted leave plans can have changes suggested.");
        if (dto.SuggestedEndDate < dto.SuggestedStartDate)
            throw new InvalidOperationException("Suggested end date must be after or equal to start date.");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        // Suggesting changes sends the plan back to the employee, so the active approval
        // workflow is cancelled; a fresh one starts when the employee re-submits.
        var cancelResult = await _workflowIntegrationService.CancelWorkflowAsync(
            EntityType, id, "Manager suggested alternative dates");
        if (!cancelResult.Success)
            throw new InvalidOperationException(cancelResult.Message ?? "Failed to update the approval workflow.");

        entity.Status = LeavePlanStatus.ChangesSuggested;
        entity.SuggestedStartDate = dto.SuggestedStartDate;
        entity.SuggestedEndDate = dto.SuggestedEndDate;
        entity.ManagerSuggestionNotes = dto.Notes;
        entity.WorkflowInstanceId = null;
        entity.ApprovedById = null;
        entity.ApprovedDate = null;

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} sent back with suggested changes by {userId}", id, userId);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeavePlanDto> RespondToSuggestionAsync(Guid id, RespondToLeaveSuggestionDto dto)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        if (entity.Status != LeavePlanStatus.ChangesSuggested)
            throw new InvalidOperationException("This leave plan has no suggested changes to respond to.");

        DateOnly newStart, newEnd;
        if (dto.Accept)
        {
            if (entity.SuggestedStartDate == null || entity.SuggestedEndDate == null)
                throw new InvalidOperationException("No suggested dates are available to accept.");
            newStart = entity.SuggestedStartDate.Value;
            newEnd = entity.SuggestedEndDate.Value;
        }
        else
        {
            if (dto.StartDate == null || dto.EndDate == null)
                throw new InvalidOperationException("Provide your preferred start and end dates.");
            newStart = dto.StartDate.Value;
            newEnd = dto.EndDate.Value;
        }

        if (newEnd < newStart)
            throw new InvalidOperationException("End date must be after or equal to start date.");

        var hasConflict = await HasConflictingPlanAsync(entity.EmployeeId, newStart, newEnd, id);
        if (hasConflict)
            throw new InvalidOperationException("Employee already has a leave plan for this period.");

        entity.StartDate = newStart;
        entity.EndDate = newEnd;
        entity.Year = newStart.Year;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;

        // The suggestion is now resolved — clear it before re-submitting.
        entity.SuggestedStartDate = null;
        entity.SuggestedEndDate = null;
        entity.ManagerSuggestionNotes = null;

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start approval workflow.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(entity, workflowResult, GetCurrentUserId());

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} re-submitted after suggestion ({mode})",
            id, dto.Accept ? "accepted" : "countered");
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task CancelLeavePlanAsync(Guid id)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        if (entity.Status == LeavePlanStatus.Cancelled)
            throw new InvalidOperationException("Leave plan is already cancelled.");

        entity.Status = LeavePlanStatus.Cancelled;
        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;

    private async Task<LeavePlan?> GetWithIncludes(Guid id)
    {
        var tenantId = GetTenantId();
        return await _leavePlanRepository
            .GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.LeaveType)
            .Include(p => p.LeaveSubType)
            .Include(p => p.OrganizationLevel)
            .Include(p => p.OrganizationUnit)
            .Include(p => p.Position)
            .Include(p => p.RelieverEmployee)
            .Include(p => p.SecondRelieverEmployee)
            .Include(p => p.PlannedByEmployee)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
    }
}
