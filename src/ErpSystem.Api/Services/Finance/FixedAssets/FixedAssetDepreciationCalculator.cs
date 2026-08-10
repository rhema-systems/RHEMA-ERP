using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

/// <summary>
/// Provides the authoritative calculation rules shared by scheduled depreciation and disposal.
/// Keeping the algorithms here prevents the disposal workflow from drifting into a parallel
/// interpretation of straight-line, diminishing-balance, or units-of-production depreciation.
/// </summary>
internal static class FixedAssetDepreciationCalculator
{
    internal static DepreciationCalculation Calculate(
        FixedAssetBookValue bookValue,
        FixedAssetProductionUsageDto? productionUsage)
        => bookValue.DepreciationMethod switch
        {
            DepreciationMethod.StraightLine => new DepreciationCalculation(
                CalculateStraightLine(bookValue), 0m, 0m, 0m, 0m, 0m, null, null),
            DepreciationMethod.DecliningBalance or DepreciationMethod.DoubleDecliningBalance
                => CalculateDiminishingBalance(bookValue),
            DepreciationMethod.UnitsOfProduction
                => CalculateUnitsOfProduction(bookValue, productionUsage
                    ?? throw new InvalidOperationException("Verified production usage is required for units-of-production depreciation.")),
            _ => throw new InvalidOperationException($"Depreciation method '{bookValue.DepreciationMethod}' is not supported by TDC policy.")
        };

    internal static decimal ResolveEffectiveDiminishingBalanceRate(FixedAssetBookValue bookValue)
    {
        var annualRate = bookValue.DiminishingBalanceRatePercent;
        if (bookValue.DepreciationMethod == DepreciationMethod.DoubleDecliningBalance && annualRate <= 0m)
        {
            annualRate = bookValue.UsefulLifeMonths <= 0
                ? 100m
                : Math.Min(RoundRate(2400m / bookValue.UsefulLifeMonths), 100m);
        }

        return RoundRate(annualRate);
    }

    internal static decimal RoundRate(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    internal static decimal RoundUnits(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static decimal CalculateStraightLine(FixedAssetBookValue bookValue)
    {
        var remaining = RoundMoney(bookValue.NetBookValue - bookValue.ResidualValue);
        if (remaining <= 0m)
        {
            return 0m;
        }

        var remainingUsefulLife = bookValue.RemainingUsefulLifeMonths.GetValueOrDefault(bookValue.UsefulLifeMonths);
        if (remainingUsefulLife <= 0)
        {
            return remaining;
        }

        var unadjustedNetBookValue = RoundMoney(bookValue.AcquisitionCost - bookValue.AccumulatedDepreciation);
        var hasValuationAdjustment = Math.Abs(unadjustedNetBookValue - RoundMoney(bookValue.NetBookValue)) >= 0.01m;
        var depreciationBase = hasValuationAdjustment
            ? remaining
            : RoundMoney(bookValue.AcquisitionCost - bookValue.ResidualValue);
        var divisor = hasValuationAdjustment ? remainingUsefulLife : bookValue.UsefulLifeMonths;
        if (divisor <= 0)
        {
            return remaining;
        }

        return Math.Min(RoundMoney(depreciationBase / divisor), remaining);
    }

    private static DepreciationCalculation CalculateDiminishingBalance(FixedAssetBookValue bookValue)
    {
        var remaining = RoundMoney(bookValue.NetBookValue - bookValue.ResidualValue);
        if (remaining <= 0m)
        {
            return DepreciationCalculation.Empty;
        }

        // A zero rate on double-declining uses the transparent 200% / useful-life-in-years
        // formula. An explicit approved rate continues to support TDC category policy.
        var annualRate = ResolveEffectiveDiminishingBalanceRate(bookValue);
        var monthlyCharge = RoundMoney(bookValue.NetBookValue * annualRate / 1200m);
        return new DepreciationCalculation(
            Math.Min(monthlyCharge, remaining),
            RoundRate(annualRate),
            0m, 0m, 0m, 0m, null, null);
    }

    private static DepreciationCalculation CalculateUnitsOfProduction(
        FixedAssetBookValue bookValue,
        FixedAssetProductionUsageDto usage)
    {
        var units = RoundUnits(usage.UnitsConsumed);
        if (units <= 0m)
        {
            throw new InvalidOperationException("Period production usage must be greater than zero.");
        }

        var evidenceReference = usage.EvidenceReference?.Trim();
        if (string.IsNullOrWhiteSpace(evidenceReference))
        {
            throw new InvalidOperationException("A meter reading, production report, or other evidence reference is required for units-of-production depreciation.");
        }
        if (evidenceReference.Length > 200)
        {
            throw new InvalidOperationException("Production evidence reference cannot exceed 200 characters.");
        }

        var evidenceNotes = string.IsNullOrWhiteSpace(usage.EvidenceNotes) ? null : usage.EvidenceNotes.Trim();
        if (evidenceNotes?.Length > 1000)
        {
            throw new InvalidOperationException("Production evidence notes cannot exceed 1000 characters.");
        }

        var capacity = RoundUnits(bookValue.LifetimeProductionCapacity);
        var cumulativeBefore = RoundUnits(bookValue.AccumulatedProductionUnits);
        var cumulativeAfter = RoundUnits(cumulativeBefore + units);
        if (cumulativeAfter > capacity)
        {
            throw new InvalidOperationException($"Production usage would exceed approved lifetime capacity. Remaining capacity is {RoundUnits(capacity - cumulativeBefore)}.");
        }

        var remainingDepreciableAmount = RoundMoney(bookValue.NetBookValue - bookValue.ResidualValue);
        var remainingCapacity = RoundUnits(capacity - cumulativeBefore);
        if (remainingDepreciableAmount <= 0m || remainingCapacity <= 0m)
        {
            return new DepreciationCalculation(0m, 0m, capacity, units, cumulativeBefore, cumulativeAfter, evidenceReference, evidenceNotes);
        }

        // Recalculate against the remaining basis/capacity so prospective valuation or estimate
        // changes affect future charges without rewriting posted history.
        var charge = RoundMoney(remainingDepreciableAmount * units / remainingCapacity);
        return new DepreciationCalculation(
            Math.Min(charge, remainingDepreciableAmount),
            0m, capacity, units, cumulativeBefore, cumulativeAfter, evidenceReference, evidenceNotes);
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

internal sealed record DepreciationCalculation(
    decimal DepreciationAmount,
    decimal EffectiveDiminishingBalanceRatePercent,
    decimal LifetimeProductionCapacity,
    decimal PeriodProductionUnits,
    decimal CumulativeProductionUnitsBefore,
    decimal CumulativeProductionUnitsAfter,
    string? ProductionEvidenceReference,
    string? ProductionEvidenceNotes)
{
    internal static DepreciationCalculation Empty { get; } = new(0m, 0m, 0m, 0m, 0m, 0m, null, null);
}
