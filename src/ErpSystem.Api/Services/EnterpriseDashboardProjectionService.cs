using ErpSystem.Core.DTOs.Dashboard;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// Read-only, tenant-scoped projections for the enterprise dashboard. Dashboard requests must
/// aggregate in SQL and must never hydrate a bounded page of transactions to derive totals.
/// </summary>
public sealed class EnterpriseDashboardProjectionService
{
    private static readonly string[] ClosedPurchaseOrderStatuses = ["Completed", "Cancelled", "Closed"];
    private static readonly string[] ClosedTenderStatuses = ["Closed", "Cancelled", "Awarded", "Completed"];
    private static readonly string[] ClosedQuoteStatuses = ["Accepted", "Rejected", "Expired"];
    private static readonly string[] ClosedLeadStatuses = ["Converted", "Unqualified"];
    private static readonly string[] AtRiskBands = ["High", "Critical"];

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;

    public EnterpriseDashboardProjectionService(
        ApplicationDbContext context,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl)
    {
        _context = context;
        _currentUser = currentUser;
        _accessControl = accessControl;
    }

    public async Task<EnterpriseOperationalQueueDto> GetProcurementQueuesAsync(
        DateTime rangeStart,
        DateTime rangeEndExclusive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var closingSoonEnd = rangeEndExclusive.AddDays(14);

        var purchaseRequisitionCount = await _context.PurchaseRequisitions.AsNoTracking()
            .CountAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.RequisitionDate >= rangeStart && value.RequisitionDate < rangeEndExclusive &&
                value.Status == "Submitted", cancellationToken);

        var purchaseOrderCount = await _context.PurchaseOrders.AsNoTracking()
            .CountAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.OrderDate >= rangeStart && value.OrderDate < rangeEndExclusive &&
                !ClosedPurchaseOrderStatuses.Contains(value.Status), cancellationToken);

        var openTenders = _context.Tenders.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                (value.PublishDate ?? value.CreatedAt) >= rangeStart &&
                (value.PublishDate ?? value.CreatedAt) < rangeEndExclusive &&
                !ClosedTenderStatuses.Contains(value.Status));

        var tenderStatuses = await openTenders
            .GroupBy(value => value.Status)
            .Select(group => new EnterpriseDashboardCountPointDto
            {
                Label = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(value => value.Count)
            .ThenBy(value => value.Label)
            .ToListAsync(cancellationToken);

        var tenderClosingSoonCount = await openTenders.CountAsync(value =>
            value.SubmissionDeadline.HasValue &&
            value.SubmissionDeadline.Value >= rangeEndExclusive.AddDays(-1) &&
            value.SubmissionDeadline.Value < closingSoonEnd,
            cancellationToken);

        return new EnterpriseOperationalQueueDto
        {
            PendingPurchaseRequisitionCount = purchaseRequisitionCount,
            OpenPurchaseOrderCount = purchaseOrderCount,
            OpenTenderCount = tenderStatuses.Sum(value => value.Count),
            TendersClosingWithin14DaysCount = tenderClosingSoonCount,
            OpenTendersByStatus = tenderStatuses
        };
    }

    public async Task<EnterpriseOperationalQueueDto> GetInventoryQueuesAsync(
        DateTime rangeStart,
        DateTime rangeEndExclusive,
        bool includeAllTenantRequisitions = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var inventoryQueue = _context.InventoryRequisitions.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.RequestDate >= rangeStart && value.RequestDate < rangeEndExclusive);
        if (includeAllTenantRequisitions)
        {
            return new EnterpriseOperationalQueueDto
            {
                PendingInventoryApprovalCount = await inventoryQueue.CountAsync(
                    value => value.Status == RequisitionStatus.Submitted, cancellationToken),
                PendingInventoryIssueCount = await inventoryQueue.CountAsync(
                    value => value.Status == RequisitionStatus.Approved ||
                        value.Status == RequisitionStatus.InProgress ||
                        value.Status == RequisitionStatus.PartiallyIssued, cancellationToken)
            };
        }

        var scopeDecisions = new Dictionary<(Guid WarehouseId, Guid? LocationId), bool>();
        var inventoryApprovalCount = await CountReadableInventoryRequisitionsAsync(
            inventoryQueue.Where(value => value.Status == RequisitionStatus.Submitted),
            scopeDecisions,
            cancellationToken);
        var inventoryIssueCount = await CountReadableInventoryRequisitionsAsync(
            inventoryQueue.Where(value => value.Status == RequisitionStatus.Approved ||
                value.Status == RequisitionStatus.InProgress ||
                value.Status == RequisitionStatus.PartiallyIssued),
            scopeDecisions,
            cancellationToken);

        return new EnterpriseOperationalQueueDto
        {
            PendingInventoryApprovalCount = inventoryApprovalCount,
            PendingInventoryIssueCount = inventoryIssueCount
        };
    }

    public async Task<EnterpriseCrmDashboardDto> GetCrmAsync(
        DateTime rangeStart,
        DateTime rangeEndExclusive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var pipelineAsOf = DateTime.UtcNow;
        const int stalledThresholdDays = 30;
        var followUpWindow = rangeEndExclusive.AddDays(14);

        var scopedLeads = _context.Leads.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.CreatedAt >= rangeStart && value.CreatedAt < rangeEndExclusive);

        var stageDefinitions = await _context.OpportunityStageDefinitions.AsNoTracking()
            .Where(stage => stage.TenantId == tenantId && !stage.IsDeleted)
            .OrderBy(stage => stage.SortOrder)
            .ThenBy(stage => stage.Name)
            .ToListAsync(cancellationToken);

        var currentOpportunities = _context.Opportunities.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted);
        var governedOpenOpportunities = currentOpportunities
            .Where(value => value.StageDefinitionId.HasValue
                && value.StageDefinition != null
                && !value.StageDefinition.IsClosed);
        var openOpportunities = currentOpportunities
            .Where(value =>
                (value.StageDefinitionId.HasValue && value.StageDefinition != null && !value.StageDefinition.IsClosed) ||
                (!value.StageDefinitionId.HasValue &&
                 value.Stage != "Closed Won" &&
                 value.Stage != "Closed Lost"));
        var visiblePipelineOpportunities = currentOpportunities
            .Where(value => value.StageDefinitionId.HasValue
                && value.StageDefinition != null
                && !value.StageDefinition.IsLost);

        var totalLeads = await scopedLeads.CountAsync(cancellationToken);
        var qualifiedLeadCount = await scopedLeads.CountAsync(value => value.LeadStatus == "Qualified", cancellationToken);
        var leadFollowUpCount = await scopedLeads.CountAsync(value =>
            !ClosedLeadStatuses.Contains(value.LeadStatus) &&
            value.NextFollowUpDate.HasValue && value.NextFollowUpDate.Value <= followUpWindow,
            cancellationToken);
        var openOpportunityCount = await openOpportunities.CountAsync(cancellationToken);
        var governedOpenOpportunityCount = await governedOpenOpportunities.CountAsync(cancellationToken);

        var scopedOpportunityIds = openOpportunities.Select(value => value.Id);
        var activeQuoteCount = await _context.Quotes.AsNoTracking().CountAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted &&
            scopedOpportunityIds.Contains(value.OpportunityId) &&
            !ClosedQuoteStatuses.Contains(value.QuoteStatus), cancellationToken);

        var activeAccountCount = await _context.BusinessPartners.AsNoTracking().CountAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted && value.IsActive, cancellationToken);
        var atRiskAccountCount = await _context.BusinessPartners.AsNoTracking().CountAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted && value.IsActive &&
            value.RiskLevel != null && AtRiskBands.Contains(value.RiskLevel), cancellationToken);

        var pipelineAmounts = await visiblePipelineOpportunities
            .GroupBy(value => new { StageId = value.StageDefinitionId!.Value, value.Currency })
            .Select(group => new
            {
                group.Key.StageId,
                group.Key.Currency,
                Amount = group.Sum(value => value.Amount),
                WeightedAmount = group.Sum(value => value.Amount * value.Probability / 100m),
                Count = group.Count(),
                NonZeroCount = group.Count(value => value.Amount != 0m)
            })
            .ToListAsync(cancellationToken);
        var quoteCountsByStage = await _context.Quotes.AsNoTracking()
            .Where(quote => quote.TenantId == tenantId && !quote.IsDeleted &&
                quote.Opportunity.StageDefinitionId.HasValue)
            .GroupBy(quote => quote.Opportunity.StageDefinitionId!.Value)
            .Select(group => new { StageId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(value => value.StageId, value => value.Count, cancellationToken);

        // SQL Server rejects AVG/COUNT over a correlated scalar subquery. Resolve
        // the latest stage entry in a grouped derived table first, then aggregate
        // the joined column. This retains database-side stage-health aggregation
        // without materializing every opportunity in the application.
        var latestStageEntries = _context.OpportunityStageHistories.AsNoTracking()
            .Where(history => history.TenantId == tenantId && !history.IsDeleted)
            .GroupBy(history => new { history.OpportunityId, history.StageDefinitionId })
            .Select(group => new
            {
                group.Key.OpportunityId,
                group.Key.StageDefinitionId,
                EnteredAt = group.Max(history => (DateTime?)history.EnteredAt)
            });
        var stageHealthQuery =
            from opportunity in governedOpenOpportunities
            join latestEntry in latestStageEntries
                on new
                {
                    OpportunityId = opportunity.Id,
                    StageDefinitionId = opportunity.StageDefinitionId!.Value
                }
                equals new
                {
                    latestEntry.OpportunityId,
                    latestEntry.StageDefinitionId
                }
                into latestEntries
            from latestEntry in latestEntries.DefaultIfEmpty()
            select new
            {
                StageId = opportunity.StageDefinitionId!.Value,
                opportunity.ExpectedCloseDate,
                EnteredAt = latestEntry != null && latestEntry.EnteredAt.HasValue
                    ? latestEntry.EnteredAt.Value
                    : opportunity.CreatedAt
            };
        var stageHealthByStage = new Dictionary<Guid, (
            double AverageAgeDays,
            int StalledOpportunityCount,
            int OverdueOpportunityCount)>();

        // EF/SQL Server cannot translate an aggregate DateDiff over the left-joined
        // grouped history projection. Materialize only these three scalar health
        // fields for open opportunities, then aggregate the compact rows once.
        // Pipeline amounts/counts and funnel metrics remain SQL-aggregated above/below.
        var stageHealthRows = await stageHealthQuery.ToListAsync(cancellationToken);
        foreach (var group in stageHealthRows.GroupBy(value => value.StageId))
        {
            stageHealthByStage[group.Key] = (
                group.Average(value => Math.Max(0, (pipelineAsOf - value.EnteredAt).TotalDays)),
                group.Count(value => (pipelineAsOf - value.EnteredAt).TotalDays > stalledThresholdDays),
                group.Count(value => value.ExpectedCloseDate < pipelineAsOf));
        }

        var pipelineByStage = stageDefinitions
            .Where(stage => stage.IsActive && !stage.IsLost)
            .Select(stage =>
        {
            var amounts = pipelineAmounts.Where(value => value.StageId == stage.Id).ToList();
            var hasHealth = stageHealthByStage.TryGetValue(stage.Id, out var health);
            return new EnterpriseDashboardStageCountDto
            {
                StageId = stage.Id,
                Stage = stage.Name,
                StageOrder = stage.SortOrder,
                IsClosed = stage.IsClosed,
                IsWon = stage.IsWon,
                IsLost = stage.IsLost,
                OpportunityCount = amounts.Sum(value => value.Count),
                QuoteCount = quoteCountsByStage.GetValueOrDefault(stage.Id),
                PercentageOfActivePipeline = stage.IsClosed || governedOpenOpportunityCount == 0
                    ? 0m
                    : decimal.Round(amounts.Sum(value => value.Count) * 100m / governedOpenOpportunityCount, 2),
                AverageAgeDays = !hasHealth
                    ? 0m
                    : decimal.Round(Math.Max(0m, (decimal)health.AverageAgeDays), 1),
                StalledOpportunityCount = hasHealth ? health.StalledOpportunityCount : 0,
                OverdueOpportunityCount = hasHealth ? health.OverdueOpportunityCount : 0,
                AmountsByCurrency = amounts
                    .Select(value => new { Code = NormalizeDashboardCurrencyCode(value.Currency), value.Amount })
                    .Where(value => value.Code is not null)
                    .GroupBy(value => value.Code!)
                    .OrderBy(group => group.Key)
                    .Select(group => new EnterpriseDashboardCurrencyAmountDto
                    {
                        Currency = group.Key,
                        Amount = decimal.Round(group.Sum(value => value.Amount), 2)
                    })
                    .ToList(),
                WeightedAmountsByCurrency = amounts
                    .Select(value => new { Code = NormalizeDashboardCurrencyCode(value.Currency), value.WeightedAmount })
                    .Where(value => value.Code is not null)
                    .GroupBy(value => value.Code!)
                    .OrderBy(group => group.Key)
                    .Select(group => new EnterpriseDashboardCurrencyAmountDto
                    {
                        Currency = group.Key,
                        Amount = decimal.Round(group.Sum(value => value.WeightedAmount), 2)
                    })
                    .ToList(),
                OpportunitiesWithoutCurrencyCount = amounts
                    .Where(value => NormalizeDashboardCurrencyCode(value.Currency) is null)
                    .Sum(value => value.NonZeroCount)
            };
        }).ToList();

        var accountRisk = await _context.BusinessPartners.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.IsActive)
            .GroupBy(value => value.RiskLevel == null || value.RiskLevel == string.Empty ? "Unrated" : value.RiskLevel)
            .Select(group => new EnterpriseDashboardCountPointDto
            {
                Label = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(value => value.Count)
            .ThenBy(value => value.Label)
            .ToListAsync(cancellationToken);

        var transitionRows = _context.OpportunityStageHistories.AsNoTracking()
            .Where(history => history.TenantId == tenantId && !history.IsDeleted
                && !history.IsLegacySnapshot
                && history.EnteredAt >= rangeStart && history.EnteredAt < rangeEndExclusive);

        // Re-entry and reversals are valid history, but one opportunity must only
        // contribute once to a stage for a funnel range. Use the first entry in
        // the range so later currency/value edits cannot double count it.
        var distinctTransitions = transitionRows.Where(history => history.Id ==
            _context.OpportunityStageHistories
                .Where(candidate => candidate.TenantId == tenantId && !candidate.IsDeleted
                    && !candidate.IsLegacySnapshot
                    && candidate.EnteredAt >= rangeStart && candidate.EnteredAt < rangeEndExclusive
                    && candidate.StageDefinitionId == history.StageDefinitionId
                    && candidate.OpportunityId == history.OpportunityId)
                .OrderBy(candidate => candidate.EnteredAt)
                .ThenBy(candidate => candidate.Id)
                .Select(candidate => candidate.Id)
                .First());

        var transitionCounts = await distinctTransitions
            .GroupBy(history => history.StageDefinitionId)
            .Select(group => new { StageId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(value => value.StageId, value => value.Count, cancellationToken);
        var transitionAmounts = await distinctTransitions
            .GroupBy(history => new { history.StageDefinitionId, history.CurrencySnapshot })
            .Select(group => new
            {
                StageId = group.Key.StageDefinitionId,
                Currency = group.Key.CurrencySnapshot,
                Amount = group.Sum(history => history.AmountSnapshot),
                NonZeroCount = group.Count(history => history.AmountSnapshot != 0m)
            })
            .ToListAsync(cancellationToken);

        var funnelStages = stageDefinitions
            .Where(stage => stage.IsActive && !stage.IsLost && (!stage.IsClosed || stage.IsWon))
            .ToList();
        var firstStageCount = funnelStages.Count == 0
            ? 0
            : transitionCounts.GetValueOrDefault(funnelStages[0].Id);
        var previousCount = firstStageCount;
        var valueFunnel = new List<EnterpriseDashboardFunnelPointDto>();
        foreach (var stage in funnelStages)
        {
            var amounts = transitionAmounts.Where(value => value.StageId == stage.Id).ToList();
            var count = transitionCounts.GetValueOrDefault(stage.Id);
            valueFunnel.Add(new EnterpriseDashboardFunnelPointDto
            {
                StageId = stage.Id,
                Stage = stage.Name,
                StageOrder = stage.SortOrder,
                Count = count,
                ConversionRate = previousCount == 0 ? null : decimal.Round(count * 100m / previousCount, 2),
                OverallConversionRate = firstStageCount == 0 ? null : decimal.Round(count * 100m / firstStageCount, 2),
                AmountsByCurrency = amounts
                    .Select(value => new { Code = NormalizeDashboardCurrencyCode(value.Currency), value.Amount })
                    .Where(value => value.Code is not null)
                    .GroupBy(value => value.Code!)
                    .OrderBy(group => group.Key)
                    .Select(group => new EnterpriseDashboardCurrencyAmountDto
                    {
                        Currency = group.Key,
                        Amount = decimal.Round(group.Sum(value => value.Amount), 2)
                    })
                    .ToList(),
                OpportunitiesWithoutCurrencyCount = amounts
                    .Where(value => NormalizeDashboardCurrencyCode(value.Currency) is null)
                    .Sum(value => value.NonZeroCount)
            });
            previousCount = count;
        }

        var legacyHistorySnapshotCount = await _context.OpportunityStageHistories.AsNoTracking()
            .CountAsync(history => history.TenantId == tenantId && !history.IsDeleted && history.IsLegacySnapshot,
                cancellationToken);
        var historyCoverageStart = await _context.OpportunityStageHistories.AsNoTracking()
            .Where(history => history.TenantId == tenantId && !history.IsDeleted && !history.IsLegacySnapshot)
            .MinAsync(history => (DateTime?)history.EnteredAt, cancellationToken);
        var lostStageIds = stageDefinitions.Where(stage => stage.IsLost).Select(stage => stage.Id).ToHashSet();
        var lostOpportunityCount = await distinctTransitions
            .Where(value => lostStageIds.Contains(value.StageDefinitionId))
            .Select(value => value.OpportunityId)
            .Distinct()
            .CountAsync(cancellationToken);

        var missingStageCount = await currentOpportunities.CountAsync(
            opportunity => !opportunity.StageDefinitionId.HasValue, cancellationToken);
        var missingCurrencyCount = await currentOpportunities.CountAsync(
            opportunity => opportunity.Amount != 0m
                && (opportunity.Currency == null || opportunity.Currency.Length != 3), cancellationToken);
        var missingHistoryCount = await currentOpportunities.CountAsync(opportunity =>
            !_context.OpportunityStageHistories.Any(history =>
                history.TenantId == tenantId && history.OpportunityId == opportunity.Id && !history.IsDeleted),
            cancellationToken);
        var dataQualityIssues = new List<string>();
        if (stageDefinitions.Count == 0)
            dataQualityIssues.Add("No opportunity stages are configured for this tenant.");
        if (missingStageCount > 0)
            dataQualityIssues.Add($"{missingStageCount} opportunities are not linked to a configured stage.");
        if (missingCurrencyCount > 0)
            dataQualityIssues.Add($"{missingCurrencyCount} valued opportunities have a missing or invalid currency.");
        if (missingHistoryCount > 0)
            dataQualityIssues.Add($"{missingHistoryCount} opportunities have no stage-history evidence.");
        if (legacyHistorySnapshotCount > 0)
            dataQualityIssues.Add($"{legacyHistorySnapshotCount} legacy opportunities have snapshot-only stage history and are excluded from historical conversion rates.");

        return new EnterpriseCrmDashboardDto
        {
            TotalLeadCount = totalLeads,
            QualifiedLeadCount = qualifiedLeadCount,
            LeadsNeedingFollowUpCount = leadFollowUpCount,
            OpenOpportunityCount = openOpportunityCount,
            ActiveQuoteCount = activeQuoteCount,
            ActiveAccountCount = activeAccountCount,
            AtRiskAccountCount = atRiskAccountCount,
            PipelineAsOf = pipelineAsOf,
            FunnelRangeStart = rangeStart,
            FunnelRangeEnd = rangeEndExclusive.AddTicks(-1),
            HistoryCoverageStart = historyCoverageStart,
            LegacyHistorySnapshotCount = legacyHistorySnapshotCount,
            LostOpportunityCount = lostOpportunityCount,
            DataQualityIssues = dataQualityIssues,
            PipelineByStage = pipelineByStage,
            AccountRiskByBand = accountRisk,
            ConversionFunnel = valueFunnel
        };
    }

    private static string? NormalizeDashboardCurrencyCode(string? value)
    {
        var code = value?.Trim().ToUpperInvariant();
        return code is { Length: 3 } && code.All(character => character is >= 'A' and <= 'Z')
            ? code
            : null;
    }

    private async Task<int> CountReadableInventoryRequisitionsAsync(
        IQueryable<InventoryRequisition> query,
        IDictionary<(Guid WarehouseId, Guid? LocationId), bool> scopeDecisions,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var readableCount = await query.CountAsync(value => value.RequestedById == userId, cancellationToken);
        var scopedCounts = await query
            .Where(value => value.RequestedById != userId)
            .GroupBy(value => new { value.WarehouseId, value.LocationId })
            .Select(group => new
            {
                group.Key.WarehouseId,
                group.Key.LocationId,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var scopedCount in scopedCounts)
        {
            var scope = (scopedCount.WarehouseId, scopedCount.LocationId);
            if (!scopeDecisions.TryGetValue(scope, out var allowed))
            {
                allowed = await CanReadInventoryScopeAsync(scope.WarehouseId, scope.LocationId, cancellationToken);
                scopeDecisions.Add(scope, allowed);
            }

            if (allowed)
                readableCount += scopedCount.Count;
        }

        return readableCount;
    }

    private async Task<bool> CanReadInventoryScopeAsync(
        Guid warehouseId,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var decision = await _accessControl.CheckCapabilityAsync(
                new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = "procurement.inventory.read",
                    WarehouseId = warehouseId,
                    LocationId = locationId,
                    RequireLocationScope = true,
                    SourceType = "EnterpriseDashboard",
                    SourceReference = $"inventory-queue:{warehouseId:N}:{locationId?.ToString("N") ?? "all"}"
                },
                Guid.NewGuid().ToString("N"),
                cancellationToken);
            return decision.Allowed;
        }
        catch (ProcurementAccessAuthorizationException)
        {
            return false;
        }
        catch (ProcurementAccessValidationException)
        {
            return false;
        }
    }
}
