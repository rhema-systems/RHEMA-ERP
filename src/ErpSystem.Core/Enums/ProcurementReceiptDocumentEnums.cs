namespace ErpSystem.Core.Enums;

public enum ProcurementReceiptDocumentKind
{
    Grn = 0,
    Mrn = 1
}

public enum ProcurementReceiptDocumentStatus
{
    Draft = 0,
    PendingSignatures = 1,
    Issued = 2,
    Cancelled = 3
}

public enum ProcurementReceiptDocumentReconciliationStatus
{
    Pending = 0,
    Reconciled = 1,
    Exception = 2,
    Cancelled = 3
}
