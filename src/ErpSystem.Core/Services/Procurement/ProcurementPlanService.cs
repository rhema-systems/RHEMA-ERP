using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class ProcurementPlanService : IProcurementPlanService
{
    private readonly IProcurementPlanRepository _planRepository;
    private readonly IProcurementPlanItemRepository _itemRepository;
    private readonly IProcurementPlanItemSupplierRepository _itemSupplierRepository;
    private readonly ITenderService _tenderService;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseOrderItemRepository _purchaseOrderItemRepository;
    private readonly IProcurementScheduleService _scheduleService;
    private readonly IProcurementBudgetRepository _budgetRepository;
    private readonly IProcurementBudgetService _budgetService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ProcurementPlanService> _logger;

    public ProcurementPlanService(
        IProcurementPlanRepository planRepository,
        IProcurementPlanItemRepository itemRepository,
        IProcurementPlanItemSupplierRepository itemSupplierRepository,
        ITenderService tenderService,
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseOrderItemRepository purchaseOrderItemRepository,
        IProcurementScheduleService scheduleService,
        IProcurementBudgetRepository budgetRepository,
        IProcurementBudgetService budgetService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<ProcurementPlanService> logger)
    {
        _planRepository = planRepository;
        _itemRepository = itemRepository;
        _itemSupplierRepository = itemSupplierRepository;
        _tenderService = tenderService;
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseOrderItemRepository = purchaseOrderItemRepository;
        _scheduleService = scheduleService;
        _budgetRepository = budgetRepository;
        _budgetService = budgetService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<ProcurementPlanDetailDto?> GetByIdAsync(Guid id)
    {
        var plan = await _planRepository.GetWithFullDetailsAsync(id);
        return plan == null ? null : MapToDetailDto(plan);
    }

    public async Task<ProcurementPlanDto?> GetByPlanNumberAsync(string planNumber)
    {
        var plan = await _planRepository.GetByPlanNumberAsync(planNumber);
        return plan == null ? null : MapToDto(plan);
    }

    public async Task<PagedResult<ProcurementPlanDto>> GetPlansAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        int? fiscalYear = null)
    {
        var result = await _planRepository.GetPlansAsync(page, pageSize, search, status, departmentId, fiscalYear);
        return new PagedResult<ProcurementPlanDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IEnumerable<ProcurementPlanDto>> GetByDepartmentAsync(Guid departmentId)
    {
        var plans = await _planRepository.GetByDepartmentAsync(departmentId);
        return plans.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcurementPlanDto>> GetByFiscalYearAsync(int fiscalYear)
    {
        var plans = await _planRepository.GetByFiscalYearAsync(fiscalYear);
        return plans.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcurementPlanDto>> GetActivePlansAsync()
    {
        var plans = await _planRepository.GetActivePlansAsync();
        return plans.Select(MapToDto);
    }

    public async Task<ProcurementPlanDetailDto> CreateAsync(CreateProcurementPlanDto dto)
    {
        var planNumber = await _planRepository.GeneratePlanNumberAsync(dto.FiscalYear);
        var currentUserId = _currentUserProvider.UserId;

        var plan = new ProcurementPlan
        {
            PlanNumber = planNumber,
            Title = dto.Title,
            Description = dto.Description,
            DepartmentId = dto.DepartmentId,
            FiscalYear = dto.FiscalYear,
            PlanStartDate = dto.PlanStartDate,
            PlanEndDate = dto.PlanEndDate,
            PlanDurationYears = dto.PlanDurationYears,
            TotalEstimatedBudget = dto.TotalEstimatedBudget,
            Currency = dto.Currency,
            Notes = dto.Notes,
            Status = "Draft",
            PreparedById = currentUserId != Guid.Empty ? currentUserId : null,
            PreparedDate = DateTime.UtcNow,
            RevisionNumber = 1,
            TenantId = _currentUserProvider.TenantId
        };

        await _planRepository.AddAsync(plan);

        // Add items if provided
        if (dto.Items.Any())
        {
            foreach (var itemDto in dto.Items)
            {
                var item = CreatePlanItem(plan.Id, itemDto);
                await _itemRepository.AddAsync(item);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created procurement plan {PlanNumber} for department {DepartmentId}", planNumber, dto.DepartmentId);

        return await GetByIdAsync(plan.Id) ?? throw new InvalidOperationException("Failed to retrieve created plan");
    }

    public async Task<ProcurementPlanDetailDto> UpdateAsync(Guid id, UpdateProcurementPlanDto dto)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {id} not found");

        if (plan.Status != "Draft")
            throw new InvalidOperationException("Only draft plans can be updated");

        plan.Title = dto.Title;
        plan.Description = dto.Description;
        plan.DepartmentId = dto.DepartmentId;
        plan.FiscalYear = dto.FiscalYear;
        plan.PlanStartDate = dto.PlanStartDate;
        plan.PlanEndDate = dto.PlanEndDate;
        plan.PlanDurationYears = dto.PlanDurationYears;
        plan.TotalEstimatedBudget = dto.TotalEstimatedBudget;
        plan.Currency = dto.Currency;
        plan.Notes = dto.Notes;
        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated plan");
    }

    public async Task<ProcurementPlanDetailDto> SubmitForApprovalAsync(Guid id, SubmitProcurementPlanDto dto)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {id} not found");

        if (plan.Status != "Draft")
            throw new InvalidOperationException("Only draft plans can be submitted for approval");

        plan.Status = "Submitted";
        plan.ReviewedById = dto.ReviewerId;
        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Procurement plan {PlanNumber} submitted for approval", plan.PlanNumber);

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve plan");
    }

    public async Task<ProcurementPlanDetailDto> ApproveAsync(Guid id, ApproveProcurementPlanDto dto)
    {
        _logger.LogInformation("ApproveAsync called for plan {PlanId}, AutoGenerateSchedules: {AutoGenerate}", id, dto.AutoGenerateSchedules);

        var plan = await _planRepository.GetWithFullDetailsAsync(id);
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {id} not found");

        _logger.LogInformation("Plan {PlanNumber} found with {ItemCount} items, TenantId: {TenantId}",
            plan.PlanNumber, plan.Items?.Count ?? 0, plan.TenantId);

        if (plan.Status != "Submitted" && plan.Status != "UnderReview")
            throw new InvalidOperationException("Only submitted plans can be approved or rejected");

        var currentUserId = _currentUserProvider.UserId;
        var currentTenantId = _currentUserProvider.TenantId;
        _logger.LogInformation("Current user: {UserId}, Current tenant: {TenantId}", currentUserId, currentTenantId);

        if (dto.IsApproved)
        {
            plan.Status = "Approved";
            plan.ApprovedById = currentUserId != Guid.Empty ? currentUserId : null;
            plan.ApprovedDate = DateTime.UtcNow;
            plan.ApprovedBudget = dto.ApprovedBudget ?? plan.TotalEstimatedBudget;
            plan.ApprovalComments = dto.Comments;

            // Update all plan items to Approved status
            var planItems = plan.Items?.Where(i => !i.IsDeleted && i.Status == "Planned").ToList() ?? new List<ProcurementPlanItem>();
            _logger.LogInformation("Found {Count} plan items with status 'Planned' to update", planItems.Count);

            foreach (var item in planItems)
            {
                item.Status = "Approved";
                item.UpdatedAt = DateTime.UtcNow;
                await _itemRepository.UpdateAsync(item);
            }

            _logger.LogInformation("Updated {Count} plan items to Approved status", planItems.Count);

            // Auto-generate procurement schedules for each item
            if (dto.AutoGenerateSchedules)
            {
                _logger.LogInformation("Auto-generating schedules for {Count} plan items", planItems.Count);
                foreach (var item in planItems)
                {
                    try
                    {
                        _logger.LogInformation("Creating schedule for item {ItemId}: {ItemDescription}", item.Id, item.ItemDescription);
                        var scheduleDto = new CreateProcurementScheduleDto
                        {
                            Title = $"Schedule for {item.ItemDescription}",
                            Description = $"Auto-generated schedule for plan item from {plan.PlanNumber}",
                            ProcurementPlanId = plan.Id,
                            ProcurementPlanItemId = item.Id,
                            DepartmentId = plan.DepartmentId,
                            ScheduleType = item.ProcurementMethod ?? "Tender",
                            PlannedStartDate = DateTime.UtcNow,
                            PlannedEndDate = item.RequiredDate ?? plan.PlanEndDate,
                            IsOptimalTiming = true,
                            TimingRationale = $"Based on required date from procurement plan item"
                        };
                        var createdSchedule = await _scheduleService.CreateAsync(scheduleDto);
                        _logger.LogInformation("Successfully created schedule {ScheduleCode} for item {ItemId}",
                            createdSchedule.ScheduleCode, item.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create schedule for plan item {ItemId}: {ErrorMessage}",
                            item.Id, ex.Message);
                    }
                }
                _logger.LogInformation("Completed auto-generating schedules for {Count} plan items", planItems.Count);
            }
            else
            {
                _logger.LogInformation("AutoGenerateSchedules is false, skipping schedule creation");
            }

            // Link budget to plan
            if (dto.AutoLinkBudget)
            {
                try
                {
                    Guid? budgetIdToLink = dto.BudgetId;

                    // If no specific budget provided, auto-match by department + fiscal year
                    if (!budgetIdToLink.HasValue)
                    {
                        var availableBudgets = await _budgetService.GetAvailableBudgetsForLinkingAsync(plan.DepartmentId, plan.FiscalYear);
                        var matchingBudget = availableBudgets.FirstOrDefault();
                        if (matchingBudget != null)
                        {
                            budgetIdToLink = matchingBudget.Id;
                            _logger.LogInformation("Auto-matched budget {BudgetCode} for plan {PlanNumber}",
                                matchingBudget.BudgetCode, plan.PlanNumber);
                        }
                        else
                        {
                            _logger.LogWarning("No available budget found for department {DepartmentId}, fiscal year {FiscalYear}",
                                plan.DepartmentId, plan.FiscalYear);
                        }
                    }

                    if (budgetIdToLink.HasValue)
                    {
                        await _budgetService.LinkBudgetToPlanAsync(budgetIdToLink.Value, plan.Id);
                        _logger.LogInformation("Linked budget {BudgetId} to plan {PlanId}", budgetIdToLink.Value, plan.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to link budget to plan {PlanId}: {ErrorMessage}", plan.Id, ex.Message);
                    // Don't fail the approval if budget linking fails
                }
            }
        }
        else
        {
            plan.Status = "Rejected";
            plan.ReviewComments = dto.Comments;
        }

        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Procurement plan {PlanNumber} {Status}", plan.PlanNumber, plan.Status);

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve plan");
    }

    public async Task DeleteAsync(Guid id)
    {
        var plan = await _planRepository.GetByIdAsync(id);
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {id} not found");

        if (plan.Status == "Approved")
            throw new InvalidOperationException("Approved plans cannot be deleted");

        plan.IsDeleted = true;
        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted procurement plan {PlanNumber}", plan.PlanNumber);
    }

    public async Task<ProcurementPlanItemDto> AddItemAsync(Guid planId, CreateProcurementPlanItemDto dto)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {planId} not found");

        if (plan.Status != "Draft")
            throw new InvalidOperationException("Items can only be added to draft plans");

        var item = CreatePlanItem(planId, dto);
        await _itemRepository.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        // Add item suppliers if provided
        if (dto.ItemSuppliers?.Any() == true)
        {
            foreach (var supplierDto in dto.ItemSuppliers)
            {
                var itemSupplier = new ProcurementPlanItemSupplier
                {
                    ProcurementPlanItemId = item.Id,
                    SupplierId = supplierDto.SupplierId,
                    IsPreferred = supplierDto.IsPreferred,
                    Priority = supplierDto.Priority,
                    QuotedUnitPrice = supplierDto.QuotedUnitPrice,
                    LeadTimeDays = supplierDto.LeadTimeDays,
                    SupplierItemCode = supplierDto.SupplierItemCode,
                    Notes = supplierDto.Notes,
                    TenantId = _currentUserProvider.TenantId
                };
                await _itemSupplierRepository.AddAsync(itemSupplier);
            }
            await _unitOfWork.SaveChangesAsync();
        }

        // Reload item with suppliers
        var items = await _itemRepository.GetByPlanIdAsync(planId);
        var reloadedItem = items.FirstOrDefault(i => i.Id == item.Id);
        return MapToItemDto(reloadedItem ?? item);
    }

    public async Task<ProcurementPlanItemDto> UpdateItemAsync(Guid itemId, UpdateProcurementPlanItemDto dto)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item == null)
            throw new KeyNotFoundException($"Plan item with ID {itemId} not found");

        item.InventoryItemId = dto.InventoryItemId;
        item.ItemDescription = dto.ItemDescription;
        item.Specifications = dto.Specifications;
        item.ItemCategory = dto.ItemCategory;
        item.EstimatedQuantity = dto.EstimatedQuantity;
        item.UnitOfMeasure = dto.UnitOfMeasure;
        item.EstimatedUnitPrice = dto.EstimatedUnitPrice;
        item.EstimatedTotalCost = dto.EstimatedQuantity * dto.EstimatedUnitPrice;
        item.Priority = dto.Priority;
        item.IsCritical = dto.IsCritical;
        item.RequiredDate = dto.RequiredDate;
        item.PlannedProcurementMonth = dto.PlannedProcurementMonth;
        item.PlannedQuarter = dto.PlannedQuarter;
        item.PreferredSupplierId = dto.PreferredSupplierId;
        item.PreferredSupplierName = dto.PreferredSupplierName;
        item.AlternativeSuppliers = dto.AlternativeSuppliers;
        item.Justification = dto.Justification;
        item.ProcurementMethod = dto.ProcurementMethod;
        item.Notes = dto.Notes;
        item.UpdatedAt = DateTime.UtcNow;

        await _itemRepository.UpdateAsync(item);

        // Update item suppliers - delete existing first, then add new ones
        // We must save deletions first to avoid unique constraint violations
        await _itemSupplierRepository.DeleteByItemIdAsync(itemId);
        await _unitOfWork.SaveChangesAsync();

        if (dto.ItemSuppliers != null && dto.ItemSuppliers.Any())
        {
            foreach (var supplierDto in dto.ItemSuppliers)
            {
                var itemSupplier = new ProcurementPlanItemSupplier
                {
                    ProcurementPlanItemId = itemId,
                    SupplierId = supplierDto.SupplierId,
                    IsPreferred = supplierDto.IsPreferred,
                    Priority = supplierDto.Priority,
                    QuotedUnitPrice = supplierDto.QuotedUnitPrice,
                    LeadTimeDays = supplierDto.LeadTimeDays,
                    SupplierItemCode = supplierDto.SupplierItemCode,
                    Notes = supplierDto.Notes,
                    TenantId = _currentUserProvider.TenantId
                };
                await _itemSupplierRepository.AddAsync(itemSupplier);
            }
            await _unitOfWork.SaveChangesAsync();
        }

        // Reload item with suppliers
        var items = await _itemRepository.GetByPlanIdAsync(item.ProcurementPlanId);
        var reloadedItem = items.FirstOrDefault(i => i.Id == itemId);
        return MapToItemDto(reloadedItem ?? item);
    }

    public async Task DeleteItemAsync(Guid itemId)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item == null)
            throw new KeyNotFoundException($"Plan item with ID {itemId} not found");

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;

        await _itemRepository.UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProcurementPlanItemDto>> GetItemsByPlanIdAsync(Guid planId)
    {
        var items = await _itemRepository.GetByPlanIdAsync(planId);
        return items.Select(MapToItemDto);
    }

    public async Task<IEnumerable<ProcurementPlanItemDto>> GetCriticalItemsAsync(Guid planId)
    {
        var items = await _itemRepository.GetCriticalItemsAsync(planId);
        return items.Select(MapToItemDto);
    }

    #region Private Methods

    private ProcurementPlanItem CreatePlanItem(Guid planId, CreateProcurementPlanItemDto dto)
    {
        return new ProcurementPlanItem
        {
            ProcurementPlanId = planId,
            InventoryItemId = dto.InventoryItemId,
            ItemDescription = dto.ItemDescription,
            Specifications = dto.Specifications,
            ItemCategory = dto.ItemCategory,
            EstimatedQuantity = dto.EstimatedQuantity,
            UnitOfMeasure = dto.UnitOfMeasure,
            EstimatedUnitPrice = dto.EstimatedUnitPrice,
            EstimatedTotalCost = dto.EstimatedQuantity * dto.EstimatedUnitPrice,
            Priority = dto.Priority,
            IsCritical = dto.IsCritical,
            RequiredDate = dto.RequiredDate,
            PlannedProcurementMonth = dto.PlannedProcurementMonth,
            PlannedQuarter = dto.PlannedQuarter,
            PreferredSupplierId = dto.PreferredSupplierId,
            PreferredSupplierName = dto.PreferredSupplierName,
            AlternativeSuppliers = dto.AlternativeSuppliers,
            Justification = dto.Justification,
            ProcurementMethod = dto.ProcurementMethod,
            Notes = dto.Notes,
            Status = "Planned",
            TenantId = _currentUserProvider.TenantId
        };
    }

    private static ProcurementPlanDto MapToDto(ProcurementPlan plan)
    {
        return new ProcurementPlanDto
        {
            Id = plan.Id,
            PlanNumber = plan.PlanNumber,
            Title = plan.Title,
            Description = plan.Description,
            DepartmentId = plan.DepartmentId,
            DepartmentName = plan.Department?.Name,
            FiscalYear = plan.FiscalYear,
            PlanStartDate = plan.PlanStartDate,
            PlanEndDate = plan.PlanEndDate,
            PlanDurationYears = plan.PlanDurationYears,
            Status = plan.Status,
            TotalEstimatedBudget = plan.TotalEstimatedBudget,
            ApprovedBudget = plan.ApprovedBudget,
            Currency = plan.Currency,
            PreparedByName = plan.PreparedBy?.FullName,
            PreparedDate = plan.PreparedDate,
            ApprovedByName = plan.ApprovedBy?.FullName,
            ApprovedDate = plan.ApprovedDate,
            RevisionNumber = plan.RevisionNumber,
            ItemCount = plan.Items?.Count(i => !i.IsDeleted) ?? 0,
            CreatedAt = plan.CreatedAt
        };
    }

    private static ProcurementPlanDetailDto MapToDetailDto(ProcurementPlan plan)
    {
        return new ProcurementPlanDetailDto
        {
            Id = plan.Id,
            PlanNumber = plan.PlanNumber,
            Title = plan.Title,
            Description = plan.Description,
            DepartmentId = plan.DepartmentId,
            DepartmentName = plan.Department?.Name,
            FiscalYear = plan.FiscalYear,
            PlanStartDate = plan.PlanStartDate,
            PlanEndDate = plan.PlanEndDate,
            PlanDurationYears = plan.PlanDurationYears,
            Status = plan.Status,
            TotalEstimatedBudget = plan.TotalEstimatedBudget,
            ApprovedBudget = plan.ApprovedBudget,
            Currency = plan.Currency,
            PreparedById = plan.PreparedById,
            PreparedByName = plan.PreparedBy?.FullName,
            PreparedDate = plan.PreparedDate,
            ReviewedById = plan.ReviewedById,
            ReviewedByName = plan.ReviewedBy?.FullName,
            ReviewedDate = plan.ReviewedDate,
            ReviewComments = plan.ReviewComments,
            ApprovedById = plan.ApprovedById,
            ApprovedByName = plan.ApprovedBy?.FullName,
            ApprovedDate = plan.ApprovedDate,
            ApprovalComments = plan.ApprovalComments,
            PreviousVersionId = plan.PreviousVersionId,
            RevisionNumber = plan.RevisionNumber,
            Notes = plan.Notes,
            ItemCount = plan.Items?.Count(i => !i.IsDeleted) ?? 0,
            CreatedAt = plan.CreatedAt,
            Items = plan.Items?.Where(i => !i.IsDeleted).Select(MapToItemDto).ToList() ?? new(),
            Budgets = plan.Budgets?.Where(b => !b.IsDeleted).Select(MapToBudgetDto).ToList() ?? new(),
            Schedules = plan.Schedules?.Where(s => !s.IsDeleted).Select(MapToScheduleDto).ToList() ?? new()
        };
    }

    private static ProcurementPlanItemDto MapToItemDto(ProcurementPlanItem item)
    {
        return new ProcurementPlanItemDto
        {
            Id = item.Id,
            ProcurementPlanId = item.ProcurementPlanId,
            InventoryItemId = item.InventoryItemId,
            InventoryItemCode = item.InventoryItem?.ItemCode,
            InventoryItemName = item.InventoryItem?.Name,
            ItemDescription = item.ItemDescription,
            Specifications = item.Specifications,
            ItemCategory = item.ItemCategory,
            EstimatedQuantity = item.EstimatedQuantity,
            UnitOfMeasure = item.UnitOfMeasure,
            EstimatedUnitPrice = item.EstimatedUnitPrice,
            EstimatedTotalCost = item.EstimatedTotalCost,
            Currency = item.Currency,
            Priority = item.Priority,
            IsCritical = item.IsCritical,
            RequiredDate = item.RequiredDate,
            PlannedProcurementMonth = item.PlannedProcurementMonth,
            PlannedQuarter = item.PlannedQuarter,
            PreferredSupplierId = item.PreferredSupplierId,
            PreferredSupplierName = item.PreferredSupplierName,
            AlternativeSuppliers = item.AlternativeSuppliers,
            Justification = item.Justification,
            Status = item.Status,
            ProcurementMethod = item.ProcurementMethod,
            PurchaseOrderId = item.PurchaseOrderId,
            TenderId = item.TenderId,
            Notes = item.Notes,
            ItemSuppliers = item.ItemSuppliers?
                .Where(s => !s.IsDeleted)
                .Select(MapToItemSupplierDto)
                .ToList() ?? new()
        };
    }

    private static ProcurementPlanItemSupplierDto MapToItemSupplierDto(ProcurementPlanItemSupplier itemSupplier)
    {
        return new ProcurementPlanItemSupplierDto
        {
            Id = itemSupplier.Id,
            ProcurementPlanItemId = itemSupplier.ProcurementPlanItemId,
            SupplierId = itemSupplier.SupplierId,
            SupplierCode = itemSupplier.BusinessPartner?.PartnerCode ?? string.Empty,
            SupplierName = itemSupplier.BusinessPartner?.PartnerName ?? string.Empty,
            IsPreferred = itemSupplier.IsPreferred,
            Priority = itemSupplier.Priority,
            QuotedUnitPrice = itemSupplier.QuotedUnitPrice,
            LeadTimeDays = itemSupplier.LeadTimeDays,
            SupplierItemCode = itemSupplier.SupplierItemCode,
            Notes = itemSupplier.Notes
        };
    }

    private static ProcurementBudgetDto MapToBudgetDto(ProcurementBudget budget)
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

    private static ProcurementScheduleDto MapToScheduleDto(ProcurementSchedule schedule)
    {
        return new ProcurementScheduleDto
        {
            Id = schedule.Id,
            ScheduleCode = schedule.ScheduleCode,
            Title = schedule.Title,
            Description = schedule.Description,
            ProcurementPlanId = schedule.ProcurementPlanId,
            ProcurementPlanNumber = schedule.ProcurementPlan?.PlanNumber,
            ProcurementPlanItemId = schedule.ProcurementPlanItemId,
            DepartmentId = schedule.DepartmentId,
            DepartmentName = schedule.Department?.Name,
            ScheduleType = schedule.ScheduleType,
            PlannedStartDate = schedule.PlannedStartDate,
            PlannedEndDate = schedule.PlannedEndDate,
            ActualStartDate = schedule.ActualStartDate,
            ActualEndDate = schedule.ActualEndDate,
            IsOptimalTiming = schedule.IsOptimalTiming,
            TimingRationale = schedule.TimingRationale,
            ConsiderSeasonalPricing = schedule.ConsiderSeasonalPricing,
            ConsiderCashFlow = schedule.ConsiderCashFlow,
            Status = schedule.Status,
            ConsolidationOpportunity = schedule.ConsolidationOpportunity,
            CreatedAt = schedule.CreatedAt
        };
    }

    #endregion

    #region Plan Item Conversion

    public async Task<PlanItemConversionResultDto> ConvertItemToTenderAsync(ConvertPlanItemToTenderDto dto)
    {
        var planItem = await _itemRepository.GetByIdAsync(dto.PlanItemId);
        if (planItem == null)
            throw new KeyNotFoundException($"Plan item with ID {dto.PlanItemId} not found");

        // Validate item status
        if (planItem.Status != "Approved" && planItem.Status != "Planned")
            throw new InvalidOperationException($"Plan item must be in 'Approved' or 'Planned' status to convert. Current status: {planItem.Status}");

        // Check if already converted
        if (planItem.TenderId.HasValue)
            throw new InvalidOperationException($"Plan item has already been converted to tender ID: {planItem.TenderId}");

        if (planItem.PurchaseOrderId.HasValue)
            throw new InvalidOperationException($"Plan item has already been converted to purchase order ID: {planItem.PurchaseOrderId}");

        // Get the plan for additional context
        var plan = await _planRepository.GetByIdAsync(planItem.ProcurementPlanId);

        // Create tender from plan item
        var createTenderDto = new CreateTenderDto
        {
            Title = dto.TenderTitle,
            Description = dto.TenderDescription ?? planItem.Specifications,
            TenderType = dto.TenderType,
            SubmissionDeadline = dto.SubmissionDeadline,
            OpeningDate = dto.OpeningDate,
            EstimatedValue = planItem.EstimatedTotalCost,
            Currency = plan?.Currency ?? "USD",
            Notes = dto.Notes,
            Items = new List<CreateTenderItemDto>
            {
                new CreateTenderItemDto
                {
                    Description = planItem.ItemDescription,
                    Specifications = planItem.Specifications,
                    Quantity = planItem.EstimatedQuantity,
                    UnitOfMeasure = planItem.UnitOfMeasure,
                    RequiredDeliveryDate = planItem.RequiredDate
                }
            }
        };

        var tender = await _tenderService.CreateTenderAsync(createTenderDto);

        // Update plan item with tender reference and procurement method
        planItem.TenderId = tender.Id;
        planItem.Status = "InProgress";
        planItem.ProcurementMethod = "Tender"; // Set the actual procurement method used
        await _itemRepository.UpdateAsync(planItem);
        await _unitOfWork.SaveChangesAsync();

        var result = new PlanItemConversionResultDto
        {
            PlanItemId = planItem.Id,
            PlanItemDescription = planItem.ItemDescription,
            ConversionType = "Tender",
            TenderId = tender.Id,
            TenderNumber = tender.TenderNumber,
            NewItemStatus = planItem.Status,
            Message = $"Successfully created tender {tender.TenderNumber} from plan item"
        };

        // Create schedule if requested
        if (dto.CreateSchedule && plan != null)
        {
            var scheduleDto = new CreateProcurementScheduleDto
            {
                Title = $"Schedule for tender {tender.TenderNumber}",
                Description = $"Auto-generated schedule for tender {tender.TenderNumber} from plan item",
                ProcurementPlanId = plan.Id,
                ProcurementPlanItemId = planItem.Id,
                DepartmentId = plan.DepartmentId,
                ScheduleType = "Tender",
                PlannedStartDate = DateTime.UtcNow,
                PlannedEndDate = dto.SubmissionDeadline ?? planItem.RequiredDate ?? DateTime.UtcNow.AddDays(30)
            };
            var schedule = await _scheduleService.CreateAsync(scheduleDto);
            result.ScheduleId = schedule.Id;
            result.ScheduleCode = schedule.ScheduleCode;
        }

        _logger.LogInformation("Converted plan item {PlanItemId} to tender {TenderId}", planItem.Id, tender.Id);
        return result;
    }

    public async Task<PlanItemConversionResultDto> ConvertItemToPurchaseOrderAsync(ConvertPlanItemToPurchaseOrderDto dto)
    {
        var planItem = await _itemRepository.GetByIdAsync(dto.PlanItemId);
        if (planItem == null)
            throw new KeyNotFoundException($"Plan item with ID {dto.PlanItemId} not found");

        // Validate item status
        if (planItem.Status != "Approved" && planItem.Status != "Planned")
            throw new InvalidOperationException($"Plan item must be in 'Approved' or 'Planned' status to convert. Current status: {planItem.Status}");

        // Check if already converted
        if (planItem.PurchaseOrderId.HasValue)
            throw new InvalidOperationException($"Plan item has already been converted to purchase order ID: {planItem.PurchaseOrderId}");

        if (planItem.TenderId.HasValue)
            throw new InvalidOperationException($"Plan item has already been converted to tender ID: {planItem.TenderId}");

        // Get the plan for additional context
        var plan = await _planRepository.GetByIdAsync(planItem.ProcurementPlanId);

        // Generate PO number
        var poNumber = await _purchaseOrderRepository.GenerateOrderNumberAsync();

        // Create purchase order
        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = poNumber,
            BusinessPartnerId = dto.SupplierId,
            OrderDate = DateTime.UtcNow,
            RequiredDate = dto.RequiredDate ?? planItem.RequiredDate ?? DateTime.UtcNow.AddDays(14),
            Status = "Draft",
            PaymentTerms = dto.PaymentTerms,
            ShippingTerms = dto.ShippingTerms,
            DeliveryWarehouseId = dto.DeliveryWarehouseId,
            DeliveryAddress = dto.DeliveryAddress,
            DeliveryInstructions = dto.DeliveryInstructions,
            Notes = dto.Notes ?? $"Created from procurement plan item: {planItem.ItemDescription}",
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _purchaseOrderRepository.AddAsync(purchaseOrder);

        // Create purchase order item
        var lineTotal = planItem.EstimatedQuantity * planItem.EstimatedUnitPrice;
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = purchaseOrder.Id,
            InventoryItemId = planItem.InventoryItemId ?? Guid.Empty,
            ItemDescription = planItem.ItemDescription,
            OrderedQuantity = planItem.EstimatedQuantity,
            UnitPrice = planItem.EstimatedUnitPrice,
            LineTotal = lineTotal,
            ReceivedQuantity = 0,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _purchaseOrderItemRepository.AddAsync(poItem);

        // Update totals
        purchaseOrder.SubTotal = lineTotal;
        purchaseOrder.TotalAmount = lineTotal;
        await _purchaseOrderRepository.UpdateAsync(purchaseOrder);

        // Update plan item with PO reference and procurement method
        planItem.PurchaseOrderId = purchaseOrder.Id;
        planItem.Status = "InProgress";
        planItem.ProcurementMethod = "DirectPurchase"; // Set the actual procurement method used
        await _itemRepository.UpdateAsync(planItem);

        await _unitOfWork.SaveChangesAsync();

        // Commit budget when PO is created
        if (plan != null)
        {
            try
            {
                // Find budget linked to this plan
                var budgets = await _budgetRepository.GetByPlanIdAsync(plan.Id);
                var budget = budgets.FirstOrDefault(b => b.Status == "Active" || b.Status == "Approved");

                // If no plan-specific budget, check department budgets for the fiscal year
                if (budget == null)
                {
                    var departmentBudgets = await _budgetRepository.GetByDepartmentAsync(plan.DepartmentId);
                    budget = departmentBudgets
                        .Where(b => b.FiscalYear == plan.FiscalYear && (b.Status == "Active" || b.Status == "Approved"))
                        .FirstOrDefault();
                }

                if (budget != null)
                {
                    await _budgetService.CommitBudgetAsync(budget.Id, lineTotal, planItem.ItemCategory);
                    _logger.LogInformation("Committed {Amount} to budget {BudgetCode} for PO {PONumber}, category: {Category}",
                        lineTotal, budget.BudgetCode, purchaseOrder.OrderNumber, planItem.ItemCategory ?? "N/A");
                }
                else
                {
                    _logger.LogWarning("No budget found for plan {PlanId} to commit PO amount", plan.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit budget for PO {PONumber}: {ErrorMessage}", purchaseOrder.OrderNumber, ex.Message);
                // Don't fail the PO creation if budget commitment fails
            }
        }

        var result = new PlanItemConversionResultDto
        {
            PlanItemId = planItem.Id,
            PlanItemDescription = planItem.ItemDescription,
            ConversionType = "PurchaseOrder",
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrderNumber = purchaseOrder.OrderNumber,
            NewItemStatus = planItem.Status,
            Message = $"Successfully created purchase order {purchaseOrder.OrderNumber} from plan item"
        };

        // Create schedule if requested
        if (dto.CreateSchedule && plan != null)
        {
            var scheduleDto = new CreateProcurementScheduleDto
            {
                Title = $"Schedule for PO {purchaseOrder.OrderNumber}",
                Description = $"Auto-generated schedule for purchase order {purchaseOrder.OrderNumber} from plan item",
                ProcurementPlanId = plan.Id,
                ProcurementPlanItemId = planItem.Id,
                DepartmentId = plan.DepartmentId,
                ScheduleType = "DirectPurchase",
                PlannedStartDate = DateTime.UtcNow,
                PlannedEndDate = purchaseOrder.RequiredDate ?? DateTime.UtcNow.AddDays(14)
            };
            var schedule = await _scheduleService.CreateAsync(scheduleDto);
            result.ScheduleId = schedule.Id;
            result.ScheduleCode = schedule.ScheduleCode;
        }

        _logger.LogInformation("Converted plan item {PlanItemId} to purchase order {PurchaseOrderId}", planItem.Id, purchaseOrder.Id);
        return result;
    }

    public async Task<PlanItemConversionResultDto> ConvertItemToRfqAsync(ConvertPlanItemToTenderDto dto)
    {
        // RFQ is just a Tender with TenderType = "RFQ"
        // Override the tender type to ensure it's RFQ
        var rfqDto = new ConvertPlanItemToTenderDto
        {
            PlanItemId = dto.PlanItemId,
            TenderTitle = dto.TenderTitle,
            TenderDescription = dto.TenderDescription,
            TenderType = "RFQ", // Force RFQ type
            SubmissionDeadline = dto.SubmissionDeadline,
            OpeningDate = dto.OpeningDate,
            Notes = dto.Notes,
            CreateSchedule = dto.CreateSchedule
        };

        var result = await ConvertItemToTenderAsync(rfqDto);
        result.ConversionType = "RFQ";
        result.Message = result.Message?.Replace("tender", "RFQ");

        // Update the plan item's procurement method to RFQ
        var planItem = await _itemRepository.GetByIdAsync(dto.PlanItemId);
        if (planItem != null)
        {
            planItem.ProcurementMethod = "RFQ";
            await _itemRepository.UpdateAsync(planItem);
            await _unitOfWork.SaveChangesAsync();
        }

        return result;
    }

    #endregion

    #region Plan Item Status Updates

    public async Task<ProcurementPlanItemDto> UpdateItemStatusAsync(Guid itemId, string newStatus)
    {
        var planItem = await _itemRepository.GetByIdAsync(itemId);
        if (planItem == null)
            throw new KeyNotFoundException($"Plan item with ID {itemId} not found");

        var validStatuses = new[] { "Planned", "Approved", "InProgress", "Procured", "Cancelled" };
        if (!validStatuses.Contains(newStatus))
            throw new InvalidOperationException($"Invalid status: {newStatus}. Valid statuses are: {string.Join(", ", validStatuses)}");

        var oldStatus = planItem.Status;
        planItem.Status = newStatus;
        planItem.UpdatedAt = DateTime.UtcNow;

        await _itemRepository.UpdateAsync(planItem);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Plan item {ItemId} status changed from {OldStatus} to {NewStatus}", itemId, oldStatus, newStatus);

        // If status changed to Procured, check if the entire plan should be completed
        if (newStatus == "Procured")
        {
            await CheckAndCompletePlanAsync(planItem.ProcurementPlanId);
        }

        return MapToItemDto(planItem);
    }

    public async Task CheckAndCompletePlanAsync(Guid planId)
    {
        var plan = await _planRepository.GetWithFullDetailsAsync(planId);
        if (plan == null)
        {
            _logger.LogWarning("Plan {PlanId} not found for completion check", planId);
            return;
        }

        // Only check approved or active plans
        if (plan.Status != "Approved" && plan.Status != "Active")
        {
            _logger.LogDebug("Plan {PlanId} status is {Status}, skipping completion check", planId, plan.Status);
            return;
        }

        // Get all non-deleted items
        var items = plan.Items?.Where(i => !i.IsDeleted).ToList() ?? new List<ProcurementPlanItem>();

        if (!items.Any())
        {
            _logger.LogDebug("Plan {PlanId} has no items, skipping completion check", planId);
            return;
        }

        // Check if ALL items are either Procured or Cancelled
        var allItemsComplete = items.All(i => i.Status == "Procured" || i.Status == "Cancelled");
        var hasAtLeastOneProcured = items.Any(i => i.Status == "Procured");

        if (allItemsComplete && hasAtLeastOneProcured)
        {
            plan.Status = "Completed";
            plan.UpdatedAt = DateTime.UtcNow;

            await _planRepository.UpdateAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Plan {PlanNumber} automatically marked as Completed - all {Count} items are procured/cancelled",
                plan.PlanNumber, items.Count);
        }
    }

    #endregion

    #region Budget Validation

    public async Task<BudgetValidationResultDto> ValidateBudgetForItemAsync(Guid planItemId, decimal? amount = null)
    {
        var planItem = await _itemRepository.GetByIdAsync(planItemId);
        if (planItem == null)
            throw new KeyNotFoundException($"Plan item with ID {planItemId} not found");

        var plan = await _planRepository.GetByIdAsync(planItem.ProcurementPlanId);
        if (plan == null)
            throw new KeyNotFoundException($"Plan for item {planItemId} not found");

        var requestedAmount = amount ?? planItem.EstimatedTotalCost;

        // Try to find a budget for this plan or department
        var budgets = await _budgetRepository.GetByPlanIdAsync(plan.Id);
        var budget = budgets.FirstOrDefault(b => b.Status == "Active" || b.Status == "Approved");

        // If no plan-specific budget, check department budgets for the fiscal year
        if (budget == null)
        {
            var departmentBudgets = await _budgetRepository.GetByDepartmentAsync(plan.DepartmentId);
            budget = departmentBudgets
                .Where(b => b.FiscalYear == plan.FiscalYear && (b.Status == "Active" || b.Status == "Approved"))
                .FirstOrDefault();
        }

        var result = new BudgetValidationResultDto
        {
            RequestedAmount = requestedAmount
        };

        if (budget == null)
        {
            result.HasBudget = false;
            result.IsValid = true; // No budget = no restriction (advisory mode)
            result.Message = "No budget allocated for this plan/department. Proceeding without budget validation.";
            result.Warnings.Add("No procurement budget found - budget tracking will not be available");
            return result;
        }

        result.HasBudget = true;
        result.BudgetCode = budget.BudgetCode;
        result.AllocatedAmount = budget.AllocatedAmount;
        result.UtilizedAmount = budget.UtilizedAmount;
        result.CommittedAmount = budget.CommittedAmount;
        result.RemainingAmount = budget.RemainingAmount;
        result.Currency = budget.Currency;
        result.ControlLevel = budget.ControlLevel;

        // Calculate available budget
        var availableBudget = budget.RemainingAmount;

        // Check budget threshold warning
        var utilizationPercent = budget.AllocatedAmount > 0
            ? ((budget.UtilizedAmount + budget.CommittedAmount) / budget.AllocatedAmount) * 100
            : 0;

        if (utilizationPercent >= budget.WarningThresholdPercent)
        {
            result.Warnings.Add($"Budget utilization is at {utilizationPercent:F1}% (threshold: {budget.WarningThresholdPercent}%)");
        }

        if (requestedAmount > availableBudget)
        {
            switch (budget.ControlLevel)
            {
                case "Strict":
                    result.IsValid = false;
                    result.Message = $"Insufficient budget. Requested: {requestedAmount:N2} {budget.Currency}, Available: {availableBudget:N2} {budget.Currency}";
                    break;
                case "Warning":
                    result.IsValid = true;
                    result.Message = "Budget will be exceeded. Approval may be required.";
                    result.Warnings.Add($"Requested amount ({requestedAmount:N2}) exceeds available budget ({availableBudget:N2})");
                    break;
                case "Advisory":
                default:
                    result.IsValid = true;
                    result.Message = "Budget advisory: Requested amount exceeds available budget.";
                    result.Warnings.Add($"Advisory: Requested amount ({requestedAmount:N2}) exceeds available budget ({availableBudget:N2})");
                    break;
            }
        }
        else
        {
            result.IsValid = true;
            result.Message = $"Budget available. Remaining after this item: {(availableBudget - requestedAmount):N2} {budget.Currency}";
        }

        return result;
    }

    #endregion
}