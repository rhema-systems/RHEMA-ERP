namespace ErpSystem.Core.Services.QuantitySurvey;

public static class QuantitySurveyAdvanceRecoveryRules
{
    public static decimal CalculateCertificateRecovery(
        decimal originalAdvance,
        decimal recoveryPercentage,
        decimal certificateGrossAmount,
        decimal alreadyCommitted)
    {
        if (originalAdvance <= 0m) throw new ArgumentException("The posted advance amount must be greater than zero.");
        if (recoveryPercentage <= 0m || recoveryPercentage > 100m)
            throw new ArgumentException("The recovery percentage must be greater than zero and no more than 100 percent.");
        if (certificateGrossAmount < 0m || alreadyCommitted < 0m)
            throw new ArgumentException("Certificate and committed amounts cannot be negative.");

        var remaining = Math.Max(0m, Round(originalAdvance) - Round(alreadyCommitted));
        var percentageAmount = Round(certificateGrossAmount * recoveryPercentage / 100m);
        return Math.Min(remaining, percentageAmount);
    }

    public static decimal Remaining(decimal originalAdvance, decimal approvedRecovery) =>
        Math.Max(0m, Round(originalAdvance) - Round(approvedRecovery));

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
