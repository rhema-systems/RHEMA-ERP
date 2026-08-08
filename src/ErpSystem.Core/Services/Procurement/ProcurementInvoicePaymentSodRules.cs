namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementInvoicePaymentSodRules
{
    public const string ControlCode = "SOD-INVOICE-PROCESSOR-PAYMENT";
    public const string RequirementCode = "AP-004";
    public const string RuleVersion = "TDC-0506";
    public const string EventType = "ProcurementInvoicePaymentSod";
    public const string PaymentSourceType = "VendorPayment";
    public const string BatchSourceType = "PaymentBatch";
    public const string ApproveAction = "ApprovePayment";
    public const string ConflictCode = "AP_PAYMENT_SOD_CONFLICT";
    public const string LineageCode = "AP_PAYMENT_SOD_LINEAGE_INCOMPLETE";
    public const string EvidenceCode = "AP_PAYMENT_SOD_EVIDENCE_INVALID";

    public static IReadOnlyList<string> DecisionKeys { get; } =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList();

    public static bool HasConflict(Guid? invoiceProcessorUserId, Guid paymentApproverUserId) =>
        invoiceProcessorUserId.HasValue &&
        invoiceProcessorUserId.Value != Guid.Empty &&
        invoiceProcessorUserId.Value == paymentApproverUserId;
}
