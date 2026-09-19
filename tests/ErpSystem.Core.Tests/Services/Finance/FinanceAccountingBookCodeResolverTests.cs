using ErpSystem.Core.Finance.Integration;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Finance;

public sealed class FinanceAccountingBookCodeResolverTests
{
    [Theory]
    [InlineData("IFRS", "IFRS")]
    [InlineData("Local", "LOCAL_STATUTORY")]
    [InlineData("LOCAL_STATUTORY", "LOCAL_STATUTORY")]
    [InlineData("Management", "MANAGEMENT")]
    public void ResolveLegacySingleBook_MapsSupportedAliases(string configured, string expected) =>
        FinanceAccountingBookCodeResolver.ResolveLegacySingleBook(configured, true).Should().Be(expected);

    [Fact]
    public void ResolveLegacySingleBook_MissingSettingsPreservesV1IfrsDefault() =>
        FinanceAccountingBookCodeResolver.ResolveLegacySingleBook(null, false).Should().Be("IFRS");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AllClassifiedBooks")]
    [InlineData("ALL_ACTIVE_BOOKS")]
    [InlineData("UnknownBook")]
    public void ResolveLegacySingleBook_RejectsBlankPseudoAndUnknownValues(string configured) =>
        FluentActions.Invoking(() => FinanceAccountingBookCodeResolver.ResolveLegacySingleBook(configured, true))
            .Should().Throw<ArgumentException>();
}
