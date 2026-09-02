using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ExternalFinanceDimensionAdapterContractTests
{
    [Fact]
    public void Every_external_contract_maps_to_a_distinct_non_finance_capture_optional_route()
    {
        var contracts = Enum.GetValues<FinanceExternalProducerContractId>();
        var routes = contracts.Select(FinanceExternalProducerContractCatalog.GetRequired)
            .Select(context => context.Definition).ToArray();

        routes.Select(route => route.Id).Should().OnlyHaveUniqueItems();
        routes.Should().OnlyContain(route =>
            route.ProducerModule != "Finance"
            && route.DefaultState == FinanceDimensionCertificationState.CaptureOptional
            && route.RequiresReadinessProvider
            && route.Grain == FinanceDimensionGrain.SourceDocumentLine);
        routes.Where(route => route.Id != FinanceDimensionRouteId.SalesCreditNote)
            .Should().OnlyContain(route => (int)route.Id > 60);
    }

    [Fact]
    public void External_contract_catalog_cannot_resolve_manual_finance_routes()
    {
        Enum.GetValues<FinanceExternalProducerContractId>()
            .Select(FinanceExternalProducerContractCatalog.GetRequired)
            .Should().OnlyContain(context => context.RouteId != FinanceDimensionRouteId.ManualJournalEntry
                                             && !context.Definition.SourceRoute.Contains(".manual", StringComparison.Ordinal));
    }

    [Fact]
    public void Stable_line_identity_is_repeatable_and_separates_semantics_and_components()
    {
        var documentId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var first = FinanceExternalDimensionIdentity.SourceLine(
            FinanceExternalProducerContractId.InventoryStockAdjustment, documentId, "inventory-control", componentId);

        FinanceExternalDimensionIdentity.SourceLine(
                FinanceExternalProducerContractId.InventoryStockAdjustment, documentId, "inventory-control", componentId)
            .Should().Be(first);
        FinanceExternalDimensionIdentity.SourceLine(
                FinanceExternalProducerContractId.InventoryStockAdjustment, documentId, "writeoff-expense", componentId)
            .Should().NotBe(first);
        FinanceExternalDimensionIdentity.SourceLine(
                FinanceExternalProducerContractId.InventoryLandedCost, documentId, "inventory-control", componentId)
            .Should().NotBe(first);
    }

    [Theory]
    [InlineData(FinanceExternalProducerContractId.QuantitySurveyPaymentCertificate, FinanceModuleLockCatalog.QuantitySurvey)]
    [InlineData(FinanceExternalProducerContractId.EstateGroundRentCharge, FinanceModuleLockCatalog.Estate)]
    [InlineData(FinanceExternalProducerContractId.LegalTransferFeeBilling, FinanceModuleLockCatalog.Legal)]
    [InlineData(FinanceExternalProducerContractId.MaintenanceWorkOrderBilling, FinanceModuleLockCatalog.Maintenance)]
    public void Newly_discovered_producers_have_dedicated_period_lock_modules(
        FinanceExternalProducerContractId contractId,
        string expectedModule)
    {
        var route = FinanceExternalProducerContractCatalog.GetRequired(contractId).Definition;
        FinanceModuleLockCatalog.TryGetDefinition(expectedModule, out var module).Should().BeTrue();
        module.Name.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Should().Contain(route.ProducerModule.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Provisional_external_readiness_is_tenant_scoped_and_blocks_promotion_without_producer_census()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"external-readiness-{Guid.NewGuid():N}").Options);
        var route = FinanceExternalProducerContractCatalog.GetRequired(
            FinanceExternalProducerContractId.EstateGroundRentCharge).Definition;
        var provider = new ExternalProducerDimensionReadinessProvider(db, route.Id);

        var result = await provider.EvaluateAsync(Guid.NewGuid(), route);

        result.Blockers.Should().ContainSingle(blocker => blocker.Code == "PRODUCER_ADOPTION_CENSUS_REQUIRED");
        result.DataVersionWatermark.Should().StartWith("external-adapter:1.0:");
    }

    [Fact]
    public async Task Adapter_rejects_cross_tenant_payload_before_dimension_or_posting_services_run()
    {
        var authenticatedTenant = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"external-adapter-tenant-{Guid.NewGuid():N}").Options);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(authenticatedTenant);
        var dimensions = new Mock<IFinanceSourceDimensionService>(MockBehavior.Strict);
        var posting = new Mock<IFinancePostingEngine>(MockBehavior.Strict);
        var adapter = new ExternalFinancePostingAdapter(db, currentUser.Object, dimensions.Object, posting.Object);

        var action = () => adapter.PostAsync(ValidEnvelope(Guid.NewGuid()));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*authenticated tenant*");
        dimensions.VerifyNoOtherCalls();
        posting.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Adapter_rejects_unbalanced_economic_lines_before_persistence()
    {
        var tenant = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"external-adapter-balance-{Guid.NewGuid():N}").Options);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenant);
        var dimensions = new Mock<IFinanceSourceDimensionService>(MockBehavior.Strict);
        var posting = new Mock<IFinancePostingEngine>(MockBehavior.Strict);
        var adapter = new ExternalFinancePostingAdapter(db, currentUser.Object, dimensions.Object, posting.Object);
        var envelope = ValidEnvelope(tenant);
        envelope.Lines[1].CreditAmount = 99m;

        var action = () => adapter.PostAsync(envelope);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*not balanced*");
        dimensions.VerifyNoOtherCalls();
        posting.VerifyNoOtherCalls();
    }

    private static FinanceExternalPostingEnvelopeDto ValidEnvelope(Guid tenant) => new()
    {
        ContractId = FinanceExternalProducerContractId.InventoryDisposalProceeds,
        TenantId = tenant,
        SourceDocumentId = Guid.NewGuid(),
        SourceDocumentReference = "DISP-001",
        Description = "Approved disposal proceeds",
        PostingDate = DateTime.UtcNow.Date,
        FunctionalCurrencyCode = "GHS",
        IdempotencyKey = "disposal-proceeds-v1",
        SourceApproved = true,
        ApprovedByUserId = Guid.NewGuid(),
        ApprovedAtUtc = DateTime.UtcNow,
        ApprovalReference = "WF-001",
        SourceEvidenceHash = new string('A', 64),
        Lines =
        [
            new FinancePostingLineDto
            {
                SourceDocumentLineId = Guid.NewGuid(), AccountId = Guid.NewGuid(), DebitAmount = 100m
            },
            new FinancePostingLineDto
            {
                SourceDocumentLineId = Guid.NewGuid(), AccountId = Guid.NewGuid(), CreditAmount = 100m
            }
        ]
    };
}
