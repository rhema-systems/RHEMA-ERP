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
    public async Task Crm_projection_uses_tenant_date_and_configured_stage_values_without_summing_currencies()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var lead = new Lead
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Ama", LastName = "Mensah",
            LeadStatus = "Qualified", CreatedAt = new DateTime(2026, 7, 2),
            NextFollowUpDate = new DateTime(2026, 7, 20)
        };
        var opportunity = new Opportunity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, LeadId = lead.Id, Name = "Airport Plot",
            Stage = "Technical Review", Currency = "GHS", Amount = 100m, Probability = 50,
            CreatedAt = new DateTime(2026, 7, 3), ExpectedCloseDate = new DateTime(2026, 7, 31)
        };
        context.Leads.Add(lead);
        context.Opportunities.Add(opportunity);
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
            Stage = "Technical Review", Currency = "USD", Amount = 999m, Probability = 90,
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
        var stage = Assert.Single(result.PipelineByStage);
        Assert.Equal("Technical Review", stage.Stage);
        Assert.Equal(1, stage.OpportunityCount);
        Assert.Equal(1, stage.QuoteCount);
    }

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
