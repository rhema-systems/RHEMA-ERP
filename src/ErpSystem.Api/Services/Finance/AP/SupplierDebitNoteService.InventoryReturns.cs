using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public sealed partial class SupplierDebitNoteService
{
    public async Task<IReadOnlyList<InventoryReturnCreditSourceDto>> GetInventoryReturnCreditSourcesAsync(
        Guid returnId, CancellationToken cancellationToken = default)
    {
        var source = await RequireDispatchedReturnAsync(returnId, cancellationToken);
        var identity = await _db.Set<ApSupplierIdentityLink>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == TenantId && !x.IsDeleted && x.BusinessPartnerId == source.SupplierId, cancellationToken);
        if (identity == null) return Array.Empty<InventoryReturnCreditSourceDto>();
        var invoices = await _db.Set<VendorInvoice>().AsNoTracking().Include(x => x.LineItems)
            .Where(x => x.TenantId == TenantId && !x.IsDeleted && x.SupplierId == identity.SupplierId &&
                x.PurchaseOrderId == source.PurchaseOrderId && x.JournalEntryId.HasValue && x.Status != VendorInvoiceStatus.Voided &&
                _db.JournalEntries.Any(j => j.Id == x.JournalEntryId && j.TenantId == TenantId && !j.IsDeleted && !j.IsReversed &&
                    j.PostingStatus == "Posted" && j.SourceDocumentType == "VendorInvoice" && j.SourceDocumentId == x.Id))
            .OrderByDescending(x => x.InvoiceDate).ToListAsync(cancellationToken);
        if (invoices.Count == 0) return Array.Empty<InventoryReturnCreditSourceDto>();
        var invoiceQuantities = await ReturnInvoiceQuantitiesAsync(source, cancellationToken);
        var candidates = new List<InventoryReturnCreditSourceDto>();
        foreach (var invoice in invoices)
        {
            if (!HasExactReturnInvoiceLines(source, invoice, invoiceQuantities)) continue;
            candidates.Add(new InventoryReturnCreditSourceDto
            {
                InvoiceId = invoice.Id, InvoiceNumber = invoice.InvoiceNumber,
                SupplierInvoiceNumber = invoice.SupplierInvoiceNumber, CurrencyCode = invoice.CurrencyCode,
                OutstandingAmount = Round(invoice.TotalAmount - invoice.PaidAmount)
            });
        }
        return candidates;
    }

    public Task<SupplierDebitNoteDto> CreateInventoryReturnCreditAsync(Guid returnId,
        CreateInventoryReturnCreditDto dto, FinancePostingProducerContext producer, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"supplier-return:{TenantId:N}:{returnId:N}", cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync(ApSettlementLockKeys.Invoice(TenantId, dto.OriginalVendorInvoiceId), cancellationToken);
                var existing = await _db.SupplierDebitNotes.Where(x => x.TenantId == TenantId && !x.IsDeleted &&
                    x.InventoryPurchaseReturnId == returnId).SingleOrDefaultAsync(cancellationToken);
                if (existing != null)
                {
                    if (existing.OriginalVendorInvoiceId != dto.OriginalVendorInvoiceId ||
                        existing.SupplierCreditNoteReference != TrimToNull(dto.SupplierCreditNoteReference))
                        throw new InvalidOperationException("RTV_CREDIT_ALREADY_LINKED: this return already has a different supplier credit. Open its existing Finance document.");
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return await GetRequiredAsync(existing.Id, producer, cancellationToken);
                }
                var source = await RequireDispatchedReturnAsync(returnId, cancellationToken);
                var identity = await _supplierIdentity.ResolveByBusinessPartnerAsync(source.SupplierId, cancellationToken);
                var invoice = await ValidateInvoiceAsync(dto.OriginalVendorInvoiceId, identity.SupplierId, cancellationToken)
                    ?? throw new InvalidOperationException("RTV_ORIGINAL_INVOICE_REQUIRED: select the posted supplier invoice.");
                await RequirePostedOriginalInvoiceAsync(invoice, cancellationToken);
                var invoiceQuantities = await ReturnInvoiceQuantitiesAsync(source, cancellationToken);
                RequireExactReturnInvoiceLines(source, invoice, invoiceQuantities);
                var date = dto.CreditDate == default ? DateTime.UtcNow.Date : dto.CreditDate.Date;
                if (date < source.ShippedDate!.Value.Date || date < invoice.InvoiceDate.Date)
                    throw new InvalidOperationException("RTV_CREDIT_DATE_INVALID: credit date cannot precede dispatch or the original invoice.");
                var mapping = MatchReturnInvoiceLines(source, invoice, invoiceQuantities);
                var created = await CreateCoreAsync(new CreateSupplierDebitNoteDto
                {
                    VendorId = source.SupplierId, OriginalVendorInvoiceId = invoice.Id,
                    SupplierCreditNoteReference = RequiredText(dto.SupplierCreditNoteReference, "Supplier credit reference"),
                    DebitNoteDate = date, Reason = TrimToNull(dto.Reason) ?? source.ReturnReason,
                    CurrencyCode = invoice.CurrencyCode, ExchangeRate = invoice.ExchangeRate,
                    FinanceDimensions = dto.FinanceDimensions,
                    Notes = $"Credit against original invoice for dispatched Inventory return {source.ReturnNumber}.",
                    Lines = mapping.Select(pair => new CreateSupplierDebitNoteLineItemDto
                    {
                        OriginalVendorInvoiceLineItemId = pair.InvoiceLine.Id, Quantity = pair.InvoiceQuantity,
                        UnitPrice = pair.InvoiceLine.UnitPrice, Description = pair.InvoiceLine.Description,
                        LineItemType = pair.InvoiceLine.LineItemType
                    }).ToList()
                }, producer, cancellationToken);
                var note = await GetTrackedAsync(created.Id, cancellationToken);
                note.InventoryPurchaseReturnId = returnId;
                foreach (var line in note.LineItems.Where(x => !x.IsDeleted))
                    line.InventoryPurchaseReturnItemId = mapping.Single(x => x.InvoiceLine.Id == line.OriginalVendorInvoiceLineItemId).ReturnLine.Id;
                await ValidateDirectInvoiceCapacityAsync(note, invoice, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await RecordAuditAsync("Finance.InventoryReturn.CreditDraftCreated", note,
                    after: new { returnId, invoice.Id, note.TotalAmount }, reason: dto.Reason, cancellationToken: cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return await GetRequiredAsync(note.Id, producer, cancellationToken);
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);

    public Task<SupplierDebitNoteDto> UpdateInventoryReturnCreditHeaderAsync(Guid noteId,
        UpdateInventoryReturnCreditHeaderDto dto, FinancePostingProducerContext producer, CancellationToken cancellationToken = default) =>
        ExecuteNoteMutationAsync(noteId, async note =>
        {
            EnsureSupplierDebitNoteRoute(producer);
            RequireInventoryCreditHeaderEditable(note);
            ApplyConcurrencyToken(note, dto.RowVersion);
            var source = await RequireDispatchedReturnAsync(note.InventoryPurchaseReturnId!.Value, cancellationToken);
            var invoice = note.OriginalVendorInvoice ?? throw new InvalidOperationException("RTV_ORIGINAL_INVOICE_REQUIRED");
            var date = dto.CreditDate == default ? note.DebitNoteDate : dto.CreditDate.Date;
            if (date < source.ShippedDate!.Value.Date || date < invoice.InvoiceDate.Date)
                throw new InvalidOperationException("RTV_CREDIT_DATE_INVALID: credit date cannot precede dispatch or the original invoice.");
            var reference = RequiredText(dto.SupplierCreditNoteReference, "Supplier credit reference");
            await ValidateSupplierReferenceUniqueAsync(note.VendorId, reference, note.Id, cancellationToken);
            var before = new { note.SupplierCreditNoteReference, note.DebitNoteDate, note.Reason, note.Status };
            note.SupplierCreditNoteReference = reference;
            note.DebitNoteDate = date;
            note.Reason = TrimToNull(dto.Reason) ?? note.Reason ?? source.ReturnReason;
            note.Status = SupplierDebitNoteStatus.Draft;
            note.RejectionReason = null;
            note.RejectedAt = null;
            note.RejectedById = null;
            note.UpdatedAt = DateTime.UtcNow;
            note.UpdatedBy = UserName;
            note.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
            await _db.SaveChangesAsync(cancellationToken);
            if (_sourceDimensions == null) throw new InvalidOperationException("Finance source dimensions are not configured for supplier debit notes.");
            await _sourceDimensions.SynchronizeDraftAsync(producer, note.Id, note.DebitNoteDate, DimensionLineContexts(note),
                await BuildDebitNoteDimensionInputAsync(note, null, cancellationToken), inheritDefaultForUnassignedLines: true,
                budgetReservationSourceDocumentType: null, "Supplier return credit header corrected.", cancellationToken);
            await RecordAuditAsync("Finance.InventoryReturn.CreditHeaderCorrected", note, before,
                new { note.SupplierCreditNoteReference, note.DebitNoteDate, note.Reason, note.Status }, cancellationToken: cancellationToken);
            return await GetRequiredAsync(note.Id, producer, cancellationToken);
        }, cancellationToken);

    internal static void RequireInventoryCreditHeaderEditable(SupplierDebitNote note)
    {
        if (!note.InventoryPurchaseReturnId.HasValue || note.Status is not (SupplierDebitNoteStatus.Draft or SupplierDebitNoteStatus.Rejected) ||
            note.JournalEntryId.HasValue || note.PostingEventId.HasValue || note.DirectInvoiceAppliedAt.HasValue || note.DirectInvoiceAppliedAmount != 0)
            throw new InvalidOperationException("RTV_CREDIT_HEADER_LOCKED: only an unposted Draft or Rejected Inventory credit header may be corrected.");
    }

    // Invoked by the Inventory owner inside its physical dispatch transaction, after movements
    // have been saved. Ambiguous/uninvoiced or not-configured handoffs remain visibly pending.
    public async Task<bool> TryRecordDispatchAsync(Guid inventoryReturnId, CancellationToken cancellationToken = default)
    {
        if (!_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("RTV_DISPATCH_TRANSACTION_REQUIRED: Finance handoff must share the Inventory dispatch transaction.");
        var settings = await _db.FinanceSettings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        if (settings?.ReturnToVendorClearingAccountId == null || settings.ControlAccountInventoryId == null) return false;
        var candidates = await GetInventoryReturnCreditSourcesAsync(inventoryReturnId, cancellationToken);
        if (candidates.Count != 1) return false;
        var source = await RequireDispatchedReturnAsync(inventoryReturnId, cancellationToken);
        var invoice = await _db.Set<VendorInvoice>().Include(x => x.LineItems).SingleAsync(x =>
            x.Id == candidates[0].InvoiceId && x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        await ValidateInvoiceAsync(invoice.Id, invoice.SupplierId, cancellationToken);
        await RequirePostedOriginalInvoiceAsync(invoice, cancellationToken);
        await EnsureReturnDispatchPostedAsync(source, invoice, source.ShippedDate!.Value.Date, cancellationToken);
        return true;
    }

    private async Task<PurchaseReturn> RequireDispatchedReturnAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await _db.Set<PurchaseReturn>().Include(x => x.Items.Where(l => !l.IsDeleted))
            .ThenInclude(x => x.GoodsReceiptNoteItem).ThenInclude(x => x!.GoodsReceiptNote)
            .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Supplier return was not found for this tenant.");
        if (source.Status is not ("Shipped" or "Acknowledged") || !source.ShippedDate.HasValue ||
            !source.PurchaseOrderId.HasValue || !source.GoodsReceiptNoteId.HasValue || source.Items.Count == 0 ||
            source.Items.Any(x => x.TenantId != TenantId || x.ReturnQuantity <= 0 || !x.StockReversed || !x.StockReversedAt.HasValue ||
                x.GoodsReceiptNoteItem == null || x.GoodsReceiptNoteItem.TenantId != TenantId || x.GoodsReceiptNoteItem.IsDeleted ||
                x.GoodsReceiptNoteItem.GoodsReceiptNoteId != source.GoodsReceiptNoteId ||
                x.GoodsReceiptNoteItem.InventoryItemId != x.InventoryItemId || !x.GoodsReceiptNoteItem.PurchaseOrderItemId.HasValue ||
                x.GoodsReceiptNoteItem.GoodsReceiptNote == null || x.GoodsReceiptNoteItem.GoodsReceiptNote.TenantId != TenantId ||
                x.GoodsReceiptNoteItem.GoodsReceiptNote.SupplierId != source.SupplierId ||
                x.GoodsReceiptNoteItem.GoodsReceiptNote.PurchaseOrderId != source.PurchaseOrderId))
            throw new InvalidOperationException("RTV_DISPATCH_SOURCE_INVALID: a dispatched return with exact supplier, GRN, PO and posted item lineage is required.");
        if (source.ApprovalRequired && (!source.ApprovedById.HasValue || !source.ApprovedDate.HasValue))
            throw new InvalidOperationException("RTV_APPROVAL_EVIDENCE_MISSING: the required Inventory approval was not completed.");
        return source;
    }

    private async Task RequirePostedOriginalInvoiceAsync(VendorInvoice invoice, CancellationToken cancellationToken)
    {
        var journal = await _db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == invoice.JournalEntryId &&
            x.TenantId == TenantId, cancellationToken);
        if (!HasPostedOriginalInvoiceEvidence(invoice, journal))
            throw new InvalidOperationException("RTV_ORIGINAL_INVOICE_NOT_POSTED: the source invoice must have its own posted, unreversed Finance journal.");
    }

    internal static bool HasPostedOriginalInvoiceEvidence(VendorInvoice invoice, JournalEntry? journal) =>
        journal != null && journal.TenantId == invoice.TenantId && !journal.IsDeleted && !journal.IsReversed &&
        journal.Id == invoice.JournalEntryId && journal.SourceDocumentId == invoice.Id &&
        string.Equals(journal.SourceDocumentType, "VendorInvoice", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(journal.PostingStatus, "Posted", StringComparison.OrdinalIgnoreCase);

    internal static bool HasExactReturnInvoiceLines(PurchaseReturn source, VendorInvoice invoice,
        IReadOnlyDictionary<Guid, decimal>? invoiceQuantities = null) =>
        source.Items.Count > 0 && invoice.TenantId == source.TenantId && !invoice.IsDeleted && invoice.JournalEntryId.HasValue &&
        invoice.Status != VendorInvoiceStatus.Voided && source.PurchaseOrderId.HasValue &&
        invoice.PurchaseOrderId == source.PurchaseOrderId && source.Items.All(returnLine =>
            returnLine.ReturnQuantity > 0 && returnLine.GoodsReceiptNoteItem?.PurchaseOrderItemId.HasValue == true &&
            invoice.LineItems.Count(line => !line.IsDeleted && line.TenantId == source.TenantId &&
                line.InventoryItemId == returnLine.InventoryItemId &&
                line.PurchaseOrderItemId == returnLine.GoodsReceiptNoteItem?.PurchaseOrderItemId &&
                line.Quantity >= (invoiceQuantities == null ? returnLine.ReturnQuantity : invoiceQuantities[returnLine.Id]) && line.UnitPrice > 0) == 1) &&
        source.Items.Select(x => x.GoodsReceiptNoteItem?.PurchaseOrderItemId).Distinct().Count() == source.Items.Count;

    private static void RequireExactReturnInvoiceLines(PurchaseReturn source, VendorInvoice invoice, IReadOnlyDictionary<Guid, decimal> invoiceQuantities)
    {
        if (!HasExactReturnInvoiceLines(source, invoice, invoiceQuantities))
            throw new InvalidOperationException("RTV_INVOICE_LINEAGE_INVALID: the invoice must cover each returned item through its exact original PO line, without ambiguous or duplicate source lines.");
    }

    private static List<(PurchaseReturnItem ReturnLine, VendorInvoiceLineItem InvoiceLine, decimal InvoiceQuantity)> MatchReturnInvoiceLines(
        PurchaseReturn source, VendorInvoice invoice, IReadOnlyDictionary<Guid, decimal> invoiceQuantities) =>
        source.Items.Select(item => (item, invoice.LineItems.Single(line => !line.IsDeleted &&
            line.InventoryItemId == item.InventoryItemId && line.PurchaseOrderItemId == item.GoodsReceiptNoteItem!.PurchaseOrderItemId), invoiceQuantities[item.Id])).ToList();

    private async Task<Dictionary<Guid, decimal>> ReturnInvoiceQuantitiesAsync(PurchaseReturn source, CancellationToken cancellationToken)
    {
        var quantities = new Dictionary<Guid, decimal>();
        foreach (var line in source.Items)
        {
            var receiptId = line.GoodsReceiptNoteItem!.GoodsReceiptNote.PurchaseOrderReceiptId
                ?? throw new InvalidOperationException("RTV_RECEIPT_UNIT_LINEAGE_REQUIRED: the original Procurement receipt must identify the invoice unit of measure.");
            var receiptLine = await _db.Set<PurchaseOrderReceiptItem>().AsNoTracking().SingleOrDefaultAsync(x =>
                x.TenantId == TenantId && !x.IsDeleted && x.ReceiptId == receiptId && x.PurchaseOrderItemId == line.GoodsReceiptNoteItem.PurchaseOrderItemId,
                cancellationToken) ?? throw new InvalidOperationException("RTV_RECEIPT_UNIT_LINEAGE_REQUIRED: original receipt line is unavailable.");
            var conversion = 1m;
            if (receiptLine.ItemUnitOfMeasureId.HasValue)
                conversion = await _db.Set<ItemUnitOfMeasure>().Where(x => x.TenantId == TenantId && !x.IsDeleted &&
                    x.Id == receiptLine.ItemUnitOfMeasureId && x.InventoryItemId == line.InventoryItemId)
                    .Select(x => x.ConversionToBase).SingleOrDefaultAsync(cancellationToken);
            if (conversion <= 0)
                throw new InvalidOperationException("RTV_UNIT_CONVERSION_INVALID: original receipt unit conversion is missing or invalid.");
            var quantity = line.ReturnQuantity / conversion;
            if (quantity <= 0 || decimal.Round(quantity, 4, MidpointRounding.AwayFromZero) != quantity ||
                quantity > receiptLine.AcceptedQuantity)
                throw new InvalidOperationException("RTV_UNIT_CONVERSION_INVALID: returned base quantity does not reconcile to the accepted original purchase units.");
            quantities.Add(line.Id, quantity);
        }
        return quantities;
    }

    private async Task<Dictionary<Guid, decimal>> RequireDispatchCarryingByLineAsync(PurchaseReturn source, CancellationToken cancellationToken)
    {
        var movements = _db.Set<InventoryMovement>();
        var posted = await movements.AsNoTracking().Where(x => x.TenantId == TenantId && !x.IsDeleted &&
            x.ReferenceType == ReferenceType.Return && x.ReferenceId == source.Id &&
            x.MovementType == InventoryMovementType.SupplierReturn && x.Direction == MovementDirection.Out &&
            x.IsPosted && x.PostedAt.HasValue && !x.IsReversal &&
            !movements.Any(r => r.TenantId == TenantId && !r.IsDeleted && r.IsReversal && r.IsPosted && r.ReversedMovementId == x.Id))
            .ToListAsync(cancellationToken);
        var expected = source.Items.GroupBy(x => (x.InventoryItemId, x.LocationId, x.LotNumber, x.SerialNumber))
            .ToDictionary(x => x.Key, x => x.Sum(l => l.ReturnQuantity));
        var actual = posted.GroupBy(x => (x.InventoryItemId, x.LocationId, x.LotNumber, x.SerialNumber)).ToDictionary(x => x.Key, x => x.Sum(l => l.Quantity));
        if (expected.Count != actual.Count || expected.Any(x => !actual.TryGetValue(x.Key, out var qty) || qty != x.Value) ||
            posted.Any(x => x.WarehouseId != source.WarehouseId || x.TotalValue <= 0))
            throw new InvalidOperationException("RTV_POSTED_MOVEMENT_MISMATCH: returned quantities do not reconcile to the immutable dispatched valuation movements.");
        var carryingByLine = new Dictionary<Guid, decimal>();
        foreach (var group in source.Items.GroupBy(x => (x.InventoryItemId, x.LocationId, x.LotNumber, x.SerialNumber)))
        {
            var total = Round(posted.Where(x => (x.InventoryItemId, x.LocationId, x.LotNumber, x.SerialNumber) == group.Key).Sum(x => x.TotalValue));
            var ordered = group.OrderBy(x => x.Id).ToList();
            var allocated = 0m;
            for (var index = 0; index < ordered.Count; index++)
            {
                var amount = index == ordered.Count - 1 ? total - allocated : Round(total * ordered[index].ReturnQuantity / expected[group.Key]);
                carryingByLine.Add(ordered[index].Id, amount);
                allocated += amount;
            }
        }
        return carryingByLine;
    }

    private async Task<InventorySupplierReturnPosting> EnsureReturnDispatchPostedAsync(PurchaseReturn source, VendorInvoice invoice,
        DateTime postingDate, CancellationToken cancellationToken)
    {
        await _unitOfWork.AcquireTransactionLockAsync($"finance-return-dispatch:{TenantId:N}:{source.Id:N}", cancellationToken);
        RequireExactReturnInvoiceLines(source, invoice, await ReturnInvoiceQuantitiesAsync(source, cancellationToken));
        var carrying = Round((await RequireDispatchCarryingByLineAsync(source, cancellationToken)).Values.Sum());
        var existing = await _db.Set<InventorySupplierReturnPosting>().SingleOrDefaultAsync(x =>
            x.TenantId == TenantId && x.InventoryPurchaseReturnId == source.Id && !x.IsDeleted, cancellationToken);
        if (existing != null)
        {
            if (existing.OriginalVendorInvoiceId != invoice.Id || existing.CarryingAmount != carrying)
                throw new InvalidOperationException("RTV_DISPATCH_CONFLICT: Finance already recorded a different dispatch lineage.");
            var postedEvent = await _db.Set<FinancePostingEvent>().AsNoTracking().AnyAsync(x => x.TenantId == TenantId &&
                x.Id == existing.PostingEventId && !x.IsDeleted && x.PostingStatus == "Posted" && x.PostedAt.HasValue &&
                x.JournalEntryId == existing.JournalEntryId && x.SourceDocumentId == source.Id &&
                x.SourceDocumentType == "InventorySupplierReturnDispatch", cancellationToken);
            var postedJournal = await _db.JournalEntries.AsNoTracking().AnyAsync(x => x.TenantId == TenantId &&
                x.Id == existing.JournalEntryId && !x.IsDeleted && !x.IsReversed && x.PostingStatus == "Posted" &&
                x.SourceDocumentId == source.Id && x.SourceDocumentType == "InventorySupplierReturnDispatch", cancellationToken);
            if (!postedEvent || !postedJournal)
                throw new InvalidOperationException("RTV_DISPATCH_POSTING_INVALID: the recorded Finance handoff is missing or reversed.");
            return existing;
        }
        var settings = await _db.FinanceSettings.AsNoTracking().SingleAsync(x => x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        var clearing = settings.ReturnToVendorClearingAccountId
            ?? throw new InvalidOperationException("RTV_CLEARING_ACCOUNT_REQUIRED: configure the dedicated return-to-vendor clearing account in Finance.");
        var inventory = settings.ControlAccountInventoryId
            ?? throw new InvalidOperationException("RTV_INVENTORY_ACCOUNT_REQUIRED: configure the Inventory control account in Finance.");
        if (clearing == inventory) throw new InvalidOperationException("RTV_ACCOUNTS_MUST_DIFFER: clearing and Inventory cannot use the same GL account.");
        await RequirePostingAccountAsync(clearing, postingDate, "Return-to-vendor clearing account", true, cancellationToken);
        await RequirePostingAccountAsync(inventory, postingDate, "Inventory control account", true, cancellationToken);
        var currency = NormalizeCurrency(settings.BaseCurrency, "GHS");
        var result = await _posting.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "Inventory", SourceDocumentType = "InventorySupplierReturnDispatch", SourceDocumentId = source.Id,
            SourceDocumentTenantId = TenantId, SourceDocumentReference = source.ReturnNumber,
            PostingAction = "Dispatch", PostingDate = postingDate, JournalType = "Supplier Return Dispatch", BookClassification = "IFRS",
            Description = $"Dispatched supplier return {source.ReturnNumber}", FunctionalCurrencyCode = currency,
            IdempotencyKey = $"Inventory:SupplierReturn:{TenantId:N}:{source.Id:N}:Dispatch", ReturnExistingOnDuplicate = true,
            Lines = new[]
            {
                PostingLine(clearing, $"Supplier return clearing {source.ReturnNumber}", carrying, 0, carrying, currency, currency, 1, postingDate, source.ReturnNumber, 1, "RTV-Dispatch-Clearing"),
                PostingLine(inventory, $"Dispatched Inventory {source.ReturnNumber}", 0, carrying, carrying, currency, currency, 1, postingDate, source.ReturnNumber, 2, "RTV-Dispatch-Inventory")
            }
        }, cancellationToken);
        var record = new InventorySupplierReturnPosting
        {
            Id = Guid.NewGuid(), TenantId = TenantId, InventoryPurchaseReturnId = source.Id, OriginalVendorInvoiceId = invoice.Id,
            PostingEventId = result.PostingEventId, JournalEntryId = result.JournalEntryId,
            ClearingAccountId = clearing, InventoryAccountId = inventory, CarryingAmount = carrying, PostingDate = postingDate,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
        };
        _db.Set<InventorySupplierReturnPosting>().Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        return record;
    }

    private async Task PrepareInventoryReturnPostingAsync(SupplierDebitNote note, FinancePostingRequestDto request, CancellationToken cancellationToken)
    {
        var source = await RequireDispatchedReturnAsync(note.InventoryPurchaseReturnId!.Value, cancellationToken);
        var invoice = note.OriginalVendorInvoice ?? throw new InvalidOperationException("RTV_ORIGINAL_INVOICE_REQUIRED");
        await RequirePostedOriginalInvoiceAsync(invoice, cancellationToken);
        var invoiceQuantities = await ReturnInvoiceQuantitiesAsync(source, cancellationToken);
        RequireExactReturnInvoiceLines(source, invoice, invoiceQuantities);
        var mapping = MatchReturnInvoiceLines(source, invoice, invoiceQuantities);
        if (note.VendorId != source.SupplierId || note.LineItems.Count(x => !x.IsDeleted) != mapping.Count ||
            mapping.Any(pair => !note.LineItems.Any(line => !line.IsDeleted && line.InventoryPurchaseReturnItemId == pair.ReturnLine.Id &&
                line.OriginalVendorInvoiceLineItemId == pair.InvoiceLine.Id && line.Quantity == pair.InvoiceQuantity &&
                Round(line.UnitPrice) == Round(pair.InvoiceLine.UnitPrice))))
            throw new InvalidOperationException("RTV_CREDIT_LINEAGE_CHANGED: credit lines no longer match the dispatched return.");
        await ValidateDirectInvoiceCapacityAsync(note, invoice, cancellationToken);
        var dispatch = await EnsureReturnDispatchPostedAsync(source, invoice, note.DebitNoteDate, cancellationToken);
        var settings = await _db.FinanceSettings.AsNoTracking().SingleAsync(x => x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        var variance = settings.PurchaseReturnVarianceAccountId
            ?? throw new InvalidOperationException("RTV_VARIANCE_ACCOUNT_REQUIRED: configure the dedicated purchase-return cost variance account.");
        if (variance == dispatch.ClearingAccountId || variance == dispatch.InventoryAccountId)
            throw new InvalidOperationException("RTV_ACCOUNTS_MUST_DIFFER: cost variance must not use Inventory or clearing.");
        await RequirePostingAccountAsync(dispatch.ClearingAccountId, note.DebitNoteDate, "Return clearing account", true, cancellationToken);
        await RequirePostingAccountAsync(variance, note.DebitNoteDate, "Purchase-return cost variance account", false, cancellationToken);
        var carryingByReturnLine = await RequireDispatchCarryingByLineAsync(source, cancellationToken);
        var carryingByCreditLine = note.LineItems.Where(x => !x.IsDeleted).ToDictionary(x => x.Id, x => carryingByReturnLine[x.InventoryPurchaseReturnItemId!.Value]);
        request.Lines = BuildReturnClearingLines(request.Lines, note, dispatch, variance, request.FunctionalCurrencyCode, carryingByCreditLine);
        note.ReturnDispatchPostingEventId = dispatch.PostingEventId;
        note.ReturnDispatchJournalEntryId = dispatch.JournalEntryId;
    }

    internal static IReadOnlyList<FinancePostingLineDto> BuildReturnClearingLines(IReadOnlyList<FinancePostingLineDto> original,
        SupplierDebitNote note, InventorySupplierReturnPosting dispatch, Guid varianceAccountId, string functionalCurrency,
        IReadOnlyDictionary<Guid, decimal> carryingByCreditLine)
    {
        // Keep the exact original AP/tax reversals, but never reverse its Inventory/GRV base
        // account twice. The dispatch clearing is settled at its recorded carrying amount.
        var lines = original.Where(x => x.TransactionTag is not ("AP-SupplierDebitNote-Line" or "AP-SupplierDebitNote-Discount")).ToList();
        if (Round(carryingByCreditLine.Values.Sum()) != dispatch.CarryingAmount)
            throw new InvalidOperationException("RTV_CARRYING_ALLOCATION_MISMATCH");
        foreach (var sourceLine in original.Where(x => x.TransactionTag == "AP-SupplierDebitNote-Line"))
        {
            if (!sourceLine.SourceDocumentLineId.HasValue || !carryingByCreditLine.TryGetValue(sourceLine.SourceDocumentLineId.Value, out var carrying))
                throw new InvalidOperationException("RTV_CARRYING_LINE_MISSING");
            var discount = original.Where(x => x.TransactionTag == "AP-SupplierDebitNote-Discount" &&
                x.SourceDocumentLineId == sourceLine.SourceDocumentLineId).Sum(x => x.DebitAmount);
            var commercialPrincipal = Round(sourceLine.CreditAmount - discount);
            var clearing = PostingLine(dispatch.ClearingAccountId, $"Clear dispatched return - {note.DebitNoteNumber}", 0,
                carrying, carrying, functionalCurrency, functionalCurrency, 1, note.DebitNoteDate, note.DebitNoteNumber,
                lines.Count + 1, "RTV-Credit-Clearing", sourceLine.SourceDocumentLineId);
            clearing.Dimensions = sourceLine.Dimensions;
            lines.Add(clearing);
            var difference = Round(carrying - commercialPrincipal);
            if (difference != 0)
            {
                var variance = PostingLine(varianceAccountId, $"Return carrying-cost variance - {note.DebitNoteNumber}",
                    Math.Max(difference, 0), Math.Max(-difference, 0), Math.Abs(difference), functionalCurrency, functionalCurrency, 1,
                    note.DebitNoteDate, note.DebitNoteNumber, lines.Count + 1, "RTV-Credit-Variance", sourceLine.SourceDocumentLineId);
                variance.Dimensions = sourceLine.Dimensions;
                lines.Add(variance);
            }
        }
        for (var i = 0; i < lines.Count; i++) lines[i].LineNumber = i + 1;
        if (Round(lines.Sum(x => x.DebitAmount - x.CreditAmount)) != 0)
            throw new InvalidOperationException("RTV_CREDIT_UNBALANCED");
        return lines;
    }

    private async Task ValidateDirectInvoiceCapacityAsync(SupplierDebitNote note, VendorInvoice invoice, CancellationToken cancellationToken)
    {
        if (note.DirectInvoiceAppliedAmount > 0 || note.DirectInvoiceAppliedAt.HasValue || EffectiveApplications(note.Applications).Count != 0)
            throw new InvalidOperationException("RTV_CREDIT_ALREADY_APPLIED: the return credit has already been settled.");
        if (invoice.PaidAmount < 0 || note.TotalAmount > Round(invoice.TotalAmount - invoice.PaidAmount))
            throw new InvalidOperationException("RTV_INVOICE_BALANCE_EXCEEDED: this credit exceeds the remaining original invoice balance; review existing payments/credits first.");
        var reservedPayment = await _db.Set<VendorPaymentAllocation>().AnyAsync(x => x.TenantId == TenantId &&
            x.VendorInvoiceId == invoice.Id && !x.IsDeleted && !x.VendorPayment.IsDeleted && !x.VendorPayment.JournalEntryId.HasValue &&
            x.VendorPayment.Status != VendorPaymentStatus.Voided && x.VendorPayment.Status != VendorPaymentStatus.Failed, cancellationToken);
        var reservedCredit = await _db.Set<SupplierDebitNoteApplication>().AnyAsync(x => x.TenantId == TenantId &&
            x.VendorInvoiceId == invoice.Id && !x.IsDeleted && !x.IsReversal && !x.AppliedAt.HasValue &&
            !x.VendorPayment.IsDeleted && x.VendorPayment.Status != VendorPaymentStatus.Voided && x.VendorPayment.Status != VendorPaymentStatus.Failed &&
            !_db.Set<SupplierDebitNoteApplication>().Any(r => r.TenantId == TenantId && !r.IsDeleted && r.IsReversal && r.OriginalApplicationId == x.Id), cancellationToken);
        if (reservedPayment || reservedCredit)
            throw new InvalidOperationException("RTV_INVOICE_RESERVED: release the draft payment/credit reservation on this invoice before posting its return credit.");
    }

    private async Task ApplyInventoryReturnCreditAsync(SupplierDebitNote note, CancellationToken cancellationToken)
    {
        var invoice = note.OriginalVendorInvoice ?? throw new InvalidOperationException("RTV_ORIGINAL_INVOICE_REQUIRED");
        await ValidateDirectInvoiceCapacityAsync(note, invoice, cancellationToken);
        invoice.PaidAmount = Round(invoice.PaidAmount + note.TotalAmount);
        invoice.Status = invoice.PaidAmount == Round(invoice.TotalAmount) ? VendorInvoiceStatus.Paid : VendorInvoiceStatus.PartiallyPaid;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = UserName;
        invoice.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
        note.DirectInvoiceAppliedAmount = note.TotalAmount;
        note.DirectInvoiceAppliedAt = DateTime.UtcNow;
        note.DirectInvoiceAppliedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
        await RecordAuditAsync("Finance.InventoryReturn.CreditAppliedToOriginalInvoice", note,
            after: new { invoice.Id, invoice.PaidAmount, note.DirectInvoiceAppliedAmount, note.DirectInvoiceAppliedAt },
            reason: "Supplier credit applied directly to its original invoice; no payment or additional stock movement was created.",
            postingEventId: note.PostingEventId, journalEntryId: note.JournalEntryId, cancellationToken: cancellationToken);
    }
}
