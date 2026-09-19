using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Tenant-scoped provisional readiness for an additive external adapter. It deliberately keeps
/// promotion blocked until the producer owner supplies a complete authoritative-document census;
/// assignment-only discovery must never silently certify external documents that bypassed Finance.
/// </summary>
public sealed class ExternalProducerDimensionReadinessProvider : IFinanceDimensionReadinessProvider
{
    private readonly ApplicationDbContext _db;

    public ExternalProducerDimensionReadinessProvider(ApplicationDbContext db, FinanceDimensionRouteId routeId)
    {
        _db = db;
        RouteId = routeId;
        var route = FinanceDimensionRouteCatalog.GetRequired(routeId);
        if (string.Equals(route.ProducerModule, "Finance", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(routeId), "External readiness cannot serve a Finance-owned route.");
    }

    public FinanceDimensionRouteId RouteId { get; }

    public async Task<FinanceDimensionReadinessContribution> EvaluateAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        CancellationToken cancellationToken = default)
    {
        if (route.Id != RouteId)
            throw new InvalidOperationException("The readiness provider was invoked for another compiled route.");

        var assignments = await _db.FinanceSourceDimensionAssignments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RouteId == RouteId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var blockers = new List<FinanceDimensionReadinessBlockerDto>
        {
            new()
            {
                Code = "PRODUCER_ADOPTION_CENSUS_REQUIRED",
                Message = $"{route.ProducerModule} has not installed an authoritative tenant-scoped document census for {route.SourceRoute}.",
                LifecycleState = "AdapterAvailable",
                RemediationStatus = "Producer owner must adopt contract and add consumer census tests.",
                DimensionIssue = "Unadapted producer documents cannot yet be enumerated.",
                Details = "CaptureOptional is safe; promotion to Enforced remains fail-closed."
            }
        };

        foreach (var group in assignments.GroupBy(item => item.SourceDocumentId))
        {
            var header = group.SingleOrDefault(item => !item.SourceLineId.HasValue);
            if (header is null)
            {
                blockers.Add(Blocker(group.Key, "SOURCE_HEADER_CONTEXT_MISSING",
                    "The adapted source document has no persisted trusted header provenance."));
            }
            var lines = group.Where(item => item.SourceLineId.HasValue).ToArray();
            if (lines.Length == 0)
            {
                blockers.Add(Blocker(group.Key, "SOURCE_LINES_MISSING",
                    "The adapted source document has no stable source-line assignments."));
            }
            foreach (var line in lines.Where(item => !item.EvidenceFrozenAt.HasValue))
            {
                blockers.Add(Blocker(group.Key, "SOURCE_LINE_EVIDENCE_NOT_FROZEN",
                    $"Source line {line.SourceLineId} has not frozen canonical dimension evidence."));
            }

            if (header is null) continue;
            if (!header.SourceDocumentDate.HasValue
                || !header.ExpectedSourceLineCount.HasValue
                || string.IsNullOrWhiteSpace(header.SourceLineManifestHash))
            {
                blockers.Add(Blocker(group.Key, "SOURCE_CONTEXT_EVIDENCE_MISSING",
                    "The adapted source document lacks its trusted date or complete economic-line manifest."));
                continue;
            }
            if (header.ExpectedSourceLineCount.Value != lines.Length)
                blockers.Add(Blocker(group.Key, "SOURCE_LINE_COUNT_MISMATCH",
                    $"Expected {header.ExpectedSourceLineCount.Value} source lines but found {lines.Length}."));
            if (lines.Any(item => !item.ResolvedAccountId.HasValue))
            {
                blockers.Add(Blocker(group.Key, "SOURCE_ACCOUNT_CONTEXT_MISSING",
                    "One or more adapted source lines lack a trusted server-resolved account."));
                continue;
            }

            var manifest = FinanceSourceLineManifest.Compute(lines.Select(item =>
                (item.SourceLineId!.Value, item.ResolvedAccountId!.Value)));
            if (!string.Equals(manifest, header.SourceLineManifestHash, StringComparison.Ordinal))
                blockers.Add(Blocker(group.Key, "SOURCE_LINE_MANIFEST_MISMATCH",
                    "The adapted source-line identities or resolved accounts no longer match the trusted manifest."));
        }

        var watermarkTicks = assignments.Count == 0
            ? 0L
            : assignments.Max(item => (item.UpdatedAt ?? item.CreatedAt).Ticks);
        return new FinanceDimensionReadinessContribution(
            $"external-adapter:{route.ContractVersion}:{assignments.Count}:{watermarkTicks}:{blockers.Count}",
            blockers);
    }

    private static FinanceDimensionReadinessBlockerDto Blocker(Guid documentId, string code, string message) => new()
    {
        Code = code,
        Message = message,
        LifecycleState = "Adapted",
        DocumentId = documentId,
        RemediationStatus = "Correct through the producer-specific adapter.",
        DimensionIssue = message
    };
}
