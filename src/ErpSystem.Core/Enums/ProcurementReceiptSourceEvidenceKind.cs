namespace ErpSystem.Core.Enums;

/// <summary>
/// Supplier-originated documents captured at goods receipt. A VAT invoice copy
/// is supporting evidence only; Accounts Payable remains the invoice owner.
/// </summary>
public enum ProcurementReceiptSourceEvidenceKind
{
    Waybill = 1,
    VatInvoiceCopy = 2
}
