using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Physical Count management service
/// Handles physical inventory counts, variances, and adjustments
/// </summary>
public partial class PhysicalCountService : IPhysicalCountService
{
    private readonly IPhysicalCountRepository _countRepository;
    private readonly IPhysicalCountItemRepository _countItemRepository;
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IStockAdjustmentService _stockAdjustmentService;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly ILogger<PhysicalCountService> _logger;
    private readonly IWarehouseDefaultLocationService _defaultLocations;
    private readonly IWorkflowIntegrationService? _approvalWorkflows;

    public PhysicalCountService(
        IPhysicalCountRepository countRepository,
        IPhysicalCountItemRepository countItemRepository,
        IInventoryItemRepository itemRepository,
        IWarehouseRepository warehouseRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IProcurementAccessControlService accessControl,
        IStockAdjustmentService stockAdjustmentService,
        IProcurementControlEventService controlEvents,
        ILogger<PhysicalCountService> logger,
        IWarehouseDefaultLocationService defaultLocations,
        IWorkflowIntegrationService? approvalWorkflows = null)
    {
        _countRepository = countRepository;
        _countItemRepository = countItemRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _accessControl = accessControl;
        _stockAdjustmentService = stockAdjustmentService;
        _controlEvents = controlEvents;
        _logger = logger;
        _defaultLocations = defaultLocations;
        _approvalWorkflows = approvalWorkflows;
    }

    #region Query Operations

    public async Task<IEnumerable<PhysicalCountDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var counts = await _countRepository.GetByDateRangeAsync(
            fromDate ?? DateTime.UtcNow.AddMonths(-3),
            toDate ?? DateTime.UtcNow);
        return (await FilterReadableAsync(counts)).OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.CountNumber).Select(MapToDto);
    }

    public async Task<IEnumerable<PhysicalCountDto>> GetByWarehouseAsync(Guid warehouseId)
    {
        var counts = await _countRepository.GetByWarehouseAsync(warehouseId);
        return (await FilterReadableAsync(counts)).OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.CountNumber).Select(MapToDto);
    }

    public async Task<IEnumerable<PhysicalCountDto>> GetInProgressAsync()
    {
        var counts = await _countRepository.GetInProgressAsync();
        return (await FilterReadableAsync(counts)).OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.CountNumber).Select(MapToDto);
    }

    public async Task<PhysicalCountDetailDto?> GetByIdAsync(Guid id)
    {
        var count = await _countRepository.GetWithItemsAsync(id);
        if (count == null || !await CanAccessAsync(count, "procurement.inventory.read")) return null;
        var detail = MapToDetailDto(count);
        var actor = Guid.TryParse(_currentUserService.UserId, out var actorId) ? actorId : Guid.Empty;
        detail.CanReview = count.CountedById == actor &&
            await CanAccessAsync(count, "procurement.inventory.count");
        var decisionRole = count.Status switch {
            "PendingStoresApproval" => StoresManagerRole,
            "PendingFinanceApproval" => FinanceReviewerRole,
            "PendingAuditAttestation" => ProcurementAccessControlRegistry.InternalAuditRole,
            _ => null
        };
        var priorActors = new Guid?[] { count.InitiatedById, count.CountedById, count.StoresApprovedById,
            count.FinanceApprovedById, count.AuditAttestedById };
        detail.CanDecide = decisionRole != null && _currentUserService.Roles.Contains(decisionRole, StringComparer.OrdinalIgnoreCase) &&
            !priorActors.Contains(actor) && !count.Items.Any(i => i.RecountedById == actor) &&
            await CanAccessAsync(count, count.Status == "PendingAuditAttestation" ? "procurement.inventory.read" : "procurement.inventory.adjust.approve");
        detail.CanPost = await CanPostCountAsync(count);
        return detail;
    }

    public async Task<PhysicalCountDetailDto?> GetByCountNumberAsync(string countNumber)
    {
        var count = await _countRepository.GetByCountNumberAsync(countNumber);
        if (count == null) return null;
        var fullCount = await _countRepository.GetWithItemsAsync(count.Id);
        if (fullCount == null || !await CanAccessAsync(fullCount, "procurement.inventory.read")) return null;
        var detail = MapToDetailDto(fullCount);
        detail.CanPost = await CanPostCountAsync(fullCount);
        return detail;
    }

    public async Task<IEnumerable<PhysicalCountDto>> GetFilteredAsync(PhysicalCountFilterDto filter)
    {
        var counts = await _countRepository.GetByDateRangeAsync(
            filter.FromDate ?? DateTime.UtcNow.AddMonths(-6),
            filter.ToDate ?? DateTime.UtcNow.AddDays(1));

        var query = counts.AsQueryable();

        if (filter.WarehouseId.HasValue)
            query = query.Where(c => c.WarehouseId == filter.WarehouseId.Value);

        if (!string.IsNullOrEmpty(filter.Status))
            query = query.Where(c => c.Status == filter.Status);

        if (!string.IsNullOrEmpty(filter.CountType))
            query = query.Where(c => c.CountType.ToString() == filter.CountType);

        if (!string.IsNullOrEmpty(filter.CountNumber))
            query = query.Where(c => c.CountNumber.Contains(filter.CountNumber, StringComparison.OrdinalIgnoreCase));

        var page = query
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.CountNumber)
            .ToList();
        var readable = await FilterReadableAsync(page);
        return readable
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(MapToDto);
    }

    #endregion

    #region Create & Manage Counts

    public async Task<PhysicalCountDto> CreateAsync(CreatePhysicalCountDto dto, Guid userId)
    {
        var countId = Guid.NewGuid();
        PhysicalCountDto? result = null;
        await InCountTransactionAsync(countId, async () => {
            result = await CreateScopedCountCoreAsync(countId, dto, userId);
            return true;
        });
        return result!;
    }

    private async Task<PhysicalCountDto> CreateScopedCountCoreAsync(Guid countId, CreatePhysicalCountDto dto, Guid userId)
    {
        EnsureActor(userId);
        var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId)
            ?? throw new ArgumentException($"Warehouse {dto.WarehouseId} not found");

        // Get TenantId from current user, fallback to warehouse's TenantId if available
        var tenantId = _currentUserService.TenantId
            ?? warehouse.TenantId;

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required to create a physical count");
        }
        if (warehouse.TenantId != tenantId || warehouse.IsDeleted || !warehouse.IsActive)
            throw new InvalidOperationException("Select an active warehouse in your organisation.");

        await EnsureAccessAsync(
            "procurement.inventory.count",
            dto.WarehouseId,
            dto.LocationId,
            "create");

        var count = new PhysicalCount
        {
            Id = countId,
            CountNumber = await GenerateCountNumberAsync(),
            WarehouseId = dto.WarehouseId,
            CountType = dto.CountType,
            CountDate = DateTime.UtcNow,
            LocationId = dto.LocationId,
            CategoryId = dto.CategoryId,
            FreezeInventory = dto.CountType == CountType.CycleCount || dto.FreezeInventory,
            BlindCount = dto.CountType == CountType.CycleCount || dto.BlindCount,
            ABCClass = dto.CountType == CountType.CycleCount
                ? NormalizeAbcClass(dto.ABCClass) ?? "C"
                : NormalizeAbcClass(dto.ABCClass),
            Notes = dto.Notes,
            Status = "Draft",
            InitiatedById = userId,
            TenantId = tenantId
        };

        await ValidateCountLocationAsync(count, count.LocationId);
        var scopeItems = await BuildScopedCountItemsAsync(count);

        await _countRepository.AddAsync(count);
        await _unitOfWork.SaveChangesAsync();

        // Snapshot the selected scope, preserving each stock location.
        await PopulateCountItemsAsync(count, scopeItems);
        await AddCountActionAsync(count, PhysicalCountActionType.Created, userId,
            $"create:{count.Id:N}", dto.Notes, null, "Manual");
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created physical count {CountNumber} for warehouse {WarehouseId}", count.CountNumber, dto.WarehouseId);
        return MapToDto(count);
    }

    public async Task<bool> StartCountAsync(Guid countId, Guid userId)
        => await InCountTransactionAsync(countId, () => StartScopedCountCoreAsync(countId, userId));

    private async Task<bool> StartScopedCountCoreAsync(Guid countId, Guid userId)
    {
        EnsureActor(userId);
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        await EnsureAccessAsync(count, "procurement.inventory.count");

        if (count.Status != "Draft")
            throw new InvalidOperationException("Count must be in Draft status to start");
        await ValidateSavedCountScopeAsync(count, userId);
        if (count.CutoffAtUtc.HasValue && DateTime.UtcNow > count.CutoffAtUtc.Value)
            throw new InvalidOperationException("The count cannot start after its governed cut-off.");

        count.Status = "InProgress";
        count.StartedDate = DateTime.UtcNow;
        count.CountedById = userId;
        if (count.FreezeInventory) count.FreezeStartedAtUtc = DateTime.UtcNow;
        await _countRepository.UpdateAsync(count);
        await AddCountActionAsync(count, PhysicalCountActionType.Started, userId,
            $"start:{count.Id:N}", "Count started and the governed freeze became effective.", null, "Counter");
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Started physical count {CountNumber}", count.CountNumber);
        return true;
    }

    public async Task<bool> RecordCountItemAsync(RecordCountItemDto dto, Guid userId)
    {
        EnsureActor(userId);
        var countId = await _countItemRepository.GetQueryable(i => i.Id == dto.PhysicalCountItemId &&
                i.TenantId == RequiredTenantId() && !i.IsDeleted)
            .AsNoTracking().Select(i => (Guid?)i.PhysicalCountId).SingleOrDefaultAsync()
            ?? throw new ArgumentException("Count line not found.");
        return await InCountTransactionAsync(countId, () => RecordCountItemCoreAsync(dto, userId));
    }

    private async Task<bool> RecordCountItemCoreAsync(RecordCountItemDto dto, Guid userId)
    {
        EnsureActor(userId);
        var item = await _countItemRepository.GetByIdAsync(dto.PhysicalCountItemId)
            ?? throw new ArgumentException($"Physical count item {dto.PhysicalCountItemId} not found");
        var count = await _countRepository.GetByIdAsync(item.PhysicalCountId)
            ?? throw new ArgumentException($"Physical count {item.PhysicalCountId} not found");
        await EnsureAccessAsync(
            "procurement.inventory.count",
            count.WarehouseId,
            item.LocationId ?? count.LocationId,
            count.CountNumber);
        var lotNumber = Normalize(dto.LotNumber, 100);
        var serialNumber = Normalize(dto.SerialNumber, 100);

        if (await ReplayCountActionAsync(count.Id, PhysicalCountActionType.CountRecorded,
                dto.IdempotencyKey, userId, "Counter", dto.Notes,
                new { Id = dto.PhysicalCountItemId, dto.CountedQuantity, LotNumber = lotNumber, SerialNumber = serialNumber }))
            return true;
        EnsureCounterCanEdit(count, userId);
        EnsureRowVersion(item.RowVersion, dto.RowVersion, "The count line changed. Reload and retry.");
        if (dto.CountedQuantity < 0 || decimal.Round(dto.CountedQuantity, 4) != dto.CountedQuantity)
            throw new InvalidOperationException("Enter a non-negative quantity with at most four decimal places.");

        item.CountedQuantity = dto.CountedQuantity;
        item.FirstCountQuantity ??= dto.CountedQuantity;
        item.VarianceQuantity = dto.CountedQuantity - item.SystemQuantity;
        item.VarianceValue = item.VarianceQuantity * item.UnitCost;
        item.IsCounted = true;
        item.CountedAt = DateTime.UtcNow;
        item.CountedById = userId;
        item.LotNumber = lotNumber;
        item.SerialNumber = serialNumber;
        item.Notes = dto.Notes;
        // Corrections are retained as separate audit actions, not independent recount attempts.
        item.CountAttempts = Math.Max(1, item.CountAttempts);

        await _countItemRepository.UpdateAsync(item);
        await AddCountActionAsync(count, PhysicalCountActionType.CountRecorded, userId,
            dto.IdempotencyKey, dto.Notes,
            new { item.Id, dto.CountedQuantity, LotNumber = lotNumber, SerialNumber = serialNumber }, "Counter");
        await _unitOfWork.SaveChangesAsync();

        // Update count summary
        await UpdateCountSummaryAsync(item.PhysicalCountId);

        return true;
    }

    public async Task<bool> RecordCountItemsAsync(IEnumerable<RecordCountItemDto> items, Guid userId)
    {
        throw new InvalidOperationException(
            "The legacy batch-count path is disabled because it cannot prove per-line concurrency and replay. Record each governed line independently.");
    }

    public Task<bool> CompleteCountAsync(Guid countId, Guid userId) =>
        throw new InvalidOperationException("Review the variance, then use Submit for approval.");

    private async Task<bool> SubmitReviewedCoreAsync(Guid countId, Guid userId, PhysicalCountMutationRequest request)
    {
        EnsureActor(userId);
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        await EnsureAccessAsync(count, "procurement.inventory.count");

        if (await ReplayCountActionAsync(count.Id, PhysicalCountActionType.Submitted, request.IdempotencyKey,
                userId, "Counter", request.Comment, new { ReviewRevision = request.RowVersion })) return true;
        EnsureCounterCanEdit(count, userId);
        EnsureRowVersion(count.RowVersion, request.RowVersion, "The count changed. Review the latest quantities before submitting.");
        var existingApprovalCycle = count.ApprovalRequired && await _unitOfWork.Repository<PhysicalCountAction>()
            .GetQueryable(action => action.TenantId == count.TenantId && action.PhysicalCountId == count.Id && !action.IsDeleted &&
                action.ActionType == PhysicalCountActionType.Submitted).AnyAsync();
        if (count.Status != "UnderReview")
            throw new InvalidOperationException("Review the variance before submitting for approval.");
        await ValidateSavedCountScopeAsync(count, userId);

        // Check if all items have been counted
        var uncounted = count.Items.Count(i => !i.IsCounted);
        if (uncounted > 0)
            throw new InvalidOperationException($"{uncounted} items have not been counted yet");
        var evidence = await ResolveSubmissionEvidenceAsync(count);

        foreach (var item in count.Items)
        {
            item.VarianceQuantity = item.CountedQuantity - item.SystemQuantity;
            item.VarianceValue = item.VarianceQuantity * item.UnitCost;
            item.RequiresRecount = false;
            await _countItemRepository.UpdateAsync(item);
        }
        count.CountedItems = count.Items.Count(i => i.IsCounted);
        count.VarianceItems = count.Items.Count(i => i.IsCounted && i.VarianceQuantity != 0);
        count.TotalSystemQuantity = count.Items.Sum(i => i.SystemQuantity);
        count.TotalCountedQuantity = count.Items.Sum(i => i.CountedQuantity);
        count.TotalVarianceQuantity = count.Items.Sum(i => i.VarianceQuantity);
        count.TotalVarianceValue = count.Items.Sum(i => i.VarianceValue);
        count.CompletedDate = DateTime.UtcNow;
        await _countRepository.UpdateAsync(count);
        await AddCountActionAsync(count,
            PhysicalCountActionType.Submitted,
            userId, request.IdempotencyKey, request.Comment,
            new
            {
                ReviewRevision = request.RowVersion,
                count.VarianceItems,
                count.TotalVarianceValue,
                Evidence = evidence.Select(value => new
                {
                    value.CentralDocumentRecordId,
                    value.CentralDocumentVersionId,
                    value.FileUploadRecordId,
                    value.DocumentReference,
                    value.VersionNumber,
                    value.EvidenceReference
                })
            }, "Counter");
        await _unitOfWork.SaveChangesAsync();
        var approvalRequired = await EnsureStockAdjustmentSubmittedAsync(count.Id, userId, request.IdempotencyKey, request.Comment);
        if (existingApprovalCycle && !approvalRequired)
            throw new InvalidOperationException("This count already entered an approval cycle. Its existing approval requirement must be completed; deactivating a workflow does not remove it.");
        if (!approvalRequired)
        {
            await AddCountActionAsync(count, PhysicalCountActionType.ApprovalNotRequired, userId,
                $"no-approval:{Hash(request.IdempotencyKey)[..24]}", InventoryOptionalApprovalPolicy.NoApprovalComment,
                new { ApprovalRequired = false, WorkflowEntityType = "StockAdjustment" }, "Counter", request.CorrelationId);
            await _unitOfWork.SaveChangesAsync();
        }
        count.ApprovalRequired = approvalRequired;
        count.Status = approvalRequired ? "PendingStoresApproval" : "ReadyToPost";
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();
        await RecordCountControlEventAsync(count, "Submit", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, request.CorrelationId, approvalRequired
                ? "Reviewed count submitted for Stores approval."
                : InventoryOptionalApprovalPolicy.NoApprovalComment);

        _logger.LogInformation("Completed physical count {CountNumber}", count.CountNumber);
        return true;
    }

    public async Task<bool> ApproveVariancesAsync(Guid countId, Guid userId)
    {
        throw new InvalidOperationException("The legacy single-approval path is disabled. Use the governed Stores, Finance, and Internal Audit stages.");
    }

    public async Task<bool> PostAdjustmentsAsync(Guid countId, Guid userId)
    {
        throw new InvalidOperationException("The legacy direct quantity overwrite path is disabled. Use the governed controlled-adjustment posting stage.");
    }

    public async Task<bool> CancelAsync(Guid countId, string reason, Guid userId)
    {
        EnsureActor(userId);
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        await EnsureAccessAsync(count, "procurement.inventory.count");

        if (count.Status is not ("Draft" or "InProgress" or "RecountRequired" or "UnderReview" or "UnderInvestigation"))
            throw new InvalidOperationException("Only a Draft, In Progress, or Recount Required count can be cancelled.");

        count.Status = "Cancelled";
        count.CancellationReason = reason;
        count.FreezeReleasedAtUtc = DateTime.UtcNow;
        count.Notes = $"{count.Notes}\nCancelled: {reason}";
        await _countRepository.UpdateAsync(count);
        await AddCountActionAsync(count, PhysicalCountActionType.Cancelled, userId,
            $"cancel:{count.Id:N}", reason, null, "Counter");
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Cancelled physical count {CountNumber}: {Reason}", count.CountNumber, reason);
        return true;
    }

    public async Task<IEnumerable<PhysicalCountItemDto>> GetItemsWithVarianceAsync(Guid countId)
    {
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");
        await EnsureAccessAsync(count, "procurement.inventory.read");
        var items = await _countItemRepository.GetItemsWithVarianceAsync(countId);
        var reveal = IsSystemQuantityVisible(count);
        return items.Select(item => MapItemToDto(item, reveal));
    }

    public async Task<PhysicalCountDto> UpdateAsync(Guid countId, UpdatePhysicalCountDto dto, Guid userId)
    {
        EnsureActor(userId);
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        await EnsureAccessAsync(count, "procurement.inventory.count");

        if (count.Status != "Draft")
            throw new InvalidOperationException("Can only update counts in Draft status");

        if (dto.Notes != null) count.Notes = dto.Notes;
        if (count.CycleCountScheduleId.HasValue && (dto.FreezeInventory == false || dto.BlindCount == false))
            throw new InvalidOperationException("Scheduled cycle counts must remain frozen and blind.");
        if (dto.FreezeInventory.HasValue) count.FreezeInventory = dto.FreezeInventory.Value;
        if (dto.BlindCount.HasValue) count.BlindCount = dto.BlindCount.Value;
        if (dto.IncludeZeroStock.HasValue) count.IncludeZeroStock = dto.IncludeZeroStock.Value;

        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated physical count {CountNumber}", count.CountNumber);
        return MapToDto(count);
    }

    public async Task<PhysicalCountItemDto> AddCountItemAsync(Guid countId, AddCountItemDto dto, Guid userId)
    {
        PhysicalCountItemDto? result = null;
        await InCountTransactionAsync(countId, async () => {
            result = await AddScopedCountItemCoreAsync(countId, dto, userId);
            return true;
        });
        return result!;
    }

    private async Task<PhysicalCountItemDto> AddScopedCountItemCoreAsync(Guid countId, AddCountItemDto dto, Guid userId)
    {
        EnsureActor(userId);
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        await EnsureAccessAsync(
            "procurement.inventory.count",
            count.WarehouseId,
            dto.LocationId ?? count.LocationId,
            count.CountNumber);

        if (count.Status != "Draft")
            throw new InvalidOperationException("Count lines can only be added before the governed count starts.");

        var locationId = dto.LocationId ?? count.LocationId
            ?? throw new InvalidOperationException("Select the item's stock location before adding it to the count.");
        if (count.LocationId.HasValue && count.LocationId.Value != locationId)
            throw new InvalidOperationException("The item must belong to the count's selected location.");
        var location = await ValidateCountLocationAsync(count, locationId);
        if (await _countItemRepository.GetQueryable(x => x.TenantId == count.TenantId &&
                x.PhysicalCountId == countId && x.InventoryItemId == dto.InventoryItemId && x.LocationId == locationId && !x.IsDeleted).AnyAsync())
            throw new InvalidOperationException("This item and location are already included in the count.");
        var scopeItems = await BuildScopedCountItemsAsync(count, dto.InventoryItemId, locationId);
        var countItem = scopeItems.SingleOrDefault()
            ?? throw new InvalidOperationException("The selected item is not assigned to this location within the count scope.");
        // Caller-supplied system quantities are never authoritative.
        countItem.LotNumber = dto.LotNumber;
        countItem.SerialNumber = dto.SerialNumber;

        await _countItemRepository.AddAsync(countItem);
        count.TotalItems++;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Added item {ItemCode} to physical count {CountNumber}", countItem.ItemCode, count.CountNumber);
        var result = MapItemToDto(countItem, IsSystemQuantityVisible(count));
        result.LocationName = location!.LocationCode;
        return result;
    }

    public async Task<bool> RemoveCountItemAsync(Guid countItemId, Guid userId)
    {
        EnsureActor(userId);
        var item = await _countItemRepository.GetByIdAsync(countItemId)
            ?? throw new ArgumentException($"Physical count item {countItemId} not found");

        var count = await _countRepository.GetByIdAsync(item.PhysicalCountId)
            ?? throw new ArgumentException($"Physical count not found");

        await EnsureAccessAsync(
            "procurement.inventory.count",
            count.WarehouseId,
            item.LocationId ?? count.LocationId,
            count.CountNumber);

        if (count.Status != "Draft")
            throw new InvalidOperationException("Cannot remove items from a started count");

        await _countItemRepository.DeleteAsync(item);
        count.TotalItems--;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Removed item from physical count {CountNumber}", count.CountNumber);
        return true;
    }

    public async Task<bool> RejectVariancesAsync(Guid countId, string reason, Guid userId)
    {
        throw new InvalidOperationException(
            "The legacy rejection path is disabled. Reject at the governed Stores, Finance, or Internal Audit stage with row-version and idempotency evidence.");
    }

    public async Task<PhysicalCountExportDto> ExportCountSheetAsync(Guid countId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        await EnsureAccessAsync(count, "procurement.inventory.read");

        var revealSystemQuantity = IsSystemQuantityVisible(count);
        var resolvedLocations = ReadResolvedCountLocations(count);
        return new PhysicalCountExportDto
        {
            Id = count.Id,
            CountNumber = count.CountNumber,
            WarehouseName = count.Warehouse?.Name ?? string.Empty,
            CountType = count.CountType.ToString(),
            Status = count.Status,
            CountDateFormatted = count.CountDate.ToString("yyyy-MM-dd"),
            Items = count.Items.Select(i => new PhysicalCountItemExportDto
            {
                ItemCode = i.ItemCode ?? string.Empty,
                ItemName = i.ItemName ?? string.Empty,
                UnitOfMeasure = i.UnitOfMeasure ?? string.Empty,
                LocationName = i.Location?.LocationCode ?? resolvedLocations.GetValueOrDefault(i.Id)?.LocationCode,
                SystemQuantity = revealSystemQuantity ? i.SystemQuantity : 0,
                CountedQuantity = revealSystemQuantity ? i.CountedQuantity : 0,
                VarianceQuantity = revealSystemQuantity ? i.VarianceQuantity : 0,
                VarianceValue = revealSystemQuantity ? i.VarianceValue : 0,
                LotNumber = i.LotNumber,
                SerialNumber = i.SerialNumber,
                IsCounted = i.IsCounted,
                Notes = i.Notes
            }).ToList()
        };
    }

    public async Task<ImportCountResultDto> ImportCountSheetAsync(Guid countId, IEnumerable<ImportCountItemDto> items, Guid userId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        await EnsureAccessAsync(count, "procurement.inventory.count");

        if (count.Status != "InProgress")
            throw new InvalidOperationException("Count quantities can only be imported while the governed count is In Progress.");

        if (count.Items.Where(x => !x.IsDeleted).GroupBy(x => x.ItemCode, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new InvalidOperationException("An item occurs in multiple locations. Use the current count-sheet Upload so each item and location is matched unambiguously.");

        var result = new ImportCountResultDto();
        var itemsList = items.ToList();
        result.TotalRows = itemsList.Count;

        foreach (var importItem in itemsList)
        {
            var countItem = count.Items.FirstOrDefault(i =>
                i.ItemCode?.Equals(importItem.ItemCode, StringComparison.OrdinalIgnoreCase) == true);

            if (countItem == null)
            {
                result.ErrorCount++;
                result.Errors.Add($"Item code '{importItem.ItemCode}' not found in count sheet");
                continue;
            }

            countItem.CountedQuantity = importItem.CountedQuantity;
            countItem.FirstCountQuantity = importItem.CountedQuantity;
            countItem.VarianceQuantity = importItem.CountedQuantity - countItem.SystemQuantity;
            countItem.VarianceValue = countItem.VarianceQuantity * countItem.UnitCost;
            countItem.IsCounted = true;
            countItem.CountedAt = DateTime.UtcNow;
            countItem.CountedById = userId;
            countItem.LotNumber = importItem.LotNumber;
            countItem.SerialNumber = importItem.SerialNumber;
            countItem.Notes = importItem.Notes;
            countItem.CountAttempts++;

            await _countItemRepository.UpdateAsync(countItem);
            result.SuccessCount++;
        }

        await _unitOfWork.SaveChangesAsync();
        await UpdateCountSummaryAsync(countId);

        _logger.LogInformation("Imported {SuccessCount} items for physical count {CountNumber}", result.SuccessCount, count.CountNumber);
        return result;
    }

    public async Task<VarianceReportDto> GetVarianceReportAsync(Guid countId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        await EnsureAccessAsync(count, "procurement.inventory.read");

        if (!IsSystemQuantityVisible(count))
            throw new InvalidOperationException("Save all counted quantities and select Review variance before opening the variance report.");

        var varianceItems = count.Items.Where(i => i.IsCounted && i.VarianceQuantity != 0).ToList();
        var resolvedLocations = ReadResolvedCountLocations(count);

        return new VarianceReportDto
        {
            CountNumber = count.CountNumber,
            WarehouseName = count.Warehouse?.Name ?? string.Empty,
            CountType = count.CountType.ToString(),
            CountDateFormatted = count.CountDate.ToString("yyyy-MM-dd"),
            TotalItems = count.TotalItems,
            ItemsWithVariance = varianceItems.Count,
            TotalSystemValue = count.Items.Sum(i => i.SystemQuantity * i.UnitCost),
            TotalCountedValue = count.Items.Where(i => i.IsCounted).Sum(i => i.CountedQuantity * i.UnitCost),
            TotalVarianceValue = varianceItems.Sum(i => i.VarianceValue),
            VariancePercentage = count.TotalSystemQuantity != 0
                ? (count.TotalVarianceQuantity / count.TotalSystemQuantity) * 100
                : 0,
            Items = varianceItems.Select(i => new VarianceItemDto
            {
                ItemCode = i.ItemCode ?? string.Empty,
                ItemName = i.ItemName ?? string.Empty,
                LocationName = i.Location?.LocationCode ?? resolvedLocations.GetValueOrDefault(i.Id)?.LocationCode,
                SystemQuantity = i.SystemQuantity,
                CountedQuantity = i.CountedQuantity,
                VarianceQuantity = i.VarianceQuantity,
                UnitCost = i.UnitCost,
                VarianceValue = i.VarianceValue,
                VariancePercent = i.SystemQuantity != 0
                    ? (i.VarianceQuantity / i.SystemQuantity) * 100
                    : 0,
                Notes = i.Notes
            }).OrderByDescending(i => Math.Abs(i.VarianceValue)).ToList()
        };
    }

    #endregion

    #region Private Helpers

    private async Task<IReadOnlyList<PhysicalCount>> FilterReadableAsync(IEnumerable<PhysicalCount> counts)
    {
        var readable = new List<PhysicalCount>();
        foreach (var count in counts)
        {
            if (await CanAccessAsync(count, "procurement.inventory.read"))
                readable.Add(count);
        }

        return readable;
    }

    private Task EnsureAccessAsync(
        PhysicalCount count,
        string permission) => EnsureAccessAsync(
            permission,
            count.WarehouseId,
            count.LocationId,
            count.CountNumber);

    private async Task EnsureAccessAsync(
        string permission,
        Guid warehouseId,
        Guid? locationId,
        string sourceReference)
    {
        var decision = await _accessControl.EnforceCapabilityAsync(
            BuildAccessRequest(permission, warehouseId, locationId, sourceReference),
            Guid.NewGuid().ToString("N"));
        if (!decision.Allowed)
            throw new ProcurementAccessAuthorizationException(decision.Message);
    }

    private async Task<bool> CanAccessAsync(PhysicalCount count, string permission)
    {
        try
        {
            var decision = await _accessControl.CheckCapabilityAsync(
                BuildAccessRequest(permission, count.WarehouseId, count.LocationId, count.CountNumber),
                Guid.NewGuid().ToString("N"));
            return decision.Allowed;
        }
        catch (ProcurementAccessAuthorizationException)
        {
            return false;
        }
        catch (ProcurementAccessValidationException)
        {
            return false;
        }
    }

    private static ProcurementAccessCapabilityRequest BuildAccessRequest(
        string permission,
        Guid warehouseId,
        Guid? locationId,
        string sourceReference) => new()
        {
            PermissionCode = permission,
            WarehouseId = warehouseId,
            LocationId = locationId,
            RequireLocationScope = true,
            SourceType = "PhysicalCount",
            SourceReference = sourceReference
        };

    private async Task<string> GenerateCountNumberAsync()
    {
        var date = DateTime.UtcNow;
        var prefix = $"PC-{date:yyyyMMdd}";
        var counts = await _countRepository.GetByDateRangeAsync(date.Date, date.Date.AddDays(1));
        var sequence = counts.Count() + 1;
        return $"{prefix}-{sequence:D4}";
    }

    private async Task PopulateCountItemsAsync(PhysicalCount count, IReadOnlyCollection<PhysicalCountItem> rows)
    {
        foreach (var countItem in rows)
            await _countItemRepository.AddAsync(countItem);
        count.TotalItems = rows.Count;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task UpdateCountSummaryAsync(Guid countId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId);
        if (count == null) return;

        count.CountedItems = count.Items.Count(i => i.IsCounted);
        count.VarianceItems = count.Items.Count(i => i.IsCounted && i.VarianceQuantity != 0);
        count.TotalSystemQuantity = count.Items.Sum(i => i.SystemQuantity);
        count.TotalCountedQuantity = count.Items.Where(i => i.IsCounted).Sum(i => i.CountedQuantity);
        count.TotalVarianceQuantity = count.Items.Where(i => i.IsCounted).Sum(i => i.VarianceQuantity);
        count.TotalVarianceValue = count.Items.Where(i => i.IsCounted).Sum(i => i.VarianceValue);

        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();
    }

    #endregion

    #region Mapping

    private static PhysicalCountDto MapToDto(PhysicalCount count)
    {
        var revealSystemQuantity = IsSystemQuantityVisible(count);
        return new PhysicalCountDto
        {
            Id = count.Id,
            CountNumber = count.CountNumber,
            WarehouseId = count.WarehouseId,
            WarehouseName = count.Warehouse?.Name ?? string.Empty,
            CountType = count.CountType,
            Status = count.Status,
            ApprovalRequired = count.ApprovalRequired,
            CountDate = count.CountDate,
            CreatedAt = count.CreatedAt,
            StartedDate = count.StartedDate,
            CompletedDate = count.CompletedDate,
            LocationId = count.LocationId,
            LocationName = count.Location?.LocationCode,
            CategoryId = count.CategoryId,
            FreezeInventory = count.FreezeInventory,
            BlindCount = count.BlindCount,
            SystemQuantityVisible = revealSystemQuantity,
            ABCClass = count.ABCClass,
            ScheduledForUtc = count.ScheduledForUtc,
            CutoffAtUtc = count.CutoffAtUtc,
            FreezeStartedAtUtc = count.FreezeStartedAtUtc,
            FreezeReleasedAtUtc = count.FreezeReleasedAtUtc,
            StockAdjustmentId = count.StockAdjustmentId,
            RowVersion = Convert.ToBase64String(count.RowVersion),
            TotalItems = count.TotalItems,
            CountedItems = count.CountedItems,
            ItemsWithVariance = revealSystemQuantity ? count.VarianceItems : 0,
            TotalVarianceValue = revealSystemQuantity ? count.TotalVarianceValue : 0m,
            InitiatedByName = count.InitiatedBy?.FullName,
            Notes = count.Notes,
            CreatedAtFormatted = count.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private static PhysicalCountDetailDto MapToDetailDto(PhysicalCount count)
    {
        var revealSystemQuantity = IsSystemQuantityVisible(count);
        var resolvedLocations = ReadResolvedCountLocations(count);
        return new PhysicalCountDetailDto
        {
            ApprovalRequired = count.ApprovalRequired,
            Id = count.Id,
            CountNumber = count.CountNumber,
            WarehouseId = count.WarehouseId,
            WarehouseName = count.Warehouse?.Name ?? string.Empty,
            CountType = count.CountType,
            Status = count.Status,
            CountDate = count.CountDate,
            CreatedAt = count.CreatedAt,
            StartedDate = count.StartedDate,
            CompletedDate = count.CompletedDate,
            LocationId = count.LocationId,
            LocationName = count.Location?.LocationCode,
            CategoryId = count.CategoryId,
            FreezeInventory = count.FreezeInventory,
            BlindCount = count.BlindCount,
            SystemQuantityVisible = revealSystemQuantity,
            ABCClass = count.ABCClass,
            ScheduledForUtc = count.ScheduledForUtc,
            CutoffAtUtc = count.CutoffAtUtc,
            FreezeStartedAtUtc = count.FreezeStartedAtUtc,
            FreezeReleasedAtUtc = count.FreezeReleasedAtUtc,
            StockAdjustmentId = count.StockAdjustmentId,
            RowVersion = Convert.ToBase64String(count.RowVersion),
            TotalItems = count.TotalItems,
            CountedItems = count.CountedItems,
            ItemsWithVariance = revealSystemQuantity ? count.VarianceItems : 0,
            TotalVarianceValue = revealSystemQuantity ? count.TotalVarianceValue : 0m,
            InitiatedByName = count.InitiatedBy?.FullName,
            Notes = count.Notes,
            CreatedAtFormatted = count.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            ApprovedByName = count.ApprovedBy?.FullName,
            ApprovedDate = count.ApprovedDate,
            StoresApprovedById = count.StoresApprovedById,
            StoresApprovedAtUtc = count.StoresApprovedAtUtc,
            FinanceApprovedById = count.FinanceApprovedById,
            FinanceApprovedAtUtc = count.FinanceApprovedAtUtc,
            AuditAttestedById = count.AuditAttestedById,
            AuditAttestedAtUtc = count.AuditAttestedAtUtc,
            InvestigationSummary = count.InvestigationSummary,
            Evidence = count.Actions
                .Where(item => item.ActionType is PhysicalCountActionType.Submitted or PhysicalCountActionType.RecountRequired)
                .OrderByDescending(item => item.Sequence)
                .SelectMany(item => ReadEvidence(item.SnapshotJson))
                .GroupBy(item => item.CentralDocumentVersionId)
                .Select(group => group.First())
                .Select(item => new PhysicalCountEvidenceDto
                {
                    CentralDocumentRecordId = item.CentralDocumentRecordId,
                    CentralDocumentVersionId = item.CentralDocumentVersionId,
                    FileUploadRecordId = item.FileUploadRecordId,
                    DocumentReference = item.DocumentReference,
                    VersionNumber = item.VersionNumber,
                    EvidenceReference = item.EvidenceReference
                }).ToList(),
            Items = count.Items.Select(item => MapItemToDto(item, revealSystemQuantity, resolvedLocations.GetValueOrDefault(item.Id))).ToList(),
            Actions = count.Actions.OrderBy(item => item.Sequence).Select(item => new PhysicalCountActionDto
            {
                Id = item.Id,
                Sequence = item.Sequence,
                ActionType = item.ActionType.ToString(),
                ActorUserId = item.ActorUserId,
                ActorRole = item.ActorRole,
                OccurredAtUtc = item.OccurredAtUtc,
                Comment = item.Comment,
                IntegrityHash = item.IntegrityHash
            }).ToList()
        };
    }

    private static PhysicalCountItemDto MapItemToDto(PhysicalCountItem item, bool revealSystemQuantity = true, ResolvedCountLocation? resolved = null)
    {
        return new PhysicalCountItemDto
        {
            Id = item.Id,
            InventoryItemId = item.InventoryItemId,
            ItemCode = item.ItemCode ?? string.Empty,
            ItemName = item.ItemName ?? string.Empty,
            LocationId = item.LocationId ?? resolved?.LocationId,
            LocationName = item.Location?.LocationCode ?? resolved?.LocationCode,
            SystemQuantity = revealSystemQuantity ? item.SystemQuantity : 0,
            CountedQuantity = item.CountedQuantity,
            VarianceQuantity = revealSystemQuantity ? item.VarianceQuantity : 0,
            VarianceValue = revealSystemQuantity ? item.VarianceValue : 0,
            CountUnitCost = revealSystemQuantity ? item.UnitCost : null,
            ItemAverageCost = revealSystemQuantity && item.InventoryItem != null &&
                item.InventoryItem.Id == item.InventoryItemId && item.InventoryItem.TenantId == item.TenantId &&
                !item.InventoryItem.IsDeleted ? item.InventoryItem.AverageCost : null,
            VariancePercent = revealSystemQuantity && item.SystemQuantity != 0
                ? (item.VarianceQuantity / item.SystemQuantity) * 100 
                : 0,
            UnitOfMeasure = item.UnitOfMeasure ?? string.Empty,
            LotNumber = item.LotNumber,
            SerialNumber = item.SerialNumber,
            IsCounted = item.IsCounted,
            CountedAt = item.CountedAt,
            CountedByName = item.CountedBy?.FullName,
            CountAttempts = item.CountAttempts,
            RequiresRecount = item.RequiresRecount,
            FirstCountQuantity = revealSystemQuantity ? item.FirstCountQuantity : null,
            RecountedQuantity = revealSystemQuantity ? item.RecountedQuantity : null,
            RecountedAtUtc = item.RecountedAtUtc,
            RecountedById = item.RecountedById,
            InvestigationNotes = revealSystemQuantity ? item.InvestigationNotes : null,
            RowVersion = Convert.ToBase64String(item.RowVersion),
            Notes = item.Notes
        };
    }

    private static bool IsSystemQuantityVisible(PhysicalCount count) =>
        !count.BlindCount || count.Status is not ("Draft" or "InProgress" or "RecountRequired");

    #endregion
}

