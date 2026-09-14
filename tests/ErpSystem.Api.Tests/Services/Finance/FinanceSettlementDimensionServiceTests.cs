using System.Reflection;
using System.Collections;
using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
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
                [
                    Component(FinanceSettlementComponentType.Principal, 100m, 100m),
                    Component(FinanceSettlementComponentType.Discount, 1m, 1m),
                    Component(FinanceSettlementComponentType.WithholdingTax, 2m, 2m),
                    Component(FinanceSettlementComponentType.Fee, 0.01m, 0.01m),
                    Component(FinanceSettlementComponentType.WriteOff, 0.02m, 0.02m),
                    Component(FinanceSettlementComponentType.RealizedFx, 0m, 0.01m)
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
                [Component(FinanceSettlementComponentType.Principal, 10m, 10m)],
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
    public async Task DerivedSettlementPostingFailsRequiredRulesDuringCaptureOptional()
    {
        await using var fixture = await Fixture.CreateAsync();
        var targetAccount = new Account
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            AccountCode = "7190", AccountNumber = "7190", AccountName = "Settlement adjustment",
            AccountType = AccountType.Expense, CurrencyCode = "GHS", Status = AccountStatus.Active,
            AllowDirectPosting = true
        };
        var definition = new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(), TenantId = targetAccount.TenantId, Code = "PROJECT", Name = "Project",
            Classification = "Analytical", ValueSourceType = "Lookup", IsActive = true
        };
        fixture.Context.Accounts.Add(targetAccount);
        fixture.Context.FinanceDimensionDefinitions.Add(definition);
        fixture.Context.FinanceDimensionAccountRules.Add(new FinanceDimensionAccountRule
        {
            Id = Guid.NewGuid(), TenantId = targetAccount.TenantId, RuleFamilyId = Guid.NewGuid(), RuleVersion = 1,
            AccountId = targetAccount.Id, FinanceDimensionDefinitionId = definition.Id, RuleType = "Required",
            RouteId = FinanceDimensionRouteId.FinanceApVendorPayment, SourceModule = "AP",
            SourceDocumentType = "VendorPayment", PostingAction = "Post",
            EffectiveDate = new DateTime(2025, 1, 1), IsActive = true
        });
        await fixture.Context.SaveChangesAsync();
        var allocationId = Guid.NewGuid();
        var evidence = await fixture.Service.SynchronizeDraftAsync(
            fixture.Producer, fixture.DocumentId, [fixture.Allocation(allocationId, 10m)]);

        var action = () => fixture.Service.ResolvePostingDimensionsAsync(
            fixture.Producer, evidence[0].Id, targetAccount.Id, new DateTime(2026, 1, 1));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Required Finance dimensions*");
    }

    [Fact]
    public async Task PaymentAdapterUsesAllocationAndInvoiceLineIdsAndPreservesExactSourceEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"finance-payment-adapter-{Guid.NewGuid():N}").Options);
        var firstLineId = Guid.Parse("00000000-0000-0000-0000-000000000011");
        var finalLineId = Guid.Parse("00000000-0000-0000-0000-000000000022");
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InvoiceNumber = "VI-DIM-1",
            SupplierId = Guid.NewGuid(), InvoiceDate = new DateTime(2026, 1, 1), CurrencyCode = "GHS",
            ExchangeRate = 1m
        };
        invoice.LineItems.Add(new VendorInvoiceLineItem
        {
            Id = finalLineId, TenantId = tenantId, VendorInvoiceId = invoice.Id,
            Description = "Final", Quantity = 1m, UnitPrice = 70m
        });
        invoice.LineItems.Add(new VendorInvoiceLineItem
        {
            Id = firstLineId, TenantId = tenantId, VendorInvoiceId = invoice.Id,
            Description = "First", Quantity = 1m, UnitPrice = 30m
        });
        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PaymentNumber = "VP-DIM-1",
            SupplierId = invoice.SupplierId, PaymentDate = new DateTime(2026, 2, 1),
            TotalAmount = 12m, CurrencyCode = "GHS", ExchangeRate = 1m
        };
        var allocation = new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, VendorPaymentId = payment.Id,
            VendorInvoiceId = invoice.Id, VendorPayment = payment, VendorInvoice = invoice,
            AllocatedAmount = 10m, PaymentCurrencyAmount = 10m,
            InvoiceCurrencyCode = "GHS", PaymentCurrencyCode = "GHS",
            PaymentExchangeRate = 1m, InvoiceSettlementExchangeRate = 1m,
            PaymentFunctionalAmount = 10m, SettlementFunctionalAmount = 14m,
            DiscountAmount = 1m, DiscountFunctionalAmount = 1m,
            WithholdingTaxAmount = 1m, WithholdingTaxFunctionalAmount = 1m,
            AllocationDate = new DateTime(2026, 2, 1)
        };
        payment.Allocations.Add(allocation);
        var firstSetId = Guid.NewGuid();
        var finalSetId = Guid.NewGuid();
        var firstSnapshotId = Guid.NewGuid();
        var finalSnapshotId = Guid.NewGuid();
        var invoiceRoute = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceApVendorInvoice);
        db.AddRange(invoice, payment);
        db.FinanceSourceDimensionAssignments.AddRange(
            SourceAssignment(tenantId, invoiceRoute, invoice.Id, firstLineId, firstSetId, firstSnapshotId),
            SourceAssignment(tenantId, invoiceRoute, invoice.Id, finalLineId, finalSetId, finalSnapshotId));
        await db.SaveChangesAsync();
        IReadOnlyList<FinanceSettlementAllocationInput>? captured = null;
        var settlements = new Mock<IFinanceSettlementDimensionService>();
        settlements.Setup(item => item.SynchronizeDraftAsync(
                It.IsAny<FinancePostingProducerContext>(), payment.Id,
                It.IsAny<IReadOnlyList<FinanceSettlementAllocationInput>>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingProducerContext, Guid, IReadOnlyList<FinanceSettlementAllocationInput>, CancellationToken>(
                (_, _, inputs, _) => captured = inputs)
            .ReturnsAsync(Array.Empty<FinanceSettlementDimensionComponentDto>());
        var user = CurrentUser(tenantId);

        await new FinancePaymentDimensionAdapter(db, user.Object, settlements.Object)
            .SynchronizeVendorPaymentAsync(payment.Id);

        captured.Should().ContainSingle();
        var input = captured![0];
        input.SettlementSourceLineId.Should().Be(allocation.Id);
        input.SettlementAllocationId.Should().Be(allocation.Id);
        input.OriginatingLines.Select(item => item.OriginatingSourceLineId)
            .Should().ContainInOrder(firstLineId, finalLineId);
        input.OriginatingLines.Select(item => item.AllocationWeight).Should().ContainInOrder(30m, 70m);
        input.OriginatingLines.Select(item => item.FinanceDimensionSetId)
            .Should().ContainInOrder(firstSetId, finalSetId);
        input.Components.Select(item => item.ComponentType).Should().BeEquivalentTo(new[]
        {
            FinanceSettlementComponentType.Principal,
            FinanceSettlementComponentType.Discount,
            FinanceSettlementComponentType.WithholdingTax,
            FinanceSettlementComponentType.RealizedFx
        });
        input.Components.Single(item => item.ComponentType == FinanceSettlementComponentType.RealizedFx)
            .FunctionalAmount.Should().Be(2m);
    }

    [Fact]
    public async Task CustomerPaymentAdapterKeepsVatWithholdingAndExactInvoiceLineEvidenceSeparate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"finance-customer-payment-adapter-{Guid.NewGuid():N}").Options);
        var firstLineId = Guid.Parse("00000000-0000-0000-0000-000000000031");
        var finalLineId = Guid.Parse("00000000-0000-0000-0000-000000000032");
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InvoiceNumber = "CI-DIM-1",
            BusinessPartnerId = Guid.NewGuid(), CustomerName = "Dimension customer",
            InvoiceDate = new DateTime(2026, 1, 1), CurrencyCode = "GHS", ExchangeRate = 1m
        };
        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = finalLineId, TenantId = tenantId, InvoiceId = invoice.Id,
            Description = "Final", Quantity = 1m, UnitPrice = 75m
        });
        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = firstLineId, TenantId = tenantId, InvoiceId = invoice.Id,
            Description = "First", Quantity = 1m, UnitPrice = 25m
        });
        var payment = new CustomerPayment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PaymentNumber = "CR-DIM-1",
            CustomerId = invoice.BusinessPartnerId, PaymentDate = new DateTime(2026, 2, 1),
            TotalAmount = 13m, CurrencyCode = "GHS", ExchangeRate = 1m,
            PaymentMethod = "BankTransfer", Status = "Pending"
        };
        var allocation = new PaymentAllocation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CustomerPaymentId = payment.Id,
            InvoiceId = invoice.Id, CustomerPayment = payment, Invoice = invoice,
            AllocatedAmount = 10m, PaymentCurrencyAmount = 10m,
            InvoiceCurrencyCode = "GHS", PaymentCurrencyCode = "GHS",
            PaymentExchangeRate = 1m, InvoiceSettlementExchangeRate = 1m,
            PaymentFunctionalAmount = 10m, SettlementFunctionalAmount = 15m,
            DiscountAmount = 1m, DiscountFunctionalAmount = 1m,
            WithholdingTaxAmount = 1m, WithholdingTaxFunctionalAmount = 1m,
            VatWithholdingAmount = 1m, VatWithholdingFunctionalAmount = 1m,
            AllocationDate = new DateTime(2026, 2, 1)
        };
        payment.Allocations.Add(allocation);
        var firstSetId = Guid.NewGuid();
        var finalSetId = Guid.NewGuid();
        var firstSnapshotId = Guid.NewGuid();
        var finalSnapshotId = Guid.NewGuid();
        var invoiceRoute = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceArCustomerInvoice);
        db.AddRange(invoice, payment);
        db.FinanceSourceDimensionAssignments.AddRange(
            SourceAssignment(tenantId, invoiceRoute, invoice.Id, firstLineId, firstSetId, firstSnapshotId),
            SourceAssignment(tenantId, invoiceRoute, invoice.Id, finalLineId, finalSetId, finalSnapshotId));
        await db.SaveChangesAsync();
        IReadOnlyList<FinanceSettlementAllocationInput>? captured = null;
        var settlements = new Mock<IFinanceSettlementDimensionService>();
        settlements.Setup(item => item.SynchronizeDraftAsync(
                It.IsAny<FinancePostingProducerContext>(), payment.Id,
                It.IsAny<IReadOnlyList<FinanceSettlementAllocationInput>>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingProducerContext, Guid, IReadOnlyList<FinanceSettlementAllocationInput>, CancellationToken>(
                (_, _, inputs, _) => captured = inputs)
            .ReturnsAsync(Array.Empty<FinanceSettlementDimensionComponentDto>());

        await new FinancePaymentDimensionAdapter(db, CurrentUser(tenantId).Object, settlements.Object)
            .SynchronizeCustomerPaymentAsync(payment.Id);

        captured.Should().ContainSingle();
        var input = captured![0];
        input.SettlementSourceLineId.Should().Be(allocation.Id);
        input.OriginatingLines.Select(item => item.OriginatingSourceLineId)
            .Should().ContainInOrder(firstLineId, finalLineId);
        input.OriginatingLines.Select(item => item.AllocationWeight).Should().ContainInOrder(25m, 75m);
        input.OriginatingLines.Select(item => item.FinanceDimensionSnapshotId)
            .Should().ContainInOrder(firstSnapshotId, finalSnapshotId);
        input.Components.Select(item => item.ComponentType).Should().BeEquivalentTo(new[]
        {
            FinanceSettlementComponentType.Principal,
            FinanceSettlementComponentType.Discount,
            FinanceSettlementComponentType.WithholdingTax,
            FinanceSettlementComponentType.VatWithholdingTax,
            FinanceSettlementComponentType.RealizedFx
        });
        input.Components.Single(item => item.ComponentType == FinanceSettlementComponentType.RealizedFx)
            .FunctionalAmount.Should().Be(2m);
    }

    [Fact]
    public void TransferDerivedAmountUsesStableLegOrderAndPlacesResidualOnFinalLeg()
    {
        var first = Guid.Parse("00000000-0000-0000-0000-000000000041");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000042");
        var third = Guid.Parse("00000000-0000-0000-0000-000000000043");
        var method = typeof(CashTransactionService).GetMethod(
            "AllocateTransferDerivedAmount", BindingFlags.Static | BindingFlags.NonPublic)!;
        var raw = (IEnumerable)method.Invoke(null, new object[]
        {
            100m,
            new (Guid SourceLineId, decimal Weight)[] { (third, 1m), (first, 1m), (second, 1m) }
        })!;
        var shares = raw.Cast<object>().Select(item => new
        {
            SourceLineId = (Guid)item.GetType().GetProperty("SourceLineId")!.GetValue(item)!,
            Amount = (decimal)item.GetType().GetProperty("Amount")!.GetValue(item)!,
            IsFinal = (bool)item.GetType().GetProperty("IsFinalResidualRecipient")!.GetValue(item)!,
            Residual = (decimal)item.GetType().GetProperty("RoundingResidualAmount")!.GetValue(item)!
        }).ToArray();

        shares.Select(item => item.SourceLineId).Should().ContainInOrder(first, second, third);
        shares.Select(item => item.Amount).Should().ContainInOrder(33.33m, 33.33m, 33.34m);
        shares.Sum(item => item.Amount).Should().Be(100m);
        shares.Should().ContainSingle(item => item.IsFinal && item.Residual == 0.01m)
            .Which.SourceLineId.Should().Be(third);
    }

    [Fact]
    public void MigrationCreatesOnlyTheGenericSettlementEvidenceStore()
    {
        var source = ArchivedMigrationSource.Read("20260831231434_AddFinanceSettlementDimensions.cs");
        foreach (var token in new[] { "FinanceSettlementDimensionComponents", "SettlementSourceLineId",
            "OriginatingSourceLineId", "FinanceDimensionSnapshotId", "RoundingResidualFunctionalAmount",
            "ComparisonExchangeRateId", "ComparisonExchangeRate", "onDelete: ReferentialAction.Restrict",
            "Migration(\"20260831231434_AddFinanceSettlementDimensions\")" }) source.Should().Contain(token);
    }

    private static FinanceSettlementComponentAmountInput Component(
        FinanceSettlementComponentType type,
        decimal transactionAmount,
        decimal functionalAmount) =>
        new(type, "GHS", transactionAmount, functionalAmount, null, 1m);

    private static FinanceSourceDimensionAssignment SourceAssignment(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        Guid documentId,
        Guid lineId,
        Guid setId,
        Guid snapshotId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, RouteId = route.Id,
        ProducerModule = route.ProducerModule, SourceRoute = route.SourceRoute,
        SourceDocumentType = route.DocumentType, ContractVersion = route.ContractVersion,
        SourceDocumentId = documentId, SourceLineId = lineId,
        FinanceDimensionSetId = setId, FinanceDimensionSnapshotId = snapshotId
    };

    private static Mock<ICurrentUserService> CurrentUser(Guid tenantId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        user.SetupGet(item => item.UserName).Returns("finance.payment.adapter.test");
        user.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
        return user;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required ApplicationDbContext Context { get; init; }
        public required FinanceSettlementDimensionService Service { get; init; }
        public required FinancePostingProducerContext Producer { get; init; }
        public required Guid DocumentId { get; init; }
        public required Guid TenantId { get; init; }
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
            [Component(FinanceSettlementComponentType.Principal, amount, amount)],
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
                Service = new FinanceSettlementDimensionService(
                    db, user.Object, new FinanceDimensionAdministrationService(db, user.Object)),
                Producer = new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceApVendorPayment),
                DocumentId = Guid.NewGuid(),
                TenantId = tenantId,
                InvoiceId = Guid.NewGuid(),
                LineIds = lineIds,
                SetIds = sets.Select(item => item.Id).ToArray(),
                SnapshotIds = snapshots.Select(item => item.Id).ToArray()
            };
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
