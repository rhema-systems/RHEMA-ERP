using System.Reflection;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class GoodsReceiptNoteIdempotencyTests
{
    [Fact]
    public void DerivedReceiptKeyHashesTheCompleteSourceKeyWithoutTruncationCollisions()
    {
        var method = typeof(GoodsReceiptNoteService).GetMethod(
            "BuildGovernedReceiptIdempotencyKey",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var commonPrefix = new string('a', 99);

        var first = (string)method.Invoke(null, [$"{commonPrefix}1"])!;
        var second = (string)method.Invoke(null, [$"{commonPrefix}2"])!;
        var retry = (string)method.Invoke(null, [$"{commonPrefix}1"])!;

        first.Should().HaveLength(68).And.StartWith("grn:");
        first.Should().NotBe(second);
        retry.Should().Be(first);
    }
}
