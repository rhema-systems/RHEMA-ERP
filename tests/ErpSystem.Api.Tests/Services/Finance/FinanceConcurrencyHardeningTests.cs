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

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void ApApprovalPaths_ShouldNotReceiptOrPostOpeningBalanceInvoices()
    {
        var root = FindRepositoryRoot();
        var approvalsSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "FinanceApprovalsController.cs"));
        var serviceSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "VendorInvoiceService.cs"));

        var approvalsMethod = ExtractMember(approvalsSource, "private async Task FinalizeVendorInvoiceApprovalAsync", "private async Task RecordFinanceWorkflowAuditAsync");
        var serviceMethod = ExtractMember(serviceSource, "public async Task<VendorInvoiceDto> ApproveAsync", "public async Task<VendorInvoiceDto> PostAsync");

        approvalsMethod.Should().Contain("!invoice.IsOpeningBalance && l.LineItemType == \"Inventory\"", "the workbench approval path must not create inventory receipts for opening-balance AP invoices");
        approvalsMethod.Should().Contain("if (invoice.IsOpeningBalance)", "the workbench approval path must skip normal AP posting for opening-balance invoices");
        approvalsMethod.IndexOf("if (invoice.IsOpeningBalance)", StringComparison.Ordinal)
            .Should().BeLessThan(approvalsMethod.IndexOf("_vendorInvoiceService.PostAsync", StringComparison.Ordinal), "opening-balance AP invoices must return before normal posting");

        serviceMethod.Should().Contain("!invoice.IsOpeningBalance && l.LineItemType == \"Inventory\"", "the service approval path must not create inventory receipts for opening-balance AP invoices");
        serviceMethod.Should().Contain("if (!invoice.IsOpeningBalance)", "the service approval path must post only normal AP invoices");
        serviceMethod.IndexOf("if (!invoice.IsOpeningBalance)", StringComparison.Ordinal)
            .Should().BeLessThan(serviceMethod.IndexOf("await PostAsync(invoice.Id", StringComparison.Ordinal), "normal AP posting must be guarded by the opening-balance check");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinancePurchaseOrders_ShouldUseDocumentNumberReservationsForGeneratedNumbers()
    {
        var root = FindRepositoryRoot();
        var controllerSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "FinancePurchaseOrderController.cs"));
        var numberingContracts = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Interfaces", "Numbering", "IDocumentNumberingService.cs"));
        var numberingService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "DocumentNumberingService.cs"));

        var generator = ExtractMember(controllerSource, "private async Task<string> GeneratePurchaseOrderNumberAsync", "private static int NormalizeLineType");

        controllerSource.Should().Contain("IDocumentNumberingService", "finance PO number generation should use central document numbering");
        generator.Should().Contain("_documentNumberingService.GenerateAsync", "generated finance PO numbers must be reserved transactionally");
        generator.Should().Contain("FinanceDocumentTypes.FinancePurchaseOrder", "finance POs need a dedicated numbering sequence");
        generator.Should().NotContain("CountAsync", "generated finance PO numbers must not be derived from visible row counts");
        numberingContracts.Should().Contain("public const string FinancePurchaseOrder", "the document type should be explicit for other finance callers");
        numberingContracts.Should().Contain("\"FPO-{YYYY}-{######}\"", "the default sequence should preserve the existing FPO year format");
        var financePoFallbackIndex = numberingService.IndexOf("FinanceDocumentTypes.FinancePurchaseOrder) => _context.Set<FinancePurchaseOrder>()", StringComparison.Ordinal);
        var financePoTenantFilterIndex = numberingService.IndexOf(".Where(e => e.TenantId == tenantId)", financePoFallbackIndex, StringComparison.Ordinal);
        var financePoSelectIndex = numberingService.IndexOf(".Select(e => e.OrderNumber)", financePoFallbackIndex, StringComparison.Ordinal);

        financePoFallbackIndex.Should().BeGreaterThan(-1, "legacy/manual finance PO numbers should be included in fallback scans");
        financePoTenantFilterIndex.Should().BeGreaterThan(financePoFallbackIndex, "finance PO fallback scans must remain tenant-scoped");
        financePoTenantFilterIndex.Should().BeLessThan(financePoSelectIndex, "tenant filtering must happen before reading finance PO order numbers");
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
