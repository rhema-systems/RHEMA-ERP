using ErpSystem.Data;
using ErpSystem.Api.Controllers.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceConcurrencyHardeningTests
{
    [Theory]
    [InlineData("ExchangeRate", true)]
    [InlineData("Invoice", true)]
    [InlineData("invoice", true)]
    [InlineData("VendorInvoice", true)]
    [InlineData("JournalEntry", false)]
    public void ApprovalOutcomeTransaction_ShouldUseSerializableIsolationWhenOutcomeTransitionsBookAuthority(
        string entityType,
        bool expected)
    {
        FinanceApprovalsController.RequiresSerializableOutcomeTransaction(entityType)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("Invoice", "AR_INVOICE_POSTING_BLOCKED")]
    [InlineData("VendorInvoice", "AP_INVOICE_POSTING_BLOCKED")]
    public void InvoiceApprovalPostingFailure_ShouldPreserveSafeValidationMessage(
        string entityType,
        string expectedCode)
    {
        const string validationMessage = "The configured tax account is missing.";

        var result = FinanceApprovalsController.CreateInvoicePostingBusinessRuleException(
            entityType,
            new InvalidOperationException(validationMessage));

        result.Code.Should().Be(expectedCode);
        result.Message.Should().Be(validationMessage);
        result.StatusCode.Should().Be(422);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "QuantitySurveyFinalAcceptance")]
    public void ApPaymentReversal_ShouldStartSerializableTransactionInsideSqlServerExecutionStrategy()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Services",
            "Finance",
            "AP",
            "VendorPaymentService.cs"));
        var reversal = ExtractMember(
            source,
            "public Task<VendorPaymentDto> ReversePaymentAsync",
            "public Task<VendorPaymentAllocationResultDto> AllocatePaymentAsync");

        var strategyIndex = reversal.IndexOf("ExecuteInStrategyAsync", StringComparison.Ordinal);
        var transactionIndex = reversal.IndexOf("BeginTransactionAsync", StringComparison.Ordinal);

        strategyIndex.Should().BeGreaterThan(-1, "SQL Server retry handling must own the complete AP reversal unit");
        transactionIndex.Should().BeGreaterThan(strategyIndex, "the serializable transaction must be created inside the execution strategy");
        reversal.Should().Contain("IsolationLevel.Serializable", "invoice settlement and compensating GL writes must remain serialized");
        reversal.Should().Contain("RollbackAsync", "a failed reversal attempt must release its transaction before retry or failure audit");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinancePeriodCloseIssue32")]
    public void PeriodCloseTransactions_ShouldStartInsideSqlServerExecutionStrategy()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Services",
            "Finance",
            "Fiscal",
            "FiscalPeriodService.cs"));
        var templateControl = ExtractMember(
            source,
            "private async Task<T> ExecuteCloseTemplateControlAsync<T>",
            "private async Task EnsureDefaultCloseTemplatesAsync");
        var periodControl = ExtractMember(
            source,
            "private async Task<T> ExecutePeriodCloseControlAsync<T>",
            "private async Task<FinanceCloseCycle> GetOrCreateActiveCloseCycleAsync");

        foreach (var control in new[] { templateControl, periodControl })
        {
            var strategyIndex = control.IndexOf("ExecuteInStrategyAsync", StringComparison.Ordinal);
            var transactionIndex = control.IndexOf("BeginTransactionAsync", StringComparison.Ordinal);
            strategyIndex.Should().BeGreaterThan(-1, "SQL Server retry handling must own the complete close-control unit");
            transactionIndex.Should().BeGreaterThan(strategyIndex, "the user transaction must be created inside the execution strategy");
            control.Should().Contain("IsolationLevel.Serializable", "period-close decisions still require serializable isolation");
            control.Should().Contain("AcquireTransactionLockAsync", "the transaction-scoped application lock remains the concurrency boundary");
            control.Should().Contain("RollbackAsync", "a failed retry attempt must release transaction state before it can be repeated");
        }

        periodControl.Should().Contain("FIN:CLOSE:", "evaluation, certification, close and reopen actions must share a period lock");
        templateControl.Should().Contain("FIN:CLOSE-TEMPLATE:", "template version allocation must retain its tenant-wide lock");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinancePostingEngine_ShouldRespectAmbientTransactionsAndUseBookScopedProjection()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "GL", "FinancePostingEngine.cs"));

        source.Should().Contain("CurrentTransaction", "posting must join an existing DbContext transaction when callers already opened one");
        source.Should().Contain("ExecutePostingAsync", "owned and ambient transaction paths should share one posting implementation");
        source.Should().Contain("IsSqlServer()", "SQL Server lock hints must not run against other relational providers used in dev/test");
        source.Should().Contain("_bookBalances.ApplyPostingAsync", "posted lines must update exact-book projections");
        source.Should().NotContain("ApplyTrackedAccountBalanceDeltasAsync", "the unscoped account snapshot must remain retired");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinancePostingEngine_ShouldCollapseConcurrentDuplicatePostsToOneDurableEvent()
    {
        var root = FindRepositoryRoot();
        var engine = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "GL", "FinancePostingEngine.cs"));
        var model = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "ApplicationDbContext.cs"));

        var raceCatch = ExtractMember(engine, "catch (DbUpdateException ex)", "private async Task<FinancePostingResultDto> ExecutePostingAsync");
        raceCatch.Should().Contain("RollbackAsync(cancellationToken)", "a losing concurrent posting must discard its partial transaction");
        raceCatch.Should().Contain("_context.ChangeTracker.Clear()", "the losing request must not retain partial tracked ledger state");
        raceCatch.Should().Contain("FindExistingPostingAsync", "the winner's durable posting must be reloaded after the unique-key race");
        raceCatch.Should().Contain("request.ReturnExistingOnDuplicate", "duplicate convergence must remain an explicit caller contract");
        raceCatch.Should().Contain("wasDuplicate: true", "the losing request must return the winner as a duplicate result");

        var postingEventModel = ExtractMember(model, "builder.Entity<FinancePostingEvent>", "builder.Entity<AccountTransaction>");
        postingEventModel.Should().Contain(".IsUnique()", "the database must arbitrate competing posting-event inserts");
        postingEventModel.Should().Contain("e.SourceModule, e.SourceDocumentType, e.SourceDocumentId, e.PostingAction", "the unique key must identify the same source action within the tenant");
        postingEventModel.Should().Contain("[IsDeleted] = 0", "active idempotency must ignore only explicitly soft-deleted historical rows");
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
        var method = ExtractMember(source, "private async Task<CashTransactionDto> PostAsync", "public async Task DeleteAsync");

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
        var method = ExtractMember(source, "private async Task<(CashTransactionDto FromTransaction, CashTransactionDto ToTransaction)> CreateTransferAsync", "public async Task<CashTransactionDto> SubmitAsync");

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
        var serviceMethod = ExtractMember(serviceSource, "private async Task<VendorInvoiceDto> ApproveCoreAsync", "public Task<VendorInvoiceDto> PostAsync");

        approvalsMethod.Should().NotContain("ProcessInventoryReceiptAsync", "the workbench approval path must leave every stock receipt to the governed purchase-receipt/inspection lifecycle");
        approvalsMethod.Should().NotContain("LineItemType == \"Inventory\"", "invoice approval must not contain a parallel inventory-receipt branch");
        approvalsMethod.Should().Contain("must not post stock again", "the authoritative receiving boundary should remain explicit in the approval path");
        approvalsMethod.Should().Contain("if (invoice.IsOpeningBalance)", "the workbench approval path must skip normal AP posting for opening-balance invoices");
        approvalsMethod.IndexOf("if (invoice.IsOpeningBalance)", StringComparison.Ordinal)
            .Should().BeLessThan(approvalsMethod.IndexOf("_vendorInvoiceService.PostAsync", StringComparison.Ordinal), "opening-balance AP invoices must return before normal posting");

        serviceMethod.Should().NotContain("ProcessInventoryReceiptAsync", "the service approval path must leave every stock receipt to the governed purchase-receipt/inspection lifecycle");
        serviceMethod.Should().NotContain("LineItemType == \"Inventory\"", "invoice approval must not contain a parallel inventory-receipt branch");
        serviceMethod.Should().Contain("must not", "the authoritative receiving boundary should remain explicit in the service approval path");
        serviceMethod.Should().Contain("if (!invoice.IsOpeningBalance)", "the service approval path must post only normal AP invoices");
        serviceMethod.IndexOf("if (!invoice.IsOpeningBalance)", StringComparison.Ordinal)
            .Should().BeLessThan(serviceMethod.IndexOf("await PostAsync(invoice.Id", StringComparison.Ordinal), "normal AP posting must be guarded by the opening-balance check");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void WorkbenchVendorInvoiceRejection_ShouldDelegateItsApOwnedOutcome()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "FinanceApprovalsController.cs"));
        var method = ExtractMember(
            source,
            "private async Task ApplyRejectedOutcomeAsync",
            "private async Task FinalizeVendorInvoiceApprovalAsync");
        var vendorStart = method.IndexOf("if (key == Normalize(\"VendorInvoice\"))", StringComparison.Ordinal);
        var nextStart = method.IndexOf("if (key == Normalize(\"Invoice\"))", vendorStart, StringComparison.Ordinal);

        vendorStart.Should().BeGreaterThan(-1);
        nextStart.Should().BeGreaterThan(vendorStart);
        var vendorBlock = method[vendorStart..nextStart];
        vendorBlock.Should().Contain("_vendorInvoiceService.ApplyRejectedWorkflowOutcomeAsync", "AP owns its document and budget-release outcome");
        vendorBlock.Should().NotContain("invoice.Status", "the shared queue must not bypass AP lifecycle invariants");
        vendorBlock.Should().NotContain("_db.SaveChangesAsync", "the shared queue must not commit a partial AP outcome");
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
    public void SupplierReturns_ShouldRemainQuarantinedUntilAuthoritativeProducerEvidenceExists()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "SupplierReturnsController.cs"));
        var createMethod = ExtractMember(source, "public ActionResult<SupplierReturnDto> Create", "[HttpPost(\"{id:guid}/approve\")]");
        var quarantineMethod = ExtractMember(source, "private ObjectResult LegacyMutationUnavailable", "private IQueryable<SupplierReturn> BaseQuery");

        createMethod.Should().Contain("LegacyMutationUnavailable(\"create\")", "Finance cannot originate Procurement or Inventory return evidence");
        source.Should().Contain("LegacyMutationUnavailable(\"approve-and-post\")", "the former one-click approval/post path must also fail closed");
        quarantineMethod.Should().Contain("StatusCodes.Status409Conflict");
        quarantineMethod.Should().Contain("FIN-INT-012");
        quarantineMethod.Should().Contain("FIN-INT-013");
        source.Should().NotContain("ValidateSupplierReturnQuantitiesAsync", "validation of a legacy Finance-owned mutation must not re-enable the quarantined workflow");
        source.Should().NotContain("PostSupplierDebitNoteThroughFinancePostingEngineAsync", "Finance must consume later producer evidence instead of manufacturing it here");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinanceApReturnCreatePage_ShouldExposeTheQuarantinedProducerBoundary()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "ap", "returns", "create", "page.tsx"));

        source.Should().Contain("The legacy Finance return wizard is quarantined");
        source.Should().Contain("FIN-INT-012");
        source.Should().Contain("FIN-INT-013");
        source.Should().NotContain("'/finance/ap/purchase-receipts'", "the quarantined page must not call the former Finance-owned GRV route");
        source.Should().NotContain("`/finance/ap/purchase-receipts/${id}`", "the quarantined page must not fetch mutable legacy GRV detail");
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
        var generator = ExtractMember(controllerSource, "private async Task<string> GenerateReceiptNumberAsync", "private async Task<string> ResolveReceiptNumberAsync");

        var transactionIndex = createMethod.IndexOf("BeginTransactionAsync(IsolationLevel.Serializable", StringComparison.Ordinal);
        var numberingIndex = createMethod.IndexOf("ResolveReceiptNumberAsync(tenantId, receiptDate, dto.ReceiptNumber, cancellationToken)", StringComparison.Ordinal);

        transactionIndex.Should().BeGreaterThan(-1, "receipt creation should hold a serializable transaction");
        transactionIndex.Should().BeLessThan(numberingIndex, "receipt number resolution and any reservation should join the receipt transaction");
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
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "20260710100000_EnsureCurrentArInvoiceTables.cs"));

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
    public void SalesReturnBusinessPartnerMigration_ShouldBeArchivedWhileTheCurrentBaselineIsDiscovered()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "20260717090000_UseBusinessPartnersForSalesReturnAccounting.cs"));

        source.Should().Contain("[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]",
            "hand-written migrations need DbContext metadata when no generated designer partial is present");
        source.Should().Contain("[Migration(\"20260717090000_UseBusinessPartnersForSalesReturnAccounting\")]",
            "EF Core needs the stable migration identifier to discover and apply this schema change");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            // Migration discovery is a relational-provider concern. No connection is opened;
            // SQL Server options only register the same migrations services used at runtime.
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);
        var migrations = context.GetService<IMigrationsAssembly>().Migrations;

        migrations.Should().ContainSingle()
            .Which.Key.Should().Be("20260916132000_DisposableDevelopmentCurrentModelBaseline",
                "legacy migration sources are retained byte-for-byte while only the true current-model baseline is compiled");
    }

    [Fact]
    [Trait("Category", "Authorization")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void WorkflowStartupSeeding_ShouldIncludeFinancePermissionsOutsideDevelopmentDataSeeding()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "DatabaseSeedingService.cs"));
        var workflowSeedMethod = ExtractMember(source, "public async Task SeedWorkflowDefinitionsAsync", "private async Task EnsureFinancePermissionAssignmentsAsync");

        workflowSeedMethod.Should().Contain("EnsureFinancePermissionAssignmentsAsync()",
            "normal startup invokes workflow seeding outside the development-only demo-data branch");
        source.Should().Contain("await SeedRolePermissionAssignmentsAsync();",
            "Finance policy definitions must have matching persisted role grants before protected endpoints are exposed");
    }

    [Fact]
    [Trait("Category", "Frontend")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinanceEntryForms_ShouldUseTheApiRateSnapshotWithoutMagnitudeBasedInversion()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "frontend", "src", "services", "finance.service.ts"));
        var arInvoice = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "ar", "invoices", "new", "page.tsx"));
        var apInvoice = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "ap", "invoices", "create", "page.tsx"));
        var purchaseOrder = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "ap", "purchase-orders", "create", "page.tsx"));

        service.Should().Contain("resolvePostingExchangeRate", "entry forms need one explicit contract for the rate validated by the posting engine");
        service.Should().Contain("return resolvedRate;", "the service must preserve the API snapshot rather than derive an inverse rate");

        foreach (var pageSource in new[] { arInvoice, apInvoice })
        {
            pageSource.Should().Contain("loadApprovedInvoiceRate", "invoice entry must consume the governed date-scoped rate snapshot helper");
            pageSource.Should().Contain("snapshot.rate", "invoice entry must preserve the approved API rate direction");
            pageSource.Should().NotContain("rawRate < 1", "invoice entry cannot infer direction from rate magnitude");
        }
        purchaseOrder.Should().Contain("resolvePostingExchangeRate(rateObj)");
        purchaseOrder.Should().NotContain("rawRate < 1", "the purchase-order rate direction cannot be inferred from magnitude");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void CustomerAccountEndpoints_ShouldUseCurrentArHistoryAndAgingBuckets()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AR", "CustomerService.cs"));

        var balanceMethod = ExtractMember(source, "public async Task<CustomerBalanceDto> GetBalanceAsync", "public async Task<CreditCheckResultDto> CheckCreditLimitAsync");
        var invoiceMethod = ExtractMember(source, "public async Task<List<InvoiceDto>> GetCustomerInvoicesAsync", "public async Task<List<CustomerPaymentDto>> GetCustomerPaymentsAsync");
        var paymentMethod = ExtractMember(source, "public async Task<List<CustomerPaymentDto>> GetCustomerPaymentsAsync", "private IQueryable<BusinessPartner> CustomerPartners()");

        balanceMethod.Should().Contain("_settlementReadModelService.RebuildAsync", "customer balance must rebuild from posted AR facts rather than mutable paid/credited snapshots");
        balanceMethod.Should().Contain("SubledgerSettlementModules.AccountsReceivable", "customer balance must use the AR settlement projection");
        balanceMethod.Should().Contain("AddSettlementBalanceToBalanceBuckets(balance, settlementBalance, asOfDate)", "overdue balances must be assigned to due-date aging buckets from the settlement projection");
        balanceMethod.Should().NotContain("BalanceAmount", "customer balance must not use mutable invoice balance snapshots");
        source.Should().Contain("i.BusinessPartnerId == customerId", "AR invoices use BusinessPartnerId as the current customer key");
        source.Should().Contain("p.BusinessPartnerId == customerId", "AR receipts use canonical Business Partner identity without a CustomerId alias");
        source.Should().Contain("daysOverdue <= 30", "the 1-30 day bucket must be calculated from invoice due dates");
        source.Should().Contain("daysOverdue <= 60", "the 31-60 day bucket must be calculated from invoice due dates");
        source.Should().Contain("daysOverdue <= 90", "the 61-90 day bucket must be calculated from invoice due dates");
        source.Should().NotContain("Days1To30 = 0m", "overdue customer balances must not be hard-coded to current");
        source.Should().NotContain("Days31To60 = 0m", "overdue customer balances must not be hard-coded to current");
        source.Should().NotContain("Days61To90 = 0m", "overdue customer balances must not be hard-coded to current");
        source.Should().NotContain("Days90Plus = 0m", "overdue customer balances must not be hard-coded to current");

        invoiceMethod.Should().Contain("GetCustomerInvoicesQuery(customerId)", "customer account review must query current Finance AR invoices");
        invoiceMethod.Should().Contain(".Include(i => i.LineItems)", "invoice history should return line detail expected by the customer endpoint DTO");
        invoiceMethod.Should().Contain("MapInvoiceToDto", "invoice history should return the existing finance invoice DTO shape");
        invoiceMethod.Should().NotContain("Task.FromResult(new List<InvoiceDto>())", "customer invoice history must not be stubbed out");

        paymentMethod.Should().Contain("GetCustomerPaymentsQuery(customerId)", "customer account review must query current Finance AR receipts");
        paymentMethod.Should().Contain(".Include(p => p.Allocations)", "payment history should include allocation context for account review");
        paymentMethod.Should().Contain("MapPaymentToDto", "payment history should return the existing finance payment DTO shape");
        paymentMethod.Should().NotContain("Task.FromResult(new List<CustomerPaymentDto>())", "customer payment history must not be stubbed out");

        var registrations = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Extensions", "ServiceCollectionExtensions.cs"));
        registrations.Should().Contain("ICustomerService, ErpSystem.Api.Services.Finance.AR.CustomerService", "customer account endpoints must resolve their Finance AR service at runtime");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void CustomerPaymentMigration_ShouldReplaceLegacyCustomerFkWithBusinessPartnerFk()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Data",
            "LegacyMigrationsArchive",
            "20260720110000_AlignCustomerPaymentsToBusinessPartners.cs"));

        source.Should().Contain("[Migration(\"20260720110000_AlignCustomerPaymentsToBusinessPartners\")]",
            "EF Core must discover the hand-written AR receipt alignment migration");
        source.Should().Contain("INNER JOIN [dbo].[Customers] c",
            "receipt-only legacy customers must be preserved as canonical BusinessPartners before the FK changes");
        source.Should().Contain("c.[TenantId] = cp.[TenantId]",
            "legacy receipt migration must never map a counterparty across tenants");
        source.Should().Contain("FK_CustomerPayment_BusinessPartners_CustomerId",
            "the archived transitional migration must remain discoverable as historical evidence");
        source.Should().Contain("DROP CONSTRAINT",
            "the obsolete Customers FK must be removed before current BusinessPartner IDs can be saved");
        source.Should().Contain("WITH CHECK",
            "the replacement FK must validate migrated rows rather than trusting unverified legacy data");
        source.Should().Contain("THROW 51000",
            "unmapped or cross-tenant receipt identities must stop the migration instead of becoming corrupt AR evidence");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);
        context.GetService<IMigrationsAssembly>().Migrations.Should().ContainSingle()
            .Which.Key.Should().Be("20260916132000_DisposableDevelopmentCurrentModelBaseline",
                "the archived compatibility source remains auditable while the merged current-model baseline is the sole compiled migration");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinanceApprovalQueue_ShouldPageAndBatchPaymentSodReadiness()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Finance",
            "FinanceApprovalsController.cs"));
        var method = ExtractMember(
            source,
            "public async Task<ActionResult<IReadOnlyList<FinanceApprovalQueueItemDto>>> GetPending",
            "[HttpPost(\"{approvalId:guid}/approve\")]");

        method.Should().Contain(".Take(pageSize + 1)",
            "the queue must not materialize an unbounded tenant approval set");
        method.Should().Contain("GetQueueReadinessAsync",
            "payment and batch participants must be evaluated from set-based page loads");
        method.Should().NotContain("GetPaymentReadinessAsync",
            "the queue must not reload the complete payment graph once per row");
        method.Should().NotContain("GetBatchReadinessAsync",
            "the queue must not reload the complete batch graph once per row");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void ApExceptionReport_ShouldCountOnlyCompletableCorrectiveActions()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Services",
            "Finance",
            "AP",
            "ApReportsService.cs"));

        source.Should().Contain("OpenCorrectiveActionCount = rows.Count(row =>");
        source.Should().Contain("VendorInvoiceMatchExceptionRules.CanCompleteCorrectiveAction(",
            "pending, rejected, cancelled, and expired exceptions are not actionable corrective work");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void GovernedGrnProjection_ShouldUseBaseUnitsAndHeaderLocationFallback()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Core",
            "Services",
            "Inventory",
            "GoodsReceiptNoteService.cs"));

        source.Should().Contain("purchaseOrderItem.OrderedQuantity * conversion");
        source.Should().Contain("sourceLine.PreviouslyReceiptedQuantity * conversion");
        source.Should().Contain("sourceLine.ToleranceQuantity * conversion");
        source.Should().Contain("sourceLine.MaximumReceivableQuantity * conversion");
        source.Should().Contain("sourceLine.RemainingQuantity * conversion");
        source.Should().Contain("UnitOfMeasure = item.UnitOfMeasure");
        source.Should().Contain("itemDto.StorageLocationId ?? dto.ReceivingLocationId");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinanceApprovalCompletion_ShouldCommitWorkflowAndBusinessOutcomeAtomically()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Finance",
            "FinanceApprovalsController.cs"));
        var processMethod = ExtractMember(
            source,
            "private async Task<ActionResult<WorkflowExecutionResult>> ProcessApprovalAsync",
            "private async Task<WorkflowExecutionResult> ProcessWorkflowAndOutcomeAtomicallyAsync");
        var atomicMethod = ExtractMember(
            source,
            "private async Task<WorkflowExecutionResult> ProcessWorkflowAndOutcomeAtomicallyAsync",
            "internal IQueryable<WorkflowApproval> QueryPendingApprovals");

        processMethod.Should().Contain("ProcessWorkflowAndOutcomeAtomicallyAsync",
            "controller actions must not finalize workflow state separately from the Finance outcome");
        processMethod.Should().NotContain("_workflowService.ProcessApprovalStepAsync",
            "workflow persistence must occur inside the shared atomic helper");
        atomicMethod.Should().Contain("CreateExecutionStrategy()",
            "the explicit transaction must run through the configured relational retry strategy");
        atomicMethod.Should().Contain("BeginTransactionAsync(",
            "workflow completion and outcome application need one database transaction");
        atomicMethod.Should().Contain("isolationLevel",
            "the transaction must retain the entity-specific isolation decision inside the retry strategy");
        atomicMethod.Should().Contain("_workflowService.ProcessApprovalStepAsync",
            "workflow state changes must occur inside the transaction");
        atomicMethod.Should().Contain("ApplyApprovedOutcomeAsync",
            "the approved Finance action must occur in the same transaction");
        atomicMethod.Should().Contain("ApplyRejectedOutcomeAsync",
            "rejected Finance state changes must occur in the same transaction");
        atomicMethod.Should().Contain("CommitAsync(cancellationToken)",
            "the transaction may commit only after the outcome succeeds");
        atomicMethod.Should().Contain("RollbackAsync(cancellationToken)",
            "a failed outcome must leave the workflow action retryable");
        atomicMethod.Should().Contain("_db.ChangeTracker.Clear()",
            "rolled-back workflow and outcome state must not remain tracked for a later save");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void ArReceiptCreation_ShouldCommitSourceAllocationAndPostingAtomically()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Services",
            "Finance",
            "AR",
            "PaymentService.cs"));
        var createMethod = ExtractMember(
            source,
            "private async Task<CustomerPaymentDto> CreateAsync",
            "public async Task<CustomerPaymentDto> UpdateAsync");
        var allocationMethod = ExtractMember(
            source,
            "private async Task<PaymentAllocationResultDto> AllocatePaymentCoreAsync",
            "private async Task<IReadOnlyDictionary<Guid, Invoice>> ResolveRequestedAllocationInvoicesAsync");
        var resolverMethod = ExtractMember(
            source,
            "private async Task<IReadOnlyDictionary<Guid, Invoice>> ResolveRequestedAllocationInvoicesAsync",
            "private async Task<PaymentAllocationResultDto> AllocatePostedCustomerAdvanceAsync");

        var transactionIndex = createMethod.IndexOf("BeginTransactionAsync(IsolationLevel.Serializable", StringComparison.Ordinal);
        var sourceSaveIndex = createMethod.IndexOf("_unitOfWork.SaveChangesAsync", StringComparison.Ordinal);
        var allocationIndex = createMethod.IndexOf("AllocatePaymentCoreAsync", StringComparison.Ordinal);
        var postingIndex = createMethod.IndexOf("PostArReceiptCoreAsync", StringComparison.Ordinal);
        var commitIndex = createMethod.IndexOf("_unitOfWork.CommitAsync", StringComparison.Ordinal);

        transactionIndex.Should().BeGreaterThan(-1, "receipt creation must establish the accounting transaction before mutation");
        transactionIndex.Should().BeLessThan(sourceSaveIndex, "the new payment row must be part of the transaction");
        sourceSaveIndex.Should().BeLessThan(allocationIndex, "allocation must use the persisted source row in the same transaction");
        allocationIndex.Should().BeLessThan(postingIndex, "validated allocation must precede GL posting");
        postingIndex.Should().BeLessThan(commitIndex, "posting and source snapshots must commit together");
        createMethod.Should().Contain("_unitOfWork.RollbackAsync", "any create/allocation/posting failure must roll back the receipt command");
        createMethod.Should().NotContain("await PostAsync(payment.Id", "create must use the shared posting core instead of starting a nested transaction");
        createMethod.Should().Contain("allocationResult.Allocations.Count != dto.Allocations.Count",
            "a partially applied allocation request must not be posted as an advance");

        allocationMethod.Should().Contain("ResolveRequestedAllocationInvoicesAsync",
            "allocation references must be validated before invoice or customer snapshots are changed");
        allocationMethod.Should().NotContain("skipping allocation",
            "missing or mismatched invoice references must fail rather than silently becoming unapplied cash");
        allocationMethod.Should().NotContain("Capping",
            "maker-entered cash and statutory deductions must never be silently rewritten");
        allocationMethod.Should().Contain("Allocation would over-settle invoice",
            "every over-allocation must fail closed for maker correction");
        resolverMethod.Should().Contain("i.TenantId == TenantId", "allocation invoice lookup must remain tenant-scoped");
        resolverMethod.Should().Contain("i.BusinessPartnerId != payment.BusinessPartnerId",
            "every allocated invoice must belong to the receipt customer");
        resolverMethod.Should().Contain("throw new KeyNotFoundException",
            "missing invoice IDs must produce an explicit client-visible failure");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void WhtCertificateGeneration_ShouldSerializeNumberAssignment()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Services",
            "Finance",
            "Taxation",
            "WithholdingTaxCertificateService.cs"));
        var generateMethod = ExtractMember(
            source,
            "public Task<WhtCertificateDto> GenerateApCertificateAsync",
            "public Task<WhtCertificateDto> ReissueApCertificateAsync");
        var coreMethod = ExtractMember(
            source,
            "private async Task<WhtCertificateDto> IssueCertificateCoreAsync",
            "private async Task<WhtCertificateDto> ReissueCertificateCoreAsync");
        var transactionHelper = ExtractMember(
            source,
            "private async Task<T> ExecuteSerializableAsync<T>",
            "private async Task RecordCertificateAuditAsync");

        generateMethod.Should().Contain("ExecuteSerializableAsync",
            "certificate generation must use the shared serializable issuance boundary");
        transactionHelper.Should().Contain("CreateExecutionStrategy",
            "SQL retry behavior must wrap the complete certificate assignment transaction");
        transactionHelper.Should().Contain("IsolationLevel.Serializable",
            "concurrent generated or manual certificate numbers must be serialized");
        generateMethod.Should().Contain("IssueCertificateCoreAsync",
            "ambient and service-owned transactions must share one assignment implementation");
        coreMethod.Should().Contain("EnsureCertificateNumberIsUniqueAsync",
            "manual certificate numbers must remain tenant-unique");
        coreMethod.Should().Contain("GenerateCertificateNumberAsync",
            "automatic numbering must be calculated inside the serialized transaction");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Contract", "FIN-INT-007")]
    public void ProcurementAssetDraft_ShouldReserveAcceptedQuantityAtomically()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Services",
            "Finance",
            "FixedAssets",
            "ProcurementFixedAssetCapitalizationAdapter.cs"));
        var createMethod = ExtractMember(
            source,
            "public async Task<ProcurementFixedAssetCapitalizationDto> CreateDraftAsync",
            "private async Task<ProcurementFixedAssetCapitalizationDto> CreateDraftCoreAsync");

        createMethod.Should().Contain("CreateExecutionStrategy()",
            "SQL Server transient retry handling must own the Finance reservation transaction");
        createMethod.Should().Contain("BeginTransactionAsync(IsolationLevel.Serializable",
            "availability and reservation must be serialized for concurrent Finance users");
        createMethod.Should().Contain("CreateDraftCoreAsync",
            "ambient and adapter-owned transactions must execute the same source-validation logic");
        createMethod.Should().Contain("CommitAsync(cancellationToken)",
            "the asset draft and accepted-unit reservation must commit together");
        createMethod.Should().Contain("RollbackAsync(cancellationToken)",
            "a failed handoff must not leave an orphaned asset draft or source reservation");
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
