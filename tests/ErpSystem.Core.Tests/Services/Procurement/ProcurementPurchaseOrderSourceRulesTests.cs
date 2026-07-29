using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderSourceRulesTests
{
    [Theory]
    [InlineData(ProcurementPurchaseOrderSourceType.RfqAward)]
    [InlineData(ProcurementPurchaseOrderSourceType.TenderAward)]
    [InlineData(ProcurementPurchaseOrderSourceType.Contract)]
    [InlineData(ProcurementPurchaseOrderSourceType.ApprovedException)]
    public void OrdinaryPurchaseOrderSourcesAreExplicit(
        ProcurementPurchaseOrderSourceType sourceType)
    {
        ProcurementPurchaseOrderSourceRules.CanCreate(sourceType).Should().BeTrue();
    }

    [Theory]
    [InlineData(ProcurementPurchaseOrderSourceType.FrameworkCallOff)]
    [InlineData(ProcurementPurchaseOrderSourceType.HistoricalMigration)]
    public void DedicatedAndMigrationSourcesCannotUseOrdinaryCreation(
        ProcurementPurchaseOrderSourceType sourceType)
    {
        ProcurementPurchaseOrderSourceRules.CanCreate(sourceType).Should().BeFalse();
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Pending Approval")]
    [InlineData("Approved")]
    [InlineData("Sent")]
    [InlineData("Acknowledged")]
    public void ApprovalAndIssueBoundariesRequireRevalidation(string status)
    {
        ProcurementPurchaseOrderSourceRules.RequiresRevalidation(status)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("PartiallyReceived")]
    [InlineData("Received")]
    [InlineData("Cancelled")]
    public void ReceivingAndTerminalStatusAreOutsideThisSourceGate(string status)
    {
        ProcurementPurchaseOrderSourceRules.RequiresRevalidation(status)
            .Should().BeFalse();
    }

    [Fact]
    public void AwardReadinessSupplierMustBeAnExactGuidMember()
    {
        var supplier = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new[] { supplier, Guid.NewGuid() });

        ProcurementPurchaseOrderSourceRules.ContainsApprovedSupplier(json, supplier)
            .Should().BeTrue();
        ProcurementPurchaseOrderSourceRules.ContainsApprovedSupplier(
                json, Guid.NewGuid())
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("not-json")]
    public void MalformedSupplierLineageFailsClosed(string json)
    {
        ProcurementPurchaseOrderSourceRules.ContainsApprovedSupplier(
                json, Guid.NewGuid())
            .Should().BeFalse();
    }

    [Fact]
    public void OneTimeAwardRequiresExactAuthoritativeLinesAndTotal()
    {
        var itemId = Guid.NewGuid();
        var result = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            ProcurementPurchaseOrderSourceType.TenderAward,
            [
                ApprovedLine(itemId, "Medical supplies", 4m, 25m)
            ],
            [
                OrderLine(itemId, "Medical supplies", 4m, 25m)
            ],
            100m,
            100m,
            "GHS",
            "GHS");

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(5, 25, "PO_SOURCE_LINE_QUANTITY_MISMATCH")]
    [InlineData(4, 30, "PO_SOURCE_LINE_PRICE_MISMATCH")]
    public void AwardCannotAuthorizeChangedQuantityOrPrice(
        decimal quantity,
        decimal price,
        string expectedCode)
    {
        var itemId = Guid.NewGuid();
        var result = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            ProcurementPurchaseOrderSourceType.TenderAward,
            [ApprovedLine(itemId, "Medical supplies", 4m, 25m)],
            [OrderLine(itemId, "Medical supplies", quantity, price)],
            100m,
            quantity * price,
            "GHS",
            "GHS");

        result.IsValid.Should().BeFalse();
        result.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void AwardCannotAuthorizeAnUnapprovedItem()
    {
        var result = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            ProcurementPurchaseOrderSourceType.ApprovedException,
            [ApprovedLine(Guid.NewGuid(), "Approved item", 1m, 100m)],
            [OrderLine(Guid.NewGuid(), "Different item", 1m, 100m)],
            100m,
            100m,
            "GHS",
            "GHS");

        result.IsValid.Should().BeFalse();
        result.Code.Should().Be("PO_SOURCE_LINE_NOT_APPROVED");
    }

    [Fact]
    public void AwardCannotOmitAnApprovedLine()
    {
        var firstItemId = Guid.NewGuid();
        var result = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            ProcurementPurchaseOrderSourceType.RfqAward,
            [
                ApprovedLine(firstItemId, "First item", 1m, 40m),
                ApprovedLine(Guid.NewGuid(), "Second item", 1m, 60m)
            ],
            [OrderLine(firstItemId, "First item", 1m, 40m)],
            100m,
            100m,
            "GHS",
            "GHS");

        result.IsValid.Should().BeFalse();
        result.Code.Should().Be("PO_SOURCE_LINE_SET_MISMATCH");
    }

    [Fact]
    public void AwardCannotChangeUnitOfMeasure()
    {
        var itemId = Guid.NewGuid();
        var changedLine = new ProcurementPurchaseOrderSourceOrderLine
        {
            InventoryItemId = itemId,
            ItemDescription = "Approved item",
            OrderedQuantity = 1m,
            UnitOfMeasure = "BOX",
            UnitPrice = 100m
        };

        var result = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            ProcurementPurchaseOrderSourceType.TenderAward,
            [ApprovedLine(itemId, "Approved item", 1m, 100m)],
            [changedLine],
            100m,
            100m,
            "GHS",
            "GHS");

        result.IsValid.Should().BeFalse();
        result.Code.Should().Be("PO_SOURCE_LINE_NOT_APPROVED");
    }

    [Fact]
    public void AwardCannotChangeTheApprovedHeaderTotal()
    {
        var itemId = Guid.NewGuid();
        var result = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            ProcurementPurchaseOrderSourceType.ApprovedException,
            [ApprovedLine(itemId, "Approved item", 1m, 100m)],
            [OrderLine(itemId, "Approved item", 1m, 100m)],
            125m,
            100m,
            "GHS",
            "GHS");

        result.IsValid.Should().BeFalse();
        result.Code.Should().Be("PO_SOURCE_TOTAL_MISMATCH");
    }

    [Fact]
    public void ContractAllowsAControlledPartialOrderButNotAnExcess()
    {
        var itemId = Guid.NewGuid();
        var approved = new[] { ApprovedLine(itemId, "Contract item", 10m, 20m) };

        ProcurementPurchaseOrderSourceRules.ValidateOrder(
                ProcurementPurchaseOrderSourceType.Contract,
                approved,
                [OrderLine(itemId, "Contract item", 5m, 20m)],
                200m,
                100m,
                "GHS",
                "GHS")
            .IsValid.Should().BeTrue();

        var excess = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            ProcurementPurchaseOrderSourceType.Contract,
            approved,
            [OrderLine(itemId, "Contract item", 11m, 20m)],
            200m,
            220m,
            "GHS",
            "GHS");
        excess.IsValid.Should().BeFalse();
        excess.Code.Should().Be("PO_SOURCE_LINE_QUANTITY_MISMATCH");
    }

    [Fact]
    public void SourceCurrencyCannotBeChanged()
    {
        var itemId = Guid.NewGuid();
        var result = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            ProcurementPurchaseOrderSourceType.RfqAward,
            [ApprovedLine(itemId, "RFQ item", 1m, 10m)],
            [OrderLine(itemId, "RFQ item", 1m, 10m)],
            10m,
            10m,
            "GHS",
            "USD");

        result.IsValid.Should().BeFalse();
        result.Code.Should().Be("PO_SOURCE_CURRENCY_MISMATCH");
    }

    private static ProcurementPurchaseOrderSourceLineDto ApprovedLine(
        Guid inventoryItemId,
        string description,
        decimal quantity,
        decimal unitPrice) =>
        new()
        {
            SourceLineId = Guid.NewGuid(),
            InventoryItemId = inventoryItemId,
            Description = description,
            Quantity = quantity,
            UnitOfMeasure = "EA",
            UnitPrice = unitPrice,
            LineTotal = quantity * unitPrice
        };

    private static ProcurementPurchaseOrderSourceOrderLine OrderLine(
        Guid inventoryItemId,
        string description,
        decimal quantity,
        decimal unitPrice) =>
        new()
        {
            InventoryItemId = inventoryItemId,
            ItemDescription = description,
            OrderedQuantity = quantity,
            UnitOfMeasure = "EA",
            UnitPrice = unitPrice
        };
}
