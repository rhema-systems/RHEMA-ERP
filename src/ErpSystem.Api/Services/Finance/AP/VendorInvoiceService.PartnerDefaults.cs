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
        Guid businessPartnerId, Guid? purchaseOrderId = null, CancellationToken cancellationToken = default, DateTime? invoiceDate = null, Guid? businessPartnerRoleId = null)
    {
        var date = (invoiceDate ?? DateTime.UtcNow).Date;
        var canonical = await ResolveCanonicalApPartnerAsync(businessPartnerId, businessPartnerRoleId, date, cancellationToken);
        var partner = canonical.Partner;
        var profile = canonical.Profile;
        if (purchaseOrderId.HasValue)
        {
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetQueryable(order =>
                order.Id == purchaseOrderId.Value && order.TenantId == TenantId && !order.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("The purchase order was not found in the current tenant.");
            if (order.BusinessPartnerId != partner.Id)
                throw new InvalidOperationException("The purchase order defaults belong to a different Business Partner.");
        }
        var term = await ResolvePaymentTermAsync(profile.PaymentTermId, "Business Partner AP profile", cancellationToken);
        var defaults = new BusinessPartnerPostingDefaultsDto
        {
            DefaultExpenseAccountId = profile.DefaultExpenseAccountId,
            DefaultTaxGroupId = profile.DefaultTaxGroupId,
            SubjectToWithholdingDeduction = profile.SubjectToWithholding
        };
        var withholding = new SupplierWithholdingDefaultDto { Required = profile.SubjectToWithholding };
        if (profile.SubjectToWithholding)
        {
            var choice = profile.WithholdingDefaults.SingleOrDefault(line => !line.IsDeleted && line.IsActive &&
                line.IsDefaultForAp);
            if (choice == null) throw new InvalidOperationException("AP_WHT_DEFAULT_REQUIRED: Select the approved profile's default AP withholding configuration.");
            var resolved = await ResolveInvoiceWhtAsync(choice.WithholdingTaxId, 0m, date, false, cancellationToken);
            defaults.DefaultWithholdingTaxId = resolved.TaxId;
            defaults.WithholdingTaxRate = resolved.Rate;
            withholding.TaxId = resolved.TaxId;
            withholding.Rate = resolved.Rate;
            withholding.TaxPayableAccountId = resolved.TaxPayableAccountId;
        }
        return new PurchaseOrderSupplierDefaultsDto
        {
            BusinessPartnerId = partner.Id, PaymentTermId = profile.PaymentTermId,
            PaymentTermsDays = term?.DueDays, Tin = partner.TaxIdentificationNumber,
            PostingDefaults = defaults, WithholdingDefault = withholding
        };
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
                    dto.BusinessPartnerId, dto.PurchaseOrderId, dto.InvoiceDate, dto.WithholdingTaxId.Value, cancellationToken, dto.BusinessPartnerRoleId);
            dto.ApplySupplierWithholdingDefaults = true;
            return false;
        }
        var source = await GetSupplierDefaultsAsync(dto.BusinessPartnerId, dto.PurchaseOrderId, cancellationToken, dto.InvoiceDate, dto.BusinessPartnerRoleId);
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
                    invoice.BusinessPartnerId, dto.PurchaseOrderId, dto.InvoiceDate, dto.WithholdingTaxId.Value, cancellationToken, invoice.BusinessPartnerRoleId);
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
            var source = await GetSupplierDefaultsAsync(invoice.BusinessPartnerId, dto.PurchaseOrderId, cancellationToken, dto.InvoiceDate, invoice.BusinessPartnerRoleId);
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
        DateTime invoiceDate, Guid selectedTaxId, CancellationToken cancellationToken, Guid? businessPartnerRoleId = null)
    {
        var source = await GetSupplierDefaultsAsync(supplierId, orderId, cancellationToken, invoiceDate, businessPartnerRoleId);
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

    /// <summary>
    /// Called only while creating a new draft, and only by clients opting in.
    /// Explicit account, term, tax, exemption and no-default selections are retained.
    /// Supplier withholding has its own automatic create hook and does not use this opt-in.
    /// </summary>
    private async Task<(int? PaymentTermsDays, Guid? TaxFallbackAccountId)> ApplyBusinessPartnerCreateDefaultsAsync(
        VendorInvoiceCreateDto dto, BusinessPartner supplier, CancellationToken cancellationToken)
    {
        if (dto.ApplyBusinessPartnerDefaults != true || dto.IsOpeningBalance) return (null, null);
        var source = await GetSupplierDefaultsAsync(dto.BusinessPartnerId, dto.PurchaseOrderId, cancellationToken, dto.InvoiceDate, dto.BusinessPartnerRoleId);
        if (source == null) return (null, null);
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
            // The effective approved AP profile owns default payment terms.
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
        return (capturedDays, null);
    }
}
