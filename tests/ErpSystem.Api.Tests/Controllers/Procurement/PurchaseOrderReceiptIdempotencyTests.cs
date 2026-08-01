using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class PurchaseOrderReceiptIdempotencyTests
{
    [Fact]
    public void ReceiptReplayMatches_AcceptsIdenticalEffectivePayload()
    {
        var (receipt, request) = CreateMatchingPayload();

        Matches(receipt, request).Should().BeTrue();
    }

    [Fact]
    public void ReceiptReplayMatches_RejectsChangedQuantityLocationOrDeliveryData()
    {
        var (receipt, request) = CreateMatchingPayload();

        request.Items[0].ReceivedQuantity += 1m;
        Matches(receipt, request).Should().BeFalse();

        (receipt, request) = CreateMatchingPayload();
        request.Items[0].LocationId = Guid.NewGuid();
        Matches(receipt, request).Should().BeFalse();

        (receipt, request) = CreateMatchingPayload();
        request.DeliveryNote = "DN-CHANGED";
        Matches(receipt, request).Should().BeFalse();
    }

    private static bool Matches(
        PurchaseOrderReceipt receipt,
        ReceivePurchaseOrderDto request)
    {
        var method = typeof(PurchaseOrdersController).GetMethod(
            "ReceiptReplayMatches",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(
                nameof(PurchaseOrdersController),
                "ReceiptReplayMatches");
        return (bool)method.Invoke(null, [receipt, request])!;
    }

    private static (PurchaseOrderReceipt Receipt, ReceivePurchaseOrderDto Request)
        CreateMatchingPayload()
    {
        var purchaseOrderId = Guid.NewGuid();
        var purchaseOrderItemId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.Date.AddMonths(6);
        var receipt = new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = purchaseOrderId,
            ReceiptNumber = "RCV-IDEMPOTENCY-001",
            DeliveryNote = "DN-001",
            CarrierName = "Carrier",
            TrackingNumber = "TRACK-001",
            Notes = "Handle with care",
            InspectedById = inspectorId,
            Items =
            [
                new PurchaseOrderReceiptItem
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderItemId = purchaseOrderItemId,
                    ReceivedQuantity = 5m,
                    LocationId = locationId,
                    SerialNumber = "SERIAL-001",
                    LotNumber = "LOT-001",
                    ExpirationDate = expirationDate,
                    Notes = "Line note"
                }
            ]
        };
        var request = new ReceivePurchaseOrderDto
        {
            PurchaseOrderId = purchaseOrderId,
            DeliveryNote = receipt.DeliveryNote,
            CarrierName = receipt.CarrierName,
            TrackingNumber = receipt.TrackingNumber,
            Notes = receipt.Notes,
            InspectedById = inspectorId,
            Items =
            [
                new ReceivePurchaseOrderItemDto
                {
                    PurchaseOrderItemId = purchaseOrderItemId,
                    ReceivedQuantity = 5m,
                    LocationId = locationId,
                    SerialNumber = "SERIAL-001",
                    LotNumber = "LOT-001",
                    ExpirationDate = expirationDate,
                    Notes = "Line note"
                }
            ]
        };
        return (receipt, request);
    }
}
