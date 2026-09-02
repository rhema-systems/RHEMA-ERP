using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

/// <summary>
/// Tenant-scoped promotion evidence for compiled Finance Fixed Assets routes.  The provider
/// inventories in-flight source documents and verifies that generic source assignments and
/// immutable snapshots exist without reading or changing another module's source records.
/// </summary>
public sealed class FinanceFixedAssetDimensionReadinessProvider : IFinanceDimensionReadinessProvider
{
    private readonly ApplicationDbContext _db;

    public FinanceFixedAssetDimensionReadinessProvider(
        ApplicationDbContext db,
        FinanceDimensionRouteId routeId)
    {
        _db = db;
        RouteId = routeId;
        var route = FinanceDimensionRouteCatalog.GetRequired(routeId);
        if (!string.Equals(route.Owner, "Finance / Fixed Assets", StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(routeId), routeId, "The route is not owned by Finance Fixed Assets.");
    }

    public FinanceDimensionRouteId RouteId { get; }

    public async Task<FinanceDimensionReadinessContribution> EvaluateAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        CancellationToken cancellationToken = default)
    {
        if (route.Id != RouteId)
            throw new InvalidOperationException("The readiness provider was invoked for a different compiled route.");

        var candidates = await LoadCandidatesAsync(tenantId, cancellationToken);
        var assignments = await _db.FinanceSourceDimensionAssignments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RouteId == RouteId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var assignmentDocumentId in assignments.Select(item => item.SourceDocumentId).Distinct())
        {
            if (candidates.All(item => item.Id != assignmentDocumentId))
                candidates.Add(new Candidate(
                    assignmentDocumentId,
                    assignmentDocumentId.ToString("D"),
                    "Captured",
                    $"/finance/dimensions/readiness/{(int)RouteId}",
                    assignments.Where(item => item.SourceDocumentId == assignmentDocumentId)
                        .Max(item => (item.UpdatedAt ?? item.CreatedAt).Ticks)));
        }

        var setIds = assignments.Where(item => item.FinanceDimensionSetId.HasValue)
            .Select(item => item.FinanceDimensionSetId!.Value).Distinct().ToArray();
        var snapshotIds = assignments.Where(item => item.FinanceDimensionSnapshotId.HasValue)
            .Select(item => item.FinanceDimensionSnapshotId!.Value).Distinct().ToArray();
        var sets = await _db.FinanceDimensionSets.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && setIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var snapshots = await _db.FinanceDimensionSnapshots.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && snapshotIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var validSets = sets.ToHashSet();
        var validSnapshots = snapshots.ToHashSet();
        var groups = assignments.GroupBy(item => item.SourceDocumentId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var blockers = new List<FinanceDimensionReadinessBlockerDto>();

        foreach (var candidate in candidates.OrderBy(item => item.Reference, StringComparer.Ordinal))
        {
            groups.TryGetValue(candidate.Id, out var documentAssignments);
            documentAssignments ??= [];
            if (documentAssignments.All(item => item.SourceLineId.HasValue))
            {
                blockers.Add(Blocker(candidate, "UNCERTIFIED_FIXED_ASSET_DOCUMENT",
                    "The in-flight source document has no trusted route/header assignment.",
                    "Open the Finance document, capture its dimensions and restart approval where applicable."));
                continue;
            }

            var lines = documentAssignments.Where(item => item.SourceLineId.HasValue).ToArray();
            if (lines.Length == 0)
            {
                blockers.Add(Blocker(candidate, "FIXED_ASSET_SOURCE_LINES_MISSING",
                    "The source document has no stable economic-line dimension assignments.",
                    "Rebuild the draft through its Finance-owned route so every posting component is captured."));
                continue;
            }

            foreach (var line in lines)
            {
                if (!line.EvidenceFrozenAt.HasValue)
                    blockers.Add(Blocker(candidate, "FIXED_ASSET_EVIDENCE_NOT_FROZEN",
                        $"Source line {line.SourceLineId:D} has not been frozen for approval/posting.",
                        "Validate the line and restart its governed approval/posting action."));
                if (line.FinanceDimensionSetId.HasValue && !validSets.Contains(line.FinanceDimensionSetId.Value))
                    blockers.Add(Blocker(candidate, "FIXED_ASSET_CANONICAL_SET_INVALID",
                        $"Source line {line.SourceLineId:D} references unavailable or cross-tenant canonical evidence.",
                        "Return the document to Draft and re-resolve its Finance dimensions."));
                if (line.EvidenceFrozenAt.HasValue && line.FinanceDimensionSetId.HasValue
                    && (!line.FinanceDimensionSnapshotId.HasValue
                        || !validSnapshots.Contains(line.FinanceDimensionSnapshotId.Value)))
                    blockers.Add(Blocker(candidate, "FIXED_ASSET_SNAPSHOT_MISSING",
                        $"Source line {line.SourceLineId:D} lacks its immutable line snapshot.",
                        "Return the document to Draft, revalidate and start a new approval workflow."));
            }
        }

        var candidateWatermark = candidates.Count == 0 ? 0L : candidates.Max(item => item.VersionTicks);
        var assignmentWatermark = assignments.Count == 0
            ? 0L
            : assignments.Max(item => (item.UpdatedAt ?? item.CreatedAt).Ticks);
        return new FinanceDimensionReadinessContribution(
            $"{route.ContractVersion}:{candidates.Count}:{assignments.Count}:{candidateWatermark}:{assignmentWatermark}:{blockers.Count}",
            blockers);
    }

    private async Task<List<Candidate>> LoadCandidatesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        switch (RouteId)
        {
            case FinanceDimensionRouteId.FinanceFixedAssetCapitalization:
            {
                var rows = await _db.FixedAssets.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status == FixedAssetStatus.PendingApproval
                        && item.CapitalizationApprovalSnapshotJson != null
                        && item.SourceDocumentType != "ProcurementFixedAssetCapitalization")
                    .Select(item => new { item.Id, item.AssetCode, item.Status, item.CapitalizationReversalPostingEventId, item.CreatedAt, item.UpdatedAt })
                    .ToListAsync(cancellationToken);
                return rows.Select(item => new Candidate(
                    item.CapitalizationReversalPostingEventId.HasValue
                        ? FinanceSourceLineIdentity.Create(item.Id, "CAPITALIZATION-CYCLE", item.CapitalizationReversalPostingEventId.Value)
                        : item.Id,
                    item.AssetCode, item.Status.ToString(), $"/finance/fixed-assets/{item.Id}",
                    (item.UpdatedAt ?? item.CreatedAt).Ticks)).ToList();
            }
            case FinanceDimensionRouteId.FinanceFixedAssetCapitalizationReversal:
                return await _db.FixedAssetCapitalizationReversals.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status != FixedAssetCapitalizationReversalStatuses.Posted
                        && item.Status != FixedAssetCapitalizationReversalStatuses.Rejected)
                    .Select(item => new Candidate(item.Id, item.Id.ToString(), item.Status,
                        $"/finance/fixed-assets/{item.FixedAssetId}", (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceFixedAssetDepreciation:
                return await _db.FixedAssetDepreciationRuns.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status != "Posted" && item.Status != "Rejected" && item.Status != "Failed")
                    .Select(item => new Candidate(item.Id, item.Id.ToString(), item.Status,
                        "/finance/fixed-assets/depreciation", (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceFixedAssetDepreciationReversal:
                return await _db.FixedAssetDepreciationReversals.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status != FixedAssetDepreciationReversalStatuses.Posted
                        && item.Status != FixedAssetDepreciationReversalStatuses.Rejected)
                    .Select(item => new Candidate(item.Id, item.Id.ToString(), item.Status,
                        "/finance/fixed-assets/depreciation", (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceFixedAssetRevaluation:
            case FinanceDimensionRouteId.FinanceFixedAssetImpairment:
            case FinanceDimensionRouteId.FinanceFixedAssetImpairmentReversal:
            {
                var valuationType = RouteId switch
                {
                    FinanceDimensionRouteId.FinanceFixedAssetRevaluation => ValuationType.Revaluation,
                    FinanceDimensionRouteId.FinanceFixedAssetImpairment => ValuationType.Impairment,
                    _ => ValuationType.ImpairmentReversal
                };
                return await _db.AssetValuations.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted && !item.IsPostedToGL
                        && item.ValuationType == valuationType
                        && item.Status != "Rejected" && item.Status != "Failed")
                    .Select(item => new Candidate(item.Id, item.Id.ToString(), item.Status,
                        $"/finance/fixed-assets/{item.FixedAssetId}/valuations", (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            }
            case FinanceDimensionRouteId.FinanceFixedAssetValuationCorrection:
                return await _db.AssetValuationCorrections.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status != AssetValuationCorrectionStatuses.Posted
                        && item.Status != AssetValuationCorrectionStatuses.Rejected)
                    .Select(item => new Candidate(item.Id, item.Id.ToString(), item.Status,
                        "/finance/fixed-assets/valuations", (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceFixedAssetDisposal:
                return await _db.AssetDisposals.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && (item.Status == AssetDisposalStatus.Draft
                            || item.Status == AssetDisposalStatus.PendingApproval
                            || item.Status == AssetDisposalStatus.Approved))
                    .Select(item => new Candidate(item.Id, item.ReferenceNumber ?? item.Id.ToString(), item.Status.ToString(),
                        $"/finance/fixed-assets/{item.FixedAssetId}/disposals", (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceFixedAssetReclassification:
                return await _db.AssetTransfers.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.TransferType == AssetTransferType.GlReclassification
                        && (item.Status == AssetTransferStatus.Draft
                            || item.Status == AssetTransferStatus.PendingApproval
                            || item.Status == AssetTransferStatus.Approved))
                    .Select(item => new Candidate(item.Id, item.ReferenceNumber ?? item.Id.ToString(), item.Status.ToString(),
                        $"/finance/fixed-assets/{item.FixedAssetId}/transfers", (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceCapitalProjectSettlement:
                return await _db.CapitalProjects.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status == ProjectStatus.InProgress && item.TotalAccumulatedCost > 0m)
                    .Select(item => new Candidate(item.Id, item.ProjectCode, item.Status.ToString(),
                        $"/finance/fixed-assets/capital-projects/{item.Id}", (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceLeaseRecognition:
                return await _db.LeaseContracts.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.Status == LeaseStatus.Draft)
                    .Select(item => new Candidate(item.Id, item.ContractNumber, item.Status.ToString(),
                        $"/finance/fixed-assets/leases/{item.Id}", (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceLeasePeriodPosting:
                return await _db.LeaseScheduleLines.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted && !item.IsPosted
                        && item.LeaseContract.Status == LeaseStatus.Active)
                    .Select(item => new Candidate(item.Id,
                        item.LeaseContract.ContractNumber + "-P" + item.PeriodNumber,
                        "Unposted", $"/finance/fixed-assets/leases/{item.LeaseContractId}",
                        (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            default:
                return [];
        }
    }

    private static FinanceDimensionReadinessBlockerDto Blocker(
        Candidate document,
        string code,
        string message,
        string remediation) => new()
    {
        Code = code,
        Message = message,
        LifecycleState = document.Lifecycle,
        DocumentId = document.Id,
        DocumentReference = document.Reference,
        DocumentLink = document.Link,
        RemediationStatus = remediation,
        DimensionIssue = message,
        ActiveReservationState = "None"
    };

    private sealed record Candidate(
        Guid Id,
        string Reference,
        string Lifecycle,
        string Link,
        long VersionTicks);
}
