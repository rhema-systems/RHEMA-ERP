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
    public void CatalogueDefinesTheNineTdc0702ReportsOnTheSharedReportProtocol()
    {
        InventoryStatutoryReportCatalogue.Definitions.Should().HaveCount(9);
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

        (await seeder.SeedAsync()).Should().Be(18);
        (await seeder.SeedAsync()).Should().Be(0);
        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(18);
        (await context.Reports.IgnoreQueryFilters()
                .CountAsync(item => item.TenantId == firstTenantId && item.ModuleId != null))
            .Should().Be(9);

        var deleted = await context.Reports.IgnoreQueryFilters()
            .FirstAsync(item => item.TenantId == firstTenantId);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        (await seeder.SeedTenantAsync(firstTenantId)).Should().Be(1);
        deleted.IsDeleted.Should().BeFalse();
        deleted.DeletedAt.Should().BeNull();
        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(18);
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
