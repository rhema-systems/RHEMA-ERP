using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class ProcurementPlanService : IProcurementPlanService
{
    private const string WorkflowEntityType = "ProcurementPlan";

    private readonly IProcurementPlanRepository _planRepository;
    private readonly IProcurementPlanItemRepository _itemRepository;
    private readonly IProcurementPlanItemSupplierRepository _itemSupplierRepository;
    private readonly ITenderService _tenderService;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseOrderItemRepository _purchaseOrderItemRepository;
    private readonly IProcurementScheduleService _scheduleService;
    private readonly IProcurementBudgetRepository _budgetRepository;
    private readonly IProcurementBudgetService _budgetService;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IMarketAnalysisRepository _marketAnalysisRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly ISupplierReportingService _supplierReportingService;
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
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IMarketAnalysisRepository marketAnalysisRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        ISupplierReportingService supplierReportingService,
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
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _marketAnalysisRepository = marketAnalysisRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _supplierReportingService = supplierReportingService;
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
            PlanningCycle = dto.PlanningCycle,
            PlanningQuarter = dto.PlanningQuarter,
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
                var item = CreatePlanItem(plan.Id, itemDto, plan.Currency);
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

        var currencyChanged = !string.Equals(plan.Currency, dto.Currency, StringComparison.OrdinalIgnoreCase);

        plan.Title = dto.Title;
        plan.Description = dto.Description;
        plan.DepartmentId = dto.DepartmentId;
        plan.FiscalYear = dto.FiscalYear;
        plan.PlanningCycle = dto.PlanningCycle;
        plan.PlanningQuarter = dto.PlanningQuarter;
        plan.PlanStartDate = dto.PlanStartDate;
        plan.PlanEndDate = dto.PlanEndDate;
        plan.PlanDurationYears = dto.PlanDurationYears;
        plan.TotalEstimatedBudget = dto.TotalEstimatedBudget;
        plan.Currency = dto.Currency;
        plan.Notes = dto.Notes;
        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);

        if (currencyChanged)
        {
            var planItems = await _itemRepository.GetByPlanIdAsync(id);
            foreach (var item in planItems.Where(i => !i.IsDeleted))
            {
                item.Currency = dto.Currency;
                item.UpdatedAt = DateTime.UtcNow;
                await _itemRepository.UpdateAsync(item);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated plan");
    }

    public async Task<ProcurementPlanDetailDto> SubmitForApprovalAsync(Guid id, SubmitProcurementPlanDto dto)
    {
        var plan = await _planRepository.GetWithFullDetailsAsync(id);
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {id} not found");

        if (plan.Status != "Draft")
            throw new InvalidOperationException("Only draft plans can be submitted for approval");

        if (plan.Items == null || !plan.Items.Any(i => !i.IsDeleted))
            throw new InvalidOperationException("Procurement plan must contain at least one item before submission");

        var currentUserId = _currentUserProvider.UserId;
        if (currentUserId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated");

        var workflowResult = await _workflowIntegrationService.SubmitAsync(WorkflowEntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start procurement plan workflow");

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
        statusAdapter.ApplySubmitOutcome(plan, workflowResult.Outcome, currentUserId);
        plan.ReviewComments = dto.Comments;
        plan.UpdatedAt = DateTime.UtcNow;

        if (workflowResult.Outcome == WorkflowOutcome.Approved)
        {
            await ApplyFinalApprovalAsync(plan, new ApproveProcurementPlanDto
            {
                ApprovedBudget = plan.TotalEstimatedBudget,
                Comments = dto.Comments,
                AutoGenerateSchedules = true,
                AutoLinkBudget = true
            }, currentUserId);
        }

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

        if (currentUserId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, id, currentUserId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current procurement plan workflow step");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            WorkflowEntityType,
            id,
            currentUserId,
            dto.IsApproved ? "Approve" : "Reject",
            dto.Comments);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process procurement plan workflow action");

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
        statusAdapter.ApplyApprovalOutcome(plan, workflowResult.Outcome, currentUserId, dto.Comments);

        if (dto.IsApproved && workflowResult.Outcome == WorkflowOutcome.Approved)
        {
            await ApplyFinalApprovalAsync(plan, dto, currentUserId);
        }
        else if (!dto.IsApproved && workflowResult.Outcome == WorkflowOutcome.Rejected)
        {
            plan.ReviewComments = dto.Comments;
            plan.ApprovalComments = null;
        }
        else if (!string.IsNullOrWhiteSpace(dto.Comments))
        {
            if (dto.IsApproved)
                plan.ApprovalComments = dto.Comments;
            else
                plan.ReviewComments = dto.Comments;
        }

        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Procurement plan {PlanNumber} {Status}", plan.PlanNumber, plan.Status);

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve plan");
    }

    private async Task ApplyFinalApprovalAsync(ProcurementPlan plan, ApproveProcurementPlanDto dto, Guid currentUserId)
    {
        plan.Status = "Approved";
        plan.ApprovedById = currentUserId != Guid.Empty ? currentUserId : null;
        plan.ApprovedDate = DateTime.UtcNow;
        plan.ApprovedBudget = dto.ApprovedBudget ?? plan.TotalEstimatedBudget;
        plan.ApprovalComments = dto.Comments;

        var planItems = plan.Items?.Where(i => !i.IsDeleted && i.Status == "Planned").ToList() ?? new List<ProcurementPlanItem>();
        _logger.LogInformation("Found {Count} plan items with status 'Planned' to update", planItems.Count);

        foreach (var item in planItems)
        {
            item.Status = "Approved";
            item.UpdatedAt = DateTime.UtcNow;
            await _itemRepository.UpdateAsync(item);
        }

        _logger.LogInformation("Updated {Count} plan items to Approved status", planItems.Count);

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
                        TimingRationale = "Based on required date from procurement plan item"
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

        if (dto.AutoLinkBudget)
        {
            try
            {
                Guid? budgetIdToLink = dto.BudgetId;

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
            }
        }
    }

    public async Task<ProcurementPlanDetailDto> PublishAsync(Guid id, PublishProcurementPlanDto dto)
    {
        var plan = await _planRepository.GetWithFullDetailsAsync(id);
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {id} not found");

        if (plan.Status == "Active")
            throw new InvalidOperationException("Procurement plan has already been published to execution");

        if (plan.Status != "Approved")
            throw new InvalidOperationException("Only approved procurement plans can be published to execution");

        var activeItems = plan.Items?.Where(i => !i.IsDeleted).ToList() ?? new List<ProcurementPlanItem>();
        if (!activeItems.Any())
            throw new InvalidOperationException("Procurement plan must contain at least one item before publishing");

        var currentUserId = _currentUserProvider.UserId;
        plan.Status = "Active";
        plan.PublishedById = currentUserId != Guid.Empty ? currentUserId : null;
        plan.PublishedDate = DateTime.UtcNow;
        plan.PublishComments = dto.Comments;
        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Published procurement plan {PlanNumber} to procurement execution", plan.PlanNumber);

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve published plan");
    }

    public async Task<ProcurementPlanDetailDto> CreateAmendmentAsync(Guid id, CreateProcurementPlanAmendmentDto dto)
    {
        var sourcePlan = await _planRepository.GetWithFullDetailsAsync(id);
        if (sourcePlan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {id} not found");

        if (sourcePlan.Status is not ("Approved" or "Active" or "Completed"))
            throw new InvalidOperationException("Only approved, active, or completed plans can be amended");

        var currentUserId = _currentUserProvider.UserId;
        var amendment = new ProcurementPlan
        {
            PlanNumber = await _planRepository.GeneratePlanNumberAsync(sourcePlan.FiscalYear),
            Title = string.IsNullOrWhiteSpace(dto.Title) ? $"{sourcePlan.Title} - Amendment {sourcePlan.RevisionNumber + 1}" : dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? sourcePlan.Description : dto.Description,
            DepartmentId = sourcePlan.DepartmentId,
            FiscalYear = sourcePlan.FiscalYear,
            PlanningCycle = sourcePlan.PlanningCycle,
            PlanningQuarter = sourcePlan.PlanningQuarter,
            PlanStartDate = sourcePlan.PlanStartDate,
            PlanEndDate = sourcePlan.PlanEndDate,
            PlanDurationYears = sourcePlan.PlanDurationYears,
            Status = "Draft",
            TotalEstimatedBudget = sourcePlan.TotalEstimatedBudget,
            ApprovedBudget = 0,
            Currency = sourcePlan.Currency,
            PreparedById = currentUserId != Guid.Empty ? currentUserId : null,
            PreparedDate = DateTime.UtcNow,
            RevisionNumber = sourcePlan.RevisionNumber + 1,
            PreviousVersionId = sourcePlan.Id,
            Notes = $"Amendment reason: {dto.Reason.Trim()}",
            TenantId = _currentUserProvider.TenantId
        };

        await _planRepository.AddAsync(amendment);

        foreach (var sourceItem in sourcePlan.Items.Where(i => !i.IsDeleted))
        {
            var item = new ProcurementPlanItem
            {
                ProcurementPlanId = amendment.Id,
                InventoryItemId = sourceItem.InventoryItemId,
                ProcurementBudgetId = sourceItem.ProcurementBudgetId,
                ProcurementBudgetAllocationId = sourceItem.ProcurementBudgetAllocationId,
                MarketAnalysisId = sourceItem.MarketAnalysisId,
                BudgetLineCode = sourceItem.BudgetLineCode,
                BudgetCategoryName = sourceItem.BudgetCategoryName,
                ApprovedBudgetAmount = sourceItem.ApprovedBudgetAmount,
                BudgetNotes = sourceItem.BudgetNotes,
                ItemDescription = sourceItem.ItemDescription,
                Specifications = sourceItem.Specifications,
                ItemCategory = sourceItem.ItemCategory,
                EstimatedQuantity = sourceItem.EstimatedQuantity,
                UnitOfMeasure = sourceItem.UnitOfMeasure,
                EstimatedUnitPrice = sourceItem.EstimatedUnitPrice,
                EstimatedTotalCost = sourceItem.EstimatedTotalCost,
                Currency = amendment.Currency,
                Priority = sourceItem.Priority,
                IsCritical = sourceItem.IsCritical,
                RequiredDate = sourceItem.RequiredDate,
                PlannedProcurementMonth = sourceItem.PlannedProcurementMonth,
                PlannedQuarter = sourceItem.PlannedQuarter,
                PreferredSupplierId = sourceItem.PreferredSupplierId,
                PreferredSupplierName = sourceItem.PreferredSupplierName,
                AlternativeSuppliers = sourceItem.AlternativeSuppliers,
                Justification = sourceItem.Justification,
                Status = "Planned",
                ProcurementMethod = sourceItem.ProcurementMethod,
                Notes = sourceItem.Notes,
                TenantId = _currentUserProvider.TenantId
            };

            await _itemRepository.AddAsync(item);
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Created amendment {AmendmentPlanNumber} from procurement plan {SourcePlanNumber}", amendment.PlanNumber, sourcePlan.PlanNumber);

        return await GetByIdAsync(amendment.Id) ?? throw new InvalidOperationException("Failed to retrieve created amendment");
    }

    public async Task<IEnumerable<ProcurementPlanDto>> GetVersionHistoryAsync(Guid id)
    {
        var current = await _planRepository.GetByIdAsync(id);
        if (current == null)
            throw new KeyNotFoundException($"Procurement plan with ID {id} not found");

        var plans = await GetPlanningQuery()
            .Where(p => p.DepartmentId == current.DepartmentId && p.FiscalYear == current.FiscalYear)
            .Include(p => p.Department)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .Include(p => p.PreparedBy)
            .Include(p => p.ApprovedBy)
            .Include(p => p.PublishedBy)
            .ToListAsync();

        var byId = plans.ToDictionary(p => p.Id);
        var versionIds = new HashSet<Guid> { current.Id };
        var cursor = current;
        while (cursor.PreviousVersionId.HasValue && byId.TryGetValue(cursor.PreviousVersionId.Value, out var previous))
        {
            if (!versionIds.Add(previous.Id))
                break;
            cursor = previous;
        }

        var expanded = true;
        while (expanded)
        {
            expanded = false;
            foreach (var child in plans.Where(p => p.PreviousVersionId.HasValue && versionIds.Contains(p.PreviousVersionId.Value)))
            {
                if (versionIds.Add(child.Id))
                    expanded = true;
            }
        }

        return plans
            .Where(p => versionIds.Contains(p.Id))
            .OrderBy(p => p.RevisionNumber)
            .ThenBy(p => p.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<IEnumerable<ProcurementPlanConsolidationOpportunityDto>> GetConsolidationOpportunitiesAsync(
        int? fiscalYear = null,
        string? planningQuarter = null,
        Guid? departmentId = null)
    {
        var plans = await GetPlanningQuery()
            .Where(p => p.Status == "Approved" || p.Status == "Active")
            .Where(p => !fiscalYear.HasValue || p.FiscalYear == fiscalYear.Value)
            .Where(p => !departmentId.HasValue || p.DepartmentId == departmentId.Value)
            .Include(p => p.Department)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.ItemSuppliers.Where(s => !s.IsDeleted))
                    .ThenInclude(s => s.BusinessPartner)
            .ToListAsync();

        var rows = plans
            .SelectMany(plan => plan.Items
                .Where(item => !item.IsDeleted)
                .Where(item => string.IsNullOrWhiteSpace(planningQuarter) || item.PlannedQuarter == planningQuarter || plan.PlanningQuarter == planningQuarter)
                .Select(item => new { plan, item }))
            .ToList();

        return rows
            .GroupBy(row => BuildConsolidationKey(row.item))
            .Select(group =>
            {
                var items = group.ToList();
                var first = items.First().item;
                var totalCost = items.Sum(x => x.item.EstimatedTotalCost);
                var totalQty = items.Sum(x => x.item.EstimatedQuantity);
                var departmentCount = items.Select(x => x.plan.DepartmentId).Distinct().Count();
                var planCount = items.Select(x => x.plan.Id).Distinct().Count();
                var supplierName = items
                    .Select(x => x.item.PreferredSupplierName)
                    .Concat(items.SelectMany(x => x.item.ItemSuppliers.Where(s => s.IsPreferred).Select(s => s.BusinessPartner?.PartnerName)))
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                var savingsRate = departmentCount >= 3 || planCount >= 3 ? 0.07m : planCount >= 2 ? 0.04m : 0m;
                var potentialSavings = Math.Round(totalCost * savingsRate, 2);

                return new ProcurementPlanConsolidationOpportunityDto
                {
                    OpportunityKey = group.Key,
                    ItemCategory = string.IsNullOrWhiteSpace(first.ItemCategory) ? "Uncategorized" : first.ItemCategory!,
                    ItemDescription = first.ItemDescription,
                    Specifications = first.Specifications,
                    UnitOfMeasure = first.UnitOfMeasure,
                    Currency = first.Currency,
                    PlanCount = planCount,
                    DepartmentCount = departmentCount,
                    ItemCount = items.Count,
                    TotalQuantity = totalQty,
                    EstimatedTotalCost = totalCost,
                    AverageUnitPrice = totalQty > 0 ? Math.Round(totalCost / totalQty, 4) : 0,
                    PotentialSavings = potentialSavings,
                    OpportunityLevel = potentialSavings >= 50000 || departmentCount >= 3 ? "High" : potentialSavings > 0 ? "Medium" : "Low",
                    RecommendedStrategy = potentialSavings > 0 ? "Consolidate" : "Maintain",
                    PreferredSupplierName = supplierName,
                    Items = items.Select(x => new ProcurementPlanConsolidationItemDto
                    {
                        PlanId = x.plan.Id,
                        PlanNumber = x.plan.PlanNumber,
                        PlanItemId = x.item.Id,
                        DepartmentId = x.plan.DepartmentId,
                        DepartmentName = x.plan.Department?.Name,
                        ItemDescription = x.item.ItemDescription,
                        Quantity = x.item.EstimatedQuantity,
                        EstimatedTotalCost = x.item.EstimatedTotalCost,
                        PlannedQuarter = x.item.PlannedQuarter ?? x.plan.PlanningQuarter,
                        RequiredDate = x.item.RequiredDate,
                        PreferredSupplierName = x.item.PreferredSupplierName
                    }).ToList()
                };
            })
            .Where(o => o.ItemCount > 1 || o.DepartmentCount > 1 || o.PotentialSavings > 0)
            .OrderByDescending(o => o.PotentialSavings)
            .ThenByDescending(o => o.EstimatedTotalCost)
            .ToList();
    }

    public async Task<ProcurementPlanningDashboardDto> GetDashboardAsync(
        int? fiscalYear = null,
        string? planningQuarter = null,
        Guid? departmentId = null)
    {
        var effectiveFiscalYear = fiscalYear ?? DateTime.UtcNow.Year;
        var plans = await GetPlanningQuery()
            .Where(p => p.FiscalYear == effectiveFiscalYear)
            .Where(p => string.IsNullOrWhiteSpace(planningQuarter) || p.PlanningQuarter == planningQuarter || p.Items.Any(i => i.PlannedQuarter == planningQuarter && !i.IsDeleted))
            .Where(p => !departmentId.HasValue || p.DepartmentId == departmentId.Value)
            .Include(p => p.Department)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .ToListAsync();

        var activeItems = plans.SelectMany(p => p.Items.Where(i => !i.IsDeleted)).ToList();
        var opportunities = (await GetConsolidationOpportunitiesAsync(effectiveFiscalYear, planningQuarter, departmentId)).ToList();
        var estimatedBudget = plans.Sum(p => p.TotalEstimatedBudget);
        var approvedBudget = plans.Sum(p => p.ApprovedBudget);
        var strategicAnalytics = await BuildStrategicAnalyticsAsync(effectiveFiscalYear, opportunities);

        return new ProcurementPlanningDashboardDto
        {
            FiscalYear = effectiveFiscalYear,
            PlanningQuarter = planningQuarter,
            TotalPlans = plans.Count,
            DraftPlans = plans.Count(p => p.Status == "Draft"),
            SubmittedPlans = plans.Count(p => p.Status == "Submitted" || p.Status == "UnderReview"),
            ApprovedPlans = plans.Count(p => p.Status == "Approved"),
            ActivePlans = plans.Count(p => p.Status == "Active"),
            CompletedPlans = plans.Count(p => p.Status == "Completed"),
            TotalItems = activeItems.Count,
            CriticalItems = activeItems.Count(i => i.IsCritical),
            EstimatedBudget = estimatedBudget,
            ApprovedBudget = approvedBudget,
            BudgetUtilizationPercent = estimatedBudget > 0 ? Math.Round((approvedBudget / estimatedBudget) * 100, 2) : 0,
            ConsolidationPotentialSavings = opportunities.Sum(o => o.PotentialSavings),
            Currency = plans.Select(p => p.Currency).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "USD",
            DepartmentSummaries = plans
                .GroupBy(p => new { p.DepartmentId, DepartmentName = p.Department?.Name ?? "Unknown" })
                .Select(g => new ProcurementPlanningDepartmentSummaryDto
                {
                    DepartmentId = g.Key.DepartmentId,
                    DepartmentName = g.Key.DepartmentName,
                    PlanCount = g.Count(),
                    ItemCount = g.Sum(p => p.Items.Count(i => !i.IsDeleted)),
                    EstimatedBudget = g.Sum(p => p.TotalEstimatedBudget),
                    ApprovedBudget = g.Sum(p => p.ApprovedBudget)
                })
                .OrderByDescending(x => x.EstimatedBudget)
                .ToList(),
            CategorySummaries = activeItems
                .GroupBy(i => string.IsNullOrWhiteSpace(i.ItemCategory) ? "Uncategorized" : i.ItemCategory!)
                .Select(g => new ProcurementPlanningCategorySummaryDto
                {
                    CategoryName = g.Key,
                    ItemCount = g.Count(),
                    EstimatedCost = g.Sum(i => i.EstimatedTotalCost),
                    ApprovedBudget = g.Sum(i => i.ApprovedBudgetAmount ?? 0)
                })
                .OrderByDescending(x => x.EstimatedCost)
                .Take(10)
                .ToList(),
            QuarterSummaries = activeItems
                .GroupBy(i => string.IsNullOrWhiteSpace(i.PlannedQuarter) ? "Unscheduled" : i.PlannedQuarter!)
                .Select(g => new ProcurementPlanningQuarterSummaryDto
                {
                    Quarter = g.Key,
                    ItemCount = g.Count(),
                    EstimatedCost = g.Sum(i => i.EstimatedTotalCost)
                })
                .OrderBy(x => x.Quarter)
                .ToList(),
            StrategicAnalytics = strategicAnalytics
        };
    }

    private async Task<ProcurementPlanningStrategicAnalyticsDto> BuildStrategicAnalyticsAsync(
        int fiscalYear,
        IReadOnlyCollection<ProcurementPlanConsolidationOpportunityDto> consolidationOpportunities)
    {
        var startDate = new DateTime(fiscalYear, 1, 1);
        var endDate = new DateTime(fiscalYear, 12, 31, 23, 59, 59);
        var analytics = new ProcurementPlanningStrategicAnalyticsDto();

        var analysisResult = await _marketAnalysisRepository.GetAnalysesAsync(1, 10000);
        var analyses = analysisResult.Items
            .Where(a => !a.IsDeleted)
            .Where(a => a.AnalysisPeriodEnd >= startDate && a.AnalysisPeriodStart <= endDate)
            .ToList();

        analytics.Market = BuildMarketAnalytics(analyses);

        try
        {
            var activeSuppliers = (await _businessPartnerRepository.GetActivePartnersAsync()).Where(IsSupplierPartner).ToList();
            var preferredSuppliers = activeSuppliers.Where(s => s.IsPreferred).ToList();
            var spendAnalysis = await _supplierReportingService.GetSupplierSpendAnalysisAsync(new SupplierSpendAnalysisRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                TopN = 10,
                IncludeBlacklisted = false
            });
            var concentration = await _supplierReportingService.GetVendorConcentrationAnalysisAsync(startDate, endDate);
            var riskAssessment = await _supplierReportingService.GetSupplierRiskAssessmentAsync(startDate, endDate);

            analytics.Supplier = new ProcurementPlanningSupplierAnalyticsDto
            {
                ActiveSuppliers = activeSuppliers.Count,
                PreferredSuppliers = preferredSuppliers.Count,
                ConsolidationOpportunities = consolidationOpportunities.Count(o => o.OpportunityLevel == "High" || o.OpportunityLevel == "Medium"),
                SupplierRiskScore = CalculateSupplierRiskScore(riskAssessment, activeSuppliers.Count),
                TotalSupplierSpend = spendAnalysis.Sum(s => s.TotalSpend),
                ConcentrationRisk = concentration.ConcentrationRisk,
                SpendBySupplier = spendAnalysis.Select(s => new ProcurementPlanningSupplierSpendSummaryDto
                {
                    SupplierId = s.SupplierId,
                    SupplierName = s.SupplierName,
                    TotalSpend = s.TotalSpend,
                    PercentageOfTotalSpend = s.PercentageOfTotalSpend,
                    IsPreferred = s.IsPreferred,
                    RiskLevel = s.RiskLevel
                }).ToList(),
                HighRiskSuppliers = riskAssessment
                    .Where(r => r.RiskLevel == "High" || r.RiskLevel == "Critical" || r.RiskFactors.Any())
                    .Take(10)
                    .Select(r => new ProcurementPlanningSupplierRiskSummaryDto
                    {
                        SupplierId = r.SupplierId,
                        SupplierName = r.SupplierName,
                        RiskLevel = r.RiskLevel,
                        TotalSpend = r.TotalSpend,
                        PercentageOfTotalSpend = r.PercentageOfTotalSpend,
                        RiskFactors = r.RiskFactors
                    }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to build supplier strategic analytics for fiscal year {FiscalYear}", fiscalYear);
        }

        return analytics;
    }

    private static ProcurementPlanningMarketAnalyticsDto BuildMarketAnalytics(IReadOnlyCollection<MarketAnalysis> analyses)
    {
        if (analyses.Count == 0)
        {
            return new ProcurementPlanningMarketAnalyticsDto();
        }

        return new ProcurementPlanningMarketAnalyticsDto
        {
            AverageMarketPrice = Math.Round(analyses.Average(a => a.CurrentMarketPrice), 4),
            AveragePriceIncreasePercent = Math.Round(analyses.Average(a => a.PriceVariancePercent ?? a.PriceChangePercent), 2),
            HighInflationCategoryCount = analyses
                .Where(a => (a.PriceVariancePercent ?? a.PriceChangePercent) >= 10 || a.InflationImpactPercent >= 10)
                .Select(a => string.IsNullOrWhiteSpace(a.ItemCategory) ? "Uncategorized" : a.ItemCategory!)
                .Distinct()
                .Count(),
            HighRiskCategoryCount = analyses
                .Where(a => a.MarketRiskLevel == "High" || a.SupplyRiskLevel == "High")
                .Select(a => string.IsNullOrWhiteSpace(a.ItemCategory) ? "Uncategorized" : a.ItemCategory!)
                .Distinct()
                .Count(),
            LongLeadTimeItemCount = analyses.Count(a => a.LeadTimeDays >= 30),
            MarketRiskIndex = Math.Round(analyses.Average(a => RiskScore(a.MarketRiskLevel, a.SupplyRiskLevel)), 2),
            InflationImpactPercent = Math.Round(analyses.Average(a => a.InflationImpactPercent), 2),
            HighRiskCategories = analyses
                .GroupBy(a => string.IsNullOrWhiteSpace(a.ItemCategory) ? "Uncategorized" : a.ItemCategory!)
                .Select(g => new ProcurementPlanningMarketCategoryRiskDto
                {
                    CategoryName = g.Key,
                    AnalysisCount = g.Count(),
                    AveragePriceIncreasePercent = Math.Round(g.Average(a => a.PriceVariancePercent ?? a.PriceChangePercent), 2),
                    LongLeadTimeCount = g.Count(a => a.LeadTimeDays >= 30),
                    HighestRiskLevel = HighestRisk(g.SelectMany(a => new[] { a.MarketRiskLevel, a.SupplyRiskLevel }))
                })
                .Where(g => g.HighestRiskLevel == "High" || g.AveragePriceIncreasePercent >= 10 || g.LongLeadTimeCount > 0)
                .OrderByDescending(g => RiskRank(g.HighestRiskLevel))
                .ThenByDescending(g => g.AveragePriceIncreasePercent)
                .Take(10)
                .ToList()
        };
    }

    private static bool IsSupplierPartner(BusinessPartner partner)
        => string.Equals(partner.PartnerType, "Supplier", StringComparison.OrdinalIgnoreCase)
            || string.Equals(partner.PartnerType, "Both", StringComparison.OrdinalIgnoreCase);

    private static decimal CalculateSupplierRiskScore(IEnumerable<SupplierRiskAssessmentDto> risks, int activeSupplierCount)
    {
        if (activeSupplierCount <= 0) return 0;
        var weightedRisk = risks.Sum(r => RiskRank(r.RiskLevel));
        return Math.Round((weightedRisk / (activeSupplierCount * 4m)) * 100m, 2);
    }

    private static decimal RiskScore(params string?[] riskLevels)
        => riskLevels.Select(RiskRank).DefaultIfEmpty(1).Max() * 25m;

    private static string HighestRisk(IEnumerable<string?> riskLevels)
        => riskLevels
            .OrderByDescending(RiskRank)
            .FirstOrDefault(level => !string.IsNullOrWhiteSpace(level)) ?? "Low";

    private static int RiskRank(string? riskLevel)
        => (riskLevel ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 1
        };

    public async Task<ProcurementPlanningReportDto> GetReportAsync(
        string reportType,
        int? fiscalYear = null,
        string? planningQuarter = null,
        Guid? departmentId = null)
    {
        var normalizedReportType = (reportType ?? string.Empty).Trim().ToLowerInvariant();
        var plans = await GetPlanningQuery()
            .Where(p => !fiscalYear.HasValue || p.FiscalYear == fiscalYear.Value)
            .Where(p => string.IsNullOrWhiteSpace(planningQuarter) || p.PlanningQuarter == planningQuarter || p.Items.Any(i => i.PlannedQuarter == planningQuarter && !i.IsDeleted))
            .Where(p => !departmentId.HasValue || p.DepartmentId == departmentId.Value)
            .Include(p => p.Department)
            .Include(p => p.PublishedBy)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .OrderBy(p => p.FiscalYear)
            .ThenBy(p => p.Department!.Name)
            .ThenBy(p => p.PlanNumber)
            .ToListAsync();

        var rows = normalizedReportType == "publish-register"
            ? plans
                .Where(p => p.PublishedDate.HasValue || p.Status == "Active")
                .Select(p => new ProcurementPlanningReportRowDto
                {
                    PlanNumber = p.PlanNumber,
                    PlanTitle = p.Title,
                    DepartmentName = p.Department?.Name,
                    FiscalYear = p.FiscalYear,
                    PlanningCycle = p.PlanningCycle,
                    PlanningQuarter = p.PlanningQuarter,
                    Status = p.Status,
                    EstimatedCost = p.TotalEstimatedBudget,
                    ApprovedBudget = p.ApprovedBudget,
                    Variance = p.ApprovedBudget - p.TotalEstimatedBudget,
                    PublishedByName = p.PublishedBy?.FullName,
                    PublishedDate = p.PublishedDate
                })
                .ToList()
            : plans
                .SelectMany(p => p.Items.Where(i => !i.IsDeleted).DefaultIfEmpty(), (p, i) => new ProcurementPlanningReportRowDto
                {
                    PlanNumber = p.PlanNumber,
                    PlanTitle = p.Title,
                    DepartmentName = p.Department?.Name,
                    FiscalYear = p.FiscalYear,
                    PlanningCycle = p.PlanningCycle,
                    PlanningQuarter = i?.PlannedQuarter ?? p.PlanningQuarter,
                    Status = p.Status,
                    ItemCategory = i?.ItemCategory,
                    ItemDescription = i?.ItemDescription,
                    Quantity = i?.EstimatedQuantity ?? 0,
                    EstimatedCost = i?.EstimatedTotalCost ?? p.TotalEstimatedBudget,
                    ApprovedBudget = i?.ApprovedBudgetAmount ?? p.ApprovedBudget,
                    Variance = (i?.ApprovedBudgetAmount ?? p.ApprovedBudget) - (i?.EstimatedTotalCost ?? p.TotalEstimatedBudget),
                    PublishedByName = p.PublishedBy?.FullName,
                    PublishedDate = p.PublishedDate
                })
                .ToList();

        if (normalizedReportType == "quarterly")
        {
            rows = rows.Where(r => string.IsNullOrWhiteSpace(planningQuarter) || r.PlanningQuarter == planningQuarter).ToList();
        }

        return new ProcurementPlanningReportDto
        {
            ReportType = normalizedReportType,
            Title = normalizedReportType switch
            {
                "quarterly" => "Quarterly Procurement Plan",
                "department" => "Department Procurement Plan",
                "budget-variance" => "Budget Variance Report",
                "publish-register" => "Published Plan Register",
                _ => "Annual Procurement Plan"
            },
            FiscalYear = fiscalYear,
            PlanningQuarter = planningQuarter,
            GeneratedAt = DateTime.UtcNow,
            Currency = plans.Select(p => p.Currency).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "USD",
            Rows = normalizedReportType == "budget-variance"
                ? rows.OrderByDescending(r => Math.Abs(r.Variance)).ToList()
                : rows
        };
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

        var item = CreatePlanItem(planId, dto, plan.Currency);
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

        var plan = await _planRepository.GetByIdAsync(item.ProcurementPlanId);
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {item.ProcurementPlanId} not found");

        item.InventoryItemId = dto.InventoryItemId;
        item.ProcurementBudgetId = dto.ProcurementBudgetId;
        item.ProcurementBudgetAllocationId = dto.ProcurementBudgetAllocationId;
        item.MarketAnalysisId = dto.MarketAnalysisId;
        item.BudgetLineCode = dto.BudgetLineCode;
        item.BudgetCategoryName = dto.BudgetCategoryName;
        item.ApprovedBudgetAmount = dto.ApprovedBudgetAmount;
        item.BudgetNotes = dto.BudgetNotes;
        item.ItemDescription = dto.ItemDescription;
        item.Specifications = dto.Specifications;
        item.ItemCategory = dto.ItemCategory;
        item.EstimatedQuantity = dto.EstimatedQuantity;
        item.UnitOfMeasure = dto.UnitOfMeasure;
        item.EstimatedUnitPrice = dto.EstimatedUnitPrice;
        item.EstimatedTotalCost = dto.EstimatedQuantity * dto.EstimatedUnitPrice;
        item.Currency = plan.Currency;
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

    private IQueryable<ProcurementPlan> GetPlanningQuery()
    {
        var tenantId = _currentUserProvider.TenantId;
        return _planRepository.GetQueryable()
            .Where(p => !p.IsDeleted && p.TenantId == tenantId);
    }

    private static string BuildConsolidationKey(ProcurementPlanItem item)
    {
        if (item.InventoryItemId.HasValue && item.InventoryItemId.Value != Guid.Empty)
        {
            return $"inventory:{item.InventoryItemId.Value:N}:{NormalizeKey(item.UnitOfMeasure)}:{NormalizeKey(item.Currency)}";
        }

        return string.Join(":",
            "text",
            NormalizeKey(item.ItemCategory),
            NormalizeKey(item.ItemDescription),
            NormalizeKey(item.Specifications),
            NormalizeKey(item.UnitOfMeasure),
            NormalizeKey(item.Currency));
    }

    private static string NormalizeKey(string? value)
        => new((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private ProcurementPlanItem CreatePlanItem(Guid planId, CreateProcurementPlanItemDto dto, string currency)
    {
        return new ProcurementPlanItem
        {
            ProcurementPlanId = planId,
            InventoryItemId = dto.InventoryItemId,
            ProcurementBudgetId = dto.ProcurementBudgetId,
            ProcurementBudgetAllocationId = dto.ProcurementBudgetAllocationId,
            MarketAnalysisId = dto.MarketAnalysisId,
            BudgetLineCode = dto.BudgetLineCode,
            BudgetCategoryName = dto.BudgetCategoryName,
            ApprovedBudgetAmount = dto.ApprovedBudgetAmount,
            BudgetNotes = dto.BudgetNotes,
            ItemDescription = dto.ItemDescription,
            Specifications = dto.Specifications,
            ItemCategory = dto.ItemCategory,
            EstimatedQuantity = dto.EstimatedQuantity,
            UnitOfMeasure = dto.UnitOfMeasure,
            EstimatedUnitPrice = dto.EstimatedUnitPrice,
            EstimatedTotalCost = dto.EstimatedQuantity * dto.EstimatedUnitPrice,
            Currency = currency,
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
            PlanningCycle = plan.PlanningCycle,
            PlanningQuarter = plan.PlanningQuarter,
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
            PublishedByName = plan.PublishedBy?.FullName,
            PublishedDate = plan.PublishedDate,
            IsPublished = plan.PublishedDate.HasValue || plan.Status == "Active",
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
            PlanningCycle = plan.PlanningCycle,
            PlanningQuarter = plan.PlanningQuarter,
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
            PublishedById = plan.PublishedById,
            PublishedByName = plan.PublishedBy?.FullName,
            PublishedDate = plan.PublishedDate,
            PublishComments = plan.PublishComments,
            IsPublished = plan.PublishedDate.HasValue || plan.Status == "Active",
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
            ProcurementBudgetId = item.ProcurementBudgetId,
            ProcurementBudgetAllocationId = item.ProcurementBudgetAllocationId,
            MarketAnalysisId = item.MarketAnalysisId,
            MarketAnalysisTitle = item.MarketAnalysis?.Title,
            BudgetLineCode = item.BudgetLineCode,
            BudgetCategoryName = item.BudgetCategoryName,
            ApprovedBudgetAmount = item.ApprovedBudgetAmount,
            BudgetNotes = item.BudgetNotes,
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
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {planItem.ProcurementPlanId} not found");

        if (plan.Status != "Active")
            throw new InvalidOperationException("Procurement plan must be published to execution before converting items");

        // Create tender from plan item
        var createTenderDto = new CreateTenderDto
        {
            SourcePurchaseRequisitionId = dto.PurchaseRequisitionId,
            SourceProcurementPlanItemId = dto.PlanItemId,
            Title = dto.TenderTitle,
            Description = dto.TenderDescription ?? planItem.Specifications,
            TenderType = dto.TenderType,
            SubmissionDeadline = dto.SubmissionDeadline,
            OpeningDate = dto.OpeningDate,
            EstimatedValue = planItem.EstimatedTotalCost,
            Currency = plan.Currency,
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
        if (dto.CreateSchedule)
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
        if (plan == null)
            throw new KeyNotFoundException($"Procurement plan with ID {planItem.ProcurementPlanId} not found");

        if (plan.Status != "Active")
            throw new InvalidOperationException("Procurement plan must be published to execution before converting items");

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
        if (dto.CreateSchedule)
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
            PurchaseRequisitionId = dto.PurchaseRequisitionId,
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
