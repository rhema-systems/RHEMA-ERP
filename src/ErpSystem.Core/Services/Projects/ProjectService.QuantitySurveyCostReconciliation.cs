using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<QuantitySurveyCostReconciliationDto> GetQuantitySurveyCostReconciliationAsync(
        Guid projectId,
        Guid estimateVersionId)
    {
        if (estimateVersionId == Guid.Empty)
        {
            throw new InvalidOperationException("Select an approved estimate version to reconcile.");
        }

        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var estimate = await GetEstimateEntityAsync(projectId, estimateVersionId);
        if (!string.Equals(estimate.Status, QuantitySurveyEstimateStatuses.Approved, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only an approved estimate version can be reconciled with project commercial values.");
        }

        var commercial = await GetCommercialSummaryAsync(projectId);
        if (commercial.HasConversionGaps)
        {
            throw new InvalidOperationException(
                $"Configure the {commercial.MissingExchangeRateCount} missing project exchange rate(s) before reconciling commercial values.");
        }

        var estimateLines = (await _unitOfWork.Repository<QuantitySurveyEstimateLine>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId
                && value.EstimateVersionId == estimate.Id
                && !value.IsDeleted))
            .OrderBy(value => value.Sequence)
            .ToList();
        if (estimateLines.Count == 0)
        {
            throw new InvalidOperationException("The approved estimate has no line snapshots to reconcile.");
        }

        var boqVersionLineIds = estimateLines.Select(value => value.ProjectBoqVersionLineId).Distinct().ToList();
        var boqVersionLines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId
                && value.ProjectId == projectId
                && boqVersionLineIds.Contains(value.Id)
                && !value.IsDeleted))
            .ToDictionary(value => value.Id);
        if (boqVersionLines.Count != boqVersionLineIds.Count)
        {
            throw new InvalidOperationException("One or more approved estimate lines have lost their BoQ lineage.");
        }

        var sourceBoqItemIds = boqVersionLines.Values
            .Where(value => value.SourceBoqItemId.HasValue)
            .Select(value => value.SourceBoqItemId!.Value)
            .Distinct()
            .ToList();
        var liveBoqItems = sourceBoqItemIds.Count == 0
            ? new List<ProjectBoqItem>()
            : (await _unitOfWork.Repository<ProjectBoqItem>().FindAsync(value =>
                    value.TenantId == _currentUserProvider.TenantId
                    && value.ProjectId == projectId
                    && sourceBoqItemIds.Contains(value.Id)
                    && !value.IsDeleted))
                .ToList();
        var liveBoqItemsById = liveBoqItems.ToDictionary(value => value.Id);
        var packages = (await GetProjectPackageEntitiesAsync(projectId)).ToList();
        var packagesById = packages.ToDictionary(value => value.Id);
        var allProjectBoqItems = (await GetProjectBoqItemEntitiesAsync(projectId)).ToList();
        var commercialContext = await BuildProjectCommercialDerivationContextAsync(projectId, packages, allProjectBoqItems);

        var targetCurrency = commercial.Currency;
        var sourceCurrencies = liveBoqItems.Select(value => value.Currency)
            .Append(estimate.CurrencyCodeSnapshot)
            .Concat(packages.Select(value => value.Currency));
        var (tenantBaseCurrency, exchangeRates) = await BuildCurrencyConversionContextAsync(targetCurrency, sourceCurrencies);

        decimal ConvertRequired(decimal amount, string? sourceCurrency, string sourceLabel)
        {
            var converted = ConvertAmountToCurrency(
                amount,
                sourceCurrency,
                targetCurrency,
                tenantBaseCurrency,
                exchangeRates,
                out var succeeded);
            if (!succeeded && amount != 0m)
            {
                throw new InvalidOperationException(
                    $"Configure an exchange rate from {sourceCurrency} to {targetCurrency} before reconciling {sourceLabel}.");
            }

            return converted;
        }

        var approvedBudgetRevision = (await _unitOfWork.Repository<ProjectBudgetRevision>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId
                && value.ProjectId == projectId
                && value.Status == "Approved"
                && value.EffectiveDate <= DateTime.UtcNow.Date
                && !value.IsDeleted))
            .OrderByDescending(value => value.EffectiveDate)
            .ThenByDescending(value => value.VersionNumber)
            .FirstOrDefault();
        if (approvedBudgetRevision == null
            && !string.Equals(project.BudgetStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Approve the project budget before reconciling the QS estimate with commercial values.");
        }
        var activeForecast = (await _unitOfWork.Repository<ProjectForecastVersion>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId
                && value.ProjectId == projectId
                && value.IsActive
                && !value.IsDeleted))
            .OrderByDescending(value => value.VersionNumber)
            .FirstOrDefault();

        var approvedBudgetTotal = decimal.Round(approvedBudgetRevision?.ApprovedBudget ?? commercial.ApprovedBudget, 2);
        var committedTotal = decimal.Round(commercial.PackageCommittedAmount, 2);
        var actualTotal = decimal.Round(commercial.PackageActualAmount, 2);
        var forecastTotal = decimal.Round(
            activeForecast == null
                ? commercial.PackageForecastAmount
                : activeForecast.EstimateAtCompletion > 0m
                    ? activeForecast.EstimateAtCompletion
                    : activeForecast.ForecastCost,
            2);
        var (certifiedTotal, certifiedConversionFailures) = await SumConvertedAmountsByEffectiveDateAsync(
            commercialContext.PaymentCertificates,
            value => value.NetCertifiedAmount,
            value => value.Currency,
            value => value.IssueDate,
            targetCurrency);
        if (certifiedConversionFailures > 0)
        {
            throw new InvalidOperationException(
                $"Configure the {certifiedConversionFailures} missing certificate exchange rate(s) before reconciling certified values.");
        }

        var linesPerPackage = estimateLines
            .Select(line => boqVersionLines[line.ProjectBoqVersionLineId])
            .Where(line => line.ProjectPackageId.HasValue)
            .GroupBy(line => line.ProjectPackageId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var mappedCertificateIds = new HashSet<Guid>();
        var resultLines = new List<QuantitySurveyCostReconciliationLineDto>(estimateLines.Count + 1);

        foreach (var estimateLine in estimateLines)
        {
            var boqVersionLine = boqVersionLines[estimateLine.ProjectBoqVersionLineId];
            ProjectBoqItem? liveBoqItem = null;
            if (boqVersionLine.SourceBoqItemId.HasValue
                && liveBoqItemsById.TryGetValue(boqVersionLine.SourceBoqItemId.Value, out var candidate)
                && candidate.VersionLineKey == boqVersionLine.LineKey)
            {
                liveBoqItem = candidate;
            }

            ProjectPackage? package = null;
            if (boqVersionLine.ProjectPackageId.HasValue)
            {
                packagesById.TryGetValue(boqVersionLine.ProjectPackageId.Value, out package);
            }

            var estimateAmount = ConvertRequired(estimateLine.LineAmount, estimate.CurrencyCodeSnapshot, "the approved estimate");
            var budgetAmount = liveBoqItem?.BudgetAmount is decimal budget
                ? ConvertRequired(budget, liveBoqItem.Currency, "the BoQ budget line")
                : 0m;
            var derived = liveBoqItem == null
                ? null
                : DeriveProjectBoqCommercialAmounts(liveBoqItem, package, commercialContext);
            var committedAmount = derived?.CommittedAmount is decimal committed
                ? ConvertRequired(committed, liveBoqItem!.Currency, "the BoQ commitment line")
                : 0m;
            var actualAmount = derived?.ActualAmount is decimal actual
                ? ConvertRequired(actual, liveBoqItem!.Currency, "the BoQ actual-cost line")
                : 0m;
            var forecastAmount = liveBoqItem?.ForecastAmount is decimal forecast
                ? ConvertRequired(forecast, liveBoqItem.Currency, "the BoQ forecast line")
                : 0m;

            var certifiedAmount = 0m;
            if (package != null
                && linesPerPackage.TryGetValue(package.Id, out var packageLineCount)
                && packageLineCount == 1)
            {
                var certificates = GetPackageAttributedPaymentCertificates(package, commercialContext)
                    .Where(value => mappedCertificateIds.Add(value.Id))
                    .ToList();
                if (certificates.Count > 0)
                {
                    var converted = await SumConvertedAmountsByEffectiveDateAsync(
                        certificates,
                        value => value.NetCertifiedAmount,
                        value => value.Currency,
                        value => value.IssueDate,
                        targetCurrency);
                    if (converted.FailedConversionCount > 0)
                    {
                        throw new InvalidOperationException("Configure the missing certificate exchange rate before reconciling this BoQ line.");
                    }
                    certifiedAmount = converted.Total;
                }
            }

            resultLines.Add(new QuantitySurveyCostReconciliationLineDto
            {
                Sequence = estimateLine.Sequence,
                EstimateLineId = estimateLine.Id,
                ProjectBoqVersionLineId = estimateLine.ProjectBoqVersionLineId,
                SourceBoqItemId = liveBoqItem?.Id,
                ProjectPackageId = boqVersionLine.ProjectPackageId,
                PackageCode = boqVersionLine.PackageCode,
                PackageName = boqVersionLine.PackageName,
                LineNumber = estimateLine.LineNumberSnapshot,
                ItemCode = estimateLine.ItemCodeSnapshot,
                Description = estimateLine.DescriptionSnapshot,
                MappingStatus = liveBoqItem == null ? "SnapshotOnly" : "Direct",
                EstimateAmount = estimateAmount,
                ApprovedBudgetAmount = budgetAmount,
                CommittedAmount = committedAmount,
                CertifiedAmount = certifiedAmount,
                ActualAmount = actualAmount,
                ForecastAmount = forecastAmount,
                BudgetVarianceAmount = decimal.Round(budgetAmount - estimateAmount, 2),
                ForecastVarianceAmount = decimal.Round(budgetAmount - forecastAmount, 2)
            });
        }

        var estimateTotal = ConvertRequired(estimate.TotalAmount, estimate.CurrencyCodeSnapshot, "the approved estimate total");
        var expectedTotals = new QuantitySurveyCostReconciliationAmounts(
            estimateTotal,
            approvedBudgetTotal,
            committedTotal,
            certifiedTotal,
            actualTotal,
            forecastTotal);
        var unallocated = QuantitySurveyCostReconciliationCalculator.BuildBalancingLine(
            resultLines.Count + 1,
            expectedTotals,
            resultLines);
        var hasUnallocatedAmounts = QuantitySurveyCostReconciliationCalculator.HasCommercialAmount(unallocated);
        if (hasUnallocatedAmounts)
        {
            resultLines.Add(unallocated);
        }
        QuantitySurveyCostReconciliationCalculator.EnsureBalanced(expectedTotals, resultLines);

        var warnings = new List<string>();
        if (approvedBudgetRevision == null)
        {
            warnings.Add(string.Equals(project.BudgetStatus, "Approved", StringComparison.OrdinalIgnoreCase)
                ? "Approved budget uses the current approved project budget because no approved budget revision exists."
                : "No approved budget revision exists; the project approved-budget value is currently zero or not approved.");
        }
        if (activeForecast == null)
        {
            warnings.Add("No active forecast version exists; forecast uses the current Project package roll-up.");
        }
        if (resultLines.Any(value => value.MappingStatus == "SnapshotOnly"))
        {
            warnings.Add("Some estimate lines no longer have a matching live BoQ item; their commercial values remain unallocated.");
        }
        if (hasUnallocatedAmounts)
        {
            warnings.Add("The unallocated row is intentional: it keeps line totals equal to the authoritative project summary without inventing line attribution.");
        }

        return new QuantitySurveyCostReconciliationDto
        {
            ProjectId = projectId,
            EstimateVersionId = estimate.Id,
            EstimateName = estimate.Name,
            EstimateType = estimate.EstimateType,
            EstimateVersionNumber = estimate.VersionNumber,
            ApprovedBudgetRevisionId = approvedBudgetRevision?.Id,
            ApprovedBudgetRevisionName = approvedBudgetRevision?.RevisionName,
            ActiveForecastVersionId = activeForecast?.Id,
            ActiveForecastVersionName = activeForecast?.VersionName,
            CurrencyCode = targetCurrency,
            GeneratedAtUtc = DateTime.UtcNow,
            EstimateAmount = estimateTotal,
            ApprovedBudgetAmount = approvedBudgetTotal,
            CommittedAmount = committedTotal,
            CertifiedAmount = certifiedTotal,
            ActualAmount = actualTotal,
            ForecastAmount = forecastTotal,
            BudgetVarianceAmount = decimal.Round(approvedBudgetTotal - estimateTotal, 2),
            ForecastVarianceAmount = decimal.Round(approvedBudgetTotal - forecastTotal, 2),
            HasUnallocatedAmounts = hasUnallocatedAmounts,
            Warnings = warnings,
            Lines = resultLines
        };
    }
}

public readonly record struct QuantitySurveyCostReconciliationAmounts(
    decimal Estimate,
    decimal ApprovedBudget,
    decimal Committed,
    decimal Certified,
    decimal Actual,
    decimal Forecast);

public static class QuantitySurveyCostReconciliationCalculator
{
    public static QuantitySurveyCostReconciliationLineDto BuildBalancingLine(
        int sequence,
        QuantitySurveyCostReconciliationAmounts expected,
        IEnumerable<QuantitySurveyCostReconciliationLineDto> mappedLines)
    {
        var mapped = Sum(mappedLines);
        var residual = Subtract(expected, mapped);
        return new QuantitySurveyCostReconciliationLineDto
        {
            Sequence = sequence,
            LineNumber = "UNALLOCATED",
            Description = "Estimate markups and package/project commercial values without an unambiguous BoQ line",
            MappingStatus = "Unallocated",
            EstimateAmount = residual.Estimate,
            ApprovedBudgetAmount = residual.ApprovedBudget,
            CommittedAmount = residual.Committed,
            CertifiedAmount = residual.Certified,
            ActualAmount = residual.Actual,
            ForecastAmount = residual.Forecast,
            BudgetVarianceAmount = decimal.Round(residual.ApprovedBudget - residual.Estimate, 2),
            ForecastVarianceAmount = decimal.Round(residual.ApprovedBudget - residual.Forecast, 2)
        };
    }

    public static bool HasCommercialAmount(QuantitySurveyCostReconciliationLineDto line)
        => new[]
        {
            line.EstimateAmount,
            line.ApprovedBudgetAmount,
            line.CommittedAmount,
            line.CertifiedAmount,
            line.ActualAmount,
            line.ForecastAmount
        }.Any(value => value != 0m);

    public static void EnsureBalanced(
        QuantitySurveyCostReconciliationAmounts expected,
        IEnumerable<QuantitySurveyCostReconciliationLineDto> lines)
    {
        var actual = Sum(lines);
        if (actual != Round(expected))
        {
            throw new InvalidOperationException("The QS reconciliation lines do not balance to the project commercial summary.");
        }
    }

    private static QuantitySurveyCostReconciliationAmounts Sum(
        IEnumerable<QuantitySurveyCostReconciliationLineDto> lines)
        => Round(new QuantitySurveyCostReconciliationAmounts(
            lines.Sum(value => value.EstimateAmount),
            lines.Sum(value => value.ApprovedBudgetAmount),
            lines.Sum(value => value.CommittedAmount),
            lines.Sum(value => value.CertifiedAmount),
            lines.Sum(value => value.ActualAmount),
            lines.Sum(value => value.ForecastAmount)));

    private static QuantitySurveyCostReconciliationAmounts Subtract(
        QuantitySurveyCostReconciliationAmounts left,
        QuantitySurveyCostReconciliationAmounts right)
        => Round(new QuantitySurveyCostReconciliationAmounts(
            left.Estimate - right.Estimate,
            left.ApprovedBudget - right.ApprovedBudget,
            left.Committed - right.Committed,
            left.Certified - right.Certified,
            left.Actual - right.Actual,
            left.Forecast - right.Forecast));

    private static QuantitySurveyCostReconciliationAmounts Round(
        QuantitySurveyCostReconciliationAmounts value)
        => new(
            decimal.Round(value.Estimate, 2),
            decimal.Round(value.ApprovedBudget, 2),
            decimal.Round(value.Committed, 2),
            decimal.Round(value.Certified, 2),
            decimal.Round(value.Actual, 2),
            decimal.Round(value.Forecast, 2));
}
