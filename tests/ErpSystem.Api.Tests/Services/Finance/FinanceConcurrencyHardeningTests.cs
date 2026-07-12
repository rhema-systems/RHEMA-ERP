using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceConcurrencyHardeningTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinancePostingEngine_ShouldRespectAmbientTransactionsAndUseAtomicBalanceDeltas()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "GL", "FinancePostingEngine.cs"));

        source.Should().Contain("CurrentTransaction", "posting must join an existing DbContext transaction when callers already opened one");
        source.Should().Contain("ExecutePostingAsync", "owned and ambient transaction paths should share one posting implementation");
        source.Should().Contain("ExecuteSqlInterpolatedAsync", "account balance snapshots should be incremented atomically in the database");
        source.Should().Contain("UPDLOCK", "same-account concurrent postings must serialize balance snapshot updates");
        source.Should().NotContain("account.Balance += transaction", "balance snapshot updates must not use read-modify-write per transaction line");
        source.Should().NotContain("account.Balance -= transaction", "balance snapshot updates must not use read-modify-write per transaction line");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinancePurchaseOrderReceipt_ShouldValidateReceivedQuantityInsideSerializableTransaction()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "FinancePurchaseOrderController.cs"));

        var transactionIndex = source.IndexOf("BeginTransactionAsync(IsolationLevel.Serializable", StringComparison.Ordinal);
        var purchaseOrderLoadIndex = source.IndexOf("var purchaseOrder = await _dbContext.FinancePurchaseOrders", transactionIndex, StringComparison.Ordinal);
        var remainingQuantityIndex = source.IndexOf("var remainingQuantity = item.OrderedQuantity - item.ReceivedQuantity - item.CancelledQuantity", transactionIndex, StringComparison.Ordinal);
        var incrementIndex = source.IndexOf("item.ReceivedQuantity += lineDto.QuantityReceived", transactionIndex, StringComparison.Ordinal);

        transactionIndex.Should().BeGreaterThan(-1, "receipt creation should use serializable isolation for quantity checks");
        transactionIndex.Should().BeLessThan(purchaseOrderLoadIndex, "the PO must be reloaded after the protected transaction starts");
        transactionIndex.Should().BeLessThan(remainingQuantityIndex, "remaining quantity must be checked inside the protected transaction");
        transactionIndex.Should().BeLessThan(incrementIndex, "received quantity must be incremented inside the protected transaction");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
