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
        FixedAssetProductionUsageDto? productionUsage,
        DepreciationTimingContext? timing = null)
    {
        var calculation = bookValue.DepreciationMethod switch
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

        if (bookValue.DepreciationMethod == DepreciationMethod.UnitsOfProduction)
        {
            DateTime? eligibleFrom = null;
            DateTime? eligibleTo = null;
            if (timing is not null)
            {
                ValidateTiming(timing);
                eligibleFrom = new[]
                {
                    timing.PeriodStartDate.Date,
                    timing.PlacedInServiceDate.Date,
                    timing.LastDepreciationDate?.Date.AddDays(1) ?? timing.PeriodStartDate.Date
                }.Max();
                eligibleTo = new[]
                {
                    timing.PeriodEndDate.Date,
                    timing.TerminationDate?.Date ?? timing.PeriodEndDate.Date
                }.Min();
            }
            return calculation with
            {
                Convention = bookValue.DepreciationConvention,
                ConventionFactor = 1m,
                ConventionBasis = "ProductionUsage",
                EligibleFromDate = eligibleFrom,
                EligibleToDate = eligibleTo
            };
        }

        if (timing is null)
        {
            return calculation;
        }

        var convention = CalculateConvention(bookValue.DepreciationConvention, timing);
        return calculation with
        {
            DepreciationAmount = Math.Min(
                RoundMoney(calculation.DepreciationAmount * convention.Factor),
                RoundMoney(bookValue.NetBookValue - bookValue.ResidualValue)),
            Convention = bookValue.DepreciationConvention,
            ConventionFactor = RoundFactor(convention.Factor),
            ConventionBasis = convention.Basis,
            EligibleFromDate = convention.EligibleFromDate,
            EligibleToDate = convention.EligibleToDate
        };
    }

    internal static DepreciationConventionResult CalculateConvention(
        DepreciationConvention convention,
        DepreciationTimingContext timing)
    {
        ValidateTiming(timing);
        var periodStart = timing.PeriodStartDate.Date;
        var periodEnd = timing.PeriodEndDate.Date;
        var serviceDate = timing.PlacedInServiceDate.Date;
        var actualEligibleFrom = new[] { periodStart, serviceDate, timing.LastDepreciationDate?.Date.AddDays(1) ?? periodStart }.Max();
        var actualEligibleTo = new[] { periodEnd, timing.TerminationDate?.Date ?? periodEnd }.Min();
        // Month-based conventions use deemed month boundaries, not the asset's actual day in
        // service. This distinction is their accounting purpose. LastDepreciationDate still
        // prevents overlap when a tenant uses periods that split a calendar month.
        var monthEligibleFrom = new[]
        {
            periodStart,
            MonthStart(serviceDate),
            timing.LastDepreciationDate?.Date.AddDays(1) ?? periodStart
        }.Max();
        var monthEligibleTo = timing.TerminationDate.HasValue
            ? convention == DepreciationConvention.FullMonth
                ? new[] { periodEnd, MonthStart(timing.TerminationDate.Value).AddDays(-1) }.Min()
                : new[] { periodEnd, MonthStart(timing.TerminationDate.Value).AddMonths(1).AddDays(-1) }.Min()
            : periodEnd;
        var eligibleFrom = convention is DepreciationConvention.FullMonth or DepreciationConvention.MidMonth
            ? monthEligibleFrom
            : actualEligibleFrom;
        var eligibleTo = convention is DepreciationConvention.FullMonth or DepreciationConvention.MidMonth
            ? monthEligibleTo
            : actualEligibleTo;
        if (eligibleFrom > eligibleTo)
        {
            return new DepreciationConventionResult(0m, ConventionBasis(convention), eligibleFrom, eligibleTo);
        }

        decimal factor = convention switch
        {
            DepreciationConvention.FullMonth => CalculateMonthWeightedFactor(
                timing, eligibleFrom, eligibleTo, commencementWeight: 1m, terminationWeight: 0m),
            DepreciationConvention.MidMonth => CalculateMonthWeightedFactor(
                timing, eligibleFrom, eligibleTo, commencementWeight: 0.5m, terminationWeight: 0.5m),
            DepreciationConvention.HalfYear => CalculateHalfYearFactor(timing, eligibleFrom, eligibleTo),
            DepreciationConvention.ActualDays => CalculateActualDaysFactor(timing, eligibleFrom, eligibleTo),
            _ => throw new InvalidOperationException($"Depreciation convention '{convention}' is not supported.")
        };

        return new DepreciationConventionResult(
            Math.Max(RoundFactor(factor), 0m),
            ConventionBasis(convention),
            eligibleFrom,
            eligibleTo);
    }

    private static decimal CalculateMonthWeightedFactor(
        DepreciationTimingContext timing,
        DateTime eligibleFrom,
        DateTime eligibleTo,
        decimal commencementWeight,
        decimal terminationWeight)
    {
        var serviceMonth = MonthStart(timing.PlacedInServiceDate);
        var terminationMonth = timing.TerminationDate.HasValue ? MonthStart(timing.TerminationDate.Value) : (DateTime?)null;
        var cursor = MonthStart(eligibleFrom);
        var factor = 0m;
        while (cursor <= eligibleTo)
        {
            var monthEnd = cursor.AddMonths(1).AddDays(-1);
            var overlapStart = new[] { cursor, timing.PeriodStartDate.Date, eligibleFrom }.Max();
            var overlapEnd = new[] { monthEnd, timing.PeriodEndDate.Date, eligibleTo }.Min();
            if (overlapStart <= overlapEnd && cursor >= serviceMonth && (!terminationMonth.HasValue || cursor <= terminationMonth.Value))
            {
                decimal weight;
                if (terminationMonth == serviceMonth && cursor == serviceMonth)
                    weight = terminationWeight;
                else if (cursor == serviceMonth)
                    weight = commencementWeight;
                else if (terminationMonth.HasValue && cursor == terminationMonth.Value)
                    weight = terminationWeight;
                else
                    weight = 1m;

                var overlapDays = (overlapEnd - overlapStart).Days + 1;
                factor += weight * overlapDays / DateTime.DaysInMonth(cursor.Year, cursor.Month);
            }
            cursor = cursor.AddMonths(1);
        }
        return factor;
    }

    private static decimal CalculateActualDaysFactor(
        DepreciationTimingContext timing,
        DateTime eligibleFrom,
        DateTime eligibleTo)
    {
        var fiscalYearDays = InclusiveDays(timing.FiscalYearStartDate, timing.FiscalYearEndDate);
        return 12m * InclusiveDays(eligibleFrom, eligibleTo) / fiscalYearDays;
    }

    private static decimal CalculateHalfYearFactor(
        DepreciationTimingContext timing,
        DateTime eligibleFrom,
        DateTime eligibleTo)
    {
        var fiscalYearDays = InclusiveDays(timing.FiscalYearStartDate, timing.FiscalYearEndDate);
        var referenceYearDays = InclusiveDays(timing.FiscalYearStartDate, timing.FiscalYearStartDate.AddYears(1).AddDays(-1));
        var annualMonthEquivalent = 12m * fiscalYearDays / referenceYearDays;
        var isCommencementYear = timing.PlacedInServiceDate.Date >= timing.FiscalYearStartDate.Date &&
                                 timing.PlacedInServiceDate.Date <= timing.FiscalYearEndDate.Date;

        if (timing.TerminationDate.HasValue)
        {
            var target = annualMonthEquivalent / 2m;
            if (timing.PriorFiscalYearConventionFactor > target + 0.00000001m)
            {
                throw new InvalidOperationException(
                    "Half-year disposal would require reversing excess current-year depreciation before disposal can proceed.");
            }
            return Math.Max(target - timing.PriorFiscalYearConventionFactor, 0m);
        }

        if (isCommencementYear)
        {
            var serviceDaysInYear = InclusiveDays(timing.PlacedInServiceDate, timing.FiscalYearEndDate);
            return (annualMonthEquivalent / 2m) * InclusiveDays(eligibleFrom, eligibleTo) / serviceDaysInYear;
        }

        return annualMonthEquivalent * InclusiveDays(eligibleFrom, eligibleTo) / fiscalYearDays;
    }

    private static void ValidateTiming(DepreciationTimingContext timing)
    {
        if (timing.PeriodStartDate.Date > timing.PeriodEndDate.Date)
            throw new InvalidOperationException("Depreciation fiscal period has an invalid date range.");
        if (timing.FiscalYearStartDate.Date > timing.FiscalYearEndDate.Date)
            throw new InvalidOperationException("Depreciation fiscal year has an invalid date range.");
        if (timing.PeriodStartDate.Date < timing.FiscalYearStartDate.Date || timing.PeriodEndDate.Date > timing.FiscalYearEndDate.Date)
            throw new InvalidOperationException("Depreciation fiscal period falls outside its fiscal year.");
        if (timing.TerminationDate.HasValue && timing.TerminationDate.Value.Date < timing.PlacedInServiceDate.Date)
            throw new InvalidOperationException("Asset termination date cannot precede its placed-in-service date.");
    }

    private static string ConventionBasis(DepreciationConvention convention) => convention switch
    {
        DepreciationConvention.FullMonth => "FullMonthNoDisposalMonth",
        DepreciationConvention.MidMonth => "MidMonthHalfAtEdges",
        DepreciationConvention.HalfYear => "FiscalHalfYear",
        DepreciationConvention.ActualDays => "ActualDaysInclusive",
        _ => throw new InvalidOperationException($"Depreciation convention '{convention}' is not supported.")
    };

    private static DateTime MonthStart(DateTime value) => new(value.Year, value.Month, 1);
    private static decimal InclusiveDays(DateTime start, DateTime end) => (end.Date - start.Date).Days + 1;
    internal static decimal RoundFactor(decimal value) => Math.Round(value, 8, MidpointRounding.AwayFromZero);

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
    string? ProductionEvidenceNotes,
    DepreciationConvention Convention = DepreciationConvention.FullMonth,
    decimal ConventionFactor = 1m,
    string ConventionBasis = "Unprorated",
    DateTime? EligibleFromDate = null,
    DateTime? EligibleToDate = null)
{
    internal static DepreciationCalculation Empty { get; } = new(0m, 0m, 0m, 0m, 0m, 0m, null, null);
}

internal sealed record DepreciationTimingContext(
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    DateTime FiscalYearStartDate,
    DateTime FiscalYearEndDate,
    DateTime PlacedInServiceDate,
    DateTime? LastDepreciationDate = null,
    DateTime? TerminationDate = null,
    decimal PriorFiscalYearConventionFactor = 0m);

internal sealed record DepreciationConventionResult(
    decimal Factor,
    string Basis,
    DateTime EligibleFromDate,
    DateTime EligibleToDate);
