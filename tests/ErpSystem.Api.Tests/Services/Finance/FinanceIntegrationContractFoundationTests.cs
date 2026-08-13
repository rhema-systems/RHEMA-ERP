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
