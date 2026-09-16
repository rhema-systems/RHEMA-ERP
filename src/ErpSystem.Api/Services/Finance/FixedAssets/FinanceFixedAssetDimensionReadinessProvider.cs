using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
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
        foreach (var header in assignments.Where(item => !item.SourceLineId.HasValue))
        {
            if (candidates.All(item => item.Id != header.SourceDocumentId))
                candidates.Add(new Candidate(
                    header.SourceDocumentId,
                    header.SourceDocumentId.ToString("D"),
                    "Captured",
                    $"/finance/dimensions/readiness/{(int)RouteId}",
                    header.SourceDocumentDate ?? header.CreatedAt,
                    false,
                    assignments.Where(item => item.SourceDocumentId == header.SourceDocumentId)
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
            var header = documentAssignments.SingleOrDefault(item => !item.SourceLineId.HasValue);
            if (header is null)
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

            if (!header.SourceDocumentDate.HasValue
                || !header.ExpectedSourceLineCount.HasValue
                || string.IsNullOrWhiteSpace(header.SourceLineManifestHash))
            {
                blockers.Add(Blocker(candidate, "FIXED_ASSET_SOURCE_CONTEXT_MISSING",
                    "The source document predates trusted account/date/line-manifest readiness evidence.",
                    "Return the document to Draft and save it through its Finance-owned dimension route."));
                continue;
            }
            if (RouteId is not FinanceDimensionRouteId.FinanceCapitalProjectSettlement
                and not FinanceDimensionRouteId.FinanceLeaseRecognition
                and not FinanceDimensionRouteId.FinanceLeasePeriodPosting
                && header.SourceDocumentDate.Value.Date != candidate.DocumentDate.Date)
                blockers.Add(Blocker(candidate, "FIXED_ASSET_DOCUMENT_DATE_DRIFT",
                    "The persisted Finance dimension rule date no longer matches the source document date.",
                    "Return the document to Draft, re-resolve its dimensions and restart approval."));
            if (header.ExpectedSourceLineCount.Value != lines.Length)
                blockers.Add(Blocker(candidate, "FIXED_ASSET_SOURCE_LINE_COUNT_MISMATCH",
                    $"Expected {header.ExpectedSourceLineCount.Value} economic lines but found {lines.Length} assignments.",
                    "Rebuild the draft through its Finance-owned route so every posting component is captured."));

            var completeContexts = lines.All(item => item.ResolvedAccountId.HasValue);
            if (!completeContexts)
                blockers.Add(Blocker(candidate, "FIXED_ASSET_SOURCE_ACCOUNT_MISSING",
                    "One or more economic lines lack a trusted server-resolved account.",
                    "Return the document to Draft and save it through its Finance-owned dimension route."));
            else
            {
                var manifest = FinanceSourceLineManifest.Compute(lines.Select(item =>
                    (item.SourceLineId!.Value, item.ResolvedAccountId!.Value)));
                if (!string.Equals(manifest, header.SourceLineManifestHash, StringComparison.Ordinal))
                    blockers.Add(Blocker(candidate, "FIXED_ASSET_SOURCE_MANIFEST_MISMATCH",
                        "The economic-line identities or resolved accounts no longer match the trusted source manifest.",
                        "Return the document to Draft and rebuild its Finance coding evidence."));
            }

            foreach (var line in lines)
            {
                if (candidate.RequiresFrozenEvidence && !line.EvidenceFrozenAt.HasValue)
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

                if (!line.ResolvedAccountId.HasValue) continue;
                FinanceDimensionSet? set = null;
                if (line.FinanceDimensionSetId.HasValue)
                    set = await _db.FinanceDimensionSets.AsNoTracking()
                        .Include(item => item.Items)
                        .SingleOrDefaultAsync(item => item.TenantId == tenantId
                            && item.Id == line.FinanceDimensionSetId.Value && !item.IsDeleted,
                            cancellationToken);
                var ruleIssues = await ValidateAccountRulesAsync(
                    tenantId,
                    route,
                    header.SourceDocumentDate.Value,
                    line.ResolvedAccountId.Value,
                    set,
                    cancellationToken);
                foreach (var issue in ruleIssues)
                    blockers.Add(Blocker(candidate, issue.Code,
                        $"Source line {line.SourceLineId:D}: {issue.Message}",
                        "Correct the draft line dimensions, refresh any affected evidence and restart approval where applicable.",
                        issue.FixedRuleDrift));
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
                    .Select(item => new { item.Id, item.AssetCode, item.Status, item.CapitalizationDate, item.CapitalizationReversalPostingEventId, item.CreatedAt, item.UpdatedAt })
                    .ToListAsync(cancellationToken);
                return rows.Select(item => new Candidate(
                    item.CapitalizationReversalPostingEventId.HasValue
                        ? FinanceSourceLineIdentity.Create(item.Id, "CAPITALIZATION-CYCLE", item.CapitalizationReversalPostingEventId.Value)
                        : item.Id,
                    item.AssetCode, item.Status.ToString(), $"/finance/fixed-assets/{item.Id}",
                    item.CapitalizationDate ?? item.CreatedAt,
                    true,
                    (item.UpdatedAt ?? item.CreatedAt).Ticks)).ToList();
            }
            case FinanceDimensionRouteId.FinanceFixedAssetCapitalizationReversal:
                return await _db.FixedAssetCapitalizationReversals.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status != FixedAssetCapitalizationReversalStatuses.Posted
                        && item.Status != FixedAssetCapitalizationReversalStatuses.Rejected)
                    .Select(item => new Candidate(item.Id, item.Id.ToString(), item.Status,
                        $"/finance/fixed-assets/{item.FixedAssetId}", item.RequestedReversalDate,
                        item.Status == FixedAssetCapitalizationReversalStatuses.PendingApproval
                            || item.Status == FixedAssetCapitalizationReversalStatuses.Approved,
                        (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceFixedAssetDepreciation:
                return await _db.FixedAssetDepreciationRuns.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status != "Posted" && item.Status != "Rejected" && item.Status != "Failed")
                    .Select(item => new Candidate(item.Id, item.Id.ToString(), item.Status,
                        "/finance/fixed-assets/depreciation", item.PostingDate,
                        item.Status == "PendingApproval" || item.Status == "Approved",
                        (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceFixedAssetDepreciationReversal:
                return await _db.FixedAssetDepreciationReversals.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status != FixedAssetDepreciationReversalStatuses.Posted
                        && item.Status != FixedAssetDepreciationReversalStatuses.Rejected)
                    .Select(item => new Candidate(item.Id, item.Id.ToString(), item.Status,
                        "/finance/fixed-assets/depreciation", item.RequestedReversalDate,
                        item.Status == FixedAssetDepreciationReversalStatuses.PendingApproval
                            || item.Status == FixedAssetDepreciationReversalStatuses.Approved,
                        (item.UpdatedAt ?? item.CreatedAt).Ticks))
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
                        $"/finance/fixed-assets/{item.FixedAssetId}/valuations", item.AccountingDate,
                        item.Status == "PendingApproval" || item.Status == "Approved",
                        (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            }
            case FinanceDimensionRouteId.FinanceFixedAssetValuationCorrection:
                return await _db.AssetValuationCorrections.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status != AssetValuationCorrectionStatuses.Posted
                        && item.Status != AssetValuationCorrectionStatuses.Rejected)
                    .Select(item => new Candidate(item.Id, item.Id.ToString(), item.Status,
                        "/finance/fixed-assets/valuations", item.RequestedReversalDate,
                        item.Status == AssetValuationCorrectionStatuses.PendingApproval
                            || item.Status == AssetValuationCorrectionStatuses.Approved,
                        (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceFixedAssetDisposal:
                return await _db.AssetDisposals.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && (item.Status == AssetDisposalStatus.Draft
                            || item.Status == AssetDisposalStatus.PendingApproval
                            || item.Status == AssetDisposalStatus.Approved))
                    .Select(item => new Candidate(item.Id, item.ReferenceNumber ?? item.Id.ToString(), item.Status.ToString(),
                        $"/finance/fixed-assets/{item.FixedAssetId}/disposals", item.AccountingDate ?? item.DisposalDate,
                        item.Status == AssetDisposalStatus.PendingApproval || item.Status == AssetDisposalStatus.Approved,
                        (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceFixedAssetReclassification:
                return await _db.AssetTransfers.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.TransferType == AssetTransferType.GlReclassification
                        && (item.Status == AssetTransferStatus.Draft
                            || item.Status == AssetTransferStatus.PendingApproval
                            || item.Status == AssetTransferStatus.Approved))
                    .Select(item => new Candidate(item.Id, item.ReferenceNumber ?? item.Id.ToString(), item.Status.ToString(),
                        $"/finance/fixed-assets/{item.FixedAssetId}/transfers", item.AccountingDate ?? item.TransferDate,
                        item.Status == AssetTransferStatus.PendingApproval || item.Status == AssetTransferStatus.Approved,
                        (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceCapitalProjectSettlement:
                return await _db.CapitalProjects.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.Status == ProjectStatus.InProgress && item.TotalAccumulatedCost > 0m)
                    .Select(item => new Candidate(item.Id, item.ProjectCode, item.Status.ToString(),
                        $"/finance/fixed-assets/capital-projects/{item.Id}", item.ActualCompletionDate ?? item.UpdatedAt ?? item.CreatedAt,
                        false, (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceLeaseRecognition:
                return await _db.LeaseContracts.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.Status == LeaseStatus.Draft)
                    .Select(item => new Candidate(item.Id, item.ContractNumber, item.Status.ToString(),
                        $"/finance/fixed-assets/leases/{item.Id}", item.StartDate,
                        false, (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            case FinanceDimensionRouteId.FinanceLeasePeriodPosting:
                return await _db.LeaseScheduleLines.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted && !item.IsPosted
                        && item.LeaseContract.Status == LeaseStatus.Active)
                    .Select(item => new Candidate(item.Id,
                        item.LeaseContract.ContractNumber + "-P" + item.PeriodNumber,
                        "Unposted", $"/finance/fixed-assets/leases/{item.LeaseContractId}",
                        item.PeriodDate, false,
                        (item.UpdatedAt ?? item.CreatedAt).Ticks))
                    .ToListAsync(cancellationToken);
            default:
                return [];
        }
    }

    private async Task<IReadOnlyList<RuleIssue>> ValidateAccountRulesAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        DateTime documentDate,
        Guid accountId,
        FinanceDimensionSet? set,
        CancellationToken cancellationToken)
    {
        var accountExists = await _db.Accounts.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.Id == accountId && !item.IsDeleted
            && item.Status == AccountStatus.Active,
            cancellationToken);
        if (!accountExists)
            return [new RuleIssue("SOURCE_ACCOUNT_INVALID", "The source account is inactive, deleted or cross-tenant.", false)];

        var candidates = await _db.FinanceDimensionAccountRules.AsNoTracking()
            .Include(item => item.FinanceDimensionDefinition)
            .Include(item => item.DefaultDimensionValue)
            .Where(item => item.TenantId == tenantId && item.AccountId == accountId
                && !item.IsDeleted && item.IsActive
                && item.EffectiveDate.Date <= documentDate.Date
                && (!item.ExpiryDate.HasValue || item.ExpiryDate.Value.Date >= documentDate.Date)
                && (!item.RouteId.HasValue || item.RouteId == route.Id)
                && (item.SourceModule == null || item.SourceModule == route.PostingSourceModule)
                && (item.SourceDocumentType == null || item.SourceDocumentType == route.DocumentType)
                && (item.PostingAction == null || item.PostingAction == "Post"))
            .ToListAsync(cancellationToken);
        var selected = new List<FinanceDimensionAccountRule>();
        var issues = new List<RuleIssue>();
        foreach (var group in candidates.GroupBy(item => item.FinanceDimensionDefinitionId))
        {
            var ordered = group.OrderByDescending(item => Specificity(item, route.Id))
                .ThenByDescending(item => item.RuleVersion).ToList();
            if (ordered.Count > 1 && Specificity(ordered[0], route.Id) == Specificity(ordered[1], route.Id))
            {
                issues.Add(new RuleIssue(
                    "AMBIGUOUS_DIMENSION_RULE",
                    $"Dimension {ordered[0].FinanceDimensionDefinition.Code} has ambiguous effective account rules.",
                    true));
                continue;
            }
            selected.Add(ordered[0]);
        }

        var values = set?.Items.ToDictionary(
            item => item.FinanceDimensionDefinitionId,
            item => item.FinanceDimensionValueId) ?? new Dictionary<Guid, Guid>();
        foreach (var rule in selected)
        {
            var hasValue = values.TryGetValue(rule.FinanceDimensionDefinitionId, out var valueId);
            switch (rule.RuleType)
            {
                case "Prohibited" when hasValue:
                    issues.Add(new RuleIssue("PROHIBITED_DIMENSION",
                        $"Dimension {rule.FinanceDimensionDefinition.Code} is prohibited for the source account.", false));
                    break;
                case "Fixed" when !rule.DefaultDimensionValueId.HasValue:
                    issues.Add(new RuleIssue("INVALID_FIXED_RULE",
                        $"Fixed dimension {rule.FinanceDimensionDefinition.Code} has no effective value.", true));
                    break;
                case "Fixed" when !hasValue || valueId != rule.DefaultDimensionValueId:
                    issues.Add(new RuleIssue("FIXED_RULE_DRIFT",
                        $"Dimension {rule.FinanceDimensionDefinition.Code} does not match the current fixed value {rule.DefaultDimensionValue?.Code}.", true));
                    break;
                case "Required" when !hasValue && !rule.DefaultDimensionValueId.HasValue:
                    issues.Add(new RuleIssue("REQUIRED_DIMENSION_MISSING",
                        $"Dimension {rule.FinanceDimensionDefinition.Code} is required for the source account.", false));
                    break;
            }
        }

        if (set is not null)
        {
            var definitionIds = set.Items.Select(item => item.FinanceDimensionDefinitionId).ToArray();
            var valueIds = set.Items.Select(item => item.FinanceDimensionValueId).ToArray();
            var validDefinitions = await _db.FinanceDimensionDefinitions.AsNoTracking().CountAsync(item =>
                item.TenantId == tenantId && definitionIds.Contains(item.Id)
                && !item.IsDeleted && item.IsActive, cancellationToken);
            var validValues = await _db.FinanceDimensionValues.AsNoTracking().CountAsync(item =>
                item.TenantId == tenantId && valueIds.Contains(item.Id)
                && !item.IsDeleted && item.IsActive
                && item.EffectiveDate.Date <= documentDate.Date
                && (!item.ExpiryDate.HasValue || item.ExpiryDate.Value.Date >= documentDate.Date),
                cancellationToken);
            if (validDefinitions != definitionIds.Distinct().Count()
                || validValues != valueIds.Distinct().Count())
                issues.Add(new RuleIssue("DIMENSION_MASTER_INVALID",
                    "One or more captured dimensions are inactive, expired, missing or cross-tenant.", false));
        }
        return issues;
    }

    private static int Specificity(FinanceDimensionAccountRule rule, FinanceDimensionRouteId routeId)
        => (rule.RouteId.HasValue && rule.RouteId == routeId ? 8 : 0)
           + (rule.SourceModule is null ? 0 : 4)
           + (rule.SourceDocumentType is null ? 0 : 2)
           + (rule.PostingAction is null ? 0 : 1);

    private static FinanceDimensionReadinessBlockerDto Blocker(
        Candidate document,
        string code,
        string message,
        string remediation,
        bool fixedRuleDrift = false) => new()
    {
        Code = code,
        Message = message,
        LifecycleState = document.Lifecycle,
        DocumentId = document.Id,
        DocumentReference = document.Reference,
        DocumentLink = document.Link,
        RemediationStatus = remediation,
        DimensionIssue = message,
        FixedRuleDrift = fixedRuleDrift,
        ActiveReservationState = "None"
    };

    private sealed record Candidate(
        Guid Id,
        string Reference,
        string Lifecycle,
        string Link,
        DateTime DocumentDate,
        bool RequiresFrozenEvidence,
        long VersionTicks);

    private sealed record RuleIssue(string Code, string Message, bool FixedRuleDrift);
}
