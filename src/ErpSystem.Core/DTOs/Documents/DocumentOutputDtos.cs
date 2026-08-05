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

/// <summary>
/// Request contract for issuing a controlled transaction document. The server validates the
/// requested copy type against retained issue history, so a caller cannot label a later copy as
/// another original.
/// </summary>
public sealed class ControlledDocumentIssueRequestDto
{
    public string Format { get; set; } = "pdf";
    public string CopyType { get; set; } = ControlledDocumentCopyTypes.Original;
    public string? ReplacementReason { get; set; }
}

public sealed class ControlledDocumentIssueSummaryDto
{
    public bool OriginalIssued { get; set; }
    public DateTime? OriginalIssuedAtUtc { get; set; }
    public string? OriginalIssuedByName { get; set; }
    public int ReplacementCount { get; set; }
    public int TotalIssued { get; set; }
    public DateTime? LastIssuedAtUtc { get; set; }
    public string? LastIssuedByName { get; set; }
}

/// <summary>
/// Copy labels used by persisted issuance records and PDF watermarks. "Reprint" remains accepted
/// by the API as an input alias, but all new records use the clearer "Replacement" terminology.
/// </summary>
public static class ControlledDocumentCopyTypes
{
    public const string Original = "Original";
    public const string Replacement = "Replacement";
}

/// <summary>
/// Immutable rendering context allocated before PDF generation. The issue service revalidates
/// this sequence under a serializable transaction before it records the emitted bytes.
/// </summary>
public sealed record ControlledDocumentIssuePreparationDto(
    Guid IssueId,
    Guid TenantId,
    string DocumentType,
    string SourceDocumentType,
    Guid SourceDocumentId,
    string DocumentNumber,
    int CopyNumber,
    string CopyType,
    string? ReplacementReason,
    DateTime IssuedAtUtc,
    Guid IssuedById,
    string IssuedByName);

public static class DocumentTypes
{
    public const string FinanceJournalVoucher = "Finance.JournalVoucher";
    /// <summary>
    /// Controlled AP payment voucher rendered from the canonical VendorPayment record. The
    /// VendorPayment ID is the entity identifier; the renderer must never create or post a
    /// parallel voucher transaction.
    /// </summary>
    public const string FinanceApPaymentVoucher = "Finance.AP.PaymentVoucher";
    /// <summary>
    /// Controlled payment instruction rendered from a posted Finance-owned CashTransaction of
    /// type Payment. The cash transaction remains the source for account allocation and GL trace.
    /// </summary>
    public const string FinanceCashBankPaymentSlip = "Finance.CashBank.PaymentSlip";
    /// <summary>
    /// Controlled customer receipt rendered from the canonical posted CustomerPayment record.
    /// </summary>
    public const string FinanceArCustomerReceipt = "Finance.AR.CustomerReceipt";
    /// <summary>
    /// Parameterized AP supplier statement rendered from the existing supplier detailed-ledger
    /// report. EntityId is not used; period, supplier IDs and presentation currency are options.
    /// </summary>
    public const string FinanceApSupplierStatement = "Finance.AP.SupplierStatement";
    public const string FinanceTrialBalance = "Finance.TrialBalance";
    public const string FinanceIncomeStatement = "Finance.IncomeStatement";
    public const string FinanceBalanceSheet = "Finance.BalanceSheet";
    public const string FinanceCashFlowStatement = "Finance.CashFlowStatement";
    public const string FinanceMultiCurrencyDetail = "Finance.MultiCurrencyDetail";
    public const string FinanceDetailedLedger = "Finance.DetailedLedger";
    /// <summary>
    /// Printable maker-checker certificate and retained evidence for one numbered period-close
    /// cycle. The entity identifier is the FinanceCloseCycle ID, not the fiscal period ID, so a
    /// reopened period never overwrites the original signed pack.
    /// </summary>
    public const string FinanceClosePack = "Finance.ClosePack";
}
