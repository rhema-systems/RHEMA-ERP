using System.Data;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public sealed partial class PurchaseReturnService
{
    public static void ApplySubmission(PurchaseReturn value, WorkflowIntegrationResult result)
    {
        if (!result.ExecutionResult.Success || value.Status != "Draft")
            throw Conflict("INV_SUPPLIER_RETURN_WORKFLOW_SUBMIT", "Only a draft with a successful workflow decision can be submitted.");
        if (result.ApprovalRequired && (result.Outcome != WorkflowOutcome.Pending || !result.ExecutionResult.WorkflowInstanceId.HasValue))
            throw Conflict("INV_SUPPLIER_RETURN_WORKFLOW_SUBMIT", "The active workflow must begin with an independent pending approval step.");
        if (!result.ApprovalRequired && (result.Outcome != WorkflowOutcome.Approved || result.ExecutionResult.WorkflowInstanceId.HasValue ||
            value.ApprovedById.HasValue || value.ApprovedDate.HasValue))
            throw Conflict("INV_SUPPLIER_RETURN_WORKFLOW_SUBMIT", "The direct-completion decision cannot replace retained approval history.");
        value.ApprovalRequired = result.ApprovalRequired;
        value.Status = result.ApprovalRequired ? "Submitted" : "ReadyToDispatch";
    }

    public static bool CanDispatchState(PurchaseReturn value) => value.ApprovalRequired
        ? value.Status == "Approved" && value.ApprovedById.HasValue && value.ApprovedDate.HasValue
        : value.Status == "ReadyToDispatch" && !value.ApprovedById.HasValue && !value.ApprovedDate.HasValue;

    private async Task<T> InTransactionAsync<T>(Func<Task<T>> action)
    {
        if (_unitOfWork.HasActiveTransaction) return await action();
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var result = await action();
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return result;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    private async Task RequireLocationAsync(Guid warehouseId, Guid? locationId)
    {
        if (!locationId.HasValue || locationId == Guid.Empty)
            throw Validation("INV_SUPPLIER_RETURN_LOCATION_REQUIRED", "The accepted GRN line must identify its source bin before a supplier return can be dispatched.");
        var location = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(value =>
            value.Id == locationId && value.TenantId == _currentUser.TenantId && !value.IsDeleted && value.IsActive &&
            (value.WarehouseId == warehouseId || value.IsConsignmentBin && value.ConsignmentWarehouseId == warehouseId))
            .AsNoTracking().SingleOrDefaultAsync();
        if (location is null)
            throw Validation("INV_SUPPLIER_RETURN_LOCATION_INVALID", "The source bin must be active and belong to this warehouse and tenant.");
    }

    private async Task ValidateSourceAsync(PurchaseReturn value)
    {
        if (!value.GoodsReceiptNoteId.HasValue || value.Items.Count == 0 || value.Items.Any(line => line.IsDeleted || line.ReturnQuantity <= 0))
            throw Validation("INV_SUPPLIER_RETURN_SOURCE_INVALID", "The return requires accepted GRN lines and positive quantities.");
        await _unitOfWork.AcquireTransactionLockAsync($"supplier-return-source:{value.TenantId:N}:{value.GoodsReceiptNoteId:N}");
        var grn = await LoadAcceptedSourceAsync(value.GoodsReceiptNoteId.Value);
        if (grn is null || grn.IsDeleted || grn.TenantId != value.TenantId || grn.Status != GRNStatus.StockUpdated || !grn.StockUpdated ||
            grn.WarehouseId != value.WarehouseId || grn.SupplierId != value.SupplierId)
            throw Conflict("INV_SUPPLIER_RETURN_SOURCE_INVALID", "The accepted, stock-updated GRN no longer matches this return.");
        var quantities = await PriorReturnQuantitiesAsync(grn.Id);
        foreach (var line in value.Items)
        {
            var source = grn.Items.SingleOrDefault(source => source.Id == line.GoodsReceiptNoteItemId && !source.IsDeleted && source.TenantId == value.TenantId);
            if (source is null || source.InventoryItemId != line.InventoryItemId || source.StorageLocationId != line.LocationId ||
                line.TenantId != value.TenantId || quantities.GetValueOrDefault(source.Id) > source.AcceptedQuantity)
                throw Conflict("INV_SUPPLIER_RETURN_SOURCE_INVALID", "Return item, location or total reserved quantity does not match the accepted GRN.");
            await RequireLocationAsync(value.WarehouseId, line.LocationId);
        }
    }

    private async Task<bool> CanAsync(string permission, Guid warehouseId, Guid? locationId = null)
        => (await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission, WarehouseId = warehouseId, LocationId = locationId,
            RequireLocationScope = locationId.HasValue, SourceType = WorkflowEntityType, SourceReference = "supplier-returns"
        }, "supplier-return:action-availability")).Allowed;

    private async Task<IEnumerable<PurchaseReturnDto>> MapAccessibleAsync(IEnumerable<PurchaseReturn> values)
    {
        var results = new List<PurchaseReturnDto>();
        foreach (var value in values.Where(value => !value.IsDeleted && value.TenantId == _currentUser.TenantId))
            if (await CanAsync("procurement.inventory.read", value.WarehouseId))
                results.Add(await AddActionsAsync(value, Map(value)));
        return results;
    }

    private async Task<T> AddActionsAsync<T>(PurchaseReturn value, T dto) where T : PurchaseReturnDto
    {
        dto.ApprovalRequired = value.Status == "Draft"
            ? await _workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType) || await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, value.Id)
            : value.ApprovalRequired;
        var canIssue = await CanAsync("procurement.inventory.issue", value.WarehouseId);
        var enforceSod = await new ProcurementSodPolicy(_unitOfWork).IsRequiredForSourceAsync(value.TenantId, WorkflowEntityType, value.Id);
        dto.CanSubmit = value.Status == "Draft" && canIssue;
        dto.CanCancel = (value.Status is "Draft" or "Submitted" or "ReadyToDispatch") && canIssue;
        dto.CanDispatch = CanDispatchState(value) && canIssue && (!enforceSod || !value.ApprovalRequired || value.ApprovedById != _currentUser.UserId);
        dto.CanApprove = value.Status == "Submitted" && value.ApprovalRequired && (!enforceSod || value.RequestedById != _currentUser.UserId) &&
            await CanAsync("procurement.inventory.adjust.approve", value.WarehouseId) &&
            await _workflow.CanUserApproveAsync(WorkflowEntityType, value.Id, _currentUser.UserId);
        if (_financeHandoff is not null && value.Status is "Shipped" or "Acknowledged")
        {
            var credits = await _unitOfWork.Repository<ErpSystem.Core.Entities.Finance.SupplierDebitNote>().GetQueryable(note =>
                note.TenantId == _currentUser.TenantId && !note.IsDeleted && note.InventoryPurchaseReturnId == value.Id)
                .AsNoTracking().Select(note => new
                {
                    note.Id,
                    note.DebitNoteNumber,
                    note.InventorySupplierReturnAccountingGroupId,
                    Completed = note.Status == ErpSystem.Core.Entities.Finance.SupplierDebitNoteStatus.Posted &&
                        note.JournalEntryId.HasValue && note.PostingEventId.HasValue &&
                        note.ReturnDispatchJournalEntryId.HasValue && note.ReturnDispatchPostingEventId.HasValue &&
                        note.OriginalVendorInvoiceId.HasValue && note.DirectInvoiceAppliedAt.HasValue &&
                        note.DirectInvoiceAppliedAmount > 0 && note.DirectInvoiceAppliedAmount == note.TotalAmount
                }).ToListAsync();
            dto.SupplierDebitNoteId = credits.Count == 1 ? credits[0].Id : null;
            dto.FinanceResolutionCompleted = credits.Count == 1 && credits[0].Completed;
            if (value.AccountingAllocationVersion == 1)
            {
                var groups = await _unitOfWork.Repository<ErpSystem.Core.Entities.Finance.InventorySupplierReturnAccountingGroup>()
                    .GetQueryable(group => group.TenantId == value.TenantId && !group.IsDeleted && group.InventoryPurchaseReturnId == value.Id)
                    .AsNoTracking().ToListAsync();
                var allocations = await _unitOfWork.Repository<ErpSystem.Core.Entities.Finance.InventorySupplierReturnAllocation>()
                    .GetQueryable(allocation => allocation.TenantId == value.TenantId && !allocation.IsDeleted && allocation.InventoryPurchaseReturnId == value.Id)
                    .AsNoTracking().ToListAsync();
                var invoiceIds = groups.Where(group => group.OriginalVendorInvoiceId.HasValue).Select(group => group.OriginalVendorInvoiceId!.Value).ToArray();
                var invoiceNames = await _unitOfWork.Repository<ErpSystem.Core.Entities.Finance.VendorInvoice>()
                    .GetQueryable(invoice => invoice.TenantId == value.TenantId && !invoice.IsDeleted && invoiceIds.Contains(invoice.Id))
                    .AsNoTracking().ToDictionaryAsync(invoice => invoice.Id, invoice => invoice.InvoiceNumber);
                foreach (var group in groups)
                {
                    var credit = credits.SingleOrDefault(note => note.InventorySupplierReturnAccountingGroupId == group.Id);
                    dto.AccountingGroups.Add(new PurchaseReturnAccountingGroupDto { Id = group.Id,
                        OriginalVendorInvoiceId = group.OriginalVendorInvoiceId,
                        OriginalInvoiceNumber = group.OriginalVendorInvoiceId.HasValue ? invoiceNames.GetValueOrDefault(group.OriginalVendorInvoiceId.Value) : null,
                        BaseQuantity = allocations.Where(allocation => allocation.AccountingGroupId == group.Id).Sum(allocation => allocation.BaseQuantity),
                        CarryingAmount = group.CarryingAmount, OriginalAccrualAmount = group.OriginalAccrualAmount,
                        FunctionalCurrency = group.FunctionalCurrency, DispatchJournalEntryId = group.DispatchJournalEntryId,
                        SupplierDebitNoteId = credit?.Id, SupplierDebitNoteNumber = credit?.DebitNoteNumber,
                        FinanceResolutionCompleted = group.DispatchPostingEventId.HasValue && group.DispatchJournalEntryId.HasValue &&
                            (!group.OriginalVendorInvoiceId.HasValue || credit?.Completed == true) });
                }
                dto.FinanceResolutionCompleted = dto.AccountingGroups.Count > 0 && dto.AccountingGroups.All(group => group.FinanceResolutionCompleted);
            }
        }
        return dto;
    }
}
