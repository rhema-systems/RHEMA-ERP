using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class EnterpriseDashboardProjectionTests
{
    [Fact]
    public async Task Operational_projection_counts_every_matching_row_without_a_250_record_page_cap()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        for (var index = 0; index < 300; index++)
        {
            context.PurchaseOrders.Add(new PurchaseOrder
            {
                Id = Guid.NewGuid(), TenantId = tenantId, OrderNumber = $"PO-{index:D4}",
                BusinessPartnerId = Guid.NewGuid(), OrderDate = new DateTime(2026, 7, 10), Status = "Approved"
            });
            context.Tenders.Add(new Tender
            {
                Id = Guid.NewGuid(), TenantId = tenantId, TenderNumber = $"TND-{index:D4}",
                Title = $"Tender {index}", TenderType = "RFQ", Status = "Published",
                PublishDate = new DateTime(2026, 7, 10), SubmissionDeadline = new DateTime(2026, 8, 5)
            });
        }
        context.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OrderNumber = "PO-CLOSED",
            BusinessPartnerId = Guid.NewGuid(), OrderDate = new DateTime(2026, 7, 10), Status = "Closed"
        });
        context.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), OrderNumber = "PO-OTHER-TENANT",
            BusinessPartnerId = Guid.NewGuid(), OrderDate = new DateTime(2026, 7, 10), Status = "Approved"
        });
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);

        var result = await service.GetProcurementQueuesAsync(
            new DateTime(2026, 7, 1), new DateTime(2026, 8, 1));

        Assert.Equal(300, result.OpenPurchaseOrderCount);
        Assert.Equal(300, result.OpenTenderCount);
        Assert.Equal(300, Assert.Single(result.OpenTendersByStatus).Count);
    }

    [Fact]
    public async Task Operational_projection_limits_inventory_counts_to_requester_and_authorized_stores_scopes()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var allowedWarehouseId = Guid.NewGuid();
        var deniedWarehouseId = Guid.NewGuid();
        var allowedLocationId = Guid.NewGuid();
        var deniedLocationId = Guid.NewGuid();
        await using var context = CreateContext();
        context.InventoryRequisitions.AddRange(
            Requisition(tenantId, allowedWarehouseId, allowedLocationId, Guid.NewGuid(), "REQ-ALLOWED", RequisitionStatus.Submitted),
            Requisition(tenantId, deniedWarehouseId, deniedLocationId, Guid.NewGuid(), "REQ-DENIED", RequisitionStatus.Submitted),
            Requisition(tenantId, deniedWarehouseId, deniedLocationId, userId, "REQ-OWN", RequisitionStatus.Submitted),
            Requisition(tenantId, allowedWarehouseId, allowedLocationId, Guid.NewGuid(), "REQ-ISSUE", RequisitionStatus.Approved));
        await context.SaveChangesAsync();
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken _) => new()
            {
                Allowed = request.WarehouseId == allowedWarehouseId && request.LocationId == allowedLocationId,
                PermissionCode = request.PermissionCode,
                WarehouseId = request.WarehouseId,
                LocationId = request.LocationId,
                CorrelationId = correlationId
            });
        var service = CreateService(context, tenantId, userId, access.Object);

        var result = await service.GetInventoryQueuesAsync(
            new DateTime(2026, 7, 1), new DateTime(2026, 8, 1));

        Assert.Equal(2, result.PendingInventoryApprovalCount);
        Assert.Equal(1, result.PendingInventoryIssueCount);
        access.Verify(value => value.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.inventory.read" && request.RequireLocationScope),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SuperAdmin_dashboard_inventory_counts_all_requisitions_only_in_the_validated_tenant()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        await using var context = CreateContext();
        context.InventoryRequisitions.AddRange(
            Requisition(tenantId, warehouseId, locationId, Guid.NewGuid(), "REQ-PENDING", RequisitionStatus.Submitted),
            Requisition(tenantId, warehouseId, locationId, Guid.NewGuid(), "REQ-ISSUE", RequisitionStatus.Approved),
            Requisition(Guid.NewGuid(), warehouseId, locationId, Guid.NewGuid(), "REQ-OTHER-TENANT", RequisitionStatus.Approved));
        await context.SaveChangesAsync();
        var access = new Mock<IProcurementAccessControlService>(MockBehavior.Strict);
        var service = CreateService(context, tenantId, userId, access.Object);

        var result = await service.GetInventoryQueuesAsync(
            new DateTime(2026, 7, 1), new DateTime(2026, 8, 1),
            includeAllTenantRequisitions: true);

        Assert.Equal(1, result.PendingInventoryApprovalCount);
        Assert.Equal(1, result.PendingInventoryIssueCount);
        access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Crm_projection_uses_configured_stage_order_current_pipeline_and_historical_transitions()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var intake = Stage(tenantId, "INTAKE", "Customer Intake", 10, probability: 10);
        var review = Stage(tenantId, "REVIEW", "Technical Review", 20, probability: 50);
        var signed = Stage(tenantId, "SIGNED", "Agreement Signed", 30, isClosed: true, isWon: true, probability: 100);
        var declined = Stage(tenantId, "DECLINED", "Declined", 40, isClosed: true, isLost: true);
        var lead = new Lead
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Ama", LastName = "Mensah",
            LeadStatus = "Qualified", CreatedAt = new DateTime(2026, 7, 2),
            NextFollowUpDate = new DateTime(2026, 7, 20)
        };
        var opportunity = new Opportunity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, LeadId = lead.Id, Name = "Airport Plot",
            StageDefinitionId = review.Id, Stage = review.Name, Currency = "GHS", Amount = 100m, Probability = 50,
            CreatedAt = new DateTime(2026, 7, 3), ExpectedCloseDate = new DateTime(2026, 7, 31)
        };
        context.OpportunityStageDefinitions.AddRange(intake, review, signed, declined);
        context.Leads.Add(lead);
        context.Opportunities.Add(opportunity);
        context.OpportunityStageHistories.AddRange(
            History(tenantId, opportunity.Id, intake.Id, new DateTime(2026, 7, 3), 80m, "GHS", 10),
            History(tenantId, opportunity.Id, review.Id, new DateTime(2026, 7, 10), 100m, "GHS", 50));
        context.Quotes.Add(new Quote
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OpportunityId = opportunity.Id,
            QuoteName = "Quote", QuoteStatus = "Sent", ValidUntil = new DateTime(2026, 8, 1),
            DocumentNumber = "Q-001", DocumentDate = new DateTime(2026, 7, 4),
            CreatedAt = new DateTime(2026, 7, 4)
        });
        var otherTenantOpportunity = new Opportunity
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "Other tenant",
            Stage = "Other tenant stage", Currency = "USD", Amount = 999m, Probability = 90,
            CreatedAt = new DateTime(2026, 7, 3), ExpectedCloseDate = new DateTime(2026, 7, 31)
        };
        context.Opportunities.Add(otherTenantOpportunity);
        await context.SaveChangesAsync();
        // ApplicationDbContext stamps Added entities with the current time. Restore the historical
        // fixture dates as an update so the test exercises the selected dashboard period.
        lead.CreatedAt = new DateTime(2026, 7, 2);
        opportunity.CreatedAt = new DateTime(2026, 7, 3);
        otherTenantOpportunity.CreatedAt = new DateTime(2026, 7, 3);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);

        var result = await service.GetCrmAsync(
            new DateTime(2026, 7, 1), new DateTime(2026, 8, 1));

        Assert.Equal(1, result.TotalLeadCount);
        Assert.Equal(1, result.QualifiedLeadCount);
        Assert.Equal(1, result.LeadsNeedingFollowUpCount);
        Assert.Equal(1, result.OpenOpportunityCount);
        Assert.Equal(new[] { "Customer Intake", "Technical Review", "Agreement Signed" },
            result.PipelineByStage.Select(stage => stage.Stage));
        var stage = result.PipelineByStage[1];
        Assert.Equal(1, stage.OpportunityCount);
        Assert.Equal(1, stage.QuoteCount);
        Assert.Equal("GHS", Assert.Single(stage.AmountsByCurrency).Currency);
        Assert.Equal(100m, Assert.Single(stage.AmountsByCurrency).Amount);
        Assert.Equal(50m, Assert.Single(stage.WeightedAmountsByCurrency).Amount);
        Assert.Equal(new[] { "Customer Intake", "Technical Review", "Agreement Signed" },
            result.ConversionFunnel.Select(point => point.Stage));
        Assert.Equal(new[] { 1, 1, 0 }, result.ConversionFunnel.Select(point => point.Count));
        Assert.Equal(80m, Assert.Single(result.ConversionFunnel[0].AmountsByCurrency).Amount);
        Assert.Equal("StageTransitionHistory", result.FunnelModel);
    }

    [Fact]
    public async Task Crm_funnel_deduplicates_reentry_tracks_skipped_and_lost_paths_and_excludes_legacy_snapshots()
    {
        var tenantId = Guid.NewGuid();
        var rangeStart = new DateTime(2026, 7, 1);
        var rangeEnd = new DateTime(2026, 8, 1);
        await using var context = CreateContext();
        var captured = Stage(tenantId, "CAPTURED", "Captured", 10, probability: 10);
        var assessed = Stage(tenantId, "ASSESSED", "Assessed", 20, probability: 35);
        var offer = Stage(tenantId, "OFFER", "Offer Issued", 30, probability: 70);
        var agreed = Stage(tenantId, "AGREED", "Agreement Signed", 40, isClosed: true, isWon: true, probability: 100);
        var declined = Stage(tenantId, "DECLINED", "Declined", 50, isClosed: true, isLost: true);
        context.OpportunityStageDefinitions.AddRange(captured, assessed, offer, agreed, declined);
        var lead = new Lead { Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Ama", LastName = "Mensah", LeadStatus = "Qualified" };
        context.Leads.Add(lead);
        var won = Opportunity(tenantId, lead.Id, agreed, "Won deal", "GHS", 120m, 100, new DateTime(2026, 7, 15));
        var skipped = Opportunity(tenantId, lead.Id, offer, "Skipped assessment", "USD", 50m, 70);
        var lost = Opportunity(tenantId, lead.Id, declined, "Lost deal", "GHS", 80m, 0, new DateTime(2026, 7, 18));
        var legacy = Opportunity(tenantId, lead.Id, captured, "Legacy snapshot", "GHS", 30m, 10);
        context.Opportunities.AddRange(won, skipped, lost, legacy);
        context.OpportunityStageHistories.AddRange(
            History(tenantId, won.Id, captured.Id, new DateTime(2026, 7, 2), 100m, "GHS", 10),
            History(tenantId, won.Id, assessed.Id, new DateTime(2026, 7, 3), 100m, "GHS", 35),
            History(tenantId, won.Id, offer.Id, new DateTime(2026, 7, 4), 100m, "GHS", 70),
            History(tenantId, won.Id, assessed.Id, new DateTime(2026, 7, 5), 110m, "GHS", 35),
            History(tenantId, won.Id, offer.Id, new DateTime(2026, 7, 6), 110m, "GHS", 70),
            History(tenantId, won.Id, agreed.Id, new DateTime(2026, 7, 7), 120m, "GHS", 100),
            History(tenantId, skipped.Id, captured.Id, new DateTime(2026, 7, 8), 50m, "USD", 10),
            History(tenantId, skipped.Id, offer.Id, new DateTime(2026, 7, 9), 50m, "USD", 70),
            History(tenantId, lost.Id, captured.Id, new DateTime(2026, 7, 10), 80m, "GHS", 10),
            History(tenantId, lost.Id, assessed.Id, new DateTime(2026, 7, 11), 80m, "GHS", 35),
            History(tenantId, lost.Id, declined.Id, new DateTime(2026, 7, 12), 80m, "GHS", 0),
            History(tenantId, legacy.Id, captured.Id, new DateTime(2026, 6, 1), 30m, "GHS", 10, isLegacy: true));
        await context.SaveChangesAsync();

        var result = await CreateService(context, tenantId).GetCrmAsync(rangeStart, rangeEnd);

        Assert.Equal(2, result.OpenOpportunityCount);
        Assert.Equal(new[] { "Captured", "Assessed", "Offer Issued", "Agreement Signed" },
            result.PipelineByStage.Select(stage => stage.Stage));
        Assert.Equal(new[] { 1, 0, 1, 1 }, result.PipelineByStage.Select(stage => stage.OpportunityCount));
        Assert.Equal("USD", Assert.Single(result.PipelineByStage[2].AmountsByCurrency).Currency);
        Assert.Equal(50m, Assert.Single(result.PipelineByStage[2].AmountsByCurrency).Amount);
        Assert.Equal(new[] { "Captured", "Assessed", "Offer Issued", "Agreement Signed" },
            result.ConversionFunnel.Select(point => point.Stage));
        Assert.Equal(new[] { 3, 2, 2, 1 }, result.ConversionFunnel.Select(point => point.Count));
        Assert.Equal(1, result.LostOpportunityCount);
        Assert.Equal(1, result.LegacyHistorySnapshotCount);
        Assert.Contains(result.DataQualityIssues, issue => issue.Contains("snapshot-only stage history"));
        Assert.Contains(result.ConversionFunnel[0].AmountsByCurrency, value => value.Currency == "GHS" && value.Amount == 180m);
        Assert.Contains(result.ConversionFunnel[0].AmountsByCurrency, value => value.Currency == "USD" && value.Amount == 50m);
        Assert.Contains(result.ConversionFunnel[2].AmountsByCurrency, value => value.Currency == "GHS" && value.Amount == 100m);
        Assert.Contains(result.ConversionFunnel[2].AmountsByCurrency, value => value.Currency == "USD" && value.Amount == 50m);
        Assert.Equal(50m, result.ConversionFunnel[3].ConversionRate);
        Assert.Equal(33.33m, result.ConversionFunnel[3].OverallConversionRate);
    }

    private static OpportunityStageDefinition Stage(
        Guid tenantId,
        string code,
        string name,
        int order,
        bool isClosed = false,
        bool isWon = false,
        bool isLost = false,
        int probability = 0) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = name, SortOrder = order,
        IsActive = true, IsClosed = isClosed, IsWon = isWon, IsLost = isLost,
        DefaultProbability = probability
    };

    private static Opportunity Opportunity(
        Guid tenantId,
        Guid leadId,
        OpportunityStageDefinition stage,
        string name,
        string currency,
        decimal amount,
        int probability,
        DateTime? actualCloseDate = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, LeadId = leadId, Name = name,
        StageDefinitionId = stage.Id, Stage = stage.Name, Currency = currency,
        Amount = amount, Probability = probability, ExpectedCloseDate = new DateTime(2026, 7, 31),
        ActualCloseDate = actualCloseDate
    };

    private static OpportunityStageHistory History(
        Guid tenantId,
        Guid opportunityId,
        Guid stageId,
        DateTime enteredAt,
        decimal amount,
        string currency,
        int probability,
        bool isLegacy = false) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, OpportunityId = opportunityId,
        StageDefinitionId = stageId, EnteredAt = enteredAt, AmountSnapshot = amount,
        CurrencySnapshot = currency, ProbabilitySnapshot = probability, IsLegacySnapshot = isLegacy
    };

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"enterprise-dashboard-{Guid.NewGuid()}")
            .Options);

    private static EnterpriseDashboardProjectionService CreateService(
        ApplicationDbContext context,
        Guid tenantId,
        Guid? userId = null,
        IProcurementAccessControlService? accessControl = null)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
        currentUser.SetupGet(value => value.UserId).Returns(userId ?? Guid.NewGuid());
        accessControl ??= AllowAllAccess();
        return new EnterpriseDashboardProjectionService(context, currentUser.Object, accessControl);
    }

    private static IProcurementAccessControlService AllowAllAccess()
    {
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken _) => new()
            {
                Allowed = true,
                PermissionCode = request.PermissionCode,
                WarehouseId = request.WarehouseId,
                LocationId = request.LocationId,
                CorrelationId = correlationId
            });
        return access.Object;
    }

    private static InventoryRequisition Requisition(
        Guid tenantId,
        Guid warehouseId,
        Guid locationId,
        Guid requestedById,
        string number,
        RequisitionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        WarehouseId = warehouseId,
        LocationId = locationId,
        RequestedById = requestedById,
        RequisitionNumber = number,
        RequestDate = new DateTime(2026, 7, 10),
        Status = status
    };
}
