using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Api.Tests.Services.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

// Real SQL provider, disposable database and real controller. Business repositories
// use a small durable probe so these transaction tests do not rebuild the ERP model.
public sealed class PurchaseOrderUpdateRetryTests
{
    public sealed class LocalSqlFactAttribute : FactAttribute
    {
        public LocalSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TDC_PO_RETRY_TEST_CONNECTION")))
                Skip = "Set TDC_PO_RETRY_TEST_CONNECTION to a local SQL Server with create-database permission.";
        }
    }

    [LocalSqlFact]
    public async Task SqlRetryEnabledSaveSupportsUnmappedStockServiceAndNonStock()
    {
        foreach (var lineType in new[] { ItemType.StockItem, ItemType.Service, ItemType.NonStock })
        {
            await using var fixture = await Fixture.CreateAsync();
            var (controller, dto, unit) = fixture.Controller(lineType);
            var result = await controller.UpdatePurchaseOrder(fixture.PoId, dto);
            result.Result.Should().BeOfType<OkObjectResult>(fixture.Error?.ToString());
            var state = await fixture.Db.Set<Probe>().AsNoTracking().SingleAsync();
            state.Saves.Should().Be(1);
            state.Lines.Should().Be(1);
            unit.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            fixture.Db.Database.CurrentTransaction.Should().BeNull();
        }
    }

    [LocalSqlFact]
    public async Task TransientFailureAfterLineWriteRollsBackAndRetriesWithoutDuplicateLines()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (controller, dto, unit) = fixture.Controller(ItemType.Service, "transient");
        var result = await controller.UpdatePurchaseOrder(fixture.PoId, dto);
        result.Result.Should().BeOfType<OkObjectResult>(fixture.Error?.ToString());
        var state = await fixture.Db.Set<Probe>().AsNoTracking().SingleAsync();
        state.Saves.Should().Be(1, "the first attempt must have rolled back");
        state.Lines.Should().Be(1, "retry must not duplicate the replacement line");
        unit.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        unit.Verify(x => x.ClearTrackedChanges(), Times.AtLeastOnce);
        unit.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [LocalSqlFact]
    public async Task SourceValidationStillReturns422AndRollsBackBeforeReturning()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (controller, dto, unit) = fixture.Controller(ItemType.NonStock, "validation");
        var result = await controller.UpdatePurchaseOrder(fixture.PoId, dto);
        result.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        var state = await fixture.Db.Set<Probe>().AsNoTracking().SingleAsync();
        state.Saves.Should().Be(0);
        state.Lines.Should().Be(1);
        unit.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        unit.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ContentTypeMigrationPreservesPdfNullAndOfficeMimeTypesAndGuardsDowngrade()
    {
        var source = ArchivedMigrationSource.Read("20260908200000_WidenContractDocumentContentType.cs");
        source.Should().Contain("type: \"nvarchar(255)\"").And.Contain("maxLength: 255")
            .And.Contain("DATALENGTH([ContentType]) > 510")
            .And.Contain("Cannot narrow ContentType to 50 characters while longer MIME types are stored.");
        typeof(ContractDocument).GetProperty(nameof(ContractDocument.ContentType))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), false).Cast<MaxLengthAttribute>()
            .Single().Length.Should().Be(255);
    }

    [LocalSqlFact]
    public async Task UpdatingAnIdentifiedLineRetainsItsIdentity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (controller, dto, unit) = fixture.Controller(ItemType.NonStock, retainLineIdentity: true);
        var result = await controller.UpdatePurchaseOrder(fixture.PoId, dto);
        result.Result.Should().BeOfType<OkObjectResult>(fixture.Error?.ToString());
        fixture.LastUpdatedLineId.Should().Be(dto.Items[0].Id);
        var state = await fixture.Db.Set<Probe>().AsNoTracking().SingleAsync();
        state.Lines.Should().Be(1);
        unit.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void LandedCostScopeMigrationPreservesSharedCostsAndEnforcesTargetFk()
    {
        var source = ArchivedMigrationSource.Read("20260908220000_ScopePlannedLandedCostsToPurchaseOrderLines.cs");
        source.Should().Contain("new[] { \"PurchaseOrderLandedCostPlanItems\", \"LandedCostItems\" }")
            .And.Contain("AddColumn<Guid>(\"PurchaseOrderItemId\", table, type: \"uniqueidentifier\", nullable: true)")
            .And.Contain("\"PurchaseOrderItems\", principalColumn: \"Id\", onDelete: ReferentialAction.NoAction")
            .And.Contain("Cannot remove scope while line-specific landed costs exist.");
    }

    [LocalSqlFact]
    public async Task InvalidPlannedCostRollsBackThePoSave()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (controller, dto, unit) = fixture.Controller(ItemType.NonStock, retainLineIdentity: true);
        dto.PlannedLandedCostPlan = new() { Currency = "GHS", Items = new() {
            new() { CostType = LandedCostType.Freight, Description = "Invalid target", Amount = 100,
                Currency = "GHS", PurchaseOrderLineIndex = 99 } } };
        var result = await controller.UpdatePurchaseOrder(fixture.PoId, dto);
        result.Result.Should().BeOfType<BadRequestObjectResult>(fixture.Error?.ToString());
        var state = await fixture.Db.Set<Probe>().AsNoTracking().SingleAsync();
        state.Saves.Should().Be(0);
        state.Lines.Should().Be(1);
        unit.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class ErrorLogger(Action<Exception?> capture) : ILogger<PurchaseOrdersController>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) { if (exception != null) capture(exception); }
    }

    private sealed class Probe
    {
        public Guid Id { get; set; }
        public int Saves { get; set; }
        public int Lines { get; set; }
    }
    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Probe>();
    }
    private sealed class Fixture(ProbeContext db, SqlConnection master, string name) : IAsyncDisposable
    {
        public ProbeContext Db { get; } = db;
        public Exception? Error { get; set; }
        public Guid PoId { get; private set; }
        public Guid? LastUpdatedLineId { get; private set; }
        public static async Task<Fixture> CreateAsync()
        {
            var source = Environment.GetEnvironmentVariable("TDC_PO_RETRY_TEST_CONNECTION")!;
            var builder = new SqlConnectionStringBuilder(source) { InitialCatalog = "master" };
            var master = new SqlConnection(builder.ConnectionString);
            await master.OpenAsync();
            var name = "TdcPoRetryTest_" + Guid.NewGuid().ToString("N");
            await using (var create = master.CreateCommand())
            {
                create.CommandText = $"CREATE DATABASE [{name}]";
                await create.ExecuteNonQueryAsync();
            }
            builder.InitialCatalog = name;
            var db = new ProbeContext(new DbContextOptionsBuilder<ProbeContext>()
                .UseSqlServer(builder.ConnectionString, sql => sql.EnableRetryOnFailure(1, TimeSpan.Zero, null)).Options);
            var fixture = new Fixture(db, master, name) { PoId = Guid.NewGuid() };
            try
            {
                await db.Database.EnsureCreatedAsync();
                db.Add(new Probe { Id = fixture.PoId, Lines = 1 });
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public (PurchaseOrdersController Controller, CreatePurchaseOrderDto Dto, Mock<IUnitOfWork> Unit)
            Controller(ItemType type, string? fault = null, bool retainLineIdentity = false)
        {
            var tenant = Guid.NewGuid();
            var po = new PurchaseOrder { Id = PoId, TenantId = tenant, Status = "Draft",
                OrderNumber = "RETRY-TEST", Currency = "GHS", BusinessPartnerId = Guid.NewGuid(),
                ProcurementSourceType = ProcurementPurchaseOrderSourceType.Contract, ProcurementSourceId = Guid.NewGuid() };
            var line = new PurchaseOrderItem { Id = Guid.NewGuid(), PurchaseOrderId = PoId,
                TenantId = tenant, LineType = type, ItemDescription = "Approved service or goods",
                UnitOfMeasure = "EACH", OrderedQuantity = 1, UnitPrice = 50 };
            var dto = new CreatePurchaseOrderDto { SourceType = po.ProcurementSourceType,
                SourceId = po.ProcurementSourceId, SupplierId = po.BusinessPartnerId, Items =
                [new() { Id = retainLineIdentity ? line.Id : null, LineType = type, ItemDescription = line.ItemDescription, UnitOfMeasure = "EACH", OrderedQuantity = 1, UnitPrice = 50 }] };
            var unit = new Mock<IUnitOfWork>();
            unit.SetupGet(x => x.HasActiveTransaction).Returns(() => Db.Database.CurrentTransaction != null);
            unit.Setup(x => x.ExecuteInStrategyAsync(It.IsAny<Func<Task<ActionResult<PurchaseOrderDetailDto>>>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task<ActionResult<PurchaseOrderDetailDto>>> action, CancellationToken _) =>
                    Db.Database.CreateExecutionStrategy().ExecuteAsync(action));
            unit.Setup(x => x.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
                .Returns(async (System.Data.IsolationLevel isolation, CancellationToken token) =>
                    { await Db.Database.BeginTransactionAsync(isolation, token); });
            unit.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(async () =>
                { var tx = Db.Database.CurrentTransaction!; await tx.CommitAsync(); await tx.DisposeAsync(); });
            unit.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(async () =>
                { var tx = Db.Database.CurrentTransaction!; await tx.RollbackAsync(); await tx.DisposeAsync(); Db.ChangeTracker.Clear(); });
            unit.Setup(x => x.ClearTrackedChanges()).Callback(() => Db.ChangeTracker.Clear());
            var framework = new Mock<IGenericRepository<ProcurementFrameworkCallOff>>();
            framework.Setup(x => x.GetQueryable(It.IsAny<Expression<Func<ProcurementFrameworkCallOff, bool>>>()))
                .Returns(Db.Set<Probe>().Where(x => x.Id == Guid.Empty)
                    .Select(x => new ProcurementFrameworkCallOff { Id = x.Id }));
            unit.Setup(x => x.Repository<ProcurementFrameworkCallOff>()).Returns(framework.Object);
            var repository = new Mock<IPurchaseOrderRepository>();
            repository.Setup(x => x.GetPurchaseOrderByIdAsync(PoId)).ReturnsAsync(po);
            repository.Setup(x => x.UpdatePurchaseOrderAsync(It.IsAny<PurchaseOrder>())).Returns(async (PurchaseOrder _) =>
                { await Db.Database.ExecuteSqlRawAsync("UPDATE Probe SET Saves = Saves + 1"); return _; });
            var items = new Mock<IPurchaseOrderItemRepository>();
            items.Setup(x => x.GetItemsByPurchaseOrderIdAsync(PoId)).ReturnsAsync(() => { line.IsDeleted = false; return new[] { line }; });
            items.Setup(x => x.DeleteItemAsync(It.IsAny<Guid>())).Returns(async (Guid _) =>
                { await Db.Database.ExecuteSqlRawAsync("UPDATE Probe SET Lines = Lines - 1"); });
            items.Setup(x => x.UpdateItemAsync(It.IsAny<PurchaseOrderItem>())).Returns(async (PurchaseOrderItem item) =>
            {
                if (item.IsDeleted) await Db.Database.ExecuteSqlRawAsync("UPDATE Probe SET Lines = Lines - 1");
                else LastUpdatedLineId = item.Id;
                return item;
            });
            var writes = 0;
            items.Setup(x => x.CreateItemAsync(It.IsAny<PurchaseOrderItem>())).Returns(async (PurchaseOrderItem item) =>
            {
                item.InventoryItemId.Should().BeNull();
                item.LineType.Should().Be(type);
                await Db.Database.ExecuteSqlRawAsync("UPDATE Probe SET Lines = Lines + 1");
                if (fault == "transient" && ++writes == 1) throw new TimeoutException("Injected after durable line write.");
                return item;
            });
            var source = new Mock<IProcurementPurchaseOrderSourceService>();
            source.Setup(x => x.RevalidateAsync(po, "Update", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementPurchaseOrderSourceResolution { CurrencyCode = "GHS" });
            source.Setup(x => x.ReserveAsync(It.IsAny<ProcurementPurchaseOrderSourceResolution>(),
                It.IsAny<IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine>>(), It.IsAny<decimal>(),
                It.IsAny<string>(), PoId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    Db.Database.CurrentTransaction.Should().NotBeNull();
                    // This real SQL query reproduces the reported execution-strategy failure before the fix.
                    await Db.Set<Probe>().AsNoTracking().SingleAsync(x => x.Id == PoId);
                    if (fault == "validation")
                        throw new ProcurementPurchaseOrderSourceValidationException("PO_SOURCE_LINE_NOT_APPROVED", "Source changed.");
                });
            var user = new Mock<ICurrentUserProvider>();
            user.SetupGet(x => x.TenantId).Returns(tenant);
            var controller = new PurchaseOrdersController(repository.Object, items.Object,
                Mock.Of<IPurchaseOrderReceiptRepository>(), Mock.Of<IPurchaseOrderReceiptItemRepository>(),
                Mock.Of<IBusinessPartnerRepository>(), Mock.Of<IInventoryItemRepository>(), Mock.Of<IWarehouseRepository>(),
                Mock.Of<IInventoryValuationService>(), Mock.Of<IProjectService>(), unit.Object, user.Object,
                Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IWorkflowStatusAdapterRegistry>(), Mock.Of<IWorkflowService>(),
                Mock.Of<ISupplierValidationService>(), source.Object, Mock.Of<IProcurementPurchaseOrderComplianceService>(),
                Mock.Of<IProcurementPurchaseOrderSodService>(), Mock.Of<IProcurementReceiptSourceControlService>(),
                Mock.Of<IProcurementReceiptInspectionService>(), Mock.Of<IProcurementReceiptDocumentService>(),
                Mock.Of<IProcurementControlEventService>(), Mock.Of<IProcurementBudgetCommitmentLifecycleService>(),
                new ErrorLogger(error => Error = error),
                new PurchaseOrderLandedCostPlanService(Mock.Of<IPurchaseOrderLandedCostPlanRepository>(),
                    Mock.Of<IPurchaseOrderLandedCostPlanItemRepository>(), Mock.Of<IBusinessPartnerRepository>()))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            return (controller, dto, unit);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            SqlConnection.ClearAllPools();
            // Only the exact newly created fixture database can be removed.
            if (!name.StartsWith("TdcPoRetryTest_") || !Guid.TryParseExact(name[15..], "N", out _))
                throw new InvalidOperationException("Unexpected fixture database name.");
            await using var drop = master.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
            await master.DisposeAsync();
        }
    }
}
