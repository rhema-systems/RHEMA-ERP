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
    private static readonly string[] ClosedOpportunityStages = ["Closed Won", "Closed Lost"];
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
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var inventoryQueue = _context.InventoryRequisitions.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.RequestDate >= rangeStart && value.RequestDate < rangeEndExclusive);
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
        var followUpWindow = rangeEndExclusive.AddDays(14);

        var scopedLeads = _context.Leads.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.CreatedAt >= rangeStart && value.CreatedAt < rangeEndExclusive);

        var scopedOpportunities = _context.Opportunities.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                (value.CreatedAt >= rangeStart && value.CreatedAt < rangeEndExclusive ||
                 (value.ActualCloseDate ?? value.ExpectedCloseDate) >= rangeStart &&
                 (value.ActualCloseDate ?? value.ExpectedCloseDate) < rangeEndExclusive));

        var openOpportunities = scopedOpportunities
            .Where(value => !ClosedOpportunityStages.Contains(value.Stage));

        var totalLeads = await scopedLeads.CountAsync(cancellationToken);
        var qualifiedLeadCount = await scopedLeads.CountAsync(value => value.LeadStatus == "Qualified", cancellationToken);
        var leadFollowUpCount = await scopedLeads.CountAsync(value =>
            !ClosedLeadStatuses.Contains(value.LeadStatus) &&
            value.NextFollowUpDate.HasValue && value.NextFollowUpDate.Value <= followUpWindow,
            cancellationToken);
        var openOpportunityCount = await openOpportunities.CountAsync(cancellationToken);

        var scopedOpportunityIds = scopedOpportunities.Select(value => value.Id);
        var activeQuoteCount = await _context.Quotes.AsNoTracking().CountAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted &&
            scopedOpportunityIds.Contains(value.OpportunityId) &&
            !ClosedQuoteStatuses.Contains(value.QuoteStatus), cancellationToken);

        var activeAccountCount = await _context.BusinessPartners.AsNoTracking().CountAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted && value.IsActive, cancellationToken);
        var atRiskAccountCount = await _context.BusinessPartners.AsNoTracking().CountAsync(value =>
            value.TenantId == tenantId && !value.IsDeleted && value.IsActive &&
            value.RiskLevel != null && AtRiskBands.Contains(value.RiskLevel), cancellationToken);

        var pipeline = await openOpportunities
            .GroupBy(value => value.Stage)
            .Select(group => new EnterpriseDashboardStageCountDto
            {
                Stage = group.Key,
                OpportunityCount = group.Count(),
                QuoteCount = 0
            })
            .OrderByDescending(value => value.OpportunityCount)
            .ThenBy(value => value.Stage)
            .ToListAsync(cancellationToken);

        var quoteCountsByStage = await _context.Quotes.AsNoTracking()
            .Where(quote => quote.TenantId == tenantId && !quote.IsDeleted &&
                scopedOpportunityIds.Contains(quote.OpportunityId))
            .GroupBy(quote => quote.Opportunity.Stage)
            .Select(group => new { Stage = group.Key, Count = group.Count() })
            .ToDictionaryAsync(value => value.Stage, value => value.Count, cancellationToken);
        foreach (var stage in pipeline)
            stage.QuoteCount = quoteCountsByStage.GetValueOrDefault(stage.Stage);

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

        var leadWithOpportunityCount = await scopedLeads.CountAsync(lead =>
            scopedOpportunities.Any(opportunity => opportunity.LeadId == lead.Id), cancellationToken);
        var quotedOpportunityCount = await scopedOpportunities.CountAsync(opportunity =>
            _context.Quotes.Any(quote => quote.TenantId == tenantId && !quote.IsDeleted &&
                quote.OpportunityId == opportunity.Id), cancellationToken);
        var convertedLeadCount = await scopedLeads.CountAsync(value => value.LeadStatus == "Converted", cancellationToken);
        var opportunityCount = await scopedOpportunities.CountAsync(cancellationToken);

        return new EnterpriseCrmDashboardDto
        {
            TotalLeadCount = totalLeads,
            QualifiedLeadCount = qualifiedLeadCount,
            LeadsNeedingFollowUpCount = leadFollowUpCount,
            OpenOpportunityCount = openOpportunityCount,
            ActiveQuoteCount = activeQuoteCount,
            ActiveAccountCount = activeAccountCount,
            AtRiskAccountCount = atRiskAccountCount,
            PipelineByStage = pipeline,
            AccountRiskByBand = accountRisk,
            ConversionFunnel =
            [
                Funnel("Leads", totalLeads, totalLeads),
                Funnel("Leads with opportunities", leadWithOpportunityCount, totalLeads),
                Funnel("Opportunities", opportunityCount, leadWithOpportunityCount),
                Funnel("Quoted opportunities", quotedOpportunityCount, opportunityCount),
                Funnel("Converted leads", convertedLeadCount, totalLeads)
            ]
        };
    }

    private static EnterpriseDashboardFunnelPointDto Funnel(string stage, int count, int priorCount) => new()
    {
        Stage = stage,
        Count = count,
        ConversionRate = priorCount == 0 ? null : decimal.Round(count * 100m / priorCount, 2)
    };

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
