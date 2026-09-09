using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Inventory;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Inventory;

/// <summary>
/// Shared-connection relational foundation for the disposal C7 owner tests.  It deliberately uses
/// SQLite transactions: the old E2E013 InMemory fixture suppresses transaction semantics and is not
/// evidence for zero-or-all disposal completion.
/// </summary>
public sealed class InventoryDisposalC7SqliteFixtureTests
{
    [Theory]
    [InlineData(InventoryDisposalMethod.Donation)]
    [InlineData(InventoryDisposalMethod.Destruction)]
    public async Task Valuation_only_disposals_retain_no_proceeds_in_the_relational_owner_fixture(
        InventoryDisposalMethod method)
    {
        await using var fixture = await Fixture.CreateAsync();

        var disposal = new InventoryDisposalCase
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Method = method,
            ProceedsAmount = 0m, ProceedsAccountId = null, Status = InventoryDisposalStatus.AdjustmentPending
        };
        fixture.Db.InventoryDisposalCases.Add(disposal);
        await fixture.Db.SaveChangesAsync();

        var durable = await fixture.Db.InventoryDisposalCases.SingleAsync(value => value.Id == disposal.Id);
        durable.ProceedsAmount.Should().Be(0m);
        durable.ProceedsAccountId.Should().BeNull();
        durable.Method.Should().Be(method);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Db { get; }
        public Guid TenantId { get; }

        private Fixture(SqliteConnection connection, ApplicationDbContext db, Guid tenantId)
        {
            _connection = connection; Db = db; TenantId = tenantId;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var tenant = new Tenant { Id = Guid.NewGuid(), Code = "C7SQLITE", Name = "C7 SQLite fixture" };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            return new Fixture(connection, db, tenant.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        // Kept beside the relational connection so follow-up C7 owner tests cannot accidentally
        // replace the disposal participant or C9 builder with the old E2E013 mocks.
        public StockAdjustmentValuationIntentBuilder CreateValuationBuilder() => new(Db);

        public IInventoryDisposalStockAdjustmentParticipant CreateDisposalParticipant(
            ICurrentUserProvider currentUser, IUnitOfWork unitOfWork) =>
            new InventoryDisposalStockAdjustmentParticipant(new StockAdjustmentService(
                new StockAdjustmentRepository(Db), new InventoryItemRepository(Db), new StockMovementRepository(Db),
                new WarehouseQuantityRepository(Db), new WarehouseLocationRepository(Db), new WarehouseRepository(Db),
                Mock.Of<IConsignmentSettlementService>(), currentUser, unitOfWork,
                Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(),
                Mock.Of<IProcurementAccessControlService>(), Mock.Of<IProcurementSodGuardService>(),
                Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IProcurementControlEventService>(),
                Mock.Of<IInventoryAdjustmentFinancePostingService>(), Mock.Of<IInventoryValuationService>(),
                NullLogger<StockAdjustmentService>.Instance));
    }

    /// <summary>Owner-test seam: it writes Finance-shaped evidence into the owner's ambient context.
    /// A requested fault is thrown before the owner commits, so SQLite verifies rollback of both sides.</summary>
    private sealed class AmbientC7Execution(ApplicationDbContext db) : IFinanceProducerApprovedExecution
    {
        public bool FailAfterStage { get; set; }
        public int Calls { get; private set; }

        public async Task<FinanceProducerApprovedExecutionResult> ExecuteWithCompatibilityResultInAmbientTransactionAsync(
            Guid accountingEventId, ProducerAccountingIntentDto request, ProducerOwnerEffectReceiptDto receipt,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (db.Database.CurrentTransaction is null)
                throw new InvalidOperationException("C7 owner execution requires the disposal ambient transaction.");
            var marker = new Tenant { Id = Guid.NewGuid(), Code = $"C7-{Calls:D8}", Name = "C7 owner marker" };
            db.Tenants.Add(marker);
            await db.SaveChangesAsync(cancellationToken);
            if (FailAfterStage) throw new InvalidOperationException("Injected C7 leaf failure");
            return new FinanceProducerApprovedExecutionResult(accountingEventId, "C7-TEST", "Posted", Guid.NewGuid(), Guid.NewGuid());
        }

        public Task<AccountingEventDto> ExecuteInAmbientTransactionAsync(Guid accountingEventId,
            ProducerAccountingIntentDto request, ProducerOwnerEffectReceiptDto receipt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordFailureAfterRollbackAsync(Guid accountingEventId, ProducerAccountingIntentDto request,
            ProducerOwnerEffectReceiptDto receipt, Exception failure, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
