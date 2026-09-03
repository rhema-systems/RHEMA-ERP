using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceIntegrationContractFoundationTests
{
    [Fact]
    public void CatalogueIdentifiersAndAvailableContractsShouldBeReleaseReady()
    {
        var contracts = FinanceIntegrationContractCatalog.Contracts;

        contracts.Should().NotBeEmpty();
        contracts.Select(contract => contract.Id).Should().OnlyHaveUniqueItems();
        contracts.Should().OnlyContain(contract =>
            contract.Id.StartsWith("FIN-INT-", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(contract.Name) &&
            !string.IsNullOrWhiteSpace(contract.ProducerOwner) &&
            !string.IsNullOrWhiteSpace(contract.FinanceOwner) &&
            !string.IsNullOrWhiteSpace(contract.Version) &&
            !string.IsNullOrWhiteSpace(contract.EntryPoint));

        // Available is a callable promise to another development team.  Planned and decision-bound
        // records may describe a proposed entry point, but an available record must name a stable
        // source document discriminator that Finance can expose in journal inquiry and audit.
        contracts.Where(contract => contract.Status == FinanceIntegrationContractStatus.Available)
            .Should().OnlyContain(contract =>
                !string.IsNullOrWhiteSpace(contract.SourceDocumentType) &&
                !contract.SourceDocumentType.StartsWith("To be agreed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PostingContractShouldExposeCanonicalAccountingBookV2AndDeprecatedV1()
    {
        var contract = FinanceIntegrationContractCatalog.GetRequired("FIN-INT-001");

        contract.Status.Should().Be(FinanceIntegrationContractStatus.Available);
        contract.Version.Should().Be("2.0");
        contract.EntryPoint.Should().Contain(nameof(FinancePostingProducerContext));
        contract.EntryPoint.Should().Contain(nameof(FinancePostingRequestV2Dto));
        contract.Notes.Should().Contain(nameof(FinancePostingRequestV2Dto.AccountingBookCode));
        FinanceIntegrationContractCatalog.GetRequired("FIN-INT-001-V1").Status
            .Should().Be(FinanceIntegrationContractStatus.Deprecated);
        typeof(FinancePostingLineDto).GetProperty(nameof(FinancePostingLineDto.Dimensions)).Should().NotBeNull();
        typeof(FinancePostingLineDto).GetProperty(nameof(FinancePostingLineDto.FinanceDimensionSetId)).Should().NotBeNull();
        typeof(FinanceSourceLineDimensionInputDto)
            .GetProperty(nameof(FinanceSourceLineDimensionInputDto.SourceLineId)).Should().NotBeNull();
    }

    [Fact]
    public void DimensionRouteCatalogueShouldKeepTrustedIdentityAndOwnershipBoundariesCodeOwned()
    {
        var routes = FinanceDimensionRouteCatalog.Routes;

        routes.Select(route => route.Id).Should().OnlyHaveUniqueItems();
        routes.Select(route => new
            {
                route.ProducerModule, route.SourceRoute, route.DocumentType, route.ContractVersion
            })
            .Should().OnlyHaveUniqueItems();

        routes.Where(route => route.Owner.StartsWith("Finance", StringComparison.Ordinal))
            .Select(route => route.Id).Should().Contain([
                FinanceDimensionRouteId.ManualJournalEntry,
                FinanceDimensionRouteId.FinanceApVendorInvoice,
                FinanceDimensionRouteId.FinanceApSupplierDebitNote,
                FinanceDimensionRouteId.FinanceArCustomerInvoice,
                FinanceDimensionRouteId.FinanceArCustomerPayment
            ]);

        var sales = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.SalesCreditNote);
        sales.ProducerModule.Should().Be("Sales");
        sales.PostingSourceModule.Should().Be("AR");
        sales.DefaultState.Should().Be(FinanceDimensionCertificationState.CaptureOptional);
        sales.Notes.Should().Contain("contract only");
        var customerPayment = routes.Should()
            .ContainSingle(route => route.DocumentType == "CustomerPayment")
            .Which;
        customerPayment.Id.Should().Be(FinanceDimensionRouteId.FinanceArCustomerPayment);
        customerPayment.ProducerModule.Should().Be("Finance");
        customerPayment.PostingSourceModule.Should().Be("AR");
        customerPayment.SourceRoute.Should().Be("finance.ar.customer-payments.manual");
        customerPayment.Owner.Should().Be("Finance / Accounts Receivable");
    }

    [Fact]
    public void DisposalContractShouldExposeExistingFinanceOwnedOrchestration()
    {
        var contract = FinanceIntegrationContractCatalog.GetRequired("fin-int-006");

        contract.Status.Should().Be(FinanceIntegrationContractStatus.Available);
        contract.EntryPoint.Should().Be("IAssetDisposalService.CompleteDisposalAsync");
        contract.LimitationId.Should().Be("FIN-LIM-0040");
        contract.SourceDocumentType.Should().Contain("FixedAssetDisposal");
        contract.SourceDocumentType.Should().Contain("CustomerInvoice");
        contract.SourceDocumentType.Should().Contain("CustomerPayment");
    }

    [Fact]
    public void ProcurementAssetContractShouldExposeFinanceOwnedAcceptedSupplyAdapter()
    {
        var contract = FinanceIntegrationContractCatalog.GetRequired("FIN-INT-007");

        contract.Status.Should().Be(FinanceIntegrationContractStatus.Available);
        contract.Version.Should().Be("1.0");
        contract.EntryPoint.Should().Be("IProcurementFixedAssetCapitalizationAdapter");
        contract.SourceDocumentType.Should().Be("ProcurementFixedAssetCapitalization");
        contract.LimitationId.Should().Be("FIN-LIM-0028");
    }

    [Fact]
    public void ProcurementBudgetContractShouldExposeFinanceOwnedCommitmentProvider()
    {
        var contract = FinanceIntegrationContractCatalog.GetRequired("FIN-INT-015");

        contract.Status.Should().Be(FinanceIntegrationContractStatus.Available);
        contract.Version.Should().Be("1.0");
        contract.ProducerOwner.Should().Be("Procurement");
        contract.EntryPoint.Should().Be("IFinanceBudgetCommitmentService");
        contract.SourceDocumentType.Should().Be("ProcurementRequisition");
        contract.Notes.Should().Contain("GL-derived actuals");
        contract.Notes.Should().Contain("without writing Finance tables");
    }

    [Fact]
    public void DirectApExpenseBudgetContractShouldPreserveProcurementAndCutoverBoundaries()
    {
        var contract = FinanceIntegrationContractCatalog.GetRequired("FIN-INT-016");

        contract.Status.Should().Be(FinanceIntegrationContractStatus.Available);
        contract.Version.Should().Be("1.0");
        contract.ProducerOwner.Should().Be("Finance / Accounts Payable");
        contract.EntryPoint.Should().Contain("IVendorInvoiceService");
        contract.EntryPoint.Should().Contain("IFinanceBudgetCommitmentService");
        contract.SourceDocumentType.Should().Be("VendorInvoice");
        contract.Notes.Should().Contain("reserve before");
        contract.Notes.Should().Contain("Opening");
        contract.Notes.Should().Contain("PO/GRV");
    }

    [Theory]
    [InlineData("FIN-INT-012", "Procurement and Inventory", "SupplierReturnDispatch")]
    [InlineData("FIN-INT-013", "Procurement", "SupplierReturnCommercialResolution")]
    public void PostAcceptanceSupplierReturnContractsShouldRemainPlannedUntilBothSidesAreProven(
        string contractId,
        string expectedProducerOwner,
        string expectedSourceDocumentType)
    {
        var contract = FinanceIntegrationContractCatalog.GetRequired(contractId);

        // Planned is intentional: a catalogue entry must not become a callable promise merely
        // because Procurement, Inventory and Finance have agreed the ownership boundary.
        contract.Status.Should().Be(FinanceIntegrationContractStatus.Planned);
        contract.Version.Should().Be("0.1");
        contract.ProducerOwner.Should().Be(expectedProducerOwner);
        contract.SourceDocumentType.Should().Contain(expectedSourceDocumentType);
        contract.EntryPoint.Should().Contain("SupplierReturnFinanceAdapter");
        contract.EntryPoint.Should().Contain("fail-closed");
        contract.Notes.Should().Contain("must not write Finance tables directly");
        contract.Notes.Should().Contain("producer consumer-contract tests pass");
    }

    [Fact]
    public void ReusableConsumerAssertionsShouldAcceptCompleteBalancedRequest()
    {
        var tenantId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var request = new FinancePostingRequestDto
        {
            SourceModule = "Inventory",
            OriginModuleCode = "Inventory",
            SourceDocumentType = "StockAdjustment",
            SourceDocumentId = sourceId,
            SourceDocumentTenantId = tenantId,
            SourceDocumentReference = "ADJ-CONTRACT-001",
            Description = "Approved cycle-count shortage",
            FunctionalCurrencyCode = "GHS",
            IdempotencyKey = $"inventory-adjustment:{sourceId:N}:post:v1",
            Lines =
            [
                new FinancePostingLineDto { AccountId = Guid.NewGuid(), DebitAmount = 25m },
                new FinancePostingLineDto { AccountId = Guid.NewGuid(), CreditAmount = 25m }
            ]
        };

        request.ShouldSatisfyPostingContract("Inventory", "StockAdjustment", sourceId, tenantId);
    }
}
