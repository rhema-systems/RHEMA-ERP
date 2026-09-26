using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    // Kept as an explicit rejection for older internal callers. Final posting belongs to AP.
    public Task<PostLandedCostResultDto> PostLandedCostAsync(Guid landedCostId,
        PostLandedCostDto dto, FinancePostingProducerContext producer, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Final landed-cost posting has moved to the supplier Invoice page. Prepare invoice drafts, then review and post them in AP.");

    public async Task<PostLandedCostResultDto> PrepareLandedCostInvoicesAsync(Guid landedCostId,
        PostLandedCostDto dto, FinancePostingProducerContext producer, CancellationToken cancellationToken = default)
    {
        if (dto == null || dto.Charges == null || dto.Charges.Any(c => c == null)) throw new ArgumentException("Provide the supplier billing details.");
        var invoices = await CreateFromLandedCostAsync(landedCostId, new CreateLandedCostInvoicesDto
        {
            InvoiceDate = dto.InvoiceDate, RequireAllVoucherCharges = true,
            Charges = dto.Charges.Select(c => new LandedCostInvoiceChargeDto { CostItemId = c.CostItemId,
                BusinessPartnerId = c.BusinessPartnerId, BusinessPartnerRoleId = c.BusinessPartnerRoleId,
                SupplierInvoiceNumber = c.SupplierInvoiceNumber }).ToList()
        }, producer, cancellationToken);
        var posted = await _unitOfWork.Repository<LandedCost>().GetQueryable(c => c.Id == landedCostId &&
            c.TenantId == TenantId && !c.IsDeleted).Select(c => c.Status == "Posted").SingleAsync(cancellationToken);
        return new PostLandedCostResultDto { InventoryPosted = posted, Invoices = invoices,
            Message = "Supplier invoice drafts are ready. Review taxes, approve and post from Invoices." };
    }

    private static void EnsureLandedCostTaxReviewed(VendorInvoice invoice)
    {
        if (invoice.WithholdingDecisionPending)
            throw new InvalidOperationException("AP_WHT_CONFIRMATION_REQUIRED: Choose Yes or No for withholding in Edit invoice before submitting or posting.");
        if (invoice.LineItems.Any(l => !l.IsDeleted && (invoice.AutoInvoiceRequestId.HasValue || invoice.EstateAcquisitionId.HasValue || l.LandedCostItemId.HasValue) &&
            (!Enum.IsDefined(l.TaxTreatment) || l.TaxTreatment == TaxTreatment.PendingReview ||
                (l.TaxTreatment == TaxTreatment.Standard && !l.TaxGroupId.HasValue))))
            throw new InvalidOperationException(invoice.EstateAcquisitionId.HasValue
                ? "Complete the tax treatment for every Estate supplier invoice line in Edit invoice before submitting, approving or posting."
                : invoice.AutoInvoiceRequestId.HasValue
                ? "Complete the tax treatment for every receipt invoice line in Edit invoice before submitting, approving or posting."
                : "Complete the tax treatment for every landed-cost invoice line in Edit invoice before submitting, approving or posting.");
    }

    private Task InInvoiceTransactionAsync(Func<CancellationToken, Task> action, CancellationToken token)
        => _unitOfWork.HasActiveTransaction ? action(token) : _unitOfWork.ExecuteInTransactionAsync(action, token);

    private static bool IsLandedCostInvoice(VendorInvoice invoice)
        => invoice.LineItems.Any(line => !line.IsDeleted && line.LandedCostItemId.HasValue);

    public Task<List<VendorInvoiceDto>> CreateFromLandedCostAsync(Guid landedCostId,
        CreateLandedCostInvoicesDto dto, FinancePostingProducerContext producer, CancellationToken cancellationToken = default)
    {
        EnsureVendorInvoiceRoute(producer);
        if (dto == null || landedCostId == Guid.Empty || dto.InvoiceDate == default || dto.Charges == null || dto.Charges.Count is < 1 or > 100 ||
            dto.Charges.Any(c => c == null) ||
            dto.Charges.Select(c => c.CostItemId).Distinct().Count() != dto.Charges.Count)
            throw new ArgumentException("Choose a receipt cost voucher, invoice date and distinct charges.");
        foreach (var charge in dto.Charges)
        {
            if (charge.BusinessPartnerId == Guid.Empty || string.IsNullOrWhiteSpace(charge.SupplierInvoiceNumber) || charge.SupplierInvoiceNumber.Trim().Length > 100)
                throw new ArgumentException("Select a cost supplier and enter its invoice reference for every charge.");
            if ((charge.TaxTreatment.HasValue && !Enum.IsDefined(charge.TaxTreatment.Value)) ||
                (charge.TaxTreatment == TaxTreatment.Standard && !charge.TaxGroupId.HasValue) ||
                (charge.TaxTreatment != TaxTreatment.Standard && charge.TaxGroupId.HasValue))
                throw new ArgumentException("Choose the tax treatment for every charge; standard-rated charges also need a tax group.");
        }
        if (_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("Start supplier invoice generation outside another transaction.");

        return _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync($"InventoryLandedCost:{landedCostId:N}", cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync($"LandedCostInvoice:{TenantId:N}:{landedCostId:N}", cancellationToken);
                var cost = await _unitOfWork.Repository<LandedCost>().GetQueryable(c =>
                        c.Id == landedCostId && c.TenantId == TenantId && !c.IsDeleted)
                    .Include(c => c.Items).SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ArgumentException("Landed-cost voucher not found in this company.");
                var accrual = await GetLandedCostPreparationAccrualAsync(cost, cancellationToken);
                var items = cost.Items.Where(i => !i.IsDeleted).ToDictionary(i => i.Id);
                if (dto.RequireAllVoucherCharges && dto.Charges.Count != items.Count)
                    throw new ArgumentException("Include every charge from this voucher; refresh if its costs have changed.");
                if (dto.Charges.Any(c => !items.ContainsKey(c.CostItemId)))
                    throw new ArgumentException("Every selected charge must belong to this voucher.");
                if (dto.UseSavedBillingDetails && dto.Charges.Any(c => items[c.CostItemId].SupplierId != c.BusinessPartnerId ||
                    items[c.CostItemId].ReferenceNumber != c.SupplierInvoiceNumber.Trim()))
                    throw new InvalidOperationException("Billing details changed. Refresh the voucher before retrying preparation.");

                var requestedIds = dto.Charges.Select(c => c.CostItemId).ToArray();
                var existing = await _unitOfWork.Repository<VendorInvoiceLineItem>()
                    .GetQueryable(l => l.TenantId == TenantId && !l.IsDeleted && l.LandedCostItemId.HasValue && requestedIds.Contains(l.LandedCostItemId.Value))
                    .Include(l => l.VendorInvoice).ToListAsync(cancellationToken);
                var suppliers = new Dictionary<(Guid PartnerId, Guid? RoleId), CanonicalApPartner>();
                foreach (var partnerId in dto.Charges.Select(c => c.BusinessPartnerId).Distinct().OrderBy(id => id))
                {
                    await _unitOfWork.AcquireTransactionLockAsync($"LandedCostSupplier:{TenantId:N}:{partnerId:N}", cancellationToken);
                    foreach (var roleId in dto.Charges.Where(c => c.BusinessPartnerId == partnerId)
                        .Select(c => c.BusinessPartnerRoleId).Distinct())
                        suppliers.Add((partnerId, roleId), await ResolveCanonicalApPartnerAsync(
                            partnerId, roleId, dto.InvoiceDate, cancellationToken));
                }

                // CreateCore and supplier defaults query canonical identities from the database.
                // Flush an onboarding handoff within this same owned transaction before those
                // reads; a later validation failure still rolls the entire generation back.
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var output = new List<VendorInvoiceDto>();
                var groups = dto.Charges.GroupBy(c => (Supplier: c.BusinessPartnerId,
                    Role: suppliers[(c.BusinessPartnerId, c.BusinessPartnerRoleId)].Role.Id,
                    Currency: items[c.CostItemId].Currency.Trim().ToUpperInvariant(), Reference: c.SupplierInvoiceNumber.Trim()));
                foreach (var group in groups)
                {
                    var rates = group.Select(c => items[c.CostItemId].ExchangeRate).Distinct().ToList();
                    if (rates.Count != 1 || rates[0] <= 0)
                        throw new InvalidOperationException("Charges on one supplier invoice must have the same recorded exchange rate.");
                    var groupIds = group.Select(c => c.CostItemId).ToHashSet();
                    var linked = existing.Where(l => groupIds.Contains(l.LandedCostItemId!.Value)).ToList();
                    if (linked.Count > 0)
                    {
                        var old = linked[0].VendorInvoice;
                        if (linked.Count != group.Count() || linked.Any(l => l.VendorInvoiceId != old.Id) || old.IsDeleted ||
                            old.BusinessPartnerId != group.Key.Supplier || old.BusinessPartnerRoleId != group.Key.Role ||
                            old.SupplierInvoiceNumber != group.Key.Reference ||
                            old.CurrencyCode != group.Key.Currency ||
                            old.Status == VendorInvoiceStatus.Voided || group.Any(c => linked.Any(l =>
                                l.LandedCostItemId == c.CostItemId && c.TaxTreatment.HasValue && (l.TaxTreatment != c.TaxTreatment || l.TaxGroupId != c.TaxGroupId))))
                            throw new InvalidOperationException("These charges already belong to supplier invoices. Open the existing invoices; do not generate replacements.");
                        output.Add((await GetByIdAsync(old.Id, producer, cancellationToken))!);
                        continue;
                    }
                    if (group.Any(c => !string.IsNullOrWhiteSpace(items[c.CostItemId].InvoiceNumber)))
                        throw new InvalidOperationException("A charge is already linked to a saved invoice. Review that link before generating an invoice.");

                    var create = new VendorInvoiceCreateDto
                    {
                        BusinessPartnerId = group.Key.Supplier, BusinessPartnerRoleId = group.Key.Role,
                        SupplierInvoiceNumber = group.Key.Reference,
                        InvoiceDate = dto.InvoiceDate.Date, CurrencyCode = group.Key.Currency, ExchangeRate = rates[0],
                        MatchingType = InvoiceMatchingType.None,
                        Reference = cost.LandedCostNumber,
                        Notes = $"Supplier charges from landed-cost voucher {cost.LandedCostNumber}. Final posting clears its accrual and capitalizes an unposted voucher once.",
                        LineItems = group.Select(c =>
                        {
                            var item = items[c.CostItemId];
                            // Only supplier/reference metadata is assigned; posted values and allocations remain immutable.
                            item.SupplierId = c.BusinessPartnerId;
                            item.SupplierName = suppliers[(c.BusinessPartnerId, c.BusinessPartnerRoleId)].Partner.PartnerName;
                            item.ReferenceNumber = group.Key.Reference; item.UpdatedAt = DateTime.UtcNow; item.LastModifiedById = CurrentUserId;
                            return new VendorInvoiceLineItemCreateDto
                            {
                                LandedCostItemId = item.Id, Description = item.Description ?? "Landed cost",
                                Quantity = 1, UnitPrice = item.Amount, Unit = "Charge", LineItemType = "Expense",
                                GLAccountId = accrual.AccountId, TaxTreatment = c.TaxTreatment ?? TaxTreatment.PendingReview, TaxGroupId = c.TaxGroupId
                            };
                        }).ToList()
                    };
                    var invoice = await CreateCoreAsync(create, producer, cancellationToken, deferSupplierWithholdingDecision: true);
                    foreach (var c in group)
                    {
                        var item = items[c.CostItemId]; item.InvoiceNumber = invoice.InvoiceNumber; item.InvoiceDate = invoice.InvoiceDate;
                    }
                    output.Add(invoice);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await EnsureLandedCostSupplierDocumentsAsync(cost, dto.Charges.Select(c => items[c.CostItemId]).ToList(), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return output;
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private async Task<(Guid AccountId, string Currency)> GetLandedCostPreparationAccrualAsync(LandedCost cost, CancellationToken token)
    {
        if (cost.Status == "Posted") return await GetPostedLandedCostAccrualAsync(cost, token);
        if (cost.TenantId != TenantId || cost.IsDeleted || cost.Status is not ("Allocated" or "Approved"))
            throw new InvalidOperationException("Allocate the voucher before preparing supplier invoices.");
        var settings = await GetFinanceSettingsAsync(token);
        if (!string.Equals(settings.BaseCurrency, cost.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The voucher must use the company's functional currency for accrual clearing.");
        return (settings.ControlAccountGRVAccrualId ?? throw new InvalidOperationException("GRV Accrual Control Account is not configured in Finance Settings."), cost.Currency);
    }

    private async Task PostInvoiceLandedCostsAsync(VendorInvoice invoice, CancellationToken token)
    {
        if (!IsLandedCostInvoice(invoice)) return;
        if (!_unitOfWork.HasActiveTransaction || _landedCosts == null)
            throw new InvalidOperationException("Landed-cost invoice posting requires the shared transaction and valuation service.");
        var ids = invoice.LineItems.Where(l => !l.IsDeleted && l.LandedCostItemId.HasValue).Select(l => l.LandedCostItemId!.Value).ToArray();
        var voucherIds = await _unitOfWork.Repository<LandedCostItem>().GetQueryable(i =>
            i.TenantId == TenantId && !i.IsDeleted && ids.Contains(i.Id)).Select(i => i.LandedCostId).Distinct().OrderBy(id => id).ToListAsync(token);
        foreach (var id in voucherIds) await _unitOfWork.AcquireTransactionLockAsync($"InventoryLandedCost:{id:N}", token);
        // Validate the immutable supplier/amount/currency/account links before any valuation.
        // A later tax, dimension, period or AP failure rolls the complete transaction back.
        await ResolveLandedCostClearingAccountsAsync(invoice, token);
        foreach (var id in voucherIds)
        {
            var posted = await _unitOfWork.Repository<LandedCost>().GetQueryable(c => c.Id == id && c.TenantId == TenantId && !c.IsDeleted)
                .Select(c => c.Status == "Posted").SingleAsync(token);
            if (!posted && !await _landedCosts.PostToInventoryAsync(id, CurrentUserId))
                throw new InvalidOperationException("Landed-cost valuation did not complete. The invoice has not been posted.");
        }
    }

    private async Task<(Guid AccountId, string Currency)> GetPostedLandedCostAccrualAsync(LandedCost cost, CancellationToken token)
    {
        if (cost.TenantId != TenantId || cost.IsDeleted || cost.Status != "Posted")
            throw new InvalidOperationException("Post the landed costs to inventory before generating their accrual-clearing invoices.");
        var posting = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(e =>
            e.TenantId == TenantId && !e.IsDeleted && e.SourceDocumentId == cost.Id &&
            e.SourceDocumentType == "InventoryLandedCost" && e.SourceModule == "Inventory" &&
            e.PostingAction == "PostLandedCost" && e.PostingStatus == "Posted").SingleOrDefaultAsync(token)
            ?? throw new InvalidOperationException("The posted landed-cost accounting event is missing.");
        var journal = await _unitOfWork.Repository<JournalEntry>().GetQueryable(j =>
            j.Id == posting.JournalEntryId && j.TenantId == TenantId && !j.IsDeleted).SingleOrDefaultAsync(token)
            ?? throw new InvalidOperationException("The landed-cost journal is missing.");
        if (journal.IsReversed || journal.ReversalJournalEntryId.HasValue)
            throw new InvalidOperationException("The landed-cost journal has been reversed.");
        var credits = await _unitOfWork.Repository<AccountTransaction>().GetQueryable(t =>
            t.TenantId == TenantId && !t.IsDeleted && t.JournalEntryId == journal.Id && t.CreditAmount > 0).ToListAsync(token);
        if (credits.Count == 0 || credits.Select(t => t.AccountId).Distinct().Count() != 1 ||
            RoundMoney(credits.Sum(t => t.CreditAmount)) != cost.TotalCost ||
            credits.Any(t => !string.Equals(t.FunctionalCurrencyCode, cost.Currency, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The landed-cost journal does not contain one matching accrual credit.");
        var settings = await GetFinanceSettingsAsync(token);
        if (!string.Equals(settings.BaseCurrency, cost.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The posted voucher currency must match the company's functional currency for accrual clearing.");
        // Use the original posted account, not a possibly changed current account mapping.
        return (credits[0].AccountId, cost.Currency);
    }

    private async Task<Dictionary<Guid, Guid>> ResolveLandedCostClearingAccountsAsync(VendorInvoice invoice, CancellationToken token)
    {
        var lines = invoice.LineItems.Where(l => !l.IsDeleted).ToList();
        if (invoice.PurchaseOrderId.HasValue || invoice.IsOpeningBalance || lines.Any(l => !l.LandedCostItemId.HasValue) ||
            invoice.MatchingType != InvoiceMatchingType.None)
            throw new InvalidOperationException("A landed-cost supplier invoice cannot mix goods, opening balances or unrelated expense lines.");
        var ids = lines.Select(l => l.LandedCostItemId!.Value).ToArray();
        var items = await _unitOfWork.Repository<LandedCostItem>().GetQueryable(i =>
            i.TenantId == TenantId && !i.IsDeleted && ids.Contains(i.Id)).Include(i => i.LandedCost).ToListAsync(token);
        if (items.Count != lines.Count || ids.Distinct().Count() != lines.Count)
            throw new InvalidOperationException("The invoice's landed-cost source links are incomplete or duplicated.");
        var supplier = await ResolveInvoiceSupplierForPostingAsync(invoice, token);
        var accounts = new Dictionary<Guid, Guid>();
        var voucherAccounts = new Dictionary<Guid, Guid>();
        foreach (var item in items)
        {
            var line = lines.Single(l => l.LandedCostItemId == item.Id);
            var partner = await _unitOfWork.Repository<BusinessPartner>().GetQueryable(p =>
                p.Id == item.SupplierId && p.TenantId == TenantId && !p.IsDeleted).SingleOrDefaultAsync(token);
            if (partner == null || !partner.IsActive || partner.IsBlacklisted ||
                partner.RegistrationStatus is not ("Active" or "Approved") ||
                partner.Id != supplier.Id)
                throw new InvalidOperationException("The invoice supplier does not match its landed-cost charges.");
            if (!string.IsNullOrWhiteSpace(item.InvoiceNumber) && item.InvoiceNumber != invoice.InvoiceNumber)
                throw new InvalidOperationException("A landed-cost charge is linked to a different invoice.");
            if (line.Quantity != 1 || line.UnitPrice != item.Amount || line.DiscountAmount != 0 || line.DiscountPercentage != 0 ||
                line.FixedAssetId.HasValue || line.InventoryItemId.HasValue || line.PurchaseOrderItemId.HasValue || line.BudgetEntryId.HasValue ||
                !string.Equals(item.Currency, invoice.CurrencyCode, StringComparison.OrdinalIgnoreCase) ||
                invoice.ExchangeRate != item.ExchangeRate || RoundMoney(item.Amount * invoice.ExchangeRate) != item.AmountInBaseCurrency)
                throw new InvalidOperationException("Landed-cost invoice amounts, currency and source quantities must match the source charges.");
            if (!voucherAccounts.TryGetValue(item.LandedCostId, out var account))
            {
                account = (await GetLandedCostPreparationAccrualAsync(item.LandedCost, token)).AccountId;
                voucherAccounts.Add(item.LandedCostId, account);
            }
            if (line.GLAccountId != account)
                throw new InvalidOperationException("The landed-cost invoice must clear the original accrual account.");
            accounts.Add(line.Id, account);
        }
        return accounts;
    }

    private static void ValidateLandedCostInvoiceUpdate(VendorInvoice invoice, VendorInvoiceUpdateDto dto)
    {
        if (!IsLandedCostInvoice(invoice)) return;
        var lines = invoice.LineItems.Where(l => !l.IsDeleted).ToList();
        if (dto.PurchaseOrderId.HasValue || dto.IsOpeningBalance || dto.AcceptedSupplyKind.HasValue || dto.AcceptedSupplySourceId.HasValue ||
            dto.CurrencyCode != invoice.CurrencyCode || dto.ExchangeRate != invoice.ExchangeRate || dto.MatchingType != InvoiceMatchingType.None ||
            dto.LineItems.Count != lines.Count || dto.LineItems.Select(l => l.Id).Distinct().Count() != lines.Count ||
            dto.SupplierInvoiceNumber != invoice.SupplierInvoiceNumber || string.IsNullOrWhiteSpace(dto.SupplierInvoiceNumber))
            throw new InvalidOperationException("This invoice's landed-cost source, supplier reference and currency are locked. Review taxes, dates and payment terms without replacing its charges.");
        foreach (var input in dto.LineItems)
        {
            var original = lines.SingleOrDefault(l => l.Id == input.Id);
            if (original == null || input.Quantity != 1 || input.UnitPrice != original.UnitPrice || input.DiscountPercentage != 0 ||
                input.GLAccountId != original.GLAccountId || input.BudgetEntryId.HasValue || input.PurchaseOrderItemId.HasValue ||
                input.FixedAssetId.HasValue || input.LineItemType != original.LineItemType)
                throw new InvalidOperationException("Posted landed-cost charge amounts and clearing accounts cannot be edited on the invoice.");
        }
    }

    private async Task<bool> DeleteLandedCostInvoiceDraftAsync(VendorInvoice invoice, CancellationToken token)
    {
        var lines = await _unitOfWork.Repository<VendorInvoiceLineItem>().GetQueryable(l =>
            l.TenantId == TenantId && l.VendorInvoiceId == invoice.Id && !l.IsDeleted).AsNoTracking().ToListAsync(token);
        if (!lines.Any(l => l.LandedCostItemId.HasValue)) return false;
        var sourceIds = lines.Where(l => l.LandedCostItemId.HasValue).Select(l => l.LandedCostItemId!.Value).ToArray();
        var voucherIds = await _unitOfWork.Repository<LandedCostItem>().GetQueryable(i =>
            i.TenantId == TenantId && sourceIds.Contains(i.Id) && !i.IsDeleted)
            .Select(i => i.LandedCostId).Distinct().OrderBy(i => i).ToListAsync(token);
        var invoiceId = invoice.Id;
        // Keep source links as history on the deleted draft; only the unique active-line reservation is released.
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            try
            {
                _unitOfWork.ClearTrackedChanges();
                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token);
                foreach (var voucherId in voucherIds)
                    await _unitOfWork.AcquireTransactionLockAsync($"LandedCostInvoice:{TenantId:N}:{voucherId:N}", token);
                invoice = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(i =>
                    i.Id == invoiceId && i.TenantId == TenantId && !i.IsDeleted).SingleOrDefaultAsync(token)
                    ?? throw new InvalidOperationException("The invoice draft is no longer available.");
                if (invoice.JournalEntryId.HasValue || invoice.Status != VendorInvoiceStatus.Draft)
                    throw new InvalidOperationException("Only an unposted draft can be deleted.");
                lines = await _unitOfWork.Repository<VendorInvoiceLineItem>().GetQueryable(l =>
                    l.TenantId == TenantId && l.VendorInvoiceId == invoiceId && !l.IsDeleted).ToListAsync(token);
                var ids = lines.Where(l => l.LandedCostItemId.HasValue).Select(l => l.LandedCostItemId!.Value).ToArray();
                if (!ids.OrderBy(i => i).SequenceEqual(sourceIds.OrderBy(i => i)))
                    throw new InvalidOperationException("The invoice source changed. Refresh before deleting the draft.");
                var items = await _unitOfWork.Repository<LandedCostItem>().GetQueryable(i =>
                    i.TenantId == TenantId && ids.Contains(i.Id) && !i.IsDeleted).ToListAsync(token);
                foreach (var item in items.Where(i => i.InvoiceNumber == invoice.InvoiceNumber))
                { item.InvoiceNumber = null; item.InvoiceDate = null; item.UpdatedAt = DateTime.UtcNow; item.LastModifiedById = CurrentUserId; }
                foreach (var line in lines) await _unitOfWork.Repository<VendorInvoiceLineItem>().DeleteAsync(line);
                await _unitOfWork.Repository<VendorInvoice>().DeleteAsync(invoice);
                await _unitOfWork.SaveChangesAsync(token); await _unitOfWork.CommitAsync(token);
            }
            catch { await _unitOfWork.RollbackAsync(token); _unitOfWork.ClearTrackedChanges(); throw; }
        }, token);
        return true;
    }
}
