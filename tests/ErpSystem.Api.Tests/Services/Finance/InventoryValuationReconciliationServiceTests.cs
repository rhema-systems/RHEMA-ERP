using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
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

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, Guid periodId,
            InventoryValuationReconciliationService service,
            Mock<IProcurementControlEventService> controlEvents)
        {
            Db = db;
            PeriodId = periodId;
            Service = service;
            ControlEvents = controlEvents;
        }

        public ApplicationDbContext Db { get; }
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
            return new Fixture(db, period.Id, service, events);
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
