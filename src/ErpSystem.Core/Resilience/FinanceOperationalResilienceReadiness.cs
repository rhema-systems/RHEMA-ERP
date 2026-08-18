namespace ErpSystem.Core.Resilience;

/// <summary>
/// Evidence categories required before Finance operational-resilience readiness can be signed off.
/// These categories deliberately include both application-owned controls and infrastructure-owned
/// exercises so a technically correct Finance build cannot be mistaken for a completed DR rehearsal.
/// </summary>
public enum FinanceOperationalEvidenceType
{
    AvailabilityProbe,
    DatabaseReadiness,
    BackupFreshness,
    RestoreDrill,
    PostingRetry,
    SupportDiagnostics
}

/// <summary>
/// One retained operational check. Reference must point to durable evidence such as a monitoring
/// run, restore-drill record, incident ticket, or signed UAT pack rather than an informal assertion.
/// </summary>
public sealed record FinanceOperationalEvidence(
    FinanceOperationalEvidenceType Type,
    bool Passed,
    DateTimeOffset ObservedAt,
    string Reference);

public sealed record FinanceOperationalEvidenceAssessment(
    FinanceOperationalEvidenceType Type,
    string Status,
    DateTimeOffset? ObservedAt,
    TimeSpan MaximumAge,
    string? Reference,
    string Message);

public sealed record FinanceOperationalReadinessAssessment(
    DateTimeOffset AssessedAt,
    bool Passed,
    IReadOnlyList<FinanceOperationalEvidenceAssessment> Evidence);

/// <summary>
/// Deterministic release-evidence policy for TDC Finance operational resilience.
/// The age windows are conservative initial release gates, not contractual TDC RPO/RTO or SLA
/// commitments. IT and Management must approve the final service targets before production sign-off.
/// </summary>
public static class FinanceOperationalResilienceReadinessPolicy
{
    private static readonly TimeSpan FutureClockTolerance = TimeSpan.FromMinutes(5);

    public static IReadOnlyDictionary<FinanceOperationalEvidenceType, TimeSpan> MaximumEvidenceAge { get; }
        = new Dictionary<FinanceOperationalEvidenceType, TimeSpan>
        {
            [FinanceOperationalEvidenceType.AvailabilityProbe] = TimeSpan.FromHours(1),
            [FinanceOperationalEvidenceType.DatabaseReadiness] = TimeSpan.FromHours(1),
            [FinanceOperationalEvidenceType.BackupFreshness] = TimeSpan.FromHours(26),
            [FinanceOperationalEvidenceType.RestoreDrill] = TimeSpan.FromDays(95),
            [FinanceOperationalEvidenceType.PostingRetry] = TimeSpan.FromDays(31),
            [FinanceOperationalEvidenceType.SupportDiagnostics] = TimeSpan.FromDays(31)
        };

    public static FinanceOperationalReadinessAssessment Assess(
        IReadOnlyCollection<FinanceOperationalEvidence> evidence,
        DateTimeOffset assessedAt)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var results = new List<FinanceOperationalEvidenceAssessment>();
        foreach (var requirement in MaximumEvidenceAge)
        {
            // Multiple runs may be retained in one pack. Only the latest run determines current
            // readiness, while the older records remain useful trend and incident evidence.
            var latest = evidence
                .Where(item => item.Type == requirement.Key)
                .OrderByDescending(item => item.ObservedAt)
                .FirstOrDefault();

            if (latest is null)
            {
                results.Add(new(
                    requirement.Key,
                    "Missing",
                    null,
                    requirement.Value,
                    null,
                    "No retained evidence was supplied."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(latest.Reference))
            {
                results.Add(new(
                    requirement.Key,
                    "Invalid",
                    latest.ObservedAt,
                    requirement.Value,
                    null,
                    "The check has no durable evidence reference."));
                continue;
            }

            if (latest.ObservedAt > assessedAt + FutureClockTolerance)
            {
                results.Add(new(
                    requirement.Key,
                    "Invalid",
                    latest.ObservedAt,
                    requirement.Value,
                    latest.Reference.Trim(),
                    "The evidence timestamp is later than the allowed clock tolerance."));
                continue;
            }

            if (!latest.Passed)
            {
                results.Add(new(
                    requirement.Key,
                    "Failed",
                    latest.ObservedAt,
                    requirement.Value,
                    latest.Reference.Trim(),
                    "The latest retained check failed."));
                continue;
            }

            if (assessedAt - latest.ObservedAt > requirement.Value)
            {
                results.Add(new(
                    requirement.Key,
                    "Stale",
                    latest.ObservedAt,
                    requirement.Value,
                    latest.Reference.Trim(),
                    "The latest successful check is older than the release-evidence window."));
                continue;
            }

            results.Add(new(
                requirement.Key,
                "Passed",
                latest.ObservedAt,
                requirement.Value,
                latest.Reference.Trim(),
                "Current successful evidence is retained."));
        }

        return new FinanceOperationalReadinessAssessment(
            assessedAt,
            results.All(item => item.Status == "Passed"),
            results);
    }
}
