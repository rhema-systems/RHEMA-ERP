using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private async Task ValidateEstateCreateAsync(VendorInvoiceCreateDto dto, CancellationToken ct)
    {
        if (!dto.EstateAcquisitionId.HasValue && !dto.EstatePayableKind.HasValue) return;
        if (!dto.EstateAcquisitionId.HasValue || !dto.EstatePayableKind.HasValue ||
            !Enum.IsDefined(dto.EstatePayableKind.Value) || dto.IsOpeningBalance || dto.PurchaseOrderId.HasValue ||
            dto.AcceptedSupplyKind.HasValue || dto.AutoInvoiceRequestId.HasValue)
            throw new InvalidOperationException("Invalid Estate supplier invoice source.");
        var acquisition = await _unitOfWork.Repository<LandAcquisition>().GetQueryable(a =>
            a.Id == dto.EstateAcquisitionId && a.TenantId == TenantId && !a.IsDeleted).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The Estate acquisition was not found in this tenant.");
        var stage = dto.EstatePayableKind.Value switch
        {
            EstatePayableKind.SurveyorFee => AcquisitionProcedure.CadastralSurvey,
            EstatePayableKind.VendorConsideration => AcquisitionProcedure.VendorPayment,
            _ => AcquisitionProcedure.StampDutyPayment
        };
        if (acquisition.StageOrder != (int)stage)
            throw new InvalidOperationException("The Estate acquisition is not at the required payable stage.");
        if (await _unitOfWork.Repository<VendorInvoice>().GetQueryable(i => i.TenantId == TenantId &&
                i.EstateAcquisitionId == dto.EstateAcquisitionId && i.EstatePayableKind == dto.EstatePayableKind).AnyAsync(ct))
            throw new InvalidOperationException("This Estate source already has a supplier invoice. Reload the acquisition to use it.");
    }

    private async Task ValidateEstateSourceAsync(VendorInvoice invoice, CancellationToken ct)
    {
        if (!invoice.EstateAcquisitionId.HasValue) return;
        var source = await _unitOfWork.Repository<LandAcquisition>().GetQueryable(a => a.TenantId == TenantId &&
                a.Id == invoice.EstateAcquisitionId && !a.IsDeleted).Include(a => a.StampDutyAssessment)
            .Include(a => a.NegotiationOffers).SingleOrDefaultAsync(ct);
        if (!invoice.EstatePayableKind.HasValue || !Enum.IsDefined(invoice.EstatePayableKind.Value) || source == null)
            throw new InvalidOperationException("The Estate source is unavailable. Review the acquisition before continuing.");
        decimal? net = null;
        try
        {
            using var json = JsonDocument.Parse(source.WorkspaceDataJson ?? "{}");
            var stage = invoice.EstatePayableKind == EstatePayableKind.SurveyorFee ? AcquisitionProcedure.CadastralSurvey :
                invoice.EstatePayableKind == EstatePayableKind.VendorConsideration ? AcquisitionProcedure.AgreementNegotiation : AcquisitionProcedure.StampDutyPayment;
            if (invoice.EstatePayableKind == EstatePayableKind.StampDuty)
                net = source.StampDutyAssessment?.IsApproved == true ? source.StampDutyAssessment.DutyAmount : null;
            else if (invoice.EstatePayableKind == EstatePayableKind.VendorConsideration)
            {
                if (json.RootElement.TryGetProperty(((int)stage).ToString(), out var negotiation))
                    net = EstateSnapshotDecimal(negotiation, "agreementPaymentAmount") ?? EstateSnapshotDecimal(negotiation, "negotiatedValue");
                net ??= source.NegotiationOffers.Where(offer => !offer.IsDeleted && offer.TenantId == TenantId && offer.NegotiatedValue.HasValue)
                    .OrderByDescending(offer => offer.UpdatedAt ?? offer.CreatedAt).Select(offer => offer.NegotiatedValue).FirstOrDefault();
            }
            else if (json.RootElement.TryGetProperty(((int)stage).ToString(), out var values))
            {
                if (invoice.EstatePayableKind == EstatePayableKind.OtherAcquisitionCosts)
                {
                    if (values.TryGetProperty("otherAcquisitionServicesJson", out var servicesValue) && servicesValue.ValueKind == JsonValueKind.String)
                    {
                        var services = JsonSerializer.Deserialize<List<EstateServiceCost>>(servicesValue.GetString() ?? "[]",
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                        net = services.Where(row => !string.IsNullOrWhiteSpace(row.ServiceName) && row.Amount > 0).Sum(row => row.Amount);
                    }
                }
                else net = EstateSnapshotDecimal(values, "surveyorFeeAmount");
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidOperationException("The Estate payable source evidence is invalid. Review the acquisition before continuing.", error);
        }
        if (!net.HasValue || decimal.Round(net.Value, 2) != decimal.Round(invoice.SubTotal, 2))
            throw new InvalidOperationException("The Estate source amount changed or is unavailable. Reconcile the acquisition and invoice before continuing.");
    }

    private sealed record EstateServiceCost(string? ServiceName, decimal Amount);

    private static decimal? EstateSnapshotDecimal(JsonElement values, string key)
    {
        if (!values.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return value.ValueKind == JsonValueKind.Number ? value.GetDecimal() :
            value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static void ValidateEstateInvoiceUpdate(VendorInvoice invoice, VendorInvoiceUpdateDto dto)
    {
        if (!invoice.EstateAcquisitionId.HasValue) return;
        var lines = invoice.LineItems.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        if (dto.IsOpeningBalance || dto.PurchaseOrderId.HasValue || dto.AcceptedSupplyKind.HasValue ||
            dto.AcceptedSupplySourceId.HasValue || dto.Reference != invoice.Reference ||
            dto.CurrencyCode != invoice.CurrencyCode || dto.ExchangeRate != invoice.ExchangeRate ||
            dto.ExchangeRateId != invoice.ExchangeRateId || dto.LineItems == null || dto.LineItems.Count != lines.Count ||
            dto.LineItems.Select(l => l.Id).Distinct().Count() != lines.Count || dto.LineItems.Any(l =>
                !l.Id.HasValue || !lines.TryGetValue(l.Id.Value, out var original) ||
                l.LineItemType != original.LineItemType || l.Description != original.Description ||
                l.Quantity != original.Quantity || l.UnitPrice != original.UnitPrice ||
                l.DiscountPercentage != original.DiscountPercentage || l.GLAccountId != original.GLAccountId ||
                l.FixedAssetId != original.FixedAssetId || l.Unit != original.Unit ||
                l.PurchaseOrderItemId != original.PurchaseOrderItemId || l.BudgetEntryId != original.BudgetEntryId ||
                l.LandedCostItemId != original.LandedCostItemId))
            throw new InvalidOperationException("Estate source amounts, accounts, currency and lineage cannot be changed in the supplier invoice. Correct the acquisition source instead. Invoice tax treatment may be reviewed here.");
    }
}
