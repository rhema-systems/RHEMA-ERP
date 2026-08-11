using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyPaymentCertificateReconciliation(
    string CertificateStatus,
    string PaymentStatus,
    string FinancePostingStatus,
    string ReconciliationStatus);

public static class QuantitySurveyPaymentCertificateReconciliationRules
{
    public static QuantitySurveyPaymentCertificateReconciliation Evaluate(
        string certificateStatus,
        string paymentStatusSnapshot,
        decimal certificateAmount,
        VendorInvoiceStatus? invoiceStatus,
        decimal? invoiceAmount,
        decimal? paidAmount,
        bool invoicePosted)
    {
        if (!invoiceStatus.HasValue || !invoiceAmount.HasValue || !paidAmount.HasValue)
        {
            return new(
                certificateStatus,
                paymentStatusSnapshot,
                "NotInvoiced",
                certificateStatus == ProjectPaymentCertificateStatuses.Approved
                    ? "AwaitingApHandoff"
                    : "NotReady");
        }

        var total = decimal.Round(invoiceAmount.Value, 2);
        var paid = decimal.Round(paidAmount.Value, 2);
        var amountMatches = Math.Abs(total - decimal.Round(certificateAmount, 2)) <= 0.01m;
        var paymentRangeValid = paid >= 0m && paid <= total + 0.01m;
        var displayedCertificateStatus = invoiceStatus == VendorInvoiceStatus.Paid
            ? ProjectPaymentCertificateStatuses.Paid
            : certificateStatus == ProjectPaymentCertificateStatuses.Paid
                ? ProjectPaymentCertificateStatuses.Approved
                : certificateStatus;
        var reconciliationStatus = !amountMatches || !paymentRangeValid
            ? "Exception"
            : invoiceStatus is VendorInvoiceStatus.Voided or VendorInvoiceStatus.Rejected
                ? "FinanceException"
                : !invoicePosted
                    ? "AwaitingFinancePosting"
                    : invoiceStatus == VendorInvoiceStatus.Paid && total - paid <= 0.01m
                        ? "Balanced"
                        : paid > 0m
                            ? "PartiallySettled"
                            : "PostedUnpaid";

        return new(
            displayedCertificateStatus,
            invoiceStatus.Value.ToString(),
            invoicePosted ? "Posted" : "Pending",
            reconciliationStatus);
    }
}
