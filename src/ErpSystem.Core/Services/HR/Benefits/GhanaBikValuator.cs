using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Benefits;

/// <summary>
/// Resolved monetary outcome of valuing a benefit for a specific employee: the gross assessed value
/// and the portion of it that is subject to income tax.
/// </summary>
public readonly record struct BenefitValuation(decimal AssessedValue, decimal TaxableValue);

/// <summary>
/// Centralizes how a benefit's monetary / Benefit-in-Kind value is computed, including the Ghana
/// (GRA, Income Tax Act 896) statutory BIK percentages encoded as configurable rate/cap fields on the
/// policy. Keeping the formulas here means the (separate) payroll module and the enrollment snapshot
/// share one source of truth and the rules are unit-test friendly.
/// </summary>
public static class GhanaBikValuator
{
    /// <summary>
    /// Computes the assessed (gross) and taxable values of <paramref name="policy"/> for an employee
    /// whose monthly basic pay and total monthly cash emoluments are supplied.
    /// </summary>
    /// <param name="policy">The benefit definition (valuation method, rate, cap, tax treatment).</param>
    /// <param name="monthlyBasicPay">Employee monthly basic pay (for %-of-basic methods).</param>
    /// <param name="monthlyCashEmoluments">Employee total monthly cash emoluments (for GRA BIK %).</param>
    /// <param name="gradeValue">Resolved per-grade row when the policy is grade-based; otherwise null.</param>
    public static BenefitValuation Value(
        BenefitPolicy policy,
        decimal monthlyBasicPay,
        decimal monthlyCashEmoluments,
        BenefitGradeValue? gradeValue = null)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var assessed = ResolveAssessedValue(policy, monthlyBasicPay, monthlyCashEmoluments, gradeValue);
        var taxable = ResolveTaxableValue(policy, assessed);

        return new BenefitValuation(Round(assessed), Round(taxable));
    }

    private static decimal ResolveAssessedValue(
        BenefitPolicy policy,
        decimal monthlyBasicPay,
        decimal monthlyCashEmoluments,
        BenefitGradeValue? gradeValue)
    {
        decimal raw = policy.ValuationMethod switch
        {
            BenefitValuationMethod.PercentageOfBasicSalary
                => Percent(policy.ValuationRate) * monthlyBasicPay,

            BenefitValuationMethod.PercentageOfCashEmoluments or BenefitValuationMethod.StatutoryFormula
                => Percent(policy.ValuationRate) * monthlyCashEmoluments,

            BenefitValuationMethod.GradeBased
                => ResolveGradeValue(gradeValue, monthlyBasicPay),

            // ActualCost / MarketValue / FlatRate all use the configured flat monetary value.
            _ => policy.FlatValue ?? 0m
        };

        // Apply the optional periodic cap (e.g. GRA vehicle BIK capped at GHS 600/month).
        if (policy.ValuationCap.HasValue && raw > policy.ValuationCap.Value)
        {
            raw = policy.ValuationCap.Value;
        }

        return raw < 0m ? 0m : raw;
    }

    private static decimal ResolveGradeValue(BenefitGradeValue? gradeValue, decimal monthlyBasicPay)
    {
        if (gradeValue is null)
        {
            return 0m;
        }

        if (gradeValue.Amount.HasValue)
        {
            return gradeValue.Amount.Value;
        }

        return Percent(gradeValue.Rate) * monthlyBasicPay;
    }

    private static decimal ResolveTaxableValue(BenefitPolicy policy, decimal assessed)
    {
        if (!policy.IsTaxable || policy.TaxTreatment == BenefitTaxTreatment.TaxExempt)
        {
            return 0m;
        }

        // Exempt threshold is applied before any partial-taxable percentage.
        var taxableBase = assessed;
        if (policy.TaxExemptThreshold.HasValue)
        {
            taxableBase = Math.Max(0m, assessed - policy.TaxExemptThreshold.Value);
        }

        return policy.TaxTreatment switch
        {
            BenefitTaxTreatment.PartiallyTaxable => Percent(policy.TaxablePercentage) * taxableBase,
            _ => taxableBase
        };
    }

    private static decimal Percent(decimal? value) => (value ?? 0m) / 100m;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
