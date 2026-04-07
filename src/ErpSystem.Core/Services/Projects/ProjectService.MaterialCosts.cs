using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    private const string MaterialIssueExpensePrefix = "Material issue from requisition";
    private const string MaterialReturnExpensePrefix = "Material return from requisition";

    public async Task<IEnumerable<ProjectMaterialCostEntryDto>> GetMaterialCostEntriesAsync(Guid projectId)
    {
        await GetProjectForOperationAsync(projectId, ProjectAccessOperation.View);
        await SyncProjectMaterialCostsAsync(new[] { projectId });
        var entries = await GetMaterialCostEntryEntitiesAsync(new[] { projectId });
        return entries
            .OrderByDescending(x => x.EntryDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<IEnumerable<ProjectMaterialCostEntryDto>> GetMaterialCostLedgerReportAsync(Guid? projectId = null, int take = 300, string? sourceDocumentType = null, string? postingState = null, bool? isReversed = null, bool? exceptionsOnly = null)
    {
        List<Guid> accessibleProjectIds;
        if (projectId.HasValue)
        {
            await GetProjectForOperationAsync(projectId.Value, ProjectAccessOperation.View);
            accessibleProjectIds = new List<Guid> { projectId.Value };
        }
        else
        {
            accessibleProjectIds = (await GetAccessibleProjectsAsync(take: Math.Max(take * 4, 400)))
                .Select(x => x.Id)
                .Distinct()
                .ToList();
        }

        if (accessibleProjectIds.Count == 0)
        {
            return Array.Empty<ProjectMaterialCostEntryDto>();
        }

        await SyncProjectMaterialCostsAsync(accessibleProjectIds);

        var entries = await GetMaterialCostEntryEntitiesAsync(accessibleProjectIds);
        return entries
            .Where(x => string.IsNullOrWhiteSpace(sourceDocumentType) || string.Equals(x.SourceDocumentType, sourceDocumentType, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(postingState) || string.Equals(x.PostingState, postingState, StringComparison.OrdinalIgnoreCase))
            .Where(x => !isReversed.HasValue || x.IsReversed == isReversed.Value)
            .Where(x => !exceptionsOnly.HasValue || !exceptionsOnly.Value || x.HasMissingSourceLink || x.HasReversalGap)
            .OrderByDescending(x => x.EntryDate)
            .ThenByDescending(x => Math.Abs(x.Amount))
            .Take(Math.Max(1, take))
            .Select(MapToDto)
            .ToList();
    }

    public Task SyncProjectMaterialCostAsync(Guid projectId)
        => SyncProjectMaterialCostsAsync(new[] { projectId });

    public async Task SyncInventoryRequisitionMaterialCostAsync(Guid requisitionId)
    {
        var requisition = await _unitOfWork.Repository<InventoryRequisition>()
            .FirstOrDefaultAsync(x => x.Id == requisitionId && x.TenantId == _currentUserProvider.TenantId);

        if (requisition?.ProjectId.HasValue == true)
        {
            await SyncProjectMaterialCostsAsync(new[] { requisition.ProjectId.Value });
        }
    }

    public async Task SyncPurchaseReceiptMaterialCostAsync(Guid receiptId)
    {
        var receipt = await _unitOfWork.Repository<PurchaseOrderReceipt>()
            .FirstOrDefaultAsync(x => x.Id == receiptId && x.TenantId == _currentUserProvider.TenantId);
        if (receipt == null)
        {
            return;
        }

        var order = await _unitOfWork.Repository<PurchaseOrder>()
            .FirstOrDefaultAsync(x => x.Id == receipt.PurchaseOrderId && x.TenantId == _currentUserProvider.TenantId);
        if (order?.SourceRequisitionId == null)
        {
            return;
        }

        var purchaseRequisition = await _unitOfWork.Repository<PurchaseRequisition>()
            .FirstOrDefaultAsync(x => x.Id == order.SourceRequisitionId.Value && x.TenantId == _currentUserProvider.TenantId);
        if (purchaseRequisition?.ProjectId.HasValue == true)
        {
            await SyncProjectMaterialCostsAsync(new[] { purchaseRequisition.ProjectId.Value });
        }
    }

    public async Task SyncPurchaseReturnMaterialCostAsync(Guid purchaseReturnId)
    {
        var purchaseReturn = await _unitOfWork.Repository<PurchaseReturn>()
            .FirstOrDefaultAsync(x => x.Id == purchaseReturnId && x.TenantId == _currentUserProvider.TenantId);
        if (purchaseReturn?.PurchaseOrderId == null)
        {
            return;
        }

        var order = await _unitOfWork.Repository<PurchaseOrder>()
            .FirstOrDefaultAsync(x => x.Id == purchaseReturn.PurchaseOrderId.Value && x.TenantId == _currentUserProvider.TenantId);
        if (order?.SourceRequisitionId == null)
        {
            return;
        }

        var purchaseRequisition = await _unitOfWork.Repository<PurchaseRequisition>()
            .FirstOrDefaultAsync(x => x.Id == order.SourceRequisitionId.Value && x.TenantId == _currentUserProvider.TenantId);
        if (purchaseRequisition?.ProjectId.HasValue == true)
        {
            await SyncProjectMaterialCostsAsync(new[] { purchaseRequisition.ProjectId.Value });
        }
    }

    private async Task SyncProjectMaterialCostsAsync(IEnumerable<Guid> projectIds)
    {
        var distinctProjectIds = projectIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
        if (distinctProjectIds.Count == 0)
        {
            return;
        }

        var projectRepo = _unitOfWork.Repository<Project>();
        var projects = (await projectRepo.FindAsync(x => x.TenantId == _currentUserProvider.TenantId && distinctProjectIds.Contains(x.Id))).ToList();
        if (projects.Count == 0)
        {
            return;
        }

        var baseCurrencyCode = await GetProjectBaseCurrencyCodeAsync();

        var requisitionRepo = _unitOfWork.Repository<InventoryRequisition>();
        var requisitions = (await requisitionRepo.FindAsync(
                x => x.TenantId == _currentUserProvider.TenantId && x.ProjectId.HasValue && distinctProjectIds.Contains(x.ProjectId.Value),
                x => x.Items))
            .ToList();
        var requisitionsByProjectId = requisitions
            .Where(x => x.ProjectId.HasValue)
            .GroupBy(x => x.ProjectId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var requisitionIds = requisitions.Select(x => x.Id).ToHashSet();

        var stockMovements = requisitionIds.Count == 0
            ? new List<StockMovement>()
            : (await _unitOfWork.Repository<StockMovement>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.ReferenceType == ReferenceType.Requisition
                    && x.ReferenceId.HasValue
                    && requisitionIds.Contains(x.ReferenceId.Value)))
                .ToList();
        var requisitionIdsWithStockMovements = stockMovements
            .Where(x => x.ReferenceId.HasValue)
            .Select(x => x.ReferenceId!.Value)
            .ToHashSet();

        var purchaseRequisitions = (await _unitOfWork.Repository<PurchaseRequisition>().FindAsync(
                x => x.TenantId == _currentUserProvider.TenantId
                    && x.ProjectId.HasValue
                    && distinctProjectIds.Contains(x.ProjectId.Value)))
            .ToList();
        var purchaseRequisitionIds = purchaseRequisitions.Select(x => x.Id).ToHashSet();

        var purchaseOrders = purchaseRequisitionIds.Count == 0
            ? new List<PurchaseOrder>()
            : (await _unitOfWork.Repository<PurchaseOrder>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.SourceRequisitionId.HasValue
                    && purchaseRequisitionIds.Contains(x.SourceRequisitionId.Value)))
                .ToList();
        var purchaseOrdersById = purchaseOrders.ToDictionary(x => x.Id, x => x);
        var purchaseOrderIds = purchaseOrders.Select(x => x.Id).ToHashSet();
        var purchaseRequisitionsById = purchaseRequisitions.ToDictionary(x => x.Id, x => x);

        var purchaseOrderItems = purchaseOrderIds.Count == 0
            ? new List<PurchaseOrderItem>()
            : (await _unitOfWork.Repository<PurchaseOrderItem>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseOrderIds.Contains(x.PurchaseOrderId)))
                .ToList();
        var purchaseOrderItemsById = purchaseOrderItems.ToDictionary(x => x.Id, x => x);

        var purchaseReceipts = purchaseOrderIds.Count == 0
            ? new List<PurchaseOrderReceipt>()
            : (await _unitOfWork.Repository<PurchaseOrderReceipt>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseOrderIds.Contains(x.PurchaseOrderId)))
                .ToList();
        var purchaseReceiptsById = purchaseReceipts.ToDictionary(x => x.Id, x => x);
        var purchaseReceiptIds = purchaseReceipts.Select(x => x.Id).ToHashSet();

        var purchaseReceiptItems = purchaseReceiptIds.Count == 0
            ? new List<PurchaseOrderReceiptItem>()
            : (await _unitOfWork.Repository<PurchaseOrderReceiptItem>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseReceiptIds.Contains(x.ReceiptId)))
                .ToList();

        var purchaseReturns = purchaseOrderIds.Count == 0
            ? new List<PurchaseReturn>()
            : (await _unitOfWork.Repository<PurchaseReturn>().FindAsync(
                    x => x.TenantId == _currentUserProvider.TenantId
                        && x.PurchaseOrderId.HasValue
                        && purchaseOrderIds.Contains(x.PurchaseOrderId.Value),
                    x => x.Items))
                .ToList();

        var inventoryItemIds = stockMovements
            .Select(x => x.InventoryItemId)
            .Concat(requisitions.SelectMany(x => x.Items).Select(x => x.InventoryItemId))
            .Concat(purchaseOrderItems.Where(x => x.InventoryItemId.HasValue).Select(x => x.InventoryItemId!.Value))
            .Concat(purchaseReturns.SelectMany(x => x.Items).Select(x => x.InventoryItemId))
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToHashSet();
        var inventoryItems = inventoryItemIds.Count == 0
            ? new Dictionary<Guid, InventoryItem>()
            : (await _unitOfWork.Repository<InventoryItem>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && inventoryItemIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x);

        var ledgerRepo = _unitOfWork.Repository<ProjectMaterialCostEntry>();
        var existingEntries = (await ledgerRepo.FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && distinctProjectIds.Contains(x.ProjectId)))
            .ToList();
        if (existingEntries.Count > 0)
        {
            await ledgerRepo.HardDeleteRangeAsync(existingEntries);
        }

        var newEntries = new List<ProjectMaterialCostEntry>();

        foreach (var stockMovement in stockMovements.OrderBy(x => x.MovementDate).ThenBy(x => x.CreatedAt))
        {
            if (!stockMovement.ReferenceId.HasValue)
            {
                continue;
            }

            var requisition = requisitions.FirstOrDefault(x => x.Id == stockMovement.ReferenceId.Value);
            if (requisition?.ProjectId == null)
            {
                continue;
            }

            inventoryItems.TryGetValue(stockMovement.InventoryItemId, out var inventoryItem);
            var isIssue = string.Equals(stockMovement.MovementType, "Issue", StringComparison.OrdinalIgnoreCase);
            var quantity = isIssue ? Math.Abs(stockMovement.Quantity) : -Math.Abs(stockMovement.Quantity);
            var amount = isIssue ? Math.Abs(stockMovement.TotalValue) : -Math.Abs(stockMovement.TotalValue);

            newEntries.Add(new ProjectMaterialCostEntry
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectId = requisition.ProjectId.Value,
                EntryDate = stockMovement.MovementDate,
                EntryType = isIssue ? "InventoryIssue" : "InventoryReturn",
                PostingState = "Posted",
                AffectsActualCost = true,
                IsReversed = !isIssue,
                SourceDocumentType = "InventoryRequisition",
                SourceDocumentId = requisition.Id,
                SourceDocumentNumber = requisition.RequisitionNumber,
                SourceTransactionType = "StockMovement",
                SourceTransactionId = stockMovement.Id,
                InventoryItemId = stockMovement.InventoryItemId,
                InventoryItemCode = inventoryItem?.ItemCode,
                InventoryItemName = inventoryItem?.Name,
                Quantity = quantity,
                UnitOfMeasure = inventoryItem?.UnitOfMeasure,
                UnitCost = stockMovement.UnitCost,
                Amount = decimal.Round(amount, 2),
                Currency = baseCurrencyCode,
                HasMissingSourceLink = inventoryItem == null,
                HasReversalGap = false,
                Notes = stockMovement.Notes,
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            });
        }

        foreach (var requisition in requisitions
                     .Where(x =>
                         x.ProjectId.HasValue
                         && !requisitionIdsWithStockMovements.Contains(x.Id)
                         && (x.Status == RequisitionStatus.PartiallyIssued
                             || x.Status == RequisitionStatus.Issued
                             || x.Status == RequisitionStatus.Completed)))
        {
            foreach (var item in requisition.Items.Where(x => x.IssuedQuantity > 0m))
            {
                inventoryItems.TryGetValue(item.InventoryItemId, out var inventoryItem);
                var unitCost = item.UnitCost > 0m
                    ? item.UnitCost
                    : item.IssuedQuantity > 0m && item.LineValue > 0m
                        ? decimal.Round(item.LineValue / item.IssuedQuantity, 2)
                        : 0m;
                var amount = decimal.Round(unitCost * item.IssuedQuantity, 2);
                if (amount <= 0m && item.LineValue > 0m)
                {
                    amount = decimal.Round(item.LineValue, 2);
                }

                newEntries.Add(new ProjectMaterialCostEntry
                {
                    TenantId = _currentUserProvider.TenantId,
                    ProjectId = requisition.ProjectId!.Value,
                    EntryDate = requisition.IssuedDate ?? requisition.CompletedDate ?? requisition.RequiredDate ?? requisition.RequestDate,
                    EntryType = "InventoryIssue",
                    PostingState = "Posted",
                    AffectsActualCost = true,
                    IsReversed = false,
                    SourceDocumentType = "InventoryRequisition",
                    SourceDocumentId = requisition.Id,
                    SourceDocumentNumber = requisition.RequisitionNumber,
                    SourceTransactionType = "InventoryRequisitionItem",
                    SourceTransactionId = item.Id,
                    InventoryItemId = item.InventoryItemId,
                    InventoryItemCode = inventoryItem?.ItemCode ?? item.ItemCode,
                    InventoryItemName = inventoryItem?.Name ?? item.ItemName,
                    Quantity = item.IssuedQuantity,
                    UnitOfMeasure = item.UnitOfMeasure ?? inventoryItem?.UnitOfMeasure,
                    UnitCost = unitCost,
                    Amount = amount,
                    Currency = baseCurrencyCode,
                    HasMissingSourceLink = false,
                    HasReversalGap = false,
                    Notes = item.Notes ?? requisition.Notes,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                });
            }
        }

        foreach (var receiptItem in purchaseReceiptItems)
        {
            if (!purchaseReceiptsById.TryGetValue(receiptItem.ReceiptId, out var receipt) ||
                !purchaseOrderItemsById.TryGetValue(receiptItem.PurchaseOrderItemId, out var orderItem) ||
                !purchaseOrdersById.TryGetValue(orderItem.PurchaseOrderId, out var order) ||
                !order.SourceRequisitionId.HasValue ||
                !purchaseRequisitionsById.TryGetValue(order.SourceRequisitionId.Value, out var purchaseRequisition) ||
                !purchaseRequisition.ProjectId.HasValue)
            {
                continue;
            }

            var projectId = purchaseRequisition.ProjectId.Value;
            inventoryItems.TryGetValue(orderItem.InventoryItemId ?? Guid.Empty, out var inventoryItem);
            var acceptedQuantity = ResolveAcceptedReceiptQuantity(receipt, receiptItem);
            if (acceptedQuantity > 0m)
            {
                var unitCost = ResolvePurchaseOrderItemUnitCost(orderItem);
                newEntries.Add(new ProjectMaterialCostEntry
                {
                    TenantId = _currentUserProvider.TenantId,
                    ProjectId = projectId,
                    EntryDate = receipt.ReceiptDate,
                    EntryType = "PurchaseReceiptAccepted",
                    PostingState = "Posted",
                    AffectsActualCost = false,
                    IsReversed = false,
                    SourceDocumentType = "PurchaseReceipt",
                    SourceDocumentId = receipt.Id,
                    SourceDocumentNumber = receipt.ReceiptNumber,
                    SourceTransactionType = "PurchaseReceiptAccepted",
                    SourceTransactionId = receiptItem.Id,
                    InventoryItemId = orderItem.InventoryItemId,
                    InventoryItemCode = inventoryItem?.ItemCode ?? orderItem.BusinessPartnerItemCode,
                    InventoryItemName = inventoryItem?.Name ?? orderItem.ItemDescription,
                    Quantity = acceptedQuantity,
                    UnitOfMeasure = receiptItem.UnitOfMeasure ?? orderItem.UnitOfMeasure ?? inventoryItem?.UnitOfMeasure,
                    UnitCost = unitCost,
                    Amount = decimal.Round(unitCost * acceptedQuantity, 2),
                    Currency = order.Currency,
                    HasMissingSourceLink = orderItem.InventoryItemId.HasValue && inventoryItem == null,
                    HasReversalGap = false,
                    Notes = receiptItem.Notes ?? receipt.Notes,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                });
            }

            var pendingInspectionQuantity = ResolvePendingInspectionQuantity(receipt, receiptItem);
            if (pendingInspectionQuantity > 0m)
            {
                var unitCost = ResolvePurchaseOrderItemUnitCost(orderItem);
                newEntries.Add(new ProjectMaterialCostEntry
                {
                    TenantId = _currentUserProvider.TenantId,
                    ProjectId = projectId,
                    EntryDate = receipt.ReceiptDate,
                    EntryType = "PurchaseReceiptPendingInspection",
                    PostingState = "PendingInspection",
                    AffectsActualCost = false,
                    IsReversed = false,
                    SourceDocumentType = "PurchaseReceipt",
                    SourceDocumentId = receipt.Id,
                    SourceDocumentNumber = receipt.ReceiptNumber,
                    SourceTransactionType = "PurchaseReceiptPendingInspection",
                    SourceTransactionId = receiptItem.Id,
                    InventoryItemId = orderItem.InventoryItemId,
                    InventoryItemCode = inventoryItem?.ItemCode ?? orderItem.BusinessPartnerItemCode,
                    InventoryItemName = inventoryItem?.Name ?? orderItem.ItemDescription,
                    Quantity = pendingInspectionQuantity,
                    UnitOfMeasure = receiptItem.UnitOfMeasure ?? orderItem.UnitOfMeasure ?? inventoryItem?.UnitOfMeasure,
                    UnitCost = unitCost,
                    Amount = decimal.Round(unitCost * pendingInspectionQuantity, 2),
                    Currency = order.Currency,
                    HasMissingSourceLink = orderItem.InventoryItemId.HasValue && inventoryItem == null,
                    HasReversalGap = false,
                    Notes = receiptItem.Notes ?? receipt.Notes,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                });
            }
        }

        foreach (var purchaseReturn in purchaseReturns)
        {
            if (!purchaseReturn.PurchaseOrderId.HasValue ||
                !purchaseOrdersById.TryGetValue(purchaseReturn.PurchaseOrderId.Value, out var order) ||
                !order.SourceRequisitionId.HasValue ||
                !purchaseRequisitionsById.TryGetValue(order.SourceRequisitionId.Value, out var purchaseRequisition) ||
                !purchaseRequisition.ProjectId.HasValue)
            {
                continue;
            }

            var projectId = purchaseRequisition.ProjectId.Value;
            foreach (var returnItem in purchaseReturn.Items)
            {
                inventoryItems.TryGetValue(returnItem.InventoryItemId, out var inventoryItem);
                var amount = returnItem.LineValue != 0m
                    ? Math.Abs(returnItem.LineValue)
                    : decimal.Round(Math.Abs(returnItem.ReturnQuantity * returnItem.UnitCost), 2);

                newEntries.Add(new ProjectMaterialCostEntry
                {
                    TenantId = _currentUserProvider.TenantId,
                    ProjectId = projectId,
                    EntryDate = purchaseReturn.ReturnDate,
                    EntryType = "PurchaseReturn",
                    PostingState = returnItem.StockReversed ? "Posted" : "PendingReversal",
                    AffectsActualCost = false,
                    IsReversed = true,
                    SourceDocumentType = "PurchaseReturn",
                    SourceDocumentId = purchaseReturn.Id,
                    SourceDocumentNumber = purchaseReturn.ReturnNumber,
                    SourceTransactionType = "PurchaseReturnItem",
                    SourceTransactionId = returnItem.Id,
                    InventoryItemId = returnItem.InventoryItemId,
                    InventoryItemCode = returnItem.ItemCode ?? inventoryItem?.ItemCode,
                    InventoryItemName = returnItem.ItemName ?? inventoryItem?.Name,
                    Quantity = -Math.Abs(returnItem.ReturnQuantity),
                    UnitOfMeasure = returnItem.UnitOfMeasure ?? inventoryItem?.UnitOfMeasure,
                    UnitCost = returnItem.UnitCost,
                    Amount = -amount,
                    Currency = order.Currency,
                    HasMissingSourceLink = inventoryItem == null,
                    HasReversalGap = !returnItem.StockReversed,
                    Notes = returnItem.Notes ?? purchaseReturn.Notes,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                });
            }
        }

        if (newEntries.Count > 0)
        {
            await ledgerRepo.AddRangeAsync(newEntries);
        }

        await RecalculateProjectActualCostsAsync(projects, distinctProjectIds, newEntries);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<List<ProjectMaterialCostEntry>> GetMaterialCostEntryEntitiesAsync(IEnumerable<Guid> projectIds)
    {
        var distinctProjectIds = projectIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
        if (distinctProjectIds.Count == 0)
        {
            return new List<ProjectMaterialCostEntry>();
        }

        return (await _unitOfWork.Repository<ProjectMaterialCostEntry>().FindAsync(
                x => x.TenantId == _currentUserProvider.TenantId && distinctProjectIds.Contains(x.ProjectId),
                x => x.Project))
            .ToList();
    }

    private async Task RecalculateProjectActualCostsAsync(IEnumerable<Project> projects, IEnumerable<Guid> projectIds, IEnumerable<ProjectMaterialCostEntry> ledgerEntries)
    {
        var distinctProjectIds = projectIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
        if (distinctProjectIds.Count == 0)
        {
            return;
        }

        var timesheetEntries = (await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && distinctProjectIds.Contains(x.ProjectId)))
            .ToList();
        var approvedTimesheets = timesheetEntries
            .Where(x => HasApprovedEntryStatus(x.Status))
            .ToList();
        var expenses = (await _unitOfWork.Repository<ProjectExpense>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && distinctProjectIds.Contains(x.ProjectId)))
            .ToList();
        var approvedExpenses = expenses
            .Where(x => HasApprovedEntryStatus(x.Status))
            .ToList();

        var approvedTimesheetCostByProject = approvedTimesheets
            .GroupBy(x => x.ProjectId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.CostAmount));
        var approvedExpenseCostByProject = approvedExpenses
            .Where(x => !IsLegacyAutoMaterialExpense(x))
            .GroupBy(x => x.ProjectId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount + y.TaxAmount));
        var materialLedgerCostByProject = ledgerEntries
            .Where(x => x.AffectsActualCost)
            .GroupBy(x => x.ProjectId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var projectsWithTrackedActuals = approvedTimesheets
            .Select(x => x.ProjectId)
            .Concat(approvedExpenses.Where(x => !IsLegacyAutoMaterialExpense(x)).Select(x => x.ProjectId))
            .Concat(ledgerEntries.Where(x => x.AffectsActualCost).Select(x => x.ProjectId))
            .Distinct()
            .ToHashSet();

        foreach (var project in projects)
        {
            if (!projectsWithTrackedActuals.Contains(project.Id))
            {
                continue;
            }

            var recalculatedCost =
                approvedTimesheetCostByProject.GetValueOrDefault(project.Id)
                + approvedExpenseCostByProject.GetValueOrDefault(project.Id)
                + materialLedgerCostByProject.GetValueOrDefault(project.Id);
            recalculatedCost = decimal.Round(recalculatedCost, 2);

            if ((project.ActualCost ?? 0m) == recalculatedCost)
            {
                continue;
            }

            project.ActualCost = recalculatedCost;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = _currentUserProvider.Username;
            project.LastModifiedById = _currentUserProvider.UserId;
            await _projectRepository.UpdateAsync(project);
        }
    }

    private static bool IsLegacyAutoMaterialExpense(ProjectExpense expense)
    {
        if (!string.Equals(expense.Category, "Materials", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(expense.Notes))
        {
            return false;
        }

        return expense.Notes.StartsWith(MaterialIssueExpensePrefix, StringComparison.OrdinalIgnoreCase)
            || expense.Notes.StartsWith(MaterialReturnExpensePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectMaterialCostEntryDto MapToDto(ProjectMaterialCostEntry entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectCode = entity.Project?.ProjectCode ?? string.Empty,
        ProjectTitle = entity.Project?.Title ?? string.Empty,
        EntryDate = entity.EntryDate,
        EntryType = entity.EntryType,
        PostingState = entity.PostingState,
        AffectsActualCost = entity.AffectsActualCost,
        IsReversed = entity.IsReversed,
        SourceDocumentType = entity.SourceDocumentType,
        SourceDocumentId = entity.SourceDocumentId,
        SourceDocumentNumber = entity.SourceDocumentNumber,
        SourceTransactionType = entity.SourceTransactionType,
        SourceTransactionId = entity.SourceTransactionId,
        InventoryItemId = entity.InventoryItemId,
        InventoryItemCode = entity.InventoryItemCode,
        InventoryItemName = entity.InventoryItemName,
        Quantity = entity.Quantity,
        UnitOfMeasure = entity.UnitOfMeasure,
        UnitCost = entity.UnitCost,
        Amount = entity.Amount,
        Currency = entity.Currency,
        HasMissingSourceLink = entity.HasMissingSourceLink,
        HasReversalGap = entity.HasReversalGap,
        Notes = entity.Notes
    };
}
