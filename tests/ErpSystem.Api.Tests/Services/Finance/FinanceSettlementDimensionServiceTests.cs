using System.Reflection;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceSettlementDimensionServiceTests
{
    [Fact]
    public void RouteCatalogueRegistersExactlyTheFiveFinanceOwnedPr3RoutesAsCaptureOptional()
    {
        var ids = new[]
        {
            FinanceDimensionRouteId.FinanceApVendorPayment,
            FinanceDimensionRouteId.FinanceArCustomerPayment,
            FinanceDimensionRouteId.FinanceCashPayment,
            FinanceDimensionRouteId.FinanceCashReceipt,
            FinanceDimensionRouteId.FinanceCashBankTransfer
        };

        FinanceDimensionRouteCatalog.Routes.Where(route => ids.Contains(route.Id)).Should().HaveCount(5)
            .And.OnlyContain(route =>
                route.ProducerModule == "Finance"
                && route.DefaultState == FinanceDimensionCertificationState.CaptureOptional
                && route.RequiresReadinessProvider);
        FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceApVendorPayment).Grain
            .Should().Be(FinanceDimensionGrain.SettlementAllocationLine);
        FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceArCustomerPayment).Grain
            .Should().Be(FinanceDimensionGrain.SettlementAllocationLine);
        FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceCashBankTransfer).Notes
            .Should().Contain("independent");
    }

    [Fact]
    public async Task ProportionalComponentsRemainSeparatedByExactOriginLineAndFinalResidualReconciles()
    {
        await using var fixture = await Fixture.CreateAsync();
        var allocationId = Guid.NewGuid();
        var result = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            [new FinanceSettlementAllocationInput(
                allocationId,
                allocationId,
                fixture.InvoiceId,
                "ghs",
                Guid.NewGuid(),
                1m,
                [
                    new(FinanceSettlementComponentType.Principal, 100m, 100m),
                    new(FinanceSettlementComponentType.Discount, 1m, 1m),
                    new(FinanceSettlementComponentType.WithholdingTax, 2m, 2m),
                    new(FinanceSettlementComponentType.Fee, 0.01m, 0.01m),
                    new(FinanceSettlementComponentType.WriteOff, 0.02m, 0.02m),
                    new(FinanceSettlementComponentType.RealizedFx, 0m, 0.01m)
                ],
                fixture.Origins())]);

        result.Should().HaveCount(18);
        result.Where(item => item.ComponentType == FinanceSettlementComponentType.Principal)
            .Sum(item => item.TransactionAmount).Should().Be(100m);
        result.Where(item => item.ComponentType == FinanceSettlementComponentType.RealizedFx)
            .Sum(item => item.FunctionalAmount).Should().Be(0.01m);
        result.Where(item => item.ComponentType == FinanceSettlementComponentType.Principal)
            .Select(item => item.OriginatingSourceLineId).Should().OnlyHaveUniqueItems();
        result.Where(item => item.ComponentType == FinanceSettlementComponentType.Principal)
            .Select(item => item.FinanceDimensionSetId!.Value).Should().BeEquivalentTo(fixture.SetIds);

        var finalPrincipal = result.Single(item =>
            item.ComponentType == FinanceSettlementComponentType.Principal
            && item.IsFinalResidualRecipient);
        finalPrincipal.TransactionAmount.Should().Be(33.34m);
        finalPrincipal.RoundingResidualTransactionAmount.Should().Be(0.01m);
        result.Where(item => item.ComponentType == FinanceSettlementComponentType.Principal
                && !item.IsFinalResidualRecipient)
            .Should().OnlyContain(item => item.RoundingResidualTransactionAmount == 0m);
    }

    [Fact]
    public async Task FreezeRejectsMissingAuthoritativeAllocationAndEnforcedRouteRejectsEmptyEvidence()
    {
        await using var fixture = await Fixture.CreateAsync(enforced: true);
        var allocationId = Guid.NewGuid();
        await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            [new FinanceSettlementAllocationInput(
                allocationId,
                allocationId,
                fixture.InvoiceId,
                "GHS",
                null,
                1m,
                [new(FinanceSettlementComponentType.Principal, 10m, 10m)],
                [new FinanceSettlementOriginLineInput(Guid.NewGuid(), 1m, null, null)])]);

        var missing = () => fixture.Service.ValidateAndFreezeAsync(
            fixture.Producer, fixture.DocumentId, [allocationId, Guid.NewGuid()]);
        await missing.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing*");

        var empty = () => fixture.Service.ValidateAndFreezeAsync(
            fixture.Producer, fixture.DocumentId, [allocationId]);
        await empty.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inherited Finance dimension evidence*");
    }

    [Fact]
    public async Task ReopenedDraftSupersedesFrozenRowsWithoutRewritingHistoricalEvidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var allocationId = Guid.NewGuid();
        var initial = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            [fixture.Allocation(allocationId, 30m)]);
        await fixture.Service.ValidateAndFreezeAsync(fixture.Producer, fixture.DocumentId, [allocationId]);
        var frozenIds = initial.Select(item => item.Id).ToArray();

        var reopened = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer,
            fixture.DocumentId,
            [fixture.Allocation(allocationId, 45m)]);

        reopened.Should().OnlyContain(item => item.EvidenceFrozenAt == null);
        reopened.Select(item => item.Id).Should().NotIntersectWith(frozenIds);
        fixture.Context.FinanceSettlementDimensionComponents.IgnoreQueryFilters()
            .Where(item => frozenIds.Contains(item.Id))
            .Should().OnlyContain(item => item.IsDeleted && item.EvidenceFrozenAt.HasValue);
        reopened.Sum(item => item.TransactionAmount).Should().Be(45m);
    }

    [Fact]
    public void MigrationCreatesOnlyTheGenericSettlementEvidenceStore()
    {
        var migration = new AddFinanceSettlementDimensions();
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [up]);

        up.Operations.OfType<CreateTableOperation>().Should().ContainSingle(table =>
            table.Name == "FinanceSettlementDimensionComponents");
        up.Operations.OfType<CreateTableOperation>().Should().NotContain(table =>
            new[] { "VendorPayments", "CustomerPayments", "CashTransactions", "AccountTransactions" }
                .Contains(table.Name));
        up.Operations.OfType<AlterColumnOperation>().Should().BeEmpty();
        var table = up.Operations.OfType<CreateTableOperation>().Single();
        table.ForeignKeys.Should().OnlyContain(key => key.OnDelete == ReferentialAction.Restrict);
        table.Columns.Should().Contain(column => column.Name == "SettlementSourceLineId");
        table.Columns.Should().Contain(column => column.Name == "OriginatingSourceLineId");
        table.Columns.Should().Contain(column => column.Name == "FinanceDimensionSnapshotId");
        table.Columns.Should().Contain(column => column.Name == "RoundingResidualFunctionalAmount");
        typeof(AddFinanceSettlementDimensions).GetCustomAttribute<MigrationAttribute>()?.Id
            .Should().Be("20260831231434_AddFinanceSettlementDimensions");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required ApplicationDbContext Context { get; init; }
        public required FinanceSettlementDimensionService Service { get; init; }
        public required FinancePostingProducerContext Producer { get; init; }
        public required Guid DocumentId { get; init; }
        public required Guid InvoiceId { get; init; }
        public required Guid[] LineIds { get; init; }
        public required Guid[] SetIds { get; init; }
        public required Guid[] SnapshotIds { get; init; }

        public IReadOnlyList<FinanceSettlementOriginLineInput> Origins() =>
            LineIds.Select((lineId, index) => new FinanceSettlementOriginLineInput(
                lineId, 1m, SetIds[index], SnapshotIds[index])).ToArray();

        public FinanceSettlementAllocationInput Allocation(Guid allocationId, decimal amount) => new(
            allocationId,
            allocationId,
            InvoiceId,
            "GHS",
            null,
            1m,
            [new(FinanceSettlementComponentType.Principal, amount, amount)],
            Origins());

        public static async Task<Fixture> CreateAsync(bool enforced = false)
        {
            var tenantId = Guid.NewGuid();
            var db = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"finance-settlement-dimensions-{Guid.NewGuid():N}").Options);
            var lineIds = new[]
            {
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Guid.Parse("00000000-0000-0000-0000-000000000003")
            };
            var sets = lineIds.Select((_, index) => new FinanceDimensionSet
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CombinationHash = new string((char)('A' + index), 64),
                DisplayValue = $"DEPT=D{index + 1}"
            }).ToArray();
            var snapshots = sets.Select(set => new FinanceDimensionSnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FinanceDimensionSetId = set.Id,
                CombinationHashSnapshot = set.CombinationHash,
                DisplayValueSnapshot = set.DisplayValue,
                SnapshotSource = "SourceDocumentSubmission",
                SnapshotQuality = "Exact"
            }).ToArray();
            db.FinanceDimensionSets.AddRange(sets);
            db.FinanceDimensionSnapshots.AddRange(snapshots);
            if (enforced)
            {
                var route = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceApVendorPayment);
                db.FinanceDimensionRouteCertifications.Add(new FinanceDimensionRouteCertification
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RouteId = route.Id,
                    ProducerModule = route.ProducerModule,
                    SourceRoute = route.SourceRoute,
                    DocumentType = route.DocumentType,
                    ContractVersion = route.ContractVersion,
                    State = FinanceDimensionCertificationState.Enforced,
                    EffectiveDate = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();

            var user = new Mock<ICurrentUserService>();
            user.SetupGet(item => item.TenantId).Returns(tenantId);
            user.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
            user.SetupGet(item => item.UserName).Returns("finance.settlement.test");
            user.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
            return new Fixture
            {
                Context = db,
                Service = new FinanceSettlementDimensionService(db, user.Object),
                Producer = new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceApVendorPayment),
                DocumentId = Guid.NewGuid(),
                InvoiceId = Guid.NewGuid(),
                LineIds = lineIds,
                SetIds = sets.Select(item => item.Id).ToArray(),
                SnapshotIds = snapshots.Select(item => item.Id).ToArray()
            };
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
