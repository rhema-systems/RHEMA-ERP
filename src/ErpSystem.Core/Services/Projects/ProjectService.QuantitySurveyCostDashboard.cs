using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<QuantitySurveyCostDashboardDto> GetQuantitySurveyCostDashboardAsync(Guid projectId)
    {
        var project = await GetProjectForOperationAsync(projectId, ProjectAccessOperation.View);
        var commercial = await GetCommercialSummaryAsync(projectId);
        var boqItems = (await GetProjectBoqItemsAsync(projectId))
            .OrderBy(value => value.PackageCode ?? value.PackageName)
            .ThenBy(value => value.SectionCode ?? value.SectionName)
            .ThenBy(value => value.CostCode ?? value.CostCodeName)
            .ThenBy(value => value.SortOrder)
            .ThenBy(value => value.LineNumber)
            .ToList();
        var activeForecast = (await GetForecastVersionEntitiesAsync(projectId))
            .Where(value => value.IsActive)
            .OrderByDescending(value => value.VersionNumber)
            .FirstOrDefault();
        var (tenantBaseCurrency, exchangeRates) = await BuildCurrencyConversionContextAsync(
            commercial.Currency,
            boqItems.Select(value => value.Currency));
        var lineConversionFailures = 0;

        decimal ConvertLineAmount(decimal amount, string? sourceCurrency)
        {
            var converted = ConvertAmountToCurrency(
                amount,
                sourceCurrency,
                commercial.Currency,
                tenantBaseCurrency,
                exchangeRates,
                out var succeeded);
            if (!succeeded && amount != 0m) lineConversionFailures++;
            return decimal.Round(converted, 2);
        }

        var amounts = QuantitySurveyCostDashboardCalculator.Calculate(
            commercial.PackageForecastAmount,
            commercial.PackageActualAmount,
            activeForecast?.ForecastCost,
            activeForecast?.EstimateAtCompletion,
            commercial.ApprovedBudget);
        var dashboardLines = boqItems.Select(value =>
        {
            var budget = ConvertLineAmount(value.BudgetAmount ?? 0m, value.Currency);
            var committed = ConvertLineAmount(value.CommittedAmount ?? 0m, value.Currency);
            var actual = ConvertLineAmount(value.ActualAmount ?? 0m, value.Currency);
            var forecast = ConvertLineAmount(value.ForecastAmount ?? 0m, value.Currency);
            return new QuantitySurveyCostDashboardLineDto
            {
                BoqItemId = value.Id,
                ProjectPackageId = value.ProjectPackageId,
                PackageCode = value.PackageCode,
                PackageName = value.PackageName,
                SectionCode = value.SectionCode,
                SectionName = value.SectionName,
                CostCode = value.CostCode,
                CostCodeName = value.CostCodeName,
                LineNumber = value.LineNumber,
                ItemCode = value.ItemCode,
                Description = value.Description,
                Quantity = value.Quantity,
                UnitOfMeasure = value.UnitOfMeasure,
                BudgetAmount = budget,
                CommittedAmount = committed,
                ActualAmount = actual,
                ForecastAmount = forecast,
                ForecastVarianceAmount = decimal.Round(budget - forecast, 2)
            };
        }).ToList();
        var warnings = commercial.Alerts
            .Select(value => value.Message)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (commercial.HasConversionGaps)
        {
            warnings.Insert(0,
                $"{commercial.MissingExchangeRateCount} commercial value(s) could not be converted to the project reporting currency.");
        }
        if (boqItems.Count == 0)
        {
            warnings.Add("No current BoQ lines are available for cost drilldown.");
        }
        if (lineConversionFailures > 0)
        {
            warnings.Add(
                $"{lineConversionFailures} BoQ cost amount(s) retain source values because a reporting-currency exchange rate is unavailable.");
        }

        return new QuantitySurveyCostDashboardDto
        {
            ProjectId = project.Id,
            ProjectCode = project.ProjectCode,
            ProjectTitle = project.Title,
            ProjectStatus = project.Status,
            ContractId = project.ContractId,
            CurrencyCode = commercial.Currency,
            GeneratedAtUtc = DateTime.UtcNow,
            ApprovedBudget = commercial.ApprovedBudget,
            CommittedValue = commercial.PackageCommittedAmount,
            CertifiedValue = commercial.NetCertifiedAmount,
            ActualCost = commercial.PackageActualAmount,
            ApprovedVariationValue = commercial.ApprovedVariationAmount,
            ForecastCost = amounts.ForecastCost,
            FinalProjectedCost = amounts.FinalProjectedCost,
            CostToComplete = amounts.CostToComplete,
            BudgetVariance = amounts.BudgetVariance,
            ForecastBasis = activeForecast == null
                ? "Current project work-component forecast"
                : activeForecast.EstimateAtCompletion > 0m
                    ? $"Active forecast EAC: {activeForecast.VersionName}"
                    : $"Active forecast: {activeForecast.VersionName}",
            HasConversionGaps = commercial.HasConversionGaps,
            MissingExchangeRateCount = commercial.MissingExchangeRateCount,
            Warnings = warnings,
            Lines = dashboardLines
        };
    }
}

public readonly record struct QuantitySurveyCostDashboardAmounts(
    decimal ForecastCost,
    decimal FinalProjectedCost,
    decimal CostToComplete,
    decimal BudgetVariance);

public static class QuantitySurveyCostDashboardCalculator
{
    public static QuantitySurveyCostDashboardAmounts Calculate(
        decimal packageForecast,
        decimal actualCost,
        decimal? activeForecastCost,
        decimal? activeEstimateAtCompletion,
        decimal approvedBudget)
    {
        if (packageForecast < 0m || actualCost < 0m || activeForecastCost < 0m ||
            activeEstimateAtCompletion < 0m || approvedBudget < 0m)
        {
            throw new InvalidOperationException("QS dashboard cost inputs cannot be negative.");
        }

        var forecastCost = activeForecastCost is > 0m ? activeForecastCost.Value : packageForecast;
        var finalProjectedCost = activeEstimateAtCompletion is > 0m
            ? activeEstimateAtCompletion.Value
            : forecastCost;
        return new QuantitySurveyCostDashboardAmounts(
            decimal.Round(forecastCost, 2),
            decimal.Round(finalProjectedCost, 2),
            decimal.Round(Math.Max(finalProjectedCost - actualCost, 0m), 2),
            decimal.Round(approvedBudget - finalProjectedCost, 2));
    }
}
