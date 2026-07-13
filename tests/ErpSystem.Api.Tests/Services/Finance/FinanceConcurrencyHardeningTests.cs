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

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void CashBankPosting_ShouldCommitPostingEngineSourceFlagsAndBankSnapshotsAtomically()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "Cash", "CashTransactionService.cs"));
        var method = ExtractMember(source, "public async Task<CashTransactionDto> PostAsync", "public async Task DeleteAsync");

        var transactionIndex = method.IndexOf("BeginTransactionAsync(cancellationToken)", StringComparison.Ordinal);
        var postingIndex = method.IndexOf("_financePostingEngine.PostAsync", StringComparison.Ordinal);
        var snapshotIndex = method.IndexOf("ApplyPostedCashBankBalanceSnapshotAsync", StringComparison.Ordinal);
        var saveIndex = method.IndexOf("_context.SaveChangesAsync(cancellationToken)", StringComparison.Ordinal);
        var commitIndex = method.IndexOf("CommitAsync(cancellationToken)", StringComparison.Ordinal);

        transactionIndex.Should().BeGreaterThan(-1, "cash/bank posting needs an ambient transaction for the posting engine to join");
        transactionIndex.Should().BeLessThan(postingIndex, "the posting engine must run inside the cash/bank transaction boundary");
        transactionIndex.Should().BeLessThan(snapshotIndex, "bank balance snapshots must be updated in the same transaction as the GL post");
        transactionIndex.Should().BeLessThan(saveIndex, "cash transaction flags must be saved in the same transaction as the GL post");
        saveIndex.Should().BeLessThan(commitIndex, "the database commit should occur only after source flags and snapshots are saved");
        method.Should().Contain("RollbackAsync(cancellationToken)", "failed cash/bank posts must roll back the ambient transaction");
        method.Should().Contain("_context.ChangeTracker.Clear()", "failure audit must not persist tracked partial cash/bank state after rollback");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void CashBankTransfer_ShouldGenerateNumbersInsideSerializableTransaction()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "Cash", "CashTransactionService.cs"));
        var method = ExtractMember(source, "public async Task<(CashTransactionDto FromTransaction, CashTransactionDto ToTransaction)> CreateTransferAsync", "public async Task<CashTransactionDto> SubmitAsync");

        var transactionIndex = method.IndexOf("BeginTransactionAsync(IsolationLevel.Serializable", StringComparison.Ordinal);
        var numberingIndex = method.IndexOf("GenerateTransactionNumberAsync(FinanceDocumentTypes.BankTransfer", StringComparison.Ordinal);

        source.Should().Contain("using System.Data;", "serializable isolation should be explicit for cash/bank transfer number generation");
        transactionIndex.Should().BeGreaterThan(-1, "bank transfer creation must protect document numbering with serializable isolation");
        transactionIndex.Should().BeLessThan(numberingIndex, "the ambient transaction used by document numbering must already be serializable");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void DocumentNumberingService_ShouldTenantFilterCashTransactionFallbackScans()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "DocumentNumberingService.cs"));

        var cashFallbackIndex = source.IndexOf("FinanceDocumentTypes.CashReceipt", StringComparison.Ordinal);
        var cashTransactionSetIndex = source.IndexOf("_context.Set<CashTransaction>()", cashFallbackIndex, StringComparison.Ordinal);
        var tenantFilterIndex = source.IndexOf(".Where(e => e.TenantId == tenantId)", cashTransactionSetIndex, StringComparison.Ordinal);
        var selectIndex = source.IndexOf(".Select(e => e.TransactionNumber)", cashTransactionSetIndex, StringComparison.Ordinal);

        cashFallbackIndex.Should().BeGreaterThan(-1, "cash receipt/payment/transfer document numbers need fallback scans");
        cashTransactionSetIndex.Should().BeGreaterThan(cashFallbackIndex, "cash document numbering should scan CashTransaction numbers");
        tenantFilterIndex.Should().BeGreaterThan(cashTransactionSetIndex, "cash fallback scans must stay within the caller tenant");
        tenantFilterIndex.Should().BeLessThan(selectIndex, "tenant filtering must happen before reading cash transaction numbers");
    }

    private static string ExtractMember(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"source should contain {startMarker}");

        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"source should contain {endMarker} after {startMarker}");

        return source[start..end];
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
