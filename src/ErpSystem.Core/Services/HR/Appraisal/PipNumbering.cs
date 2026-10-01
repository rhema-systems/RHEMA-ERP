using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// A plan's reference, <c>PIP-{year}-NNNN</c>, per tenant and year — the one scheme every plan gets
/// (performance closure D-75). <c>PipRecommendationHandler</c> wrote <c>PIP-APR-{date}-{hex}</c>, so a
/// plan raised from an appraisal outcome was numbered outside the tenant's sequence.
///
/// <para>Not race-proof: two plans created at the same moment can read the same set and take the same
/// number, because the column's index is neither unique nor per tenant. Making it a filtered unique
/// index per tenant is migration batch 2's (closure plan § 5); until then a clash is possible, not
/// prevented.</para>
/// </summary>
public static class PipNumbering
{
    public static async Task<string> NextAsync(
        IGenericRepository<PerformanceImprovementPlan> plans, Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"PIP-{DateTime.UtcNow.Year}-";

        // Deleted drafts included: a deleted draft keeps its number, so a new plan does not reuse it.
        var used = await plans
            .GetQueryableIncludingDeleted(p => p.TenantId == tenantId && p.PipNumber.StartsWith(prefix))
            .Select(p => p.PipNumber)
            .ToListAsync(cancellationToken);

        var taken = new HashSet<string>(used, StringComparer.OrdinalIgnoreCase);
        for (var next = used.Count + 1; next <= used.Count + 1000; next++)
        {
            var candidate = $"{prefix}{next:D4}";
            if (!taken.Contains(candidate))
                return candidate;
        }

        // Unreachable in practice; a distinct fallback beats handing back a duplicate.
        return $"{prefix}{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
    }
}
