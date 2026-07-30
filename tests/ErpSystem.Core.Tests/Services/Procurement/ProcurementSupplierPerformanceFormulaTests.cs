using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierPerformanceFormulaTests
{
    [Fact]
    public void Calculate_renormalizes_available_dimensions_without_inventing_missing_data()
    {
        var measures = Measures(
            80m,
            60m,
            null,
            null,
            null,
            null,
            100m);

        var result = ProcurementSupplierPerformanceFormula.Calculate(measures, 50m);

        result.CoveragePercent.Should().Be(50m);
        result.OverallScore.Should().Be(82m);
        result.DataComplete.Should().BeTrue();
        measures[0].AppliedWeightPercent.Should().Be(30m);
        measures[0].WeightedContribution.Should().Be(24m);
        measures[2].AppliedWeightPercent.Should().BeNull();
        measures[2].WeightedContribution.Should().BeNull();
    }

    [Fact]
    public void Calculate_flags_insufficient_coverage_even_when_a_score_can_be_calculated()
    {
        var measures = Measures(
            100m,
            null,
            null,
            null,
            null,
            null,
            null);

        var result = ProcurementSupplierPerformanceFormula.Calculate(measures, 60m);

        result.CoveragePercent.Should().Be(15m);
        result.OverallScore.Should().Be(100m);
        result.DataComplete.Should().BeFalse();
    }

    [Fact]
    public void Calculate_returns_no_score_when_there_is_no_qualifying_activity()
    {
        var measures = Measures(null, null, null, null, null, null, null);

        var result = ProcurementSupplierPerformanceFormula.Calculate(measures, 60m);

        result.CoveragePercent.Should().Be(0m);
        result.OverallScore.Should().BeNull();
        result.DataComplete.Should().BeFalse();
        measures.Should().OnlyContain(item =>
            item.AppliedWeightPercent == null &&
            item.WeightedContribution == null);
    }

    [Fact]
    public void Calculate_rejects_policy_weights_that_do_not_total_one_hundred()
    {
        var measures = Measures(80m, 70m, 60m, 50m, 40m, 30m, 20m);
        measures[0].WeightPercent = 14m;

        var action = () =>
            ProcurementSupplierPerformanceFormula.Calculate(measures, 60m);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*weights must total 100 percent*");
    }

    [Fact]
    public void Evidence_formula_uses_all_seven_authoritative_source_families()
    {
        var now = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var requisitionItem = new PurchaseRequisitionItem
        {
            Id = Guid.NewGuid(),
            RequisitionId = Guid.NewGuid(),
            InventoryItemId = Guid.NewGuid(),
            ItemDescription = "Test item",
            Quantity = 10,
            UnitOfMeasure = "EA",
            EstimatedUnitPrice = 100
        };
        var rfqItem = new RequestForQuotationItem
        {
            Id = Guid.NewGuid(),
            RfqId = Guid.NewGuid(),
            SourcePurchaseRequisitionItemId = requisitionItem.Id,
            InventoryItemId = requisitionItem.InventoryItemId,
            Description = "Test item",
            Quantity = 10,
            UnitOfMeasure = "EA"
        };
        var orderLine = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            InventoryItemId = requisitionItem.InventoryItemId,
            ItemDescription = "Test item",
            OrderedQuantity = 10,
            UnitOfMeasure = "EA",
            UnitPrice = 110,
            SourceRfqItemId = rfqItem.Id
        };
        var receipt = new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = "GRN-001",
            ReceiptDate = now.AddDays(-8),
            Status = "Accepted",
            InspectionResult = "Passed"
        };
        receipt.Items.Add(new PurchaseOrderReceiptItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderItemId = orderLine.Id,
            ReceivedQuantity = 10,
            AcceptedQuantity = 8,
            RejectedQuantity = 2,
            QualityStatus = "Passed"
        });
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = "PO-001",
            OrderDate = now.AddDays(-20),
            PromisedDate = now.AddDays(-10),
            Status = "Received",
            SourceRfqId = rfqItem.RfqId
        };
        order.Items.Add(orderLine);
        order.Receipts.Add(receipt);
        var invitations = new[]
        {
            new RequestForQuotationInvitation
            {
                Id = Guid.NewGuid(),
                RfqId = rfqItem.RfqId,
                BusinessPartnerId = Guid.NewGuid(),
                InvitedAt = now.AddDays(-4),
                RespondedAt = now.AddDays(-3)
            }
        };
        var incidents = new[]
        {
            new QualityIncident
            {
                Id = Guid.NewGuid(),
                BusinessPartnerId = Guid.NewGuid(),
                IncidentNumber = "QI-001",
                IncidentDate = now.AddDays(-5),
                IncidentType = "Defect",
                Description = "Damaged unit",
                Status = "Resolved",
                ReportedDate = now.AddDays(-5),
                RequiresSupplierResponse = true,
                SupplierResponseDate = now.AddDays(-2)
            }
        };
        var contracts = new[]
        {
            new Contract
            {
                Id = Guid.NewGuid(),
                ContractNumber = "CON-001",
                ContractTitle = "Supply",
                Status = "Completed",
                EndDate = now.AddDays(-3),
                CompletedAt = now.AddDays(-2),
                CreatedAt = now.AddMonths(-2)
            }
        };

        var result =
            ProcurementSupplierPerformanceScorecardService.BuildObservations(
                [order],
                [rfqItem],
                [requisitionItem],
                invitations,
                incidents,
                contracts,
                48,
                now);

        result[ProcurementSupplierPerformanceMetricKey.DeliveryTimeliness]
            .Score.Should().Be(90m);
        result[ProcurementSupplierPerformanceMetricKey.GrnQuality]
            .Score.Should().Be(100m);
        result[ProcurementSupplierPerformanceMetricKey.RejectionRate]
            .Score.Should().Be(80m);
        result[ProcurementSupplierPerformanceMetricKey.PriceCompetitiveness]
            .Score.Should().Be(90m);
        result[ProcurementSupplierPerformanceMetricKey.Responsiveness]
            .Score.Should().Be(83.33m);
        result[ProcurementSupplierPerformanceMetricKey.ComplaintResolution]
            .Score.Should().Be(100m);
        result[ProcurementSupplierPerformanceMetricKey.ContractCompletion]
            .Score.Should().Be(98m);
        result.Values.Should().OnlyContain(item =>
            item.Score != null && item.Count > 0 && item.MissingReason == null);
    }

    private static List<ProcurementSupplierPerformanceMeasureDto> Measures(
        decimal? delivery,
        decimal? quality,
        decimal? rejection,
        decimal? price,
        decimal? responsiveness,
        decimal? complaints,
        decimal? contracts) =>
    [
        Measure(ProcurementSupplierPerformanceMetricKey.DeliveryTimeliness, 15m, delivery),
        Measure(ProcurementSupplierPerformanceMetricKey.GrnQuality, 15m, quality),
        Measure(ProcurementSupplierPerformanceMetricKey.RejectionRate, 15m, rejection),
        Measure(ProcurementSupplierPerformanceMetricKey.PriceCompetitiveness, 15m, price),
        Measure(ProcurementSupplierPerformanceMetricKey.Responsiveness, 10m, responsiveness),
        Measure(ProcurementSupplierPerformanceMetricKey.ComplaintResolution, 10m, complaints),
        Measure(ProcurementSupplierPerformanceMetricKey.ContractCompletion, 20m, contracts)
    ];

    private static ProcurementSupplierPerformanceMeasureDto Measure(
        ProcurementSupplierPerformanceMetricKey metric,
        decimal weight,
        decimal? score) =>
        new()
        {
            Metric = metric,
            WeightPercent = weight,
            Score = score
        };
}
