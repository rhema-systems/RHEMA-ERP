namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyPaymentCertificateAmounts(
    decimal CertifiedToDate,
    decimal PreviouslyCertified,
    decimal GrossCurrent,
    decimal RetentionHeld,
    decimal RetentionReleased,
    decimal AdvanceRecovery,
    decimal MaterialOnSite,
    decimal MaterialOffSite,
    decimal MaterialDeduction,
    decimal OtherDeductions,
    decimal Tax,
    decimal NetCurrent);

public static class QuantitySurveyPaymentCertificateRules
{
    public static QuantitySurveyPaymentCertificateAmounts Calculate(
        decimal certifiedToDate,
        decimal previouslyCertified,
        decimal retentionHeld,
        decimal retentionReleased,
        decimal advanceRecovery,
        decimal materialOnSite,
        decimal materialOffSite,
        decimal materialDeduction,
        decimal otherDeductions,
        decimal tax,
        bool addTaxToPayable = true)
    {
        var values = new[] { certifiedToDate, previouslyCertified, retentionHeld, retentionReleased,
            advanceRecovery, materialOnSite, materialOffSite, materialDeduction, otherDeductions, tax };
        if (values.Any(value => value < 0m)) throw new ArgumentOutOfRangeException(nameof(certifiedToDate), "Certificate amounts cannot be negative.");
        if (previouslyCertified > certifiedToDate) throw new ArgumentException("Previous certification cannot exceed certification to date.");
        var gross = Round(certifiedToDate - previouslyCertified + materialOnSite + materialOffSite);
        var net = Round(gross + retentionReleased + (addTaxToPayable ? tax : 0m) - retentionHeld - advanceRecovery - materialDeduction - otherDeductions);
        if (net < 0m) throw new ArgumentException("Certificate deductions cannot exceed the current gross amount plus releases and tax.");
        return new(Round(certifiedToDate), Round(previouslyCertified), gross, Round(retentionHeld),
            Round(retentionReleased), Round(advanceRecovery), Round(materialOnSite), Round(materialOffSite),
            Round(materialDeduction), Round(otherDeductions), Round(tax), net);
    }

    public static void RequireIndependentApprover(Guid preparedBy, Guid submittedBy, Guid approver)
    {
        if (approver == Guid.Empty || approver == preparedBy || approver == submittedBy)
            throw new InvalidOperationException("The payment-certificate approver must be independent of its preparer and submitter.");
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
