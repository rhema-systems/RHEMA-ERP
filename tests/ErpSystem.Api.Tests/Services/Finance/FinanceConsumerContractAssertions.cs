using ErpSystem.Core.DTOs.Finance;
using FluentAssertions;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Reusable assertions for producer-module tests that capture a request sent to Finance.
///
/// Keeping these checks in one place prevents Procurement, Inventory, Sales and future adapters
/// from each inventing a weaker definition of a valid Finance request.  These assertions do not
/// replace FinancePostingEngine validation; they fail the producing module's tests earlier and with
/// a message that identifies the contract evidence it omitted.
/// </summary>
public static class FinanceConsumerContractAssertions
{
    public static void ShouldSatisfyPostingContract(
        this FinancePostingRequestDto request,
        string expectedOriginModule,
        string expectedDocumentType,
        Guid expectedDocumentId,
        Guid expectedTenantId)
    {
        request.Should().NotBeNull();
        request.OriginModuleCode.Should().Be(expectedOriginModule);
        request.SourceDocumentType.Should().Be(expectedDocumentType);
        request.SourceDocumentId.Should().Be(expectedDocumentId);
        request.SourceDocumentTenantId.Should().Be(expectedTenantId);
        request.SourceDocumentReference.Should().NotBeNullOrWhiteSpace();
        request.Description.Should().NotBeNullOrWhiteSpace();
        request.FunctionalCurrencyCode.Should().MatchRegex("^[A-Z]{3}$");
        request.IdempotencyKey.Should().NotBeNullOrWhiteSpace(
            "retries of an approved source transaction must resolve to the original Finance posting");
        request.ReturnExistingOnDuplicate.Should().BeTrue();
        request.Lines.Should().NotBeEmpty();

        request.Lines.Should().OnlyContain(line =>
            line.AccountId != Guid.Empty &&
            line.DebitAmount >= 0m &&
            line.CreditAmount >= 0m &&
            (line.DebitAmount == 0m || line.CreditAmount == 0m));

        request.Lines.Sum(line => line.DebitAmount).Should().BeGreaterThan(0m);
        request.Lines.Sum(line => line.DebitAmount).Should().Be(
            request.Lines.Sum(line => line.CreditAmount),
            "producer adapters must send a balanced functional-currency instruction");
    }
}
