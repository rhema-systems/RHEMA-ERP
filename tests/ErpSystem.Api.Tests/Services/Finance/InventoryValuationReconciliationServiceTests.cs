using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class InventoryValuationReconciliationServiceTests
{
    [Fact, Trait("Batch", "TDC-0613")]
    public async Task Generate_creates_a_clean_immutable_snapshot_and_replays_idempotently()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new GenerateInventoryValuationReconciliationRequest
        {
            FiscalPeriodId = fixture.PeriodId,
            ToleranceAmount = 0m,
            IdempotencyKey = "tdc0613-generate-clean",
            CorrelationId = "tdc0613-test-generate"
        };

        var first = await fixture.Service.GenerateAsync(request);
        var replay = await fixture.Service.GenerateAsync(request);

        first.Id.Should().Be(replay.Id);
        first.Status.Should().Be(InventoryValuationReconciliationStatus.Reconciled);
        first.ExceptionCount.Should().Be(0);
        first.ReconciliationVariance.Should().Be(0m);
        first.Actions.Should().ContainSingle(action =>
            action.ActionType == InventoryValuationReconciliationActionType.Generated);
        (await fixture.Db.InventoryValuationReconciliations.CountAsync()).Should().Be(1);
        (await fixture.Db.InventoryValuationReconciliationActions.CountAsync()).Should().Be(1);
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(value =>
                value.RuleCode == "TDC-0613" &&
                value.Result == ProcurementControlEventResult.Allowed &&
                value.DecisionKeys.Count == 14),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Batch", "TDC-0613")]
    public async Task Accepted_non_stock_receipt_does_not_require_an_inventory_valuation_movement()
    {
        await using var fixture = await Fixture.CreateAsync();
        var purchaseOrderId = Guid.NewGuid();
        var purchaseOrderItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, PurchaseOrderId = purchaseOrderId,
            InventoryItemId = null, ItemDescription = "Professional services",
            OrderedQuantity = 1m, ReceivedQuantity = 1m, RemainingQuantity = 0m,
            UnitOfMeasure = "SERVICE", UnitPrice = 250m, LineTotal = 250m
        };
        var receipt = new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, PurchaseOrderId = purchaseOrderId,
            ReceiptNumber = "POR-SERVICE-001", ReceiptDate = new DateTime(2026, 12, 10),
            Status = "Accepted"
        };
        receipt.Items.Add(new PurchaseOrderReceiptItem
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ReceiptId = receipt.Id,
            PurchaseOrderItemId = purchaseOrderItem.Id, PurchaseOrderItem = purchaseOrderItem,
            ReceivedQuantity = 1m, AcceptedQuantity = 1m, UnitOfMeasure = "SERVICE"
        });
        fixture.Db.PurchaseOrderItems.Add(purchaseOrderItem);
        fixture.Db.PurchaseOrderReceipts.Add(receipt);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GenerateAsync(new GenerateInventoryValuationReconciliationRequest
        {
            FiscalPeriodId = fixture.PeriodId,
            ToleranceAmount = 0m,
            IdempotencyKey = "tdc0613-non-stock-receipt",
            CorrelationId = "tdc0613-non-stock-receipt"
        });

        result.Exceptions.Should().NotContain(value =>
            value.Code == "INV_RECEIPT_VALUATION_MISSING" && value.Reference == receipt.ReceiptNumber);
        result.Status.Should().Be(InventoryValuationReconciliationStatus.Reconciled);
    }

    [Fact, Trait("Batch", "TDC-0613")]
    public async Task Generator_cannot_freeze_own_reconciliation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var generated = await fixture.Service.GenerateAsync(new GenerateInventoryValuationReconciliationRequest
        {
            FiscalPeriodId = fixture.PeriodId,
            ToleranceAmount = 0m,
            IdempotencyKey = "tdc0613-generate-sod",
            CorrelationId = "tdc0613-test-sod"
        });
        var row = await fixture.Db.InventoryValuationReconciliations.SingleAsync(value => value.Id == generated.Id);
        row.RowVersion = [1, 2, 3, 4];
        await fixture.Db.SaveChangesAsync();

        var action = async () => await fixture.Service.FreezeAsync(row.Id,
            new FreezeInventoryValuationReconciliationRequest
            {
                RowVersion = Convert.ToBase64String(row.RowVersion),
                Reason = "Independent year-end freeze",
                IdempotencyKey = "tdc0613-freeze-sod",
                CorrelationId = "tdc0613-test-sod"
            });

        var error = await action.Should().ThrowAsync<InventoryValuationReconciliationException>();
        error.Which.Code.Should().Be("INV_VALUATION_RECONCILIATION_SOD");
        (await fixture.Db.InventoryValuationReconciliationActions.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("INV-RECEIPT-CONTROL")]
    [InlineData("RTV-Dispatch-Inventory")]
    public async Task Reconciliation_includes_item_mappings_and_historical_inventory_accounts_in_same_tenant(string historicalTag)
    {
        await using var fixture = await Fixture.CreateAsync();
        var globalAccountId = (await fixture.Db.FinanceSettings.SingleAsync()).ControlAccountInventoryId!.Value;
        var mappedAccount = new Account { TenantId = fixture.TenantId, AccountCode = "ITEM-INV", AccountNumber = "1301", AccountName = "Item inventory", AccountType = AccountType.Asset };
        var historicalAccount = new Account { TenantId = fixture.TenantId, AccountCode = "OLD-INV", AccountNumber = "1302", AccountName = "Old inventory", AccountType = AccountType.Asset };
        fixture.Db.Accounts.AddRange(mappedAccount, historicalAccount);
        var item = new InventoryItem { TenantId = fixture.TenantId, ItemCode = "ITEM-001", Name = "Item", InventoryAccountId = mappedAccount.Id };
        fixture.Db.InventoryItems.Add(item);
        fixture.Db.InventoryMovements.Add(new InventoryMovement { TenantId = fixture.TenantId, InventoryItemId = item.Id,
            WarehouseId = Guid.NewGuid(), MovementNumber = "ADJ-001", MovementType = InventoryMovementType.AdjustmentIn,
            Direction = MovementDirection.In, TotalValue = 90m, Quantity = 9m, UnitCost = 10m, IsPosted = true, PostingDate = new DateTime(2026, 12, 10) });
        fixture.Db.InventoryBalances.Add(new InventoryBalance { TenantId = fixture.TenantId, InventoryItemId = item.Id, WarehouseId = Guid.NewGuid(), QuantityOnHand = 9m, TotalValue = 90m });
        foreach (var (accountId, amount, tag) in new[] { (globalAccountId, 30m, "INV-ADJ-CONTROL"), (mappedAccount.Id, 20m, "INV-ADJ-CONTROL"), (historicalAccount.Id, 40m, historicalTag) })
            fixture.Db.AccountTransactions.Add(new AccountTransaction { TenantId = fixture.TenantId, AccountId = accountId, JournalEntryId = Guid.NewGuid(), DebitAmount = amount, PostingStatus = "Posted", TransactionDate = new DateTime(2026, 12, 10), TransactionTag = tag });
        fixture.Db.AccountTransactions.Add(new AccountTransaction { TenantId = Guid.NewGuid(), AccountId = mappedAccount.Id, JournalEntryId = Guid.NewGuid(), DebitAmount = 100m, PostingStatus = "Posted", TransactionDate = new DateTime(2026, 12, 10), TransactionTag = "INV-ADJ-CONTROL" });
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GenerateAsync(new GenerateInventoryValuationReconciliationRequest { FiscalPeriodId = fixture.PeriodId, ToleranceAmount = 0m, IdempotencyKey = "item-account-snapshot", CorrelationId = "item-account-test" });
        result.GeneralLedgerValue.Should().Be(90m);
        result.ReconciliationVariance.Should().Be(0m);
        result.Exceptions.Should().NotContain(value => value.Code == "INV_VALUATION_GL_MISMATCH");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, Guid tenantId, Guid periodId,
            InventoryValuationReconciliationService service,
            Mock<IProcurementControlEventService> controlEvents)
        {
            Db = db;
            TenantId = tenantId;
            PeriodId = periodId;
            Service = service;
            ControlEvents = controlEvents;
        }

        public ApplicationDbContext Db { get; }
        public Guid TenantId { get; }
        public Guid PeriodId { get; }
        public InventoryValuationReconciliationService Service { get; }
        public Mock<IProcurementControlEventService> ControlEvents { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var user = new TestCurrentUser(Guid.NewGuid(), tenantId);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tdc0613-reconciliation-{Guid.NewGuid():N}")
                .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var db = new ApplicationDbContext(options);
            var year = new FiscalYear
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FiscalYearName = "FY 2026",
                FiscalYearCode = "2026", Year = 2026, StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31), TotalDays = 365, NumberOfPeriods = 12,
                Status = "Open", IsActive = true
            };
            var period = new FiscalPeriod
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FiscalYearId = year.Id, FiscalYear = year,
                PeriodName = "December 2026", PeriodCode = "2026-12", PeriodNumber = 12,
                PeriodType = PeriodType.Monthly, StartDate = new DateTime(2026, 12, 1),
                EndDate = new DateTime(2026, 12, 31), PeriodDays = 31, PeriodStatus = "Open",
                IsOpen = true, IsClosed = false, IsLocked = false, IsYearEnd = true
            };
            var inventoryAccount = new Account
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "INV-CTRL",
                AccountNumber = "1300", AccountName = "Inventory Control",
                AccountType = AccountType.Asset, CurrencyCode = "GHS"
            };
            db.FiscalYears.Add(year);
            db.FiscalPeriods.Add(period);
            db.Accounts.Add(inventoryAccount);
            db.FinanceSettings.Add(new FinanceSettings
            {
                Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS",
                ControlAccountInventoryId = inventoryAccount.Id
            });
            await db.SaveChangesAsync();

            var events = new Mock<IProcurementControlEventService>();
            events.Setup(service => service.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            var service = new InventoryValuationReconciliationService(
                db, user, Mock.Of<IFiscalPeriodService>(), events.Object);
            return new Fixture(db, tenantId, period.Id, service, events);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestCurrentUser(Guid userId, Guid tenantId) : ICurrentUserProvider
    {
        public Guid UserId { get; } = userId;
        public Guid TenantId { get; } = tenantId;
        public string Username => "tdc0613.test";
        public string FullName => "TDC 0613 Test";
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles => ["Finance Manager"];
        public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> Claims => new Dictionary<string, string>();
        public bool IsExternalUser => false;
        public string AuthenticationProvider => "Test";
    }
}
