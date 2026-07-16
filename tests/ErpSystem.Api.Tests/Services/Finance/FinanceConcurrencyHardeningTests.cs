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
        source.Should().Contain("IsSqlServer()", "SQL Server lock hints must not run against other relational providers used in dev/test");
        source.Should().Contain("ExecuteSqlInterpolatedAsync", "account balance snapshots should be incremented atomically in the database");
        source.Should().Contain("UPDLOCK", "same-account concurrent postings must serialize balance snapshot updates");
        source.Should().Contain("ApplyTrackedAccountBalanceDeltasAsync", "non-SQL Server providers need a provider-neutral balance update path");
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

        var createMethod = ExtractMember(controllerSource, "public async Task<ActionResult<FinancePurchaseOrderDto>> Create", "[HttpPost(\"{id:guid}/submit-for-approval\")]");
        var resolver = ExtractMember(controllerSource, "private async Task<string> ResolvePurchaseOrderNumberAsync", "private static int NormalizeLineType");
        var generator = ExtractMember(controllerSource, "private async Task<string> GeneratePurchaseOrderNumberAsync", "private static int NormalizeLineType");

        controllerSource.Should().Contain("IDocumentNumberingService", "finance PO number generation should use central document numbering");
        createMethod.Should().Contain("ResolvePurchaseOrderNumberAsync(tenantId, orderDate, dto.OrderNumber, cancellationToken)", "API callers must not bypass tenant sequence policy by supplying an order number");
        createMethod.Should().NotContain(": dto.OrderNumber.Trim()", "manual finance PO numbers must be accepted only through the sequence policy");
        resolver.Should().Contain("AllowManualEntry", "manual finance PO numbers are valid only when the tenant sequence explicitly permits them");
        resolver.Should().Contain("GetDefinitionsAsync", "manual-entry checks must read the tenant's document sequence policy");
        resolver.Should().Contain("GeneratePurchaseOrderNumberAsync", "controlled sequence numbering remains the default when manual entry is disabled");
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

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void SupplierReturns_ShouldValidateSourceLineOwnershipAndRemainingQuantities()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "SupplierReturnsController.cs"));
        var createMethod = ExtractMember(source, "public async Task<ActionResult<SupplierReturnDto>> Create", "[HttpPost(\"{id:guid}/approve\")]");
        var validationRegion = ExtractMember(source, "private async Task<string?> ValidateSupplierReturnQuantitiesAsync", "private async Task PostSupplierDebitNoteThroughFinancePostingEngineAsync");

        createMethod.Should().Contain("ValidateSupplierReturnQuantitiesAsync(dto, tenantId, cancellationToken)", "server-side source validation must run before supplier return lines are persisted");
        createMethod.IndexOf("ValidateSupplierReturnQuantitiesAsync(dto, tenantId, cancellationToken)", StringComparison.Ordinal)
            .Should().BeLessThan(createMethod.IndexOf("supplierReturn.LineItems.Add", StringComparison.Ordinal), "supplier returns should not be created before source-line availability is checked");

        validationRegion.Should().Contain("ValidateVendorInvoiceReturnQuantitiesAsync", "invoice-backed returns need source invoice line validation");
        validationRegion.Should().Contain("ValidateFinanceGrvReturnQuantitiesAsync", "GRV-backed returns need source receipt line validation");
        validationRegion.Should().Contain("does not belong to the selected supplier invoice", "source line IDs must belong to the selected invoice");
        validationRegion.Should().Contain("does not belong to the selected finance GRV", "source line IDs must belong to the selected GRV");
        validationRegion.Should().Contain("SupplierReturn.Status != SupplierReturnStatus.Cancelled", "previous non-cancelled returns must consume remaining quantity");
        validationRegion.Should().Contain("submittedByLine", "duplicate submitted lines must be aggregated before checking availability");
        validationRegion.Should().Contain("exceeds the remaining returnable quantity", "over-returns must fail at the API boundary");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinanceApReturnCreatePage_ShouldUseFinancePurchaseReceiptRoutesForGrvs()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "ap", "returns", "create", "page.tsx"));

        source.Should().Contain("'/finance/ap/purchase-receipts'", "GRV-backed supplier returns should call the finance purchase receipt list endpoint");
        source.Should().Contain("`/finance/ap/purchase-receipts/${id}`", "GRV-backed supplier returns should call the finance purchase receipt detail endpoint");
        source.Should().NotContain("'/ap/purchase-receipts'", "the AP route is not registered for finance GRV receipts");
        source.Should().NotContain("`/ap/purchase-receipts/${id}`", "the AP route is not registered for finance GRV receipt details");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinancePurchaseOrderCreatePage_ShouldUseActivePurchaseTaxGroups()
    {
        var root = FindRepositoryRoot();
        var pageSource = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "ap", "purchase-orders", "create", "page.tsx"));
        var taxService = File.ReadAllText(Path.Combine(root, "frontend", "src", "services", "finance", "tax-data.service.ts"));

        pageSource.Should().Contain("taxDataService.getActiveTaxGroups('Purchases')", "PO creation should only offer active purchase-applicable tax groups");
        pageSource.Should().NotContain("taxDataService.getTaxGroups({ isActive: true, applicability: 'Purchases' })", "the general groups endpoint ignores these filters");
        taxService.Should().Contain("getActiveTaxGroups", "transaction entry forms need a typed helper for the filtered active tax endpoint");
        taxService.Should().Contain("/finance/tax/groups/active", "the helper must call the controller action that applies active/applicability filtering");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinancePurchaseReceipts_ShouldUseDocumentNumberReservationsForGeneratedNumbers()
    {
        var root = FindRepositoryRoot();
        var controllerSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "FinancePurchaseOrderController.cs"));
        var numberingContracts = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Interfaces", "Numbering", "IDocumentNumberingService.cs"));
        var numberingService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "DocumentNumberingService.cs"));

        var createMethod = ExtractMember(controllerSource, "public async Task<ActionResult<FinancePurchaseOrderReceiptDto>> Create", "[HttpPost(\"{id:guid}/convert-to-vendor-invoice\")]");
        var generator = ExtractMember(controllerSource, "private async Task<string> GenerateReceiptNumberAsync", "private static FinancePostingLineDto BuildPostingLine");

        var transactionIndex = createMethod.IndexOf("BeginTransactionAsync(IsolationLevel.Serializable", StringComparison.Ordinal);
        var numberingIndex = createMethod.IndexOf("GenerateReceiptNumberAsync(tenantId, receiptDate, cancellationToken)", StringComparison.Ordinal);

        transactionIndex.Should().BeGreaterThan(-1, "receipt creation should hold a serializable transaction");
        transactionIndex.Should().BeLessThan(numberingIndex, "receipt number reservations should join the receipt transaction");
        generator.Should().Contain("_documentNumberingService.GenerateAsync", "generated finance GRV numbers must be reserved transactionally");
        generator.Should().Contain("FinanceDocumentTypes.FinancePurchaseOrderReceipt", "finance GRVs need a dedicated numbering sequence");
        generator.Should().NotContain("CountAsync", "generated finance GRV numbers must not be derived from visible row counts");
        numberingContracts.Should().Contain("public const string FinancePurchaseOrderReceipt", "the document type should be explicit for other finance callers");
        numberingContracts.Should().Contain("\"FGRV-{YYYY}-{######}\"", "the default sequence should preserve the existing FGRV year format");

        var receiptFallbackIndex = numberingService.IndexOf("FinanceDocumentTypes.FinancePurchaseOrderReceipt) => _context.Set<FinancePurchaseOrderReceipt>()", StringComparison.Ordinal);
        var receiptTenantFilterIndex = numberingService.IndexOf(".Where(e => e.TenantId == tenantId)", receiptFallbackIndex, StringComparison.Ordinal);
        var receiptSelectIndex = numberingService.IndexOf(".Select(e => e.ReceiptNumber)", receiptFallbackIndex, StringComparison.Ordinal);

        receiptFallbackIndex.Should().BeGreaterThan(-1, "legacy/manual finance GRV numbers should be included in fallback scans");
        receiptTenantFilterIndex.Should().BeGreaterThan(receiptFallbackIndex, "finance GRV fallback scans must remain tenant-scoped");
        receiptTenantFilterIndex.Should().BeLessThan(receiptSelectIndex, "tenant filtering must happen before reading finance GRV receipt numbers");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void ReceiptToInvoiceConversion_ShouldSerializeInvoiceLinkCheckAndCreation()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "FinancePurchaseOrderController.cs"));
        var method = ExtractMember(source, "public async Task<ActionResult<VendorInvoiceDto>> ConvertToVendorInvoice", "private IQueryable<FinancePurchaseOrderReceipt> BaseReceiptQuery");

        var transactionIndex = method.IndexOf("BeginTransactionAsync(IsolationLevel.Serializable", StringComparison.Ordinal);
        var receiptLoadIndex = method.IndexOf("var receipt = await _dbContext.FinancePurchaseOrderReceipts", StringComparison.Ordinal);
        var linkCheckIndex = method.IndexOf("if (receipt.VendorInvoiceId.HasValue)", StringComparison.Ordinal);
        var invoiceAddIndex = method.IndexOf("_dbContext.VendorInvoices.Add(invoice)", StringComparison.Ordinal);
        var saveIndex = method.IndexOf("SaveChangesAsync(cancellationToken)", invoiceAddIndex, StringComparison.Ordinal);
        var commitIndex = method.IndexOf("CommitAsync(cancellationToken)", StringComparison.Ordinal);

        transactionIndex.Should().BeGreaterThan(-1, "receipt-to-invoice conversion must serialize the invoice-link check against concurrent conversions");
        transactionIndex.Should().BeLessThan(receiptLoadIndex, "the receipt and its invoice link must be read inside the protected transaction");
        receiptLoadIndex.Should().BeLessThan(linkCheckIndex, "the duplicate-conversion check must run on the transactionally loaded receipt");
        linkCheckIndex.Should().BeLessThan(invoiceAddIndex, "the draft invoice must only be created after the link check inside the same transaction");
        saveIndex.Should().BeLessThan(commitIndex, "the receipt link and draft invoice must be committed together");
        method.Should().Contain("PurchaseOrderItemId = null", "finance GRV conversion must not store finance PO line ids in the legacy procurement PO FK");
        method.Should().NotContain("PurchaseOrderItemId = poItem.Id", "VendorInvoiceLineItem.PurchaseOrderItemId targets legacy PurchaseOrderItems, not FinancePurchaseOrderItems");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FrontendFinanceDocumentTypes_ShouldMatchBackendSequenceNames()
    {
        var root = FindRepositoryRoot();
        var frontendSource = File.ReadAllText(Path.Combine(root, "frontend", "src", "types", "document-numbering.ts"));
        var backendSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Interfaces", "Numbering", "IDocumentNumberingService.cs"));

        var frontendFinanceBlock = ExtractMember(frontendSource, "export const FinanceDocumentTypes = {", "export const SalesDocumentTypes = {");
        var frontendSalesBlock = ExtractMember(frontendSource, "export const SalesDocumentTypes = {", "export interface DocumentSequenceDefinition");

        // getDefinition/GenerateAsync match documentType strings exactly, so every frontend constant must exist
        // verbatim in the backend FinanceDocumentTypes/SalesDocumentTypes or the tenant sequence is silently ignored.
        foreach (var value in ExtractTypeScriptDocumentTypeValues(frontendFinanceBlock))
        {
            backendSource.Should().Contain($"= \"{value}\";", $"frontend finance document type '{value}' must match a backend sequence name exactly");
        }

        foreach (var value in ExtractTypeScriptDocumentTypeValues(frontendSalesBlock))
        {
            backendSource.Should().Contain($"= \"{value}\";", $"frontend sales document type '{value}' must match a backend sequence name exactly");
        }
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void CurrentArInvoiceMigration_ShouldRepairExistingLegacyCustomerIdTables()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260710100000_EnsureCurrentArInvoiceTables.cs"));

        var createMissingTableIndex = source.IndexOf("IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NULL", StringComparison.Ordinal);
        var repairExistingTableIndex = source.IndexOf("IF COL_LENGTH(N'[dbo].[Invoices]', N'BusinessPartnerId') IS NULL", StringComparison.Ordinal);

        createMissingTableIndex.Should().BeGreaterThan(-1, "the migration must still create the current AR table for clean schemas");
        repairExistingTableIndex.Should().BeGreaterThan(createMissingTableIndex, "upgraded databases with an existing Invoices table still need BusinessPartnerId repair");
        source.Should().Contain("ALTER TABLE [dbo].[Invoices] ADD [BusinessPartnerId] uniqueidentifier NULL", "older Invoices tables must get the current AR customer FK");
        source.Should().Contain("INNER JOIN [dbo].[Customers] c", "legacy CustomerId rows must be mapped from the older customer table when present");
        source.Should().Contain("LEFT(CONCAT(N'AR-CUST-', CONVERT(nvarchar(36), c.[Id])), 50)", "legacy customer partner codes must not collide with existing supplier/contractor partner codes");
        source.Should().Contain("FK_Invoices_BusinessPartners_BusinessPartnerId", "the current AR model requires the BusinessPartner FK");
        source.Should().Contain("CREATE INDEX [IX_Invoices_BusinessPartnerId]", "the current AR model requires the BusinessPartner lookup index");
        source.Should().Contain("ALTER TABLE [dbo].[Invoices] ALTER COLUMN [BusinessPartnerId] uniqueidentifier NOT NULL", "BusinessPartnerId should be enforced after the backfill succeeds");
        source.Should().Contain("THROW 51000", "the migration must fail loudly instead of leaving unmapped legacy AR rows");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void YearEndClose_ShouldPostThroughFinancePostingEngine()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "GL", "GeneralLedgerService.cs"));
        var transferMethod = ExtractMember(source, "private async Task<(Guid? ClosingJournalEntryId, decimal NetIncome)> TransferRetainedEarningsAsync", "#endregion");

        transferMethod.Should().Contain("_financePostingEngine.PostAsync", "the year-end closing journal must post through the finance posting engine");
        transferMethod.Should().NotContain("PostingStatus = \"Posted\"", "the closing journal must not be marked posted by direct GL writes");
        transferMethod.Should().NotContain("_context.JournalEntries.Add", "the engine, not the GL service, owns closing journal creation");
        transferMethod.Should().Contain("AllowPostingToClosedPeriod = true", "the closing entry posts into the year's closed final period via the explicit narrow exception");

        var engineSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "GL", "FinancePostingEngine.cs"));
        engineSource.Should().Contain("YearEndCloseReversal", "the closed-period exception must be limited to year-end close source types");
        engineSource.Should().Contain("!fiscalPeriod.IsLocked", "locked periods must stay closed to year-end postings too");
    }

    private static IReadOnlyList<string> ExtractTypeScriptDocumentTypeValues(string block)
    {
        var values = System.Text.RegularExpressions.Regex
            .Matches(block, @"\w+:\s*'([^']+)'")
            .Select(m => m.Groups[1].Value)
            .ToList();

        values.Should().NotBeEmpty("the frontend document type block should declare at least one constant");
        return values;
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
