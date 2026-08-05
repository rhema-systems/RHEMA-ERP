namespace ErpSystem.Core.DTOs.Documents;

public sealed class DocumentRenderRequestDto
{
    public string DocumentType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Format { get; set; } = "pdf";
    public string CopyType { get; set; } = "Original";
    public Dictionary<string, string>? Options { get; set; }
}

public sealed class RenderedDocumentDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "document";
    public string DocumentType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Format { get; set; } = "pdf";
}

public static class DocumentTypes
{
    public const string FinanceJournalVoucher = "Finance.JournalVoucher";
    public const string FinanceTrialBalance = "Finance.TrialBalance";
    public const string FinanceIncomeStatement = "Finance.IncomeStatement";
    public const string FinanceBalanceSheet = "Finance.BalanceSheet";
    public const string FinanceCashFlowStatement = "Finance.CashFlowStatement";
    public const string FinanceMultiCurrencyDetail = "Finance.MultiCurrencyDetail";
    public const string FinanceDetailedLedger = "Finance.DetailedLedger";
    public const string InventoryStoreIssueVoucher = "Inventory.StoreIssueVoucher";
    public const string InventoryStoreReturnVoucher = "Inventory.StoreReturnVoucher";
}
