using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveySubcontractChargeTotals(decimal BackCharge, decimal ContraCharge, decimal Total);

public static class QuantitySurveySubcontractChargeRules
{
    public static IReadOnlyList<string> ValidateDraft(string chargeType, decimal amount, DateTime noticeDate,
        DateTime responseDueDate, decimal subcontractValue, bool controlBackCharges, bool controlContraCharges)
    {
        var issues = new List<string>();
        if (chargeType is not (QuantitySurveySubcontractChargeTypes.BackCharge or QuantitySurveySubcontractChargeTypes.ContraCharge))
            issues.Add("Select Back charge or Contra charge.");
        if (amount <= 0 || amount > subcontractValue)
            issues.Add("The proposed charge must be positive and cannot exceed the subcontract value.");
        if (noticeDate == default || responseDueDate == default || responseDueDate.Date < noticeDate.Date)
            issues.Add("The response due date cannot precede the notice date.");
        if (responseDueDate.Date > noticeDate.Date.AddDays(90))
            issues.Add("The response period cannot exceed 90 days.");
        if (chargeType == QuantitySurveySubcontractChargeTypes.BackCharge && !controlBackCharges)
            issues.Add("Back charges are disabled by the effective QS policy.");
        if (chargeType == QuantitySurveySubcontractChargeTypes.ContraCharge && !controlContraCharges)
            issues.Add("Contra charges are disabled by the effective QS policy.");
        return issues;
    }

    public static decimal ValidateApprovedAmount(decimal proposedAmount, decimal approvedAmount)
    {
        proposedAmount = Round(proposedAmount);
        approvedAmount = Round(approvedAmount);
        if (approvedAmount <= 0 || approvedAmount > proposedAmount)
            throw new ArgumentOutOfRangeException(nameof(approvedAmount), "The approved amount must be positive and cannot exceed the notified amount.");
        return approvedAmount;
    }

    public static QuantitySurveySubcontractChargeTotals SumApproved(
        IEnumerable<(string ChargeType, decimal ApprovedAmount)> charges)
    {
        decimal back = 0m, contra = 0m;
        foreach (var charge in charges)
        {
            var amount = Round(charge.ApprovedAmount);
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(charges));
            if (charge.ChargeType == QuantitySurveySubcontractChargeTypes.BackCharge) back += amount;
            else if (charge.ChargeType == QuantitySurveySubcontractChargeTypes.ContraCharge) contra += amount;
            else throw new ArgumentOutOfRangeException(nameof(charges));
        }
        back = Round(back); contra = Round(contra);
        return new(back, contra, Round(back + contra));
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
