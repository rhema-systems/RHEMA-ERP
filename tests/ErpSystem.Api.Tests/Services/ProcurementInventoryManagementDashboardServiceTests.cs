using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class ProcurementInventoryManagementDashboardServiceTests
{
    [Fact, Trait("Batch", "TDC-0703")]
    public async Task Dashboard_composes_authoritative_sources_and_excludes_other_tenants()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.GetAsync(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), fixture.WarehouseId, null);

        result.SpendByCurrency.Single().Amount.Should().Be(1500m);
        result.SpendByCategory.Single().Label.Should().Be("Materials");
        result.SpendByDepartment.Single().Label.Should().Be("Operations");
        result.OpenPurchaseOrders.Count.Should().Be(1);
        result.OpenPurchaseOrders.RemainingValueByCurrency.Single().Amount.Should().Be(400m);
        result.Inventory.StockValue.Should().Be(300m);
        result.Inventory.ItemLocationCount.Should().Be(2);
        result.CycleTime.AverageRequisitionToPurchaseOrderDays.Should().Be(9.5m);
        result.CycleTime.AveragePurchaseOrderToReceiptDays.Should().Be(5m);
        result.ServiceLevel.OnTimeDeliveryPercent.Should().Be(100m);
        result.SupplierRisk.AssessedSupplierCount.Should().Be(1);
        result.SupplierRisk.HighOrCriticalSupplierCount.Should().Be(1);

        fixture.Access.Verify(service => service.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.reports.read"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Access.Verify(service => service.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.inventory.read" &&
                request.WarehouseId == fixture.WarehouseId),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request =>
                request.RuleCode == "RPT-003" && request.Action == "Read"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Batch", "TDC-0703")]
    public async Task Location_filter_is_forwarded_to_authorization_and_applied_to_orders_and_stock()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.GetAsync(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            fixture.WarehouseId, fixture.LocationId);

        result.SpendByCurrency.Single().Amount.Should().Be(500m);
        result.OpenPurchaseOrders.Count.Should().Be(0);
        result.Inventory.StockValue.Should().Be(100m);
        result.Inventory.ItemLocationCount.Should().Be(1);
        fixture.Access.Verify(service => service.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.inventory.read" &&
                request.WarehouseId == fixture.WarehouseId && request.LocationId == fixture.LocationId),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Batch", "TDC-0703")]
    public async Task Dashboard_fails_closed_when_report_capability_is_denied()
    {
        await using var fixture = await Fixture.CreateAsync(allowReports: false);

        var action = () => fixture.Service.GetAsync(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), null, null);

        await action.Should().ThrowAsync<ProcurementAccessAuthorizationException>();
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            ApplicationDbContext db,
            ProcurementInventoryManagementDashboardService service,
            Mock<IProcurementAccessControlService> access,
            Mock<IProcurementControlEventService> controlEvents,
            Guid warehouseId,
            Guid locationId)
        {
            Db = db;
            Service = service;
            Access = access;
            ControlEvents = controlEvents;
            WarehouseId = warehouseId;
            LocationId = locationId;
        }

        public ApplicationDbContext Db { get; }
        public ProcurementInventoryManagementDashboardService Service { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public Mock<IProcurementControlEventService> ControlEvents { get; }
        public Guid WarehouseId { get; }
        public Guid LocationId { get; }

        public static async Task<Fixture> CreateAsync(bool allowReports = true)
        {
            var tenantId = Guid.NewGuid();
            var otherTenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var otherLocationId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tdc0703-dashboard-{Guid.NewGuid():N}").Options;
            var db = new ApplicationDbContext(options);

            var category = new InventoryCategory
            {
                TenantId = tenantId, Code = "MAT", Name = "Materials"
            };
            var item = new InventoryItem
            {
                TenantId = tenantId, CategoryId = category.Id, Category = category,
                ItemCode = "MAT-001", Name = "Material", UnitOfMeasure = "EA"
            };
            var openRequisition = Requisition(tenantId, "PR-OPEN", new DateTime(2026, 1, 1));
            var receivedRequisition = Requisition(tenantId, "PR-RECEIVED", new DateTime(2026, 1, 10));
            var openOrder = Order(tenantId, "PO-OPEN", new DateTime(2026, 1, 10),
                "Sent", 1000m, warehouseId, openRequisition, item, 10m, 6m);
            var receivedOrder = Order(tenantId, "PO-RECEIVED", new DateTime(2026, 1, 20),
                "Received", 500m, warehouseId, receivedRequisition, item, 5m, 5m);
            receivedOrder.PromisedDate = new DateTime(2026, 1, 30);
            receivedOrder.ReceivedDate = new DateTime(2026, 1, 25);
            var receivedLine = receivedOrder.Items.Single();
            receivedOrder.Receipts.Add(new PurchaseOrderReceipt
            {
                TenantId = tenantId,
                PurchaseOrderId = receivedOrder.Id,
                ReceiptNumber = "GRN-001",
                ReceiptDate = new DateTime(2026, 1, 25),
                Status = "Accepted",
                Items =
                [
                    new PurchaseOrderReceiptItem
                    {
                        TenantId = tenantId,
                        PurchaseOrderItemId = receivedLine.Id,
                        ReceivedQuantity = 5m,
                        AcceptedQuantity = 5m,
                        LocationId = locationId
                    }
                ]
            });
            var otherTenantOrder = new PurchaseOrder
            {
                TenantId = otherTenantId,
                BusinessPartnerId = Guid.NewGuid(),
                OrderNumber = "PO-OTHER",
                OrderDate = new DateTime(2026, 1, 15),
                Status = "Sent",
                Currency = "GHS",
                TotalAmount = 9999m,
                DeliveryWarehouseId = warehouseId
            };
            db.InventoryCategories.Add(category);
            db.InventoryItems.Add(item);
            db.PurchaseRequisitions.AddRange(openRequisition, receivedRequisition);
            db.PurchaseOrders.AddRange(openOrder, receivedOrder, otherTenantOrder);
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserProvider>();
            currentUser.SetupGet(value => value.IsAuthenticated).Returns(true);
            currentUser.SetupGet(value => value.IsExternalUser).Returns(false);
            currentUser.SetupGet(value => value.UserId).Returns(userId);
            currentUser.SetupGet(value => value.TenantId).Returns(tenantId);

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(service => service.CheckCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                    new ProcurementAccessCapabilityDecisionDto
                    {
                        Allowed = request.PermissionCode != "procurement.reports.read" || allowReports,
                        Message = allowReports ? "Allowed" : "Denied",
                        PermissionCode = request.PermissionCode,
                        WarehouseId = request.WarehouseId,
                        LocationId = request.LocationId,
                        ActorUserId = userId,
                        TenantId = tenantId,
                        CorrelationId = correlation
                    });

            var analytics = new Mock<IInventoryAnalyticsReportSource>();
            analytics.Setup(service => service.GetReportSourceAsync(
                    It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InventoryAnalyticsDto
                {
                    AsOfUtc = new DateTime(2026, 1, 31),
                    Items =
                    [
                        new InventoryItemLocationAnalyticsDto
                        {
                            InventoryItemId = item.Id, ItemCode = item.ItemCode,
                            ItemName = item.Name, CategoryName = category.Name,
                            WarehouseId = warehouseId, WarehouseName = "Main Warehouse",
                            LocationId = locationId, LocationName = "A-01",
                            QuantityOnHand = 10m, InventoryValue = 100m
                        },
                        new InventoryItemLocationAnalyticsDto
                        {
                            InventoryItemId = item.Id, ItemCode = item.ItemCode,
                            ItemName = item.Name, CategoryName = category.Name,
                            WarehouseId = warehouseId, WarehouseName = "Main Warehouse",
                            LocationId = otherLocationId, LocationName = "B-01",
                            QuantityOnHand = 20m, InventoryValue = 200m
                        }
                    ]
                });

            var risk = new Mock<IProcurementSupplierRiskService>();
            risk.Setup(service => service.SearchAsync(
                    It.IsAny<ProcurementSupplierRiskSearchRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSupplierRiskPageDto
                {
                    Page = 1,
                    PageSize = 100,
                    TotalCount = 2,
                    Items =
                    [
                        new ProcurementSupplierRiskListItemDto
                        {
                            BusinessPartnerId = Guid.NewGuid(), AssessedAtUtc = new DateTime(2026, 1, 20),
                            NextReviewDueAtUtc = new DateTime(2026, 12, 31), RiskBand = "High",
                            AwardBlocked = true, OpenAlertCount = 1
                        },
                        new ProcurementSupplierRiskListItemDto
                        {
                            BusinessPartnerId = Guid.NewGuid(), AssessedAtUtc = new DateTime(2025, 12, 20),
                            NextReviewDueAtUtc = new DateTime(2026, 12, 31), RiskBand = "Low"
                        }
                    ]
                });

            var controlEvents = new Mock<IProcurementControlEventService>();
            controlEvents.Setup(service => service.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());

            var service = new ProcurementInventoryManagementDashboardService(
                db, currentUser.Object, access.Object, analytics.Object, risk.Object, controlEvents.Object);
            return new Fixture(db, service, access, controlEvents, warehouseId, locationId);
        }

        private static PurchaseRequisition Requisition(Guid tenantId, string number, DateTime date) => new()
        {
            TenantId = tenantId,
            RequisitionNumber = number,
            RequisitionDate = date,
            RequestedById = Guid.NewGuid(),
            Department = "Operations"
        };

        private static PurchaseOrder Order(
            Guid tenantId,
            string number,
            DateTime date,
            string status,
            decimal total,
            Guid warehouseId,
            PurchaseRequisition requisition,
            InventoryItem item,
            decimal ordered,
            decimal received)
        {
            var line = new PurchaseOrderItem
            {
                TenantId = tenantId,
                InventoryItemId = item.Id,
                InventoryItem = item,
                ItemDescription = item.Name,
                OrderedQuantity = ordered,
                ReceivedQuantity = received,
                RemainingQuantity = ordered - received,
                UnitOfMeasure = "EA",
                UnitPrice = 100m,
                LineTotal = total,
                WarehouseId = warehouseId
            };
            var order = new PurchaseOrder
            {
                TenantId = tenantId,
                BusinessPartnerId = Guid.NewGuid(),
                OrderNumber = number,
                OrderDate = date,
                RequiredDate = date.AddDays(5),
                Status = status,
                Currency = "GHS",
                TotalAmount = total,
                DeliveryWarehouseId = warehouseId,
                SourceRequisitionId = requisition.Id,
                SourceRequisition = requisition,
                Items = [line]
            };
            line.PurchaseOrderId = order.Id;
            return order;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
