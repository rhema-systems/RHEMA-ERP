using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;
using System.Text.Json;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class PettyPurchaseQuotationRulesTests
{
    [Theory]
    [InlineData("{\"price\":750,\"quantity\":1}", "{\"quantity\":1.0000,\"price\":750.0000}", true)]
    [InlineData("[{\"price\":750}]", "[{\"price\":751}]", false)]
    [InlineData("{\"supplier\":\"a\"}", "{\"supplier\":\"b\"}", false)]
    [InlineData("{\"evidence\":\"one\"}", "{\"evidence\":\"two\"}", false)]
    [InlineData("{\"lines\":[1,2]}", "{\"lines\":[1]}", false)]
    [InlineData("{\"price\":750}", "{\"price\":\"750\"}", false)]
    [InlineData("{\"price\":750,\"quantity\":1}", "{\"price\":750}", false)]
    public void ComparesQuotationValuesNotSqlDecimalFormatting(string expected, string actual, bool matches)
    {
        using var expectedJson = JsonDocument.Parse(expected);
        using var actualJson = JsonDocument.Parse(actual);
        PettyPurchaseQuotationRules.MatchesSnapshot(expectedJson.RootElement, actualJson.RootElement).Should().Be(matches);
    }

    private readonly TenderItem _line = new() { Id = Guid.NewGuid(), Quantity = 1, Description = "Kit" };
    private PettyPurchaseQuotationRequest Quote(decimal price = 750) => new()
    {
        Reference = "UAT-QUOTE", EvidenceReference = "UAT evidence, not a real offer",
        Items = [new() { TenderItemId = _line.Id, UnitPrice = price }]
    };

    [Theory]
    [InlineData(750, 750)]
    [InlineData(700, 700)]
    public void AcceptsQuotedPriceWithinApprovedCeiling(decimal price, decimal expected) =>
        PettyPurchaseQuotationRules.Validate(Quote(price), [_line], 750).Should().Be(expected);

    [Theory]
    [InlineData(750.01, "PETTY_QUOTATION_EXCEEDS_APPROVAL")]
    [InlineData(0, "PETTY_QUOTATION_PRICE_INVALID")]
    [InlineData(-1, "PETTY_QUOTATION_PRICE_INVALID")]
    [InlineData(1.001, "PETTY_QUOTATION_PRICE_INVALID")]
    public void RejectsInvalidPrice(decimal price, string code) =>
        AssertCode(() => PettyPurchaseQuotationRules.Validate(Quote(price), [_line], 750), code);

    [Fact]
    public void RequiresEvidenceAndExactUniqueSourceLines()
    {
        var quote = Quote();
        quote.EvidenceReference = "";
        AssertCode(() => PettyPurchaseQuotationRules.Validate(quote, [_line], 750), "PETTY_QUOTATION_REQUIRED");
        quote = Quote(); quote.Items[0].TenderItemId = Guid.NewGuid();
        AssertCode(() => PettyPurchaseQuotationRules.Validate(quote, [_line], 750), "PETTY_QUOTATION_LINES_MISMATCH");
        quote = Quote(); quote.Items.Add(quote.Items[0]);
        AssertCode(() => PettyPurchaseQuotationRules.Validate(quote, [_line], 750), "PETTY_QUOTATION_LINES_MISMATCH");
        quote = Quote(); quote.Items.Clear();
        AssertCode(() => PettyPurchaseQuotationRules.Validate(quote, [_line], 750), "PETTY_QUOTATION_LINES_MISMATCH");
    }

    private static void AssertCode(Action action, string code) => action.Should()
        .Throw<ProcurementExceptionalSourcingValidationException>().Which.Code.Should().Be(code);
}
