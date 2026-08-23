using System.Data;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class ProcurementBudgetService : IProcurementBudgetService
{
    private const string WorkflowEntityType = "ProcurementBudget";
    private readonly IProcurementBudgetRepository _budgetRepository;
    private readonly IProcurementBudgetAllocationRepository _allocationRepository;
    private readonly IProcurementBudgetRevisionRepository _revisionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ILogger<ProcurementBudgetService> _logger;

    public ProcurementBudgetService(
        IProcurementBudgetRepository budgetRepository,
        IProcurementBudgetAllocationRepository allocationRepository,
        IProcurementBudgetRevisionRepository revisionRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ILogger<ProcurementBudgetService> logger)
    {
        _budgetRepository = budgetRepository;
        _allocationRepository = allocationRepository;
        _revisionRepository = revisionRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _logger = logger;
    }

    public async Task<ProcurementBudgetDetailDto?> GetByIdAsync(Guid id)
    {
        var budget = await _budgetRepository.GetWithFullDetailsAsync(id);
        return budget == null ? null : MapToDetailDto(budget);
    }

    public async Task<ProcurementBudgetDto?> GetByBudgetCodeAsync(string budgetCode)
    {
        var budget = await _budgetRepository.GetByBudgetCodeAsync(budgetCode);
        return budget == null ? null : MapToDto(budget);
    }

    public async Task<PagedResult<ProcurementBudgetDto>> GetBudgetsAsync(
        int page, int pageSize, string? search = null, string? status = null,
        Guid? departmentId = null, int? fiscalYear = null)
    {
        var result = await _budgetRepository.GetBudgetsAsync(page, pageSize, search, status, departmentId, fiscalYear);
        return new PagedResult<ProcurementBudgetDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IEnumerable<ProcurementBudgetDto>> GetByDepartmentAsync(Guid departmentId)
    {
        var budgets = await _budgetRepository.GetByDepartmentAsync(departmentId);
        return budgets.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcurementBudgetDto>> GetByFiscalYearAsync(int fiscalYear)
    {
        var budgets = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);
        return budgets.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcurementBudgetDto>> GetActiveBudgetsAsync()
    {
        var budgets = await _budgetRepository.GetActiveBudgetsAsync();
        return budgets.Select(MapToDto);
    }

    public async Task<ProcurementBudgetDetailDto> CreateAsync(CreateProcurementBudgetDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A tenant context is required to create a procurement budget.");

        async Task<ProcurementBudgetDetailDto> CreateUnderNumberLockAsync()
        {
            await _unitOfWork.AcquireTransactionLockAsync(
                $"procurement-budget-number:{tenantId:N}:{dto.FiscalYear}");

            var budgetCode = await _budgetRepository.GenerateBudgetCodeAsync(dto.FiscalYear, tenantId);

            var budget = new ProcurementBudget
            {
                BudgetCode = budgetCode,
                Title = dto.Title,
                Description = dto.Description,
                DepartmentId = dto.DepartmentId,
                ProcurementPlanId = dto.ProcurementPlanId,
                FiscalYear = dto.FiscalYear,
                AllocatedAmount = dto.AllocatedAmount,
                Currency = dto.Currency,
                ControlLevel = dto.ControlLevel,
                WarningThresholdPercent = dto.WarningThresholdPercent,
                EffectiveDate = dto.EffectiveDate,
                ExpiryDate = dto.ExpiryDate,
                Notes = dto.Notes,
                Status = "Draft",
                TenantId = tenantId
            };

            await _budgetRepository.AddAsync(budget);

            foreach (var allocationDto in dto.Allocations)
            {
                var allocation = new ProcurementBudgetAllocation
                {
                    ProcurementBudgetId = budget.Id,
                    CategoryName = allocationDto.CategoryName,
                    CategoryDescription = allocationDto.CategoryDescription,
                    AllocatedAmount = allocationDto.AllocatedAmount,
                    Notes = allocationDto.Notes,
                    TenantId = tenantId
                };
                await _allocationRepository.AddAsync(allocation);
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Created procurement budget {BudgetCode}", budgetCode);

            return await GetByIdAsync(budget.Id)
                ?? throw new InvalidOperationException("Failed to retrieve created budget");
        }

        if (_unitOfWork.HasActiveTransaction)
            return await CreateUnderNumberLockAsync();

        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var result = await CreateUnderNumberLockAsync();
                await _unitOfWork.CommitAsync();
                return result;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync();
                else
                    _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    public async Task<ProcurementBudgetDetailDto> UpdateAsync(Guid id, CreateProcurementBudgetDto dto)
    {
        var budget = await _budgetRepository.GetByIdAsync(id);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {id} not found");
        if (budget.Status != "Draft") throw new InvalidOperationException("Only draft budgets can be updated");

        budget.Title = dto.Title;
        budget.Description = dto.Description;
        budget.DepartmentId = dto.DepartmentId;
        budget.ProcurementPlanId = dto.ProcurementPlanId;
        budget.FiscalYear = dto.FiscalYear;
        budget.AllocatedAmount = dto.AllocatedAmount;
        budget.Currency = dto.Currency;
        budget.ControlLevel = dto.ControlLevel;
        budget.WarningThresholdPercent = dto.WarningThresholdPercent;
        budget.EffectiveDate = dto.EffectiveDate;
        budget.ExpiryDate = dto.ExpiryDate;
        budget.Notes = dto.Notes;
        budget.UpdatedAt = DateTime.UtcNow;

        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated budget");
    }

    public async Task<ProcurementBudgetDetailDto> SubmitForApprovalAsync(Guid id)
    {
        var budget = await _budgetRepository.GetByIdAsync(id);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {id} not found");
        if (budget.Status != "Draft") throw new InvalidOperationException("Only draft budgets can be submitted for approval.");

        var currentUserId = _currentUserProvider.UserId;
        if (currentUserId == Guid.Empty) throw new UnauthorizedAccessException("User not authenticated.");

        var workflowResult = await _workflowIntegrationService.SubmitAsync(WorkflowEntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start the procurement budget workflow.");

        _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType)
            .ApplySubmitOutcome(budget, workflowResult.Outcome, currentUserId);
        budget.UpdatedAt = DateTime.UtcNow;
        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Procurement budget {BudgetCode} submitted to shared workflow", budget.BudgetCode);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve budget");
    }

    public async Task<ProcurementBudgetDetailDto> ApproveAsync(Guid id, ApproveProcurementBudgetDto dto)
    {
        var budget = await _budgetRepository.GetByIdAsync(id);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {id} not found");
        if (!string.Equals(budget.Status, "Submitted", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(budget.Status, "UnderReview", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a submitted procurement budget can be approved or rejected.");

        var currentUserId = _currentUserProvider.UserId;
        if (currentUserId == Guid.Empty) throw new UnauthorizedAccessException("User not authenticated.");
        if (!await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, id, currentUserId))
            throw new UnauthorizedAccessException("You are not assigned to the active procurement budget workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            WorkflowEntityType,
            id,
            currentUserId,
            dto.IsApproved ? "Approve" : "Reject",
            dto.Comments);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the procurement budget workflow decision.");

        _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType)
            .ApplyApprovalOutcome(budget, workflowResult.Outcome, currentUserId, dto.Comments);
        budget.UpdatedAt = DateTime.UtcNow;

        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Procurement budget {BudgetCode} workflow decision resulted in {Status}", budget.BudgetCode, budget.Status);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve budget");
    }

    public async Task DeleteAsync(Guid id)
    {
        var budget = await _budgetRepository.GetByIdAsync(id);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {id} not found");
        if (budget.Status == "Approved") throw new InvalidOperationException("Approved budgets cannot be deleted");

        budget.IsDeleted = true;
        budget.UpdatedAt = DateTime.UtcNow;
        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ProcurementBudgetAllocationDto> AddAllocationAsync(Guid budgetId, CreateProcurementBudgetAllocationDto dto)
    {
        var allocation = new ProcurementBudgetAllocation
        {
            ProcurementBudgetId = budgetId,
            CategoryName = dto.CategoryName,
            CategoryDescription = dto.CategoryDescription,
            AllocatedAmount = dto.AllocatedAmount,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };
        await _allocationRepository.AddAsync(allocation);
        await _unitOfWork.SaveChangesAsync();
        return MapToAllocationDto(allocation);
    }

    public async Task<ProcurementBudgetAllocationDto> UpdateAllocationAsync(Guid allocationId, CreateProcurementBudgetAllocationDto dto)
    {
        var allocation = await _allocationRepository.GetByIdAsync(allocationId);
        if (allocation == null) throw new KeyNotFoundException($"Allocation with ID {allocationId} not found");

        allocation.CategoryName = dto.CategoryName;
        allocation.CategoryDescription = dto.CategoryDescription;
        allocation.AllocatedAmount = dto.AllocatedAmount;
        allocation.Notes = dto.Notes;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _allocationRepository.UpdateAsync(allocation);
        await _unitOfWork.SaveChangesAsync();
        return MapToAllocationDto(allocation);
    }

    public async Task DeleteAllocationAsync(Guid allocationId)
    {
        var allocation = await _allocationRepository.GetByIdAsync(allocationId);
        if (allocation == null) throw new KeyNotFoundException($"Allocation with ID {allocationId} not found");

        allocation.IsDeleted = true;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _allocationRepository.UpdateAsync(allocation);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ProcurementBudgetRevisionDto> CreateRevisionAsync(Guid budgetId, CreateProcurementBudgetRevisionDto dto)
    {
        var budget = await _budgetRepository.GetByIdAsync(budgetId);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {budgetId} not found");

        var revisionNumber = await _revisionRepository.GetNextRevisionNumberAsync(budgetId);
        var revision = new ProcurementBudgetRevision
        {
            ProcurementBudgetId = budgetId,
            RevisionNumber = revisionNumber,
            RevisionType = dto.RevisionType,
            PreviousAmount = budget.AllocatedAmount,
            NewAmount = dto.NewAmount,
            ChangeAmount = dto.NewAmount - budget.AllocatedAmount,
            Reason = dto.Reason,
            Status = "Pending",
            TenantId = _currentUserProvider.TenantId
        };
        await _revisionRepository.AddAsync(revision);
        await _unitOfWork.SaveChangesAsync();
        return MapToRevisionDto(revision);
    }

    public async Task<ProcurementBudgetRevisionDto> ApproveRevisionAsync(Guid revisionId)
    {
        var revision = await _revisionRepository.GetByIdAsync(revisionId);
        if (revision == null) throw new KeyNotFoundException($"Revision with ID {revisionId} not found");

        var budget = await _budgetRepository.GetByIdAsync(revision.ProcurementBudgetId);
        if (budget == null) throw new KeyNotFoundException("Associated budget not found");

        var currentUserId = _currentUserProvider.UserId;
        revision.Status = "Approved";
        revision.ApprovedById = currentUserId != Guid.Empty ? currentUserId : null;
        revision.ApprovedDate = DateTime.UtcNow;
        budget.AllocatedAmount = revision.NewAmount;
        budget.UpdatedAt = DateTime.UtcNow;

        await _revisionRepository.UpdateAsync(revision);
        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();
        return MapToRevisionDto(revision);
    }

    public async Task<ProcurementBudgetRevisionDto> RejectRevisionAsync(Guid revisionId, string reason)
    {
        var revision = await _revisionRepository.GetByIdAsync(revisionId);
        if (revision == null) throw new KeyNotFoundException($"Revision with ID {revisionId} not found");

        revision.Status = "Rejected";
        revision.Reason = reason;
        await _revisionRepository.UpdateAsync(revision);
        await _unitOfWork.SaveChangesAsync();
        return MapToRevisionDto(revision);
    }

    public async Task<IEnumerable<ProcurementBudgetRevisionDto>> GetRevisionsAsync(Guid budgetId)
    {
        var revisions = await _revisionRepository.GetByBudgetIdAsync(budgetId);
        return revisions.Select(MapToRevisionDto);
    }

    public async Task<decimal> GetTotalAllocatedAsync(Guid departmentId, int fiscalYear)
        => await _budgetRepository.GetTotalAllocatedBudgetAsync(departmentId, fiscalYear);

    public async Task<decimal> GetTotalUtilizedAsync(Guid departmentId, int fiscalYear)
        => await _budgetRepository.GetTotalUtilizedBudgetAsync(departmentId, fiscalYear);

    public async Task RecordUtilizationAsync(Guid budgetId, decimal amount, string? category = null)
    {
        var budget = await _budgetRepository.GetByIdAsync(budgetId);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {budgetId} not found");

        budget.UtilizedAmount += amount;
        budget.RemainingAmount = budget.AllocatedAmount - budget.UtilizedAmount - budget.CommittedAmount;
        budget.UpdatedAt = DateTime.UtcNow;

        await UpdateCategoryAllocation(budgetId, category, utilizedDelta: amount);

        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task CommitBudgetAsync(Guid budgetId, decimal amount, string? category = null)
    {
        var budget = await _budgetRepository.GetByIdAsync(budgetId);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {budgetId} not found");

        budget.CommittedAmount += amount;
        budget.RemainingAmount = budget.AllocatedAmount - budget.UtilizedAmount - budget.CommittedAmount;
        budget.UpdatedAt = DateTime.UtcNow;

        await UpdateCategoryAllocation(budgetId, category, committedDelta: amount);

        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Committed {Amount} to budget {BudgetId}, category: {Category}", amount, budgetId, category ?? "N/A");
    }

    public async Task ReleaseCommittedBudgetAsync(Guid budgetId, decimal amount, string? category = null)
    {
        var budget = await _budgetRepository.GetByIdAsync(budgetId);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {budgetId} not found");

        budget.CommittedAmount = Math.Max(0, budget.CommittedAmount - amount);
        budget.RemainingAmount = budget.AllocatedAmount - budget.UtilizedAmount - budget.CommittedAmount;
        budget.UpdatedAt = DateTime.UtcNow;

        await UpdateCategoryAllocation(budgetId, category, committedDelta: -amount);

        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Released {Amount} from committed budget {BudgetId}, category: {Category}", amount, budgetId, category ?? "N/A");
    }

    public async Task UtilizeCommittedBudgetAsync(Guid budgetId, decimal amount, string? category = null)
    {
        var budget = await _budgetRepository.GetByIdAsync(budgetId);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {budgetId} not found");

        // Move from committed to utilized
        budget.CommittedAmount = Math.Max(0, budget.CommittedAmount - amount);
        budget.UtilizedAmount += amount;
        budget.RemainingAmount = budget.AllocatedAmount - budget.UtilizedAmount - budget.CommittedAmount;
        budget.UpdatedAt = DateTime.UtcNow;

        await UpdateCategoryAllocation(budgetId, category, committedDelta: -amount, utilizedDelta: amount);

        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Utilized {Amount} from committed budget {BudgetId}, category: {Category}", amount, budgetId, category ?? "N/A");
    }

    public async Task<bool> UtilizePurchaseOrderCommittedBudgetAsync(
        Guid purchaseOrderId,
        decimal amount)
    {
        if (purchaseOrderId == Guid.Empty)
            throw new ArgumentException("A purchase order is required.", nameof(purchaseOrderId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "The utilized amount must be greater than zero.");

        var tenantId = _currentUserProvider.TenantId;
        var planItem = await _unitOfWork.Repository<ProcurementPlanItem>()
            .GetQueryable(item =>
                item.TenantId == tenantId &&
                item.PurchaseOrderId == purchaseOrderId &&
                !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync();
        if (planItem is null)
        {
            _logger.LogWarning(
                "No procurement plan item is linked to purchase order {PurchaseOrderId}; budget utilization was skipped",
                purchaseOrderId);
            return false;
        }

        var budget = await _budgetRepository.GetQueryable(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                (item.Status == "Active" || item.Status == "Approved") &&
                (planItem.ProcurementBudgetId.HasValue
                    ? item.Id == planItem.ProcurementBudgetId.Value
                    : item.ProcurementPlanId == planItem.ProcurementPlanId))
            .OrderByDescending(item => item.ApprovedDate)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync();
        if (budget is null)
        {
            _logger.LogWarning(
                "No active or approved procurement budget is linked to purchase order {PurchaseOrderId}; budget utilization was skipped",
                purchaseOrderId);
            return false;
        }

        await UtilizeCommittedBudgetAsync(budget.Id, amount, planItem.ItemCategory);
        _logger.LogInformation(
            "Utilized {Amount} from budget {BudgetCode} for purchase order {PurchaseOrderId}, category {Category}",
            amount,
            budget.BudgetCode,
            purchaseOrderId,
            planItem.ItemCategory ?? "N/A");
        return true;
    }

    public async Task<IEnumerable<ProcurementBudgetDto>> GetAvailableBudgetsForLinkingAsync(
        Guid departmentId,
        int fiscalYear,
        bool includeLinked = false)
    {
        var budgets = await _budgetRepository.GetByDepartmentAsync(departmentId);
        var filtered = budgets.Where(b =>
            b.FiscalYear == fiscalYear &&
            (b.Status == "Active" || b.Status == "Approved"));

        return filtered.Select(MapToDto);
    }

    public async Task LinkBudgetToPlanAsync(Guid budgetId, Guid planId)
    {
        var budget = await _budgetRepository.GetByIdAsync(budgetId);
        if (budget == null) throw new KeyNotFoundException($"Budget with ID {budgetId} not found");

        if (budget.TenantId != _currentUserProvider.TenantId || budget.IsDeleted)
            throw new KeyNotFoundException($"Budget with ID {budgetId} not found");
        if (!string.Equals(budget.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(budget.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only an approved or active procurement budget can be linked to a plan.");
        if (budget.ProcurementPlanId.HasValue && budget.ProcurementPlanId.Value != planId)
            throw new InvalidOperationException("The procurement budget is already linked to another plan.");

        budget.ProcurementPlanId = planId;
        budget.UpdatedAt = DateTime.UtcNow;

        await _budgetRepository.UpdateAsync(budget);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Linked budget {BudgetId} to plan {PlanId}", budgetId, planId);
    }

    /// <summary>
    /// Update category allocation with fallback to "General" or "Other" if category not found
    /// </summary>
    private async Task UpdateCategoryAllocation(Guid budgetId, string? category, decimal committedDelta = 0, decimal utilizedDelta = 0)
    {
        if (committedDelta == 0 && utilizedDelta == 0) return;

        ProcurementBudgetAllocation? allocation = null;

        // Try to find exact category match
        if (!string.IsNullOrEmpty(category))
        {
            allocation = await _allocationRepository.GetByCategoryAsync(budgetId, category);
        }

        // Fallback to "General" or "Other" if no match
        if (allocation == null)
        {
            allocation = await _allocationRepository.GetByCategoryAsync(budgetId, "General");
            if (allocation == null)
            {
                allocation = await _allocationRepository.GetByCategoryAsync(budgetId, "Other");
            }
        }

        if (allocation != null)
        {
            allocation.UtilizedAmount += utilizedDelta;
            allocation.RemainingAmount = allocation.AllocatedAmount - allocation.UtilizedAmount;
            allocation.UpdatedAt = DateTime.UtcNow;
            await _allocationRepository.UpdateAsync(allocation);
        }
    }

    #region Mapping Methods

    private static ProcurementBudgetDto MapToDto(ProcurementBudget budget)
    {
        return new ProcurementBudgetDto
        {
            Id = budget.Id,
            BudgetCode = budget.BudgetCode,
            Title = budget.Title,
            Description = budget.Description,
            DepartmentId = budget.DepartmentId,
            DepartmentName = budget.Department?.Name,
            ProcurementPlanId = budget.ProcurementPlanId,
            FiscalYear = budget.FiscalYear,
            AllocatedAmount = budget.AllocatedAmount,
            UtilizedAmount = budget.UtilizedAmount,
            CommittedAmount = budget.CommittedAmount,
            RemainingAmount = budget.AllocatedAmount - budget.UtilizedAmount - budget.CommittedAmount,
            Currency = budget.Currency,
            Status = budget.Status,
            ControlLevel = budget.ControlLevel,
            WarningThresholdPercent = budget.WarningThresholdPercent,
            EffectiveDate = budget.EffectiveDate,
            ExpiryDate = budget.ExpiryDate,
            ApprovedByName = budget.ApprovedBy?.FullName,
            ApprovedDate = budget.ApprovedDate,
            UtilizationPercent = budget.AllocatedAmount > 0 ? (budget.UtilizedAmount / budget.AllocatedAmount) * 100 : 0,
            CreatedAt = budget.CreatedAt
        };
    }

    private static ProcurementBudgetDetailDto MapToDetailDto(ProcurementBudget budget)
    {
        var dto = new ProcurementBudgetDetailDto
        {
            Id = budget.Id,
            BudgetCode = budget.BudgetCode,
            Title = budget.Title,
            Description = budget.Description,
            DepartmentId = budget.DepartmentId,
            DepartmentName = budget.Department?.Name,
            ProcurementPlanId = budget.ProcurementPlanId,
            FiscalYear = budget.FiscalYear,
            AllocatedAmount = budget.AllocatedAmount,
            UtilizedAmount = budget.UtilizedAmount,
            CommittedAmount = budget.CommittedAmount,
            RemainingAmount = budget.AllocatedAmount - budget.UtilizedAmount - budget.CommittedAmount,
            Currency = budget.Currency,
            Status = budget.Status,
            ControlLevel = budget.ControlLevel,
            WarningThresholdPercent = budget.WarningThresholdPercent,
            EffectiveDate = budget.EffectiveDate,
            ExpiryDate = budget.ExpiryDate,
            ApprovedById = budget.ApprovedById,
            ApprovedByName = budget.ApprovedBy?.FullName,
            ApprovedDate = budget.ApprovedDate,
            UtilizationPercent = budget.AllocatedAmount > 0 ? (budget.UtilizedAmount / budget.AllocatedAmount) * 100 : 0,
            CreatedAt = budget.CreatedAt,
            Notes = budget.Notes,
            Allocations = budget.Allocations?.Where(a => !a.IsDeleted).Select(MapToAllocationDto).ToList() ?? new(),
            Revisions = budget.Revisions?.Where(r => !r.IsDeleted).Select(MapToRevisionDto).ToList() ?? new()
        };
        return dto;
    }

    private static ProcurementBudgetAllocationDto MapToAllocationDto(ProcurementBudgetAllocation allocation)
    {
        return new ProcurementBudgetAllocationDto
        {
            Id = allocation.Id,
            ProcurementBudgetId = allocation.ProcurementBudgetId,
            CategoryName = allocation.CategoryName,
            CategoryDescription = allocation.CategoryDescription,
            AllocatedAmount = allocation.AllocatedAmount,
            UtilizedAmount = allocation.UtilizedAmount,
            RemainingAmount = allocation.AllocatedAmount - allocation.UtilizedAmount,
            UtilizationPercent = allocation.AllocatedAmount > 0 ? (allocation.UtilizedAmount / allocation.AllocatedAmount) * 100 : 0,
            Notes = allocation.Notes
        };
    }

    private static ProcurementBudgetRevisionDto MapToRevisionDto(ProcurementBudgetRevision revision)
    {
        return new ProcurementBudgetRevisionDto
        {
            Id = revision.Id,
            ProcurementBudgetId = revision.ProcurementBudgetId,
            RevisionNumber = revision.RevisionNumber,
            RevisionType = revision.RevisionType,
            PreviousAmount = revision.PreviousAmount,
            NewAmount = revision.NewAmount,
            ChangeAmount = revision.ChangeAmount,
            Reason = revision.Reason,
            ApprovedByName = revision.ApprovedBy?.FullName,
            ApprovedDate = revision.ApprovedDate,
            Status = revision.Status,
            CreatedAt = revision.CreatedAt
        };
    }

    #endregion
}
