using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryStatutoryReportServiceTests
{
    [Fact]
    public void CatalogueIncludesInventoryLedgerOnTheSharedReportProtocol()
    {
        InventoryStatutoryReportCatalogue.Definitions.Should().HaveCount(10);
        InventoryStatutoryReportCatalogue.Definitions.Select(item => item.Code).Should().OnlyHaveUniqueItems();
        InventoryStatutoryReportCatalogue.Definitions.Should().OnlyContain(item =>
            item.Query.StartsWith(InventoryStatutoryReportCatalogue.QueryPrefix, StringComparison.Ordinal) &&
            item.Columns.Count > 0 &&
            item.Tags.Contains("TDC-0702") &&
            item.Tags.Contains("RPT-002"));
    }

    [Fact]
    public async Task EveryCatalogueReportExecutesThroughItsExistingInventoryOwner()
    {
        await using var fixture = new Fixture();

        foreach (var definition in InventoryStatutoryReportCatalogue.Definitions)
        {
            var result = await fixture.Service.ExecuteAsync(
                definition.Query,
                new ExecuteReportDto { Page = 1, PageSize = 25 },
                isAdministrator: true);

            result.Columns.Select(item => item.Name).Should().Equal(definition.Columns.Select(item => item.Name));
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(25);
            result.Metadata!.Query.Should().Be(definition.Query);
            result.Metadata.DataSource.Should().Contain("assigned-scope");
        }

        fixture.Analytics.Verify(item => item.GetReportSourceAsync(
            null, null, 90, 180, 90, It.IsAny<CancellationToken>()), Times.Exactly(5));
        fixture.PhysicalCounts.Verify(item => item.GetAllAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
        fixture.Valuation.Verify(item => item.GetReportSourceAsync(
            null, null, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Disposals.Verify(item => item.GetReportSourceAsync(
            null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MovementRegisterEnforcesTenantAndExactLocationScope()
    {
        await using var fixture = new Fixture();
        var permittedLocation = Guid.NewGuid();
        var deniedLocation = Guid.NewGuid();
        fixture.AllowInventoryScope = request => request.LocationId == permittedLocation;
        fixture.AddMovement(fixture.TenantId, "LOCAL-ALLOWED", permittedLocation);
        fixture.AddMovement(fixture.TenantId, "LOCAL-DENIED", deniedLocation);
        fixture.AddMovement(fixture.ForeignTenantId, "FOREIGN", Guid.NewGuid());
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ExecuteAsync(
            InventoryStatutoryReportCatalogue.QueryPrefix + InventoryStatutoryReportCatalogue.MovementCode,
            new ExecuteReportDto { Page = 1, PageSize = 100 },
            isAdministrator: true);

        result.TotalRows.Should().Be(1);
        result.Data.Should().ContainSingle();
        result.Data.Single()["ItemCode"].Should().Be("LOCAL-ALLOWED");
    }

    [Fact]
    public async Task NonAdministratorRequiresReadAndExportCapabilities()
    {
        await using var fixture = new Fixture(allowReportCapabilities: false);
        var definition = InventoryStatutoryReportCatalogue.Definitions[0];

        (await fixture.Service.CanReadAsync(isAdministrator: false)).Should().BeFalse();
        await fixture.Service.Invoking(service => service.ExecuteAsync(
                definition.Query, new ExecuteReportDto(), isAdministrator: false))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await fixture.Service.Invoking(service => service.AuthorizeExportAsync(
                definition.Query, isAdministrator: false))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ExecutionRejectsAnEmptyTenantContextAndInvalidThresholds()
    {
        await using var emptyTenant = new Fixture(tenantId: Guid.Empty);
        await emptyTenant.Service.Invoking(service => service.ExecuteAsync(
                InventoryStatutoryReportCatalogue.Definitions[0].Query,
                new ExecuteReportDto(),
                isAdministrator: true))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        await using var fixture = new Fixture();
        await fixture.Service.Invoking(service => service.ExecuteAsync(
                InventoryStatutoryReportCatalogue.Definitions[0].Query,
                new ExecuteReportDto
                {
                    Parameters = new Dictionary<string, object>
                    {
                        ["slowMovingDays"] = 180,
                        ["nonMovingDays"] = 90
                    }
                },
                isAdministrator: true))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*non-moving days must be greater*");
    }

    [Fact]
    public async Task SeederIsTenantWideIdempotentAndRepairsSoftDeletedDefinitions()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var firstTenantId = Guid.NewGuid();
        var secondTenantId = Guid.NewGuid();
        context.Tenants.AddRange(
            new Tenant { Id = firstTenantId, Code = "ONE", Name = "Tenant One" },
            new Tenant { Id = secondTenantId, Code = "TWO", Name = "Tenant Two" });
        context.TenantModules.AddRange(
            new TenantModule { TenantId = firstTenantId, ModuleName = "Inventory" },
            new TenantModule { TenantId = secondTenantId, ModuleName = "Inventory" });
        await context.SaveChangesAsync();
        var seeder = new InventoryStatutoryReportSeeder(
            context, NullLogger<InventoryStatutoryReportSeeder>.Instance);

        (await seeder.SeedAsync()).Should().Be(20);
        (await seeder.SeedAsync()).Should().Be(0);
        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(20);
        (await context.Reports.IgnoreQueryFilters()
                .CountAsync(item => item.TenantId == firstTenantId && item.ModuleId != null))
            .Should().Be(10);

        var deleted = await context.Reports.IgnoreQueryFilters()
            .FirstAsync(item => item.TenantId == firstTenantId);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        (await seeder.SeedTenantAsync(firstTenantId)).Should().Be(1);
        deleted.IsDeleted.Should().BeFalse();
        deleted.DeletedAt.Should().BeNull();
        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(20);
    }

    [Fact]
    public async Task LedgerIncludesOpeningAndHiddenMovementsBeforePaginationAndIncludesCompleteEndDate()
    {
        await using var fixture = new Fixture();
        fixture.AddMovement(fixture.TenantId, "ITEM-LEDGER", Guid.NewGuid());
        var source = fixture.Context.StockMovements.Local.Single();
        source.InventoryItem.CurrentStock = 9999; // Current stock and the duplicate operational log are not ledger inputs.
        var day = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        AddLedgerMovement(fixture.Context, source, day.AddDays(-1), 100, MovementDirection.In, InventoryMovementType.OpeningBalance);
        AddLedgerMovement(fixture.Context, source, day.AddHours(1), 20, MovementDirection.In, InventoryMovementType.PurchaseReceipt);
        AddLedgerMovement(fixture.Context, source, day.AddHours(2), 10, MovementDirection.Out, InventoryMovementType.SalesIssue);
        AddLedgerMovement(fixture.Context, source, day.AddHours(23), 5, MovementDirection.In, InventoryMovementType.PurchaseReceipt);
        AddLedgerMovement(fixture.Context, source, day.AddDays(1), 500, MovementDirection.In, InventoryMovementType.PurchaseReceipt);
        AddLedgerMovement(fixture.Context, source, day.AddHours(3), 500, MovementDirection.In, InventoryMovementType.PurchaseReceipt).IsPosted = false;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ExecuteAsync(InventoryStatutoryReportCatalogue.QueryPrefix + InventoryStatutoryReportCatalogue.LedgerCode,
            new ExecuteReportDto { Page = 2, PageSize = 1, Parameters = new()
            {
                ["startDate"] = "2026-09-20", ["endDate"] = "2026-09-20", ["movementType"] = "PurchaseReceipt",
                ["inventoryItemId"] = source.InventoryItemId, ["warehouseId"] = source.WarehouseId
            } }, true);
        result.TotalRows.Should().Be(2);
        var row = result.Data.Should().ContainSingle().Subject;
        row["BalanceBefore"].Should().Be(110m);
        row["BalanceAfter"].Should().Be(115m);
        row["QuantityIn"].Should().Be(5m);
        row["QuantityOut"].Should().Be(0m);
    }

    [Fact]
    public async Task LedgerKeepsTenantAndLocationAuthorizationForHistoricalBalances()
    {
        await using var fixture = new Fixture();
        var allowedLocation = Guid.NewGuid();
        fixture.AllowInventoryScope = request => request.LocationId == allowedLocation;
        fixture.AddMovement(fixture.TenantId, "VISIBLE", allowedLocation);
        fixture.AddMovement(fixture.TenantId, "DENIED", Guid.NewGuid());
        fixture.AddMovement(fixture.ForeignTenantId, "FOREIGN", Guid.NewGuid());
        foreach (var source in fixture.Context.StockMovements.Local.ToList())
            AddLedgerMovement(fixture.Context, source, DateTime.UtcNow.AddDays(-1), 9, MovementDirection.In, InventoryMovementType.PurchaseReceipt);
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.Service.ExecuteAsync(InventoryStatutoryReportCatalogue.QueryPrefix + InventoryStatutoryReportCatalogue.LedgerCode,
            new ExecuteReportDto { Page = 1, PageSize = 100 }, true);
        var row = result.Data.Should().ContainSingle().Subject;
        row["ItemCode"].Should().Be("VISIBLE");
        row["BalanceBefore"].Should().Be(0m);
        row["BalanceAfter"].Should().Be(9m);
    }

    [Fact]
    public async Task LedgerUsesDeterministicChronologyAndRetainsZeroQuantityValueAdjustments()
    {
        await using var fixture = new Fixture();
        fixture.AddMovement(fixture.TenantId, "TIES", Guid.NewGuid());
        var source = fixture.Context.StockMovements.Local.Single();
        var date = DateTime.UtcNow.AddDays(-1);
        var first = AddLedgerMovement(fixture.Context, source, date, 10, MovementDirection.In, InventoryMovementType.PurchaseReceipt);
        var second = AddLedgerMovement(fixture.Context, source, date, 3, MovementDirection.Out, InventoryMovementType.SalesIssue);
        var third = AddLedgerMovement(fixture.Context, source, date, 0, MovementDirection.In, InventoryMovementType.LandedCostRevaluation);
        first.CreatedAt = date; second.CreatedAt = date.AddSeconds(1); third.CreatedAt = date.AddSeconds(2);
        third.TotalValue = 7m;
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.Service.ExecuteAsync(InventoryStatutoryReportCatalogue.QueryPrefix + InventoryStatutoryReportCatalogue.LedgerCode,
            new ExecuteReportDto { Page = 1, PageSize = 100, Parameters = new() { ["itemCode"] = "TIES" } }, true);
        result.Data.Select(row => row["BalanceBefore"]).Should().Equal(0m, 10m, 7m);
        result.Data.Select(row => row["BalanceAfter"]).Should().Equal(10m, 7m, 7m);
        result.Data.Last()["TotalValue"].Should().Be(7m);
    }

    private static InventoryMovement AddLedgerMovement(ApplicationDbContext context, StockMovement source, DateTime date,
        decimal quantity, MovementDirection direction, InventoryMovementType type)
    {
        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(), TenantId = source.TenantId, InventoryItemId = source.InventoryItemId, InventoryItem = source.InventoryItem,
            WarehouseId = source.WarehouseId, Warehouse = source.Warehouse!, LocationId = source.LocationId, Location = source.Location,
            MovementNumber = Guid.NewGuid().ToString("N"), MovementDate = date, PostingDate = date, PostedAt = date,
            MovementType = type, Direction = direction, Quantity = quantity, UnitCost = 2m, TotalValue = quantity * 2m,
            IsPosted = true, ReferenceType = ReferenceType.Manual
        };
        context.InventoryMovements.Add(movement);
        return movement;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;

        public Fixture(bool allowReportCapabilities = true, Guid? tenantId = null)
        {
            TenantId = tenantId ?? Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            _unitOfWork = new UnitOfWork(Context);

            var currentUser = new Mock<ICurrentUserProvider>();
            currentUser.SetupGet(item => item.TenantId).Returns(TenantId);
            currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
            currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.CheckCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken _) =>
                {
                    var allowed = request.PermissionCode == "procurement.inventory.read"
                        ? AllowInventoryScope(request)
                        : allowReportCapabilities;
                    return Decision(allowed);
                });
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest _, string _, CancellationToken _) =>
                    Decision(allowReportCapabilities));

            Analytics = new Mock<IInventoryAnalyticsReportSource>();
            Analytics.Setup(item => item.GetReportSourceAsync(
                    It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InventoryAnalyticsDto { AsOfUtc = DateTime.UtcNow });
            PhysicalCounts = new Mock<IPhysicalCountService>();
            PhysicalCounts.Setup(item => item.GetAllAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync([]);
            Valuation = new Mock<IInventoryValuationReconciliationReportSource>();
            Valuation.Setup(item => item.GetReportSourceAsync(
                    It.IsAny<Guid?>(), It.IsAny<InventoryValuationReconciliationStatus?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            Disposals = new Mock<IInventoryDisposalReportSource>();
            Disposals.Setup(item => item.GetReportSourceAsync(
                    It.IsAny<InventoryDisposalStatus?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            Service = new InventoryStatutoryReportService(_unitOfWork, currentUser.Object, access.Object,
                Analytics.Object, PhysicalCounts.Object, Valuation.Object, Disposals.Object);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public ApplicationDbContext Context { get; }
        public InventoryStatutoryReportService Service { get; }
        public Mock<IInventoryAnalyticsReportSource> Analytics { get; }
        public Mock<IPhysicalCountService> PhysicalCounts { get; }
        public Mock<IInventoryValuationReconciliationReportSource> Valuation { get; }
        public Mock<IInventoryDisposalReportSource> Disposals { get; }
        public Func<ProcurementAccessCapabilityRequest, bool> AllowInventoryScope { get; set; } = _ => true;

        public void AddMovement(Guid tenantId, string itemCode, Guid locationId)
        {
            var category = new InventoryCategory
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = $"CAT-{itemCode}", Name = $"Category {itemCode}"
            };
            var item = new InventoryItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = category.Id,
                ItemCode = itemCode, Name = itemCode, Category = category
            };
            var warehouse = new Warehouse
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = $"WH-{itemCode}", Name = $"Warehouse {itemCode}"
            };
            var location = new WarehouseLocation
            {
                Id = locationId, TenantId = tenantId, WarehouseId = warehouse.Id,
                LocationCode = $"LOC-{itemCode}", Name = $"Location {itemCode}", Warehouse = warehouse
            };
            Context.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId, InventoryItemId = item.Id, InventoryItem = item,
                WarehouseId = warehouse.Id, Warehouse = warehouse, LocationId = location.Id, Location = location,
                MovementType = "Receipt", Quantity = 1m, UnitCost = 10m, TotalValue = 10m,
                RunningBalance = 1m, ReferenceType = ReferenceType.Manual
            });
        }

        private static ProcurementAccessCapabilityDecisionDto Decision(bool allowed) => new()
        {
            Allowed = allowed,
            Code = allowed ? "ACCESS_ALLOWED" : "ACCESS_PERMISSION_DENIED",
            Message = allowed ? "Allowed" : "Denied"
        };

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
