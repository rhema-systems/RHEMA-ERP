using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    /// <summary>Read-only projection; never creates or updates a Finance supplier identity.</summary>
    public async Task<PurchaseOrderSupplierDefaultsDto?> GetSupplierDefaultsAsync(
        Guid supplierId, Guid? purchaseOrderId = null, CancellationToken cancellationToken = default, DateTime? invoiceDate = null)
    {
        var partner = await _unitOfWork.Repository<BusinessPartner>().GetQueryable(candidate =>
            candidate.Id == supplierId && candidate.TenantId == TenantId && !candidate.IsDeleted)
            .Include(candidate => candidate.PaymentTerm).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        Supplier? supplier = null;
        if (partner == null)
        {
            supplier = await _unitOfWork.Repository<Supplier>().GetQueryable(candidate =>
                candidate.Id == supplierId && candidate.TenantId == TenantId && !candidate.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (supplier == null) throw new KeyNotFoundException("The selected supplier was not found in the current tenant.");
            var matches = await _unitOfWork.Repository<BusinessPartner>().GetQueryable(candidate =>
                candidate.TenantId == TenantId && !candidate.IsDeleted &&
                (candidate.Id == supplier.Id ||
                 (!string.IsNullOrWhiteSpace(supplier.SupplierCode) && candidate.PartnerCode == supplier.SupplierCode)))
                .Include(candidate => candidate.PaymentTerm).AsNoTracking().Take(2).ToListAsync(cancellationToken);
            if (matches.Count > 1)
                throw new InvalidOperationException("The supplier maps to multiple business partners. Resolve the supplier mapping before applying defaults.");
            partner = matches.SingleOrDefault();
        }

        if (purchaseOrderId.HasValue)
        {
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetQueryable(order =>
                order.Id == purchaseOrderId.Value && order.TenantId == TenantId && !order.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("The purchase order was not found in the current tenant.");
            if (partner == null || order.BusinessPartnerId != partner.Id)
                throw new InvalidOperationException("The purchase order defaults belong to a different supplier.");
            var snapshot = BusinessPartnerPostingDefaults.ReadSnapshot(order.SupplierDefaultsSnapshotJson);
            if (snapshot != null)
            {
                if (snapshot.BusinessPartnerId != order.BusinessPartnerId)
                    throw new InvalidOperationException("The saved purchase order supplier defaults are inconsistent. Review the purchase order.");
                return await WithholdingProjectionAsync(snapshot, partner, invoiceDate, cancellationToken);
            }
        }
        return partner == null ? null : await WithholdingProjectionAsync(
            BusinessPartnerPostingDefaults.Snapshot(partner), partner, invoiceDate, cancellationToken);
    }

    private async Task<PurchaseOrderSupplierDefaultsDto> WithholdingProjectionAsync(
        PurchaseOrderSupplierDefaultsDto source, BusinessPartner partner, DateTime? invoiceDate, CancellationToken cancellationToken)
    {
        // Terms and posting accounts retain the PO snapshot. A new invoice's WHT
        // decision is based on the supplier's current designation, not an old PO.
        source.WithholdingDefault = await ResolveSupplierWithholdingDefaultAsync(
            BusinessPartnerPostingDefaults.FromPartner(partner), (invoiceDate ?? DateTime.UtcNow).Date, cancellationToken);
        return source;
    }

    /// <summary>Withholding needs a transaction decision, independent of optional VAT/account defaults.</summary>
    private async Task<bool> ApplySupplierWithholdingCreateDefaultAsync(
        VendorInvoiceCreateDto dto, bool deferDecision, CancellationToken cancellationToken)
    {
        if (dto.IsOpeningBalance) { dto.ApplySupplierWithholdingDefaults = null; dto.WithholdingTaxRateOverride = null; return false; }
        if (dto.ApplySupplierWithholdingDefaults == false)
        {
            dto.WithholdingTaxId = null; dto.WithholdingTaxRate = 0m; dto.WithholdingTaxAccountId = null;
            dto.WithholdingTaxRateOverride = null;
            return false;
        }
        ValidateInvoiceWithholdingOverride(dto.WithholdingTaxRateOverride);
        if (dto.WithholdingTaxId.HasValue && dto.WithholdingTaxId != Guid.Empty)
        {
            if (dto.ApplySupplierWithholdingDefaults == true && !dto.WithholdingTaxRateOverride.HasValue)
                dto.WithholdingTaxRateOverride = await ConfirmedSupplierRateAsync(
                    dto.SupplierId, dto.PurchaseOrderId, dto.InvoiceDate, dto.WithholdingTaxId.Value, cancellationToken);
            dto.ApplySupplierWithholdingDefaults = true;
            return false;
        }
        var source = await GetSupplierDefaultsAsync(dto.SupplierId, dto.PurchaseOrderId, cancellationToken, dto.InvoiceDate);
        var withholding = source?.WithholdingDefault;
        if (withholding?.Required != true && dto.ApplySupplierWithholdingDefaults != true) return false;
        if (!dto.ApplySupplierWithholdingDefaults.HasValue)
        {
            if (!deferDecision)
                throw new InvalidOperationException("AP_WHT_CONFIRMATION_REQUIRED: This supplier is subject to withholding. Choose Yes or No for this invoice before saving.");
            dto.WithholdingTaxId = null; dto.WithholdingTaxRate = 0m; dto.WithholdingTaxAccountId = null;
            dto.WithholdingTaxRateOverride = null;
            return true;
        }
        if (withholding?.Message != null || withholding?.TaxId == null)
            throw new InvalidOperationException(withholding?.Message ?? "Select a configured purchase WHT rule for this invoice.");
        dto.WithholdingTaxId = withholding.TaxId;
        dto.WithholdingTaxRate = withholding.Rate;
        dto.WithholdingTaxRateOverride ??= withholding.Rate;
        dto.WithholdingTaxAccountId = withholding.TaxPayableAccountId;
        return false;
    }

    private async Task<bool> ApplySupplierWithholdingUpdateDecisionAsync(
        VendorInvoice invoice, VendorInvoiceUpdateDto dto, CancellationToken cancellationToken)
    {
        if (dto.IsOpeningBalance) { dto.ApplySupplierWithholdingDefaults = null; dto.WithholdingTaxRateOverride = null; return false; }
        dto.ApplySupplierWithholdingDefaults ??= invoice.ApplySupplierWithholdingDefaults;
        if (dto.ApplySupplierWithholdingDefaults == false)
        {
            dto.WithholdingTaxId = null; dto.WithholdingTaxRate = 0m; dto.WithholdingTaxAccountId = null;
            dto.WithholdingTaxRateOverride = null;
            return false;
        }
        ValidateInvoiceWithholdingOverride(dto.WithholdingTaxRateOverride);
        if (dto.WithholdingTaxId.HasValue && dto.WithholdingTaxId != Guid.Empty)
        {
            if (dto.WithholdingTaxId == invoice.WithholdingTaxId)
                dto.WithholdingTaxRateOverride ??= invoice.WithholdingTaxRateOverride ?? invoice.WithholdingTaxRate;
            else if (!invoice.WithholdingTaxId.HasValue && dto.ApplySupplierWithholdingDefaults == true && !dto.WithholdingTaxRateOverride.HasValue)
                dto.WithholdingTaxRateOverride = await ConfirmedSupplierRateAsync(
                    invoice.SupplierId, dto.PurchaseOrderId, dto.InvoiceDate, dto.WithholdingTaxId.Value, cancellationToken);
            dto.ApplySupplierWithholdingDefaults ??= true;
            return false;
        }
        if (dto.ApplySupplierWithholdingDefaults == true)
        {
            if (invoice.WithholdingTaxId.HasValue)
            {
                // Keep the rate the maker previously accepted, including zero.
                // A catalogue edit must not re-rate an unrelated draft edit.
                dto.WithholdingTaxRateOverride ??= invoice.WithholdingTaxRateOverride ?? invoice.WithholdingTaxRate;
                dto.WithholdingTaxId = invoice.WithholdingTaxId;
                return false;
            }
            var source = await GetSupplierDefaultsAsync(invoice.SupplierId, dto.PurchaseOrderId, cancellationToken, dto.InvoiceDate);
            var withholding = source?.WithholdingDefault;
            if (withholding?.Message != null || withholding?.TaxId == null)
                throw new InvalidOperationException(withholding?.Message ?? "Select a configured purchase WHT rule for this invoice.");
            dto.WithholdingTaxId = withholding.TaxId;
            dto.WithholdingTaxRateOverride ??= withholding.Rate;
            return false;
        }
        // An old draft's unspecified choice remains unchanged. Only explicitly deferred
        // generated drafts retain a pending decision that prevents submission/posting.
        return invoice.WithholdingDecisionPending;
    }

    private async Task<decimal?> ConfirmedSupplierRateAsync(Guid supplierId, Guid? orderId,
        DateTime invoiceDate, Guid selectedTaxId, CancellationToken cancellationToken)
    {
        var source = await GetSupplierDefaultsAsync(supplierId, orderId, cancellationToken, invoiceDate);
        var withholding = source?.WithholdingDefault;
        return withholding is { Required: true, Message: null } && withholding.TaxId == selectedTaxId
            ? withholding.Rate : null;
    }

    private static void ValidateInvoiceWithholdingOverride(decimal? rate)
    {
        if (rate.HasValue && (rate.Value < 0m || rate.Value > 100m))
            throw new InvalidOperationException("Invoice WHT rate must be between zero and 100 percent.");
        if (rate.HasValue && decimal.Round(rate.Value, 4) != rate.Value)
            throw new InvalidOperationException("Invoice WHT rate supports up to four decimal places.");
    }

    private async Task<SupplierWithholdingDefaultDto> ResolveSupplierWithholdingDefaultAsync(
        BusinessPartnerPostingDefaultsDto defaults, DateTime invoiceDate, CancellationToken cancellationToken)
    {
        var result = new SupplierWithholdingDefaultDto
        {
            Required = defaults.SubjectToWithholdingDeduction && defaults.WithholdingTaxRate > 0m
        };
        if (!result.Required) return result;
        try
        {
            var selectedTaxId = defaults.DefaultWithholdingTaxId;
            if (!selectedTaxId.HasValue)
            {
                // Legacy masters have only a rate. Resolve only one effective tenant purchase
                // rule; a matching rate alone must never choose between different tax identities.
                var taxes = await _unitOfWork.Repository<Tax>().GetQueryable(tax =>
                    tax.TenantId == TenantId && !tax.IsDeleted && tax.IsActive && tax.Category == TaxCategory.Withholding &&
                    (tax.Applicability == TaxApplicability.Purchases || tax.Applicability == TaxApplicability.Both))
                    .AsNoTracking().ToListAsync(cancellationToken);
                var matches = new List<Guid>();
                foreach (var tax in taxes)
                    if (await ResolveEffectiveInvoiceWhtRateAsync(tax, invoiceDate, cancellationToken) == defaults.WithholdingTaxRate)
                        matches.Add(tax.Id);
                if (matches.Count != 1)
                    throw new InvalidOperationException(matches.Count == 0
                        ? $"No active purchase WHT rule matches the supplier's {defaults.WithholdingTaxRate:0.####}% rate on {invoiceDate:yyyy-MM-dd}. Select its WHT rule in Business Partner details."
                        : $"More than one purchase WHT rule matches the supplier's {defaults.WithholdingTaxRate:0.####}% rate. Select its WHT rule in Business Partner details; no rule has been guessed.");
                selectedTaxId = matches[0];
            }
            // The supplier's entered rate is a default for this transaction. The
            // configured effective rule still owns its identity and payable account.
            var resolved = await ResolveInvoiceWhtAsync(selectedTaxId, 0m, invoiceDate, false, cancellationToken,
                defaults.WithholdingTaxRate);
            var account = await _unitOfWork.Accounts.GetByIdAsync(resolved.TaxPayableAccountId!.Value);
            if (account == null || account.TenantId != TenantId || account.IsDeleted || account.Status != AccountStatus.Active ||
                account.AccountType != AccountType.Liability || (!account.AllowDirectPosting && !account.IsControlAccount) ||
                (account.EffectiveDate.HasValue && account.EffectiveDate.Value.Date > invoiceDate.Date) ||
                (account.ExpirationDate.HasValue && account.ExpirationDate.Value.Date <= invoiceDate.Date))
                throw new InvalidOperationException("The supplier's WHT rule needs an active payable GL account in the current tenant.");
            result.TaxId = resolved.TaxId;
            result.Rate = resolved.Rate;
            result.TaxPayableAccountId = resolved.TaxPayableAccountId;
        }
        catch (InvalidOperationException exception)
        {
            result.Message = exception.Message;
        }
        return result;
    }

    /// <summary>
    /// Called only while creating a new draft, and only by clients opting in.
    /// Explicit account, term, tax, exemption and no-default selections are retained.
    /// Supplier withholding has its own automatic create hook and does not use this opt-in.
    /// </summary>
    private async Task<int?> ApplyBusinessPartnerCreateDefaultsAsync(
        VendorInvoiceCreateDto dto, Supplier supplier, CancellationToken cancellationToken)
    {
        if (dto.ApplyBusinessPartnerDefaults != true || dto.IsOpeningBalance) return null;
        var source = await GetSupplierDefaultsAsync(dto.SupplierId, dto.PurchaseOrderId, cancellationToken, dto.InvoiceDate);
        if (source == null) return null;
        var defaults = source.PostingDefaults;
        // AP control-account authority belongs exclusively to tenant Finance settings. Partner
        // defaults may influence expense/tax/payment treatment but must never route AP control.
        dto.ApAccountId = null;
        dto.ExpenseAccountId ??= defaults.DefaultExpenseAccountId;

        int? capturedDays = null;
        // The old DTO uses 30 as its default. Preserve explicit timing before
        // CreateCore resolves a supplier/global fallback payment-term catalogue.
        if (!dto.PaymentTermId.HasValue && (dto.PaymentTermsDays != 30 || dto.DueDate.HasValue))
        {
            capturedDays = dto.PaymentTermsDays;
        }
        else if (!dto.PaymentTermId.HasValue || dto.PaymentTermId == source.PaymentTermId)
        {
            // A PO's captured term days remain authoritative when its default
            // term is prefilled in the UI, even if the catalogue changes later.
            dto.PaymentTermId ??= source.PaymentTermId;
            capturedDays = source.PaymentTermsDays;
            if (capturedDays.HasValue) dto.PaymentTermsDays = capturedDays.Value;
        }

        if (defaults.DefaultTaxGroupId.HasValue)
        {
            foreach (var line in dto.LineItems.Where(line => line.TaxTreatment == TaxTreatment.Standard &&
                !line.TaxGroupId.HasValue && line.TaxRate == 0m && string.IsNullOrWhiteSpace(line.TaxCode)))
            {
                if (_taxEngine == null)
                    throw new InvalidOperationException("The tax calculation service is unavailable. The default tax schedule cannot be calculated.");
                line.TaxGroupId = defaults.DefaultTaxGroupId;
            }
        }
        return capturedDays;
    }
}
