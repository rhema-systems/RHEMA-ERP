namespace ErpSystem.Core.Performance;

/// <summary>
/// Finance workloads covered by the TDC performance-readiness release gate.
/// Keep this list focused on business journeys rather than individual SQL statements so the
/// measurements remain meaningful when an implementation is refactored.
/// </summary>
public enum FinancePerformanceWorkload
{
    Posting,
    JournalInquiry,
    TrialBalance,
    AccountsPayableAging,
    AccountsReceivableAging,
    BankReconciliation,
    PeriodClose
}

/// <summary>
/// Describes the minimum representative tenant volume used for Finance performance assurance.
/// These values are test-data targets, not hard limits on what TDC may store in production.
/// </summary>
public sealed record FinancePerformanceVolumeProfile(
    string Name,
    int AccountCount,
    int JournalEntryCount,
    int LedgerLineCount,
    int AccountsPayableDocumentCount,
    int AccountsReceivableDocumentCount,
    int BankStatementLineCount,
    int PeriodCloseTaskCount)
{
    /// <summary>
    /// A fast developer profile for checking query shape before opening a pull request.
    /// </summary>
    public static FinancePerformanceVolumeProfile DeveloperSmoke { get; } = new(
        "DeveloperSmoke",
        AccountCount: 100,
        JournalEntryCount: 1_000,
        LedgerLineCount: 2_000,
        AccountsPayableDocumentCount: 500,
        AccountsReceivableDocumentCount: 500,
        BankStatementLineCount: 1_000,
        PeriodCloseTaskCount: 100);

    /// <summary>
    /// The initial TDC UAT baseline. It is deliberately substantial enough to reveal table scans,
    /// N+1 queries and unbounded reads while remaining repeatable on the agreed UAT environment.
    /// Re-baseline these numbers from measured production growth before go-live capacity sign-off.
    /// </summary>
    public static FinancePerformanceVolumeProfile TdcRepresentative { get; } = new(
        "TdcRepresentative",
        AccountCount: 1_000,
        JournalEntryCount: 50_000,
        LedgerLineCount: 100_000,
        AccountsPayableDocumentCount: 25_000,
        AccountsReceivableDocumentCount: 25_000,
        BankStatementLineCount: 50_000,
        PeriodCloseTaskCount: 500);
}

/// <summary>
/// A workload's release-gate budget. Query count protects query shape and p95 protects elapsed
/// database time. The figures are regression thresholds for a controlled environment, not a
/// contractual end-user SLA because network, browser and infrastructure time are outside them.
/// </summary>
public sealed record FinancePerformanceThreshold(int MaximumQueryCount, double MaximumP95Milliseconds);

/// <summary>
/// One captured execution of a Finance workload.
/// </summary>
public sealed record FinancePerformanceSample(int QueryCount, double DatabaseElapsedMilliseconds);

/// <summary>
/// Result returned by the deterministic release-gate evaluator.
/// </summary>
public sealed record FinancePerformanceAssessment(
    FinancePerformanceWorkload Workload,
    int MaximumObservedQueryCount,
    double P95DatabaseElapsedMilliseconds,
    FinancePerformanceThreshold Threshold,
    bool Passed);

/// <summary>
/// Central policy used by automated tests and UAT performance runs. Keeping the budgets in one
/// reviewed location prevents individual tests from silently weakening their own acceptance bar.
/// </summary>
public static class FinancePerformanceReadinessPolicy
{
    public static IReadOnlyDictionary<FinancePerformanceWorkload, FinancePerformanceThreshold> Thresholds { get; }
        = new Dictionary<FinancePerformanceWorkload, FinancePerformanceThreshold>
        {
            [FinancePerformanceWorkload.Posting] = new(12, 750),
            [FinancePerformanceWorkload.JournalInquiry] = new(3, 500),
            [FinancePerformanceWorkload.TrialBalance] = new(7, 1_500),
            [FinancePerformanceWorkload.AccountsPayableAging] = new(4, 1_000),
            [FinancePerformanceWorkload.AccountsReceivableAging] = new(4, 1_000),
            [FinancePerformanceWorkload.BankReconciliation] = new(6, 1_500),
            [FinancePerformanceWorkload.PeriodClose] = new(12, 2_000)
        };

    public static FinancePerformanceAssessment Assess(
        FinancePerformanceWorkload workload,
        IReadOnlyCollection<FinancePerformanceSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
        {
            throw new ArgumentException("At least one performance sample is required.", nameof(samples));
        }

        if (!Thresholds.TryGetValue(workload, out var threshold))
        {
            throw new ArgumentOutOfRangeException(nameof(workload), workload, "No Finance performance threshold is configured.");
        }

        var maximumQueryCount = samples.Max(sample => sample.QueryCount);
        var orderedElapsedTimes = samples
            .Select(sample => sample.DatabaseElapsedMilliseconds)
            .OrderBy(value => value)
            .ToArray();

        // Nearest-rank is intentionally used here: it is deterministic for small release-gate
        // sample sets and never interpolates a value that was not actually observed.
        var percentileIndex = Math.Max(0, (int)Math.Ceiling(orderedElapsedTimes.Length * 0.95) - 1);
        var p95 = orderedElapsedTimes[percentileIndex];

        return new FinancePerformanceAssessment(
            workload,
            maximumQueryCount,
            p95,
            threshold,
            maximumQueryCount <= threshold.MaximumQueryCount && p95 <= threshold.MaximumP95Milliseconds);
    }
}
