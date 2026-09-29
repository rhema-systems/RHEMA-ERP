using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Finance.Integration;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public sealed partial class SupplierDebitNoteService
{
    private async Task ValidateAllocatedDispatchAsync(PurchaseReturn source,
        IReadOnlyList<InventorySupplierReturnAccountingGroup> groups, CancellationToken ct)
    {
        if (source.AccountingAllocationVersion != 1 || groups.Count == 0 || groups.Any(x => x.TenantId != TenantId || x.InventoryPurchaseReturnId != source.Id || x.IsDeleted ||
            !x.DispatchPostingEventId.HasValue || !x.DispatchJournalEntryId.HasValue) ||
            groups.Select(x => x.DispatchPostingEventId).Distinct().Count() != 1 || groups.Select(x => x.DispatchJournalEntryId).Distinct().Count() != 1)
            throw new InvalidOperationException("RTV_ALLOCATED_DISPATCH_INVALID: accounting groups do not share retained dispatch evidence.");
        var first = groups[0];
        if (!await _db.Set<FinancePostingEvent>().AsNoTracking().AnyAsync(x => x.Id == first.DispatchPostingEventId && x.TenantId == TenantId &&
            !x.IsDeleted && x.PostingStatus == "Posted" && x.JournalEntryId == first.DispatchJournalEntryId &&
            x.SourceDocumentType == "SupplierReturnDispatch" && x.SourceDocumentId == source.Id, ct) ||
            !await _db.JournalEntries.AsNoTracking().AnyAsync(x => x.Id == first.DispatchJournalEntryId && x.TenantId == TenantId && !x.IsDeleted &&
                !x.IsReversed && x.PostingStatus == "Posted" && x.SourceDocumentType == "SupplierReturnDispatch" && x.SourceDocumentId == source.Id, ct))
            throw new InvalidOperationException("RTV_ALLOCATED_DISPATCH_INVALID: original dispatch posting is missing or reversed.");
        var allocations = await _db.Set<InventorySupplierReturnAllocation>().AsNoTracking().Where(x =>
            x.TenantId == TenantId && !x.IsDeleted && x.InventoryPurchaseReturnId == source.Id).ToListAsync(ct);
        var carrying = await RequireDispatchCarryingByLineAsync(source, ct);
        if (source.Items.Any(line => allocations.Where(x => x.InventoryPurchaseReturnItemId == line.Id).Sum(x => x.BaseQuantity) != line.ReturnQuantity ||
            allocations.Where(x => x.InventoryPurchaseReturnItemId == line.Id).Sum(x => x.CarryingAmount) != carrying[line.Id]) ||
            groups.Any(group => allocations.Where(x => x.AccountingGroupId == group.Id).Sum(x => x.CarryingAmount) != group.CarryingAmount ||
                allocations.Where(x => x.AccountingGroupId == group.Id).Sum(x => x.OriginalAccrualAmount) != group.OriginalAccrualAmount) ||
            allocations.Any(x => !groups.Any(group => group.Id == x.AccountingGroupId && group.OriginalVendorInvoiceId == x.OriginalVendorInvoiceId)))
            throw new InvalidOperationException("RTV_ALLOCATION_CHANGED: receipt and invoice claims no longer reconcile to the dispatched return.");
    }

    private async Task<IReadOnlyList<InventoryReturnCreditSourceDto>?> ReadAllocatedReturnCreditSourcesAsync(PurchaseReturn source, CancellationToken ct)
    {
        var groups = await _db.Set<InventorySupplierReturnAccountingGroup>().AsNoTracking().Where(x =>
            x.TenantId == TenantId && !x.IsDeleted && x.InventoryPurchaseReturnId == source.Id).ToListAsync(ct);
        if (groups.Count == 0) return null;
        await ValidateAllocatedDispatchAsync(source, groups, ct);
        var result = new List<InventoryReturnCreditSourceDto>();
        foreach (var group in groups.Where(x => x.OriginalVendorInvoiceId.HasValue))
        {
            if (await _db.SupplierDebitNotes.AsNoTracking().AnyAsync(x => x.TenantId == TenantId && !x.IsDeleted &&
                x.InventorySupplierReturnAccountingGroupId == group.Id, ct)) continue;
            var invoice = await _db.Set<VendorInvoice>().AsNoTracking().SingleAsync(x =>
                x.Id == group.OriginalVendorInvoiceId && x.TenantId == TenantId && !x.IsDeleted, ct);
            await RequirePostedOriginalInvoiceAsync(invoice, ct);
            result.Add(new InventoryReturnCreditSourceDto { InvoiceId = invoice.Id, InvoiceNumber = invoice.InvoiceNumber,
                SupplierInvoiceNumber = invoice.SupplierInvoiceNumber, CurrencyCode = invoice.CurrencyCode,
                OutstandingAmount = Round(invoice.TotalAmount - invoice.PaidAmount), AccountingGroupId = group.Id,
                ReturnBaseQuantity = await _db.Set<InventorySupplierReturnAllocation>().Where(x => x.TenantId == TenantId && !x.IsDeleted &&
                    x.AccountingGroupId == group.Id).SumAsync(x => x.BaseQuantity, ct) });
        }
        return result;
    }

    private async Task<SupplierDebitNoteDto> CreateAllocatedReturnCreditAsync(Guid returnId, CreateInventoryReturnCreditDto dto,
        FinancePostingProducerContext producer, CancellationToken ct)
    {
        var source = await RequireDispatchedReturnAsync(returnId, ct);
        var groups = await _db.Set<InventorySupplierReturnAccountingGroup>().AsNoTracking().Where(x =>
            x.TenantId == TenantId && !x.IsDeleted && x.InventoryPurchaseReturnId == returnId).ToListAsync(ct);
        await ValidateAllocatedDispatchAsync(source, groups, ct);
        var group = groups.SingleOrDefault(x => x.OriginalVendorInvoiceId == dto.OriginalVendorInvoiceId)
            ?? throw new InvalidOperationException("RTV_INVOICE_GROUP_REQUIRED: select an original invoice retained on this return.");
        var existing = await _db.SupplierDebitNotes.SingleOrDefaultAsync(x => x.TenantId == TenantId && !x.IsDeleted &&
            x.InventorySupplierReturnAccountingGroupId == group.Id, ct);
        if (existing != null)
        {
            if (existing.SupplierCreditNoteReference != TrimToNull(dto.SupplierCreditNoteReference))
                throw new InvalidOperationException("RTV_CREDIT_ALREADY_LINKED: this original invoice group already has a different credit reference.");
            return await GetRequiredAsync(existing.Id, producer, ct);
        }
        var invoice = await ValidateInvoiceAsync(dto.OriginalVendorInvoiceId, source.SupplierId, ct)
            ?? throw new InvalidOperationException("RTV_ORIGINAL_INVOICE_REQUIRED");
        await RequirePostedOriginalInvoiceAsync(invoice, ct);
        var claims = await _db.Set<InventorySupplierReturnAllocation>().AsNoTracking().Where(x =>
            x.TenantId == TenantId && !x.IsDeleted && x.AccountingGroupId == group.Id).ToListAsync(ct);
        var date = dto.CreditDate == default ? DateTime.UtcNow.Date : dto.CreditDate.Date;
        if (date < source.ShippedDate!.Value.Date || date < invoice.InvoiceDate.Date)
            throw new InvalidOperationException("RTV_CREDIT_DATE_INVALID: credit date cannot precede dispatch or original invoice.");
        var creditLines = new List<CreateSupplierDebitNoteLineItemDto>();
        foreach (var lineClaims in claims.GroupBy(x => x.OriginalVendorInvoiceLineItemId))
        {
            var invoiceLine = invoice.LineItems.SingleOrDefault(x => x.Id == lineClaims.Key && x.TenantId == TenantId && !x.IsDeleted)
                ?? throw new InvalidOperationException("RTV_ORIGINAL_INVOICE_LINE_REQUIRED");
            creditLines.Add(new CreateSupplierDebitNoteLineItemDto { OriginalVendorInvoiceLineItemId = invoiceLine.Id,
                Quantity = lineClaims.Sum(x => x.PurchaseQuantity), UnitPrice = invoiceLine.UnitPrice,
                Description = invoiceLine.Description, LineItemType = invoiceLine.LineItemType });
        }
        var created = await CreateCoreAsync(new CreateSupplierDebitNoteDto { VendorId = source.SupplierId, OriginalVendorInvoiceId = invoice.Id,
            BusinessPartnerRoleId = invoice.BusinessPartnerRoleId, SupplierCreditNoteReference = RequiredText(dto.SupplierCreditNoteReference, "Supplier credit reference"),
            DebitNoteDate = date, Reason = TrimToNull(dto.Reason) ?? source.ReturnReason, CurrencyCode = invoice.CurrencyCode, ExchangeRate = invoice.ExchangeRate,
            FinanceDimensions = dto.FinanceDimensions, Notes = $"Original invoice allocation of Inventory return {source.ReturnNumber}.", Lines = creditLines }, producer, ct,
            inventoryReturnId: source.Id, inventoryAccountingGroupId: group.Id);
        var note = await GetTrackedAsync(created.Id, ct);
        note.InventoryPurchaseReturnId = source.Id;
        note.InventorySupplierReturnAccountingGroupId = group.Id;
        foreach (var line in note.LineItems.Where(x => !x.IsDeleted))
        {
            var returnLines = claims.Where(x => x.OriginalVendorInvoiceLineItemId == line.OriginalVendorInvoiceLineItemId)
                .Select(x => x.InventoryPurchaseReturnItemId).Distinct().ToArray();
            line.InventoryPurchaseReturnItemId = returnLines.Length == 1 ? returnLines[0] : null;
        }
        await ValidateDirectInvoiceCapacityAsync(note, invoice, ct);
        await _db.SaveChangesAsync(ct);
        await RecordAuditAsync("Finance.InventoryReturn.AllocatedCreditDraftCreated", note,
            after: new { returnId, AccountingGroupId = group.Id, invoice.Id, note.TotalAmount }, cancellationToken: ct);
        return await GetRequiredAsync(note.Id, producer, ct);
    }

    private async Task PrepareAllocatedReturnCreditPostingAsync(SupplierDebitNote note, FinancePostingRequestV2Dto request, CancellationToken ct)
    {
        var source = await RequireDispatchedReturnAsync(note.InventoryPurchaseReturnId!.Value, ct);
        var groups = await _db.Set<InventorySupplierReturnAccountingGroup>().AsNoTracking().Where(x =>
            x.TenantId == TenantId && !x.IsDeleted && x.InventoryPurchaseReturnId == source.Id).ToListAsync(ct);
        await ValidateAllocatedDispatchAsync(source, groups, ct);
        var group = groups.SingleOrDefault(x => x.Id == note.InventorySupplierReturnAccountingGroupId && x.OriginalVendorInvoiceId == note.OriginalVendorInvoiceId)
            ?? throw new InvalidOperationException("RTV_CREDIT_GROUP_INVALID");
        if (!group.ClearingAccountId.HasValue) throw new InvalidOperationException("RTV_CLEARING_ACCOUNT_REQUIRED");
        var invoice = note.OriginalVendorInvoice ?? throw new InvalidOperationException("RTV_ORIGINAL_INVOICE_REQUIRED");
        await RequirePostedOriginalInvoiceAsync(invoice, ct);
        var originalBook = await _db.JournalEntries.AsNoTracking().Where(x => x.Id == invoice.JournalEntryId && x.TenantId == TenantId)
            .Select(x => x.AccountingBookId).SingleAsync(ct);
        if (!await _db.JournalEntries.AsNoTracking().AnyAsync(x => x.Id == group.DispatchJournalEntryId &&
            x.TenantId == TenantId && x.AccountingBookId == originalBook, ct))
            throw new InvalidOperationException("RTV_ORIGINAL_BOOK_MISMATCH: credit and retained return clearing must use the same original accounting book.");
        var claims = await _db.Set<InventorySupplierReturnAllocation>().AsNoTracking().Where(x =>
            x.TenantId == TenantId && !x.IsDeleted && x.AccountingGroupId == group.Id).ToListAsync(ct);
        var quantities = claims.GroupBy(x => x.OriginalVendorInvoiceLineItemId!.Value).ToDictionary(x => x.Key, x => x.Sum(a => a.PurchaseQuantity));
        var lines = note.LineItems.Where(x => !x.IsDeleted).ToArray();
        if (note.VendorId != source.SupplierId || lines.Length != quantities.Count || lines.Any(x =>
            !x.OriginalVendorInvoiceLineItemId.HasValue || !quantities.TryGetValue(x.OriginalVendorInvoiceLineItemId.Value, out var quantity) || quantity != x.Quantity))
            throw new InvalidOperationException("RTV_CREDIT_LINEAGE_CHANGED: credit quantities no longer match the retained original invoice group.");
        await ValidateDirectInvoiceCapacityAsync(note, invoice, ct);
        var settings = await _db.FinanceSettings.AsNoTracking().SingleAsync(x => x.TenantId == TenantId && !x.IsDeleted, ct);
        var variance = settings.PurchaseReturnVarianceAccountId ?? throw new InvalidOperationException("RTV_VARIANCE_ACCOUNT_REQUIRED");
        var inventoryAccounts = await _db.AccountTransactions.AsNoTracking().Where(x => x.TenantId == TenantId && !x.IsDeleted &&
            x.JournalEntryId == group.DispatchJournalEntryId && x.TransactionTag == "RTV-Dispatch-Inventory").Select(x => x.AccountId).ToListAsync(ct);
        RequireReturnVarianceAccount(variance, group.ClearingAccountId.Value, inventoryAccounts.ToHashSet());
        await RequirePostingAccountAsync(variance, note.DebitNoteDate, "Purchase-return variance", false, ct);
        await RequirePostingAccountAsync(group.ClearingAccountId.Value, note.DebitNoteDate, "Retained return clearing", true, ct);
        var carrying = lines.ToDictionary(x => x.Id, x => claims.Where(a => a.OriginalVendorInvoiceLineItemId == x.OriginalVendorInvoiceLineItemId).Sum(a => a.CarryingAmount));
        request.Lines = BuildReturnClearingLines(request.Lines, note, new InventorySupplierReturnPosting {
            InventoryPurchaseReturnId = source.Id, OriginalVendorInvoiceId = invoice.Id, ClearingAccountId = group.ClearingAccountId.Value,
            CarryingAmount = group.CarryingAmount, PostingEventId = group.DispatchPostingEventId!.Value,
            JournalEntryId = group.DispatchJournalEntryId!.Value }, variance, request.FunctionalCurrencyCode, carrying);
        note.ReturnDispatchPostingEventId = group.DispatchPostingEventId;
        note.ReturnDispatchJournalEntryId = group.DispatchJournalEntryId;
    }
}
