using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Api.Services.Finance.Reporting;

/// <summary>
/// Server-owned Finance reporting surface for FR-RP-012. SQL fragments live only
/// here and are selected by catalogue keys; browser input can never supply a table,
/// join or SQL expression. Adding another dataset is therefore an explicit code
/// review rather than a runtime schema-exposure decision.
/// </summary>
public static class FinanceAdHocReportCatalog
{
    public static readonly IReadOnlyList<FinanceAdHocDataset> All =
    [
        new("gl-lines", "General ledger lines",
            "Posted and draft journal lines with account, source, book and currency evidence.",
            "[AccountTransactions] [t] INNER JOIN [JournalEntries] [j] ON [j].[Id] = [t].[JournalEntryId] " +
            "INNER JOIN [Accounts] [a] ON [a].[Id] = [t].[AccountId]",
            "[t].[TenantId]", "[t].[IsDeleted] = 0 AND [j].[IsDeleted] = 0 AND [a].[IsDeleted] = 0",
            Fields(
                Text("journalNumber", "Journal number", "[j].[JournalEntryNumber]"),
                Date("transactionDate", "Transaction date", "[t].[TransactionDate]"),
                Date("postingDate", "Posting date", "[t].[PostedDate]"),
                Text("accountNumber", "Account number", "[a].[AccountNumber]"),
                Text("accountName", "Account name", "[a].[AccountName]"),
                Text("description", "Line description", "[t].[Description]"),
                Money("debit", "Debit (functional)", "[t].[DebitAmount]"),
                Money("credit", "Credit (functional)", "[t].[CreditAmount]"),
                Text("functionalCurrency", "Functional currency", "[t].[FunctionalCurrencyCode]"),
                Text("transactionCurrency", "Transaction currency", "[t].[TransactionCurrency]"),
                Text("sourceModule", "Source module", "[t].[SourceModule]"),
                Text("sourceDocumentType", "Source document type", "[t].[SourceDocumentType]"),
                Text("sourceReference", "Source reference", "[t].[SourceReferenceNumber]"),
                Text("postingStatus", "Posting status", "[t].[PostingStatus]"),
                Text("book", "Accounting book", "[t].[BookClassification]"),
                Text("segments", "Account segments", "[t].[SegmentString]"))),

        new("ap-invoices", "Accounts payable invoices",
            "Supplier invoice balances, approval, matching, currency and due-date evidence.",
            "[VendorInvoices] [i]", "[i].[TenantId]", "[i].[IsDeleted] = 0",
            Fields(
                Text("invoiceNumber", "Invoice number", "[i].[InvoiceNumber]"),
                Text("supplierInvoiceNumber", "Supplier invoice number", "[i].[SupplierInvoiceNumber]"),
                Text("supplierName", "Supplier", "[i].[SupplierName]"),
                Date("invoiceDate", "Invoice date", "[i].[InvoiceDate]"),
                Date("dueDate", "Due date", "[i].[DueDate]"),
                Money("totalAmount", "Invoice total", "[i].[TotalAmount]"),
                Money("paidAmount", "Paid amount", "[i].[PaidAmount]"),
                Money("balanceAmount", "Outstanding balance", "([i].[TotalAmount] - [i].[PaidAmount])"),
                Text("currency", "Currency", "[i].[CurrencyCode]"),
                Money("baseAmount", "Functional amount", "[i].[BaseCurrencyAmount]"),
                Text("status", "Invoice status", "CASE [i].[Status] WHEN 1 THEN 'Draft' WHEN 2 THEN 'Pending approval' WHEN 3 THEN 'Approved' WHEN 4 THEN 'Partially paid' WHEN 5 THEN 'Paid' WHEN 6 THEN 'Overdue' WHEN 7 THEN 'Voided' WHEN 8 THEN 'Rejected' WHEN 9 THEN 'On hold' ELSE 'Unknown' END"),
                Text("approvalStatus", "Approval status", "[i].[ApprovalStatus]"),
                Text("matchingStatus", "Matching status", "CONVERT(nvarchar(30), [i].[MatchingStatus])"),
                Text("reference", "Reference", "[i].[Reference]"))),

        new("ar-invoices", "Accounts receivable invoices",
            "Customer invoice balances, status, currency and collection due dates.",
            "[Invoices] [i]", "[i].[TenantId]", "[i].[IsDeleted] = 0",
            Fields(
                Text("invoiceNumber", "Invoice number", "[i].[InvoiceNumber]"),
                Text("customerName", "Customer", "[i].[CustomerName]"),
                Date("invoiceDate", "Invoice date", "[i].[InvoiceDate]"),
                Date("dueDate", "Due date", "[i].[DueDate]"),
                Money("totalAmount", "Invoice total", "[i].[TotalAmount]"),
                Money("paidAmount", "Paid amount", "[i].[PaidAmount]"),
                Money("creditedAmount", "Credited amount", "[i].[CreditedAmount]"),
                Money("balanceAmount", "Outstanding balance", "([i].[TotalAmount] - [i].[PaidAmount] - [i].[CreditedAmount])"),
                Text("currency", "Currency", "[i].[CurrencyCode]"),
                Money("baseAmount", "Functional amount", "[i].[BaseCurrencyAmount]"),
                Text("status", "Invoice status", "CASE [i].[Status] WHEN 1 THEN 'Draft' WHEN 2 THEN 'Sent' WHEN 3 THEN 'Partially paid' WHEN 4 THEN 'Paid' WHEN 5 THEN 'Overdue' WHEN 6 THEN 'Cancelled' WHEN 7 THEN 'Pending approval' WHEN 8 THEN 'Approved' WHEN 9 THEN 'Rejected' ELSE 'Unknown' END"),
                Text("reference", "Reference", "[i].[Reference]"))),

        new("fixed-assets", "Fixed asset register",
            "Asset cost, net book value, location, category and capitalization status.",
            "[FixedAssets] [f] INNER JOIN [FixedAssetCategories] [c] ON [c].[Id] = [f].[FixedAssetCategoryId]",
            "[f].[TenantId]", "[f].[IsDeleted] = 0 AND [c].[IsDeleted] = 0",
            Fields(
                Text("assetCode", "Asset code", "[f].[AssetCode]"),
                Text("assetName", "Asset name", "[f].[Name]"),
                Text("categoryCode", "Category code", "[c].[Code]"),
                Text("categoryName", "Category", "[c].[Name]"),
                Text("location", "Location", "[f].[Location]"),
                Text("segments", "Current segments", "[f].[CurrentSegmentString]"),
                Date("purchaseDate", "Purchase date", "[f].[PurchaseDate]"),
                Date("capitalizationDate", "Capitalization date", "[f].[CapitalizationDate]"),
                Money("acquisitionCost", "Acquisition cost", "[f].[AcquisitionCost]"),
                Money("netBookValue", "Net book value", "[f].[NetBookValue]"),
                Money("residualValue", "Residual value", "[f].[ResidualValue]"),
                Text("currency", "Functional currency", "[f].[FunctionalCurrencyCode]"),
                Text("status", "Asset status", "CASE [f].[Status] WHEN 1 THEN 'Draft' WHEN 2 THEN 'Active' WHEN 3 THEN 'Fully depreciated' WHEN 4 THEN 'Disposed' WHEN 5 THEN 'Held for sale' WHEN 6 THEN 'Written off' WHEN 7 THEN 'Under construction' WHEN 8 THEN 'On hold' WHEN 9 THEN 'Acquired' WHEN 10 THEN 'Capitalized' WHEN 11 THEN 'Pending approval' WHEN 12 THEN 'Rejected' ELSE 'Unknown' END"),
                Text("serialNumber", "Serial number", "[f].[SerialNumber]"))),

        new("chart-of-accounts", "Chart of accounts",
            "Account classifications, posting controls, currency and reporting mappings. Use book-balances for balances.",
            "[Accounts] [a] OUTER APPLY (SELECT [ac].[Code], [ac].[Name], [parent].[Name] AS [ParentName] " +
            "FROM [AccountAccountingBooks] [aab] INNER JOIN [AccountingBooks] [ab] ON [ab].[Id] = [aab].[AccountingBookId] " +
            "AND [ab].[TenantId] = [a].[TenantId] AND [ab].[IsDeleted] = 0 AND [ab].[IsActive] = 1 AND [ab].[AllowsPosting] = 1 AND [ab].[IsDefault] = 1 " +
            "INNER JOIN [AccountClassifications] [ac] ON [ac].[Id] = [aab].[AccountClassificationId] " +
            "AND [ac].[TenantId] = [a].[TenantId] AND [ac].[AccountingBookId] = [ab].[Id] AND [ac].[IsDeleted] = 0 AND [ac].[Status] = 2 " +
            "LEFT JOIN [AccountClassifications] [parent] ON [parent].[Id] = [ac].[ParentClassificationId] " +
            "AND [parent].[TenantId] = [a].[TenantId] AND [parent].[AccountingBookId] = [ab].[Id] AND [parent].[IsDeleted] = 0 " +
            "WHERE [aab].[AccountId] = [a].[Id] AND [aab].[TenantId] = [a].[TenantId] AND [aab].[IsDeleted] = 0 " +
            "AND [aab].[IsEnabled] = 1 AND (SELECT COUNT_BIG(*) FROM [AccountingBooks] [authority] " +
            "WHERE [authority].[TenantId] = [a].[TenantId] AND [authority].[IsDeleted] = 0 AND [authority].[IsActive] = 1 " +
            "AND [authority].[AllowsPosting] = 1 AND [authority].[IsDefault] = 1) = 1) [classification]",
            "[a].[TenantId]", "[a].[IsDeleted] = 0",
            Fields(
                Text("accountNumber", "Account number", "[a].[AccountNumber]"),
                Text("accountCode", "Account code", "[a].[AccountCode]"),
                Text("accountName", "Account name", "[a].[AccountName]"),
                Text("accountType", "Account type", "CASE [a].[AccountType] WHEN 1 THEN 'Asset' WHEN 2 THEN 'Liability' WHEN 3 THEN 'Equity' WHEN 4 THEN 'Revenue' WHEN 5 THEN 'Expense' ELSE 'Unknown' END"),
                Text("category", "Classification parent", "COALESCE([classification].[ParentName], [classification].[Name])"),
                Text("subCategory", "Classification", "[classification].[Name]"),
                Text("classificationCode", "Classification code", "[classification].[Code]"),
                Text("currency", "Currency", "[a].[CurrencyCode]"),
                Bool("allowPosting", "Allows direct posting", "[a].[AllowDirectPosting]"),
                Bool("controlAccount", "Control account", "[a].[IsControlAccount]"),
                Text("status", "Account status", "CASE [a].[Status] WHEN 1 THEN 'Active' WHEN 2 THEN 'Inactive' WHEN 3 THEN 'Closed' WHEN 4 THEN 'Pending approval' ELSE 'Unknown' END"),
                Date("lastTransactionDate", "Last transaction date", "[a].[LastTransactionDate]"))),

        new("book-balances", "Accounting book balances",
            "Period balances and movements for an exact accounting book, account and functional currency.",
            "[AccountBalances] [b] INNER JOIN [Accounts] [a] ON [a].[Id] = [b].[AccountId] " +
            "AND [a].[TenantId] = [b].[TenantId] AND [a].[IsDeleted] = 0 " +
            "INNER JOIN [AccountingBooks] [book] ON [book].[Id] = [b].[AccountingBookId] " +
            "AND [book].[TenantId] = [b].[TenantId] AND [book].[IsDeleted] = 0 " +
            "INNER JOIN [FiscalPeriods] [period] ON [period].[Id] = [b].[FiscalPeriodId] " +
            "AND [period].[TenantId] = [b].[TenantId] AND [period].[IsDeleted] = 0",
            "[b].[TenantId]", "[b].[IsDeleted] = 0",
            Fields(
                Text("accountNumber", "Account number", "[a].[AccountNumber]"),
                Text("accountName", "Account name", "[a].[AccountName]"),
                Text("book", "Accounting book", "[book].[Code]"),
                Text("period", "Fiscal period", "[period].[PeriodCode]"),
                Text("currency", "Functional currency", "[b].[Currency]"),
                Money("openingBalance", "Opening signed balance", "[b].[OpeningBalance]"),
                Money("periodDebits", "Period debits", "[b].[PeriodDebits]"),
                Money("periodCredits", "Period credits", "[b].[PeriodCredits]"),
                Money("closingBalance", "Closing signed balance", "[b].[ClosingBalance]")))
    ];

    public static FinanceAdHocDataset Required(string code) => All.FirstOrDefault(item =>
        item.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("The selected Finance reporting dataset is not available.");

    public static FinanceAdHocDatasetDto Map(FinanceAdHocDataset dataset) => new()
    {
        Code = dataset.Code,
        Name = dataset.Name,
        Description = dataset.Description,
        Fields = dataset.Fields.Values.Select(field => new FinanceAdHocDatasetFieldDto
        {
            Key = field.Key,
            Label = field.Label,
            DataType = field.DataType,
            CanFilter = true,
            CanGroup = field.CanGroup,
            CanAggregate = field.CanAggregate
        }).ToList()
    };

    private static IReadOnlyDictionary<string, FinanceAdHocField> Fields(params FinanceAdHocField[] fields) =>
        fields.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
    private static FinanceAdHocField Text(string key, string label, string sql) => new(key, label, "String", sql, true, false);
    private static FinanceAdHocField Date(string key, string label, string sql) => new(key, label, "DateTime", sql, true, false);
    private static FinanceAdHocField Money(string key, string label, string sql) => new(key, label, "Decimal", sql, true, true);
    private static FinanceAdHocField Bool(string key, string label, string sql) => new(key, label, "Boolean", sql, true, false);
}

public sealed record FinanceAdHocDataset(
    string Code,
    string Name,
    string Description,
    string FromSql,
    string TenantSql,
    string BaselinePredicate,
    IReadOnlyDictionary<string, FinanceAdHocField> Fields);

public sealed record FinanceAdHocField(
    string Key,
    string Label,
    string DataType,
    string SqlExpression,
    bool CanGroup,
    bool CanAggregate);
