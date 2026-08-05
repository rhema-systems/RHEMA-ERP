using ErpSystem.Core.DTOs.Numbering;
using ErpSystem.Core.Entities.Numbering;

namespace ErpSystem.Core.Interfaces.Numbering;

public interface IDocumentNumberingService
{
    Task<string> GenerateAsync(
        string module,
        string documentType,
        Guid? tenantId = null,
        DateTime? documentDate = null,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default);

    Task<string> GenerateConfiguredAsync(
        string module,
        string documentType,
        string name,
        string format,
        string resetPolicy,
        Guid? tenantId = null,
        DateTime? documentDate = null,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentSequenceDefinitionDto>> GetDefinitionsAsync(
        string? module = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<DocumentSequenceDefinitionDto> UpdateDefinitionAsync(
        Guid id,
        UpdateDocumentSequenceDefinitionDto dto,
        CancellationToken cancellationToken = default);

    Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public static class DocumentNumberingModules
{
    public const string Finance = "Finance";
    public const string Procurement = "Procurement";
    public const string Sales = "Sales";
}

public static class FinanceDocumentTypes
{
    public const string JournalEntry = "JournalEntry";
    public const string JournalBatch = "JournalBatch";
    public const string UnitJournalEntry = "UnitJournalEntry";
    public const string ARInvoice = "ARInvoice";
    public const string ARPayment = "ARPayment";
    public const string ARCreditNote = "ARCreditNote";
    public const string ARAdjustmentJournal = "ARAdjustmentJournal";
    public const string APInvoice = "APInvoice";
    public const string APPayment = "APPayment";
    public const string APPaymentBatch = "APPaymentBatch";
    public const string APAdjustmentJournal = "APAdjustmentJournal";
    public const string APSupplierReturn = "APSupplierReturn";
    public const string APSupplierDebitNote = "APSupplierDebitNote";
    public const string FinancePurchaseOrder = "FinancePurchaseOrder";
    public const string FinancePurchaseOrderReceipt = "FinancePurchaseOrderReceipt";
    public const string CustomerAccount = "CustomerAccount";
    public const string CashReceipt = "CashReceipt";
    public const string CashPayment = "CashPayment";
    public const string BankTransfer = "BankTransfer";
    public const string BankDeposit = "BankDeposit";
    public const string LiquidityEntry = "LiquidityEntry";
    public const string CashTillSession = "CashTillSession";
    public const string ReturnedCheque = "ReturnedCheque";
    public const string CurrencyRevaluation = "CurrencyRevaluation";
    public const string YearEndClose = "YearEndClose";
    public const string FixedAssetJournal = "FixedAssetJournal";
    public const string AssetTransfer = "AssetTransfer";
    public const string AssetVerification = "AssetVerification";
    public const string AssetDisposal = "AssetDisposal";
    public const string LeaseJournal = "LeaseJournal";
}

public static class SalesDocumentTypes
{
    public const string Quote = "Quote";
    public const string SalesOrder = "SalesOrder";
    public const string DeliveryNote = "DeliveryNote";
    public const string SalesAgreement = "SalesAgreement";
    public const string ReturnOrder = "ReturnOrder";
    public const string CreditNote = "CreditNote";
    public const string Refund = "Refund";
    public const string CommissionStatement = "CommissionStatement";
}

public static class DocumentSequenceDefaults
{
    public static IReadOnlyList<DocumentSequenceDefinition> Create(Guid tenantId)
    {
        return new List<DocumentSequenceDefinition>
        {
            Finance(tenantId, FinanceDocumentTypes.JournalEntry, "General Journal Entry", "JE-{YYYY}-{######}", 6, DocumentSequenceResetPolicies.Yearly, true, "GL journal entries, reversals, and subledger postings."),
            Finance(tenantId, FinanceDocumentTypes.JournalBatch, "General Journal Batch", "JB-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, false, "Controlled groups of manual GL journal entries."),
            Finance(tenantId, FinanceDocumentTypes.UnitJournalEntry, "Unit Journal Entry", "UJE-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, true, "Unit accounting journal entries and reversals."),
            Finance(tenantId, FinanceDocumentTypes.ARInvoice, "Customer Invoice", "INV-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Accounts receivable invoices."),
            Finance(tenantId, FinanceDocumentTypes.ARPayment, "Customer Payment", "PMT-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Accounts receivable receipts/payments."),
            Finance(tenantId, FinanceDocumentTypes.ARCreditNote, "AR Credit Note", "CN-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Accounts receivable credit notes."),
            Finance(tenantId, FinanceDocumentTypes.ARAdjustmentJournal, "AR Adjustment Journal", "ARJ-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Accounts receivable subledger adjustment journals."),
            Finance(tenantId, FinanceDocumentTypes.APInvoice, "Vendor Invoice", "VI-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, false, "Accounts payable invoices."),
            Finance(tenantId, FinanceDocumentTypes.APPayment, "Vendor Payment", "VP-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, false, "Accounts payable payments."),
            Finance(tenantId, FinanceDocumentTypes.APPaymentBatch, "Vendor Payment Batch", "PB-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, false, "Accounts payable payment batches."),
            Finance(tenantId, FinanceDocumentTypes.APAdjustmentJournal, "AP Adjustment Journal", "APJ-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Accounts payable subledger adjustment journals."),
            Finance(tenantId, FinanceDocumentTypes.APSupplierReturn, "AP Supplier Return", "SR-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, true, "Supplier return slips and AP return workflows."),
            Finance(tenantId, FinanceDocumentTypes.APSupplierDebitNote, "AP Supplier Debit Note", "SDN-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, false, "Supplier debit notes generated from AP supplier returns."),
            Finance(tenantId, FinanceDocumentTypes.FinancePurchaseOrder, "Finance Purchase Order", "FPO-{YYYY}-{######}", 6, DocumentSequenceResetPolicies.Yearly, false, "Finance module purchase orders."),
            Finance(tenantId, FinanceDocumentTypes.FinancePurchaseOrderReceipt, "Finance Purchase Receipt", "FGRV-{YYYY}-{######}", 6, DocumentSequenceResetPolicies.Yearly, false, "Finance module purchase receipts and GRVs."),
            Finance(tenantId, FinanceDocumentTypes.CustomerAccount, "Customer Account", "CUST-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, true, "AR customer account codes."),
            Finance(tenantId, FinanceDocumentTypes.CashReceipt, "Cash Receipt", "RCT-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Cash management receipts."),
            Finance(tenantId, FinanceDocumentTypes.CashPayment, "Cash Payment", "CPY-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Cash management payments."),
            Finance(tenantId, FinanceDocumentTypes.BankTransfer, "Bank Transfer", "TRF-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Bank and cash transfers."),
            Finance(tenantId, FinanceDocumentTypes.BankDeposit, "Bank Deposit", "DEP-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Approved settlements from liquidity accounts into bank accounts."),
            Finance(tenantId, FinanceDocumentTypes.LiquidityEntry, "Liquidity Entry", "LQE-{YYYY}{MM}-{#####}", 5, DocumentSequenceResetPolicies.Monthly, true, "Operational settlement subledger entries."),
            Finance(tenantId, FinanceDocumentTypes.CashTillSession, "Cashier Till Session", "TILL-{YYYY}{MM}{DD}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Physical cash custody, denomination count, and independent closure."),
            Finance(tenantId, FinanceDocumentTypes.ReturnedCheque, "Returned Cheque", "RCH-{YYYY}{MM}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Returned customer cheque cases."),
            Finance(tenantId, FinanceDocumentTypes.CurrencyRevaluation, "Currency Revaluation", "REV-{YYYY}{MM}{DD}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Multi-currency revaluation journals."),
            Finance(tenantId, FinanceDocumentTypes.YearEndClose, "Year-end Close", "YE-CLOSE-{YYYY}-{###}", 3, DocumentSequenceResetPolicies.Yearly, false, "Fiscal year closing journals."),
            Finance(tenantId, FinanceDocumentTypes.FixedAssetJournal, "Fixed Asset Journal", "FAJ-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, false, "Fixed asset depreciation, transfer, disposal, and valuation journals."),
            Finance(tenantId, FinanceDocumentTypes.AssetTransfer, "Fixed Asset Transfer", "TRF-{YYYY}{MM}{DD}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Fixed asset transfer requests."),
            Finance(tenantId, FinanceDocumentTypes.AssetVerification, "Fixed Asset Verification", "VRF-{YYYY}{MM}{DD}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Fixed asset verification sessions."),
            Finance(tenantId, FinanceDocumentTypes.AssetDisposal, "Fixed Asset Disposal", "DSP-{YYYY}{MM}{DD}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Fixed asset disposal requests."),
            Finance(tenantId, FinanceDocumentTypes.LeaseJournal, "Lease Journal", "LEASE-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, false, "Lease activation and payment journals."),

            Sales(tenantId, SalesDocumentTypes.Quote, "Sales Quote", "QT-{######}", 6, DocumentSequenceResetPolicies.Never, false, "Sales quotes."),
            Sales(tenantId, SalesDocumentTypes.SalesOrder, "Sales Order", "SO-{######}", 6, DocumentSequenceResetPolicies.Never, false, "Sales orders."),
            Sales(tenantId, SalesDocumentTypes.DeliveryNote, "Delivery Note", "DN-{######}", 6, DocumentSequenceResetPolicies.Never, false, "Delivery notes."),
            Sales(tenantId, SalesDocumentTypes.SalesAgreement, "Sales Agreement", "SA-{YYYY}-{#####}", 5, DocumentSequenceResetPolicies.Yearly, false, "Sales agreements and contracts."),
            Sales(tenantId, SalesDocumentTypes.ReturnOrder, "Return Order", "RO-{######}", 6, DocumentSequenceResetPolicies.Never, false, "Sales return orders."),
            Sales(tenantId, SalesDocumentTypes.CreditNote, "Sales Credit Note", "CN-{######}", 6, DocumentSequenceResetPolicies.Never, false, "Sales credit notes."),
            Sales(tenantId, SalesDocumentTypes.Refund, "Customer Refund", "RF-{######}", 6, DocumentSequenceResetPolicies.Never, false, "Customer refunds."),
            Sales(tenantId, SalesDocumentTypes.CommissionStatement, "Commission Statement", "COM-{YYYY}{MM}{DD}-{####}", 4, DocumentSequenceResetPolicies.Monthly, false, "Sales commission statements.")
        };
    }

    private static DocumentSequenceDefinition Finance(
        Guid tenantId,
        string documentType,
        string name,
        string format,
        int minimumDigits,
        string resetPolicy,
        bool allowManualEntry,
        string description)
    {
        return Create(tenantId, DocumentNumberingModules.Finance, documentType, name, format, minimumDigits, resetPolicy, allowManualEntry, description);
    }

    private static DocumentSequenceDefinition Sales(
        Guid tenantId,
        string documentType,
        string name,
        string format,
        int minimumDigits,
        string resetPolicy,
        bool allowManualEntry,
        string description)
    {
        return Create(tenantId, DocumentNumberingModules.Sales, documentType, name, format, minimumDigits, resetPolicy, allowManualEntry, description);
    }

    private static DocumentSequenceDefinition Create(
        Guid tenantId,
        string module,
        string documentType,
        string name,
        string format,
        int minimumDigits,
        string resetPolicy,
        bool allowManualEntry,
        string description)
    {
        return new DocumentSequenceDefinition
        {
            TenantId = tenantId,
            Module = module,
            DocumentType = documentType,
            Name = name,
            Format = format,
            MinimumDigits = minimumDigits,
            ResetPolicy = resetPolicy,
            AllowManualEntry = allowManualEntry,
            Description = description
        };
    }
}
