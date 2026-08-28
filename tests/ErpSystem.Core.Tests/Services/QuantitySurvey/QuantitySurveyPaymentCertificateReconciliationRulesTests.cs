using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyPaymentCertificateReconciliationRulesTests
{
    [Fact]
    public void Approved_certificate_without_invoice_is_awaiting_handoff()
    {
        var result = QuantitySurveyPaymentCertificateReconciliationRules.Evaluate(
            ProjectPaymentCertificateStatuses.Approved, "NotInvoiced", 100m,
            null, null, null, false);

        result.ReconciliationStatus.Should().Be("AwaitingApHandoff");
        result.FinancePostingStatus.Should().Be("NotInvoiced");
    }

    [Theory]
    [InlineData(VendorInvoiceStatus.Draft, 0, false, "AwaitingFinancePosting")]
    [InlineData(VendorInvoiceStatus.Approved, 0, true, "PostedUnpaid")]
    [InlineData(VendorInvoiceStatus.PartiallyPaid, 40, true, "PartiallySettled")]
    [InlineData(VendorInvoiceStatus.Paid, 100, true, "Balanced")]
    public void Finance_owned_status_is_reconciled_without_mutating_finance(
        VendorInvoiceStatus status, decimal paid, bool posted, string expected)
    {
        var result = QuantitySurveyPaymentCertificateReconciliationRules.Evaluate(
            ProjectPaymentCertificateStatuses.Approved, "Draft", 100m,
            status, 100m, paid, posted);

        result.ReconciliationStatus.Should().Be(expected);
        result.CertificateStatus.Should().Be(status == VendorInvoiceStatus.Paid
            ? ProjectPaymentCertificateStatuses.Paid
            : ProjectPaymentCertificateStatuses.Approved);
    }

    [Fact]
    public void Amount_drift_is_an_exception_and_payment_reversal_removes_paid_display()
    {
        var mismatch = QuantitySurveyPaymentCertificateReconciliationRules.Evaluate(
            ProjectPaymentCertificateStatuses.Approved, "Draft", 100m,
            VendorInvoiceStatus.Approved, 99m, 0m, true);
        var reversed = QuantitySurveyPaymentCertificateReconciliationRules.Evaluate(
            ProjectPaymentCertificateStatuses.Paid, "Paid", 100m,
            VendorInvoiceStatus.Approved, 100m, 0m, true);

        mismatch.ReconciliationStatus.Should().Be("Exception");
        reversed.CertificateStatus.Should().Be(ProjectPaymentCertificateStatuses.Approved);
        reversed.PaymentStatus.Should().Be(nameof(VendorInvoiceStatus.Approved));
    }
}
