namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Outcome of a single projection pass.
/// </summary>
public sealed record SalaryStructureProjectionResult
{
    public bool SkippedAsUnchanged { get; init; }

    public int GradesCreated { get; init; }
    public int GradesUpdated { get; init; }
    public int GradesDeactivated { get; init; }

    public int LevelsCreated { get; init; }
    public int LevelsUpdated { get; init; }

    public int NotchesCreated { get; init; }
    public int NotchesUpdated { get; init; }
    public int NotchesDeactivated { get; init; }

    /// <summary>
    /// Payroll grades that could not be projected (for example, duplicate grade codes within the
    /// tenant). Surfaced rather than silently mangled — the fix belongs in payroll's own setup data.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public bool ChangedAnything =>
        GradesCreated + GradesUpdated + GradesDeactivated +
        LevelsCreated + LevelsUpdated +
        NotchesCreated + NotchesUpdated + NotchesDeactivated > 0;
}

/// <summary>
/// Projects the payroll-owned salary structure (<c>PayrollGrade</c> / <c>PayrollGradeNotch</c>) into
/// the HR-owned <c>SalaryGrade</c> / <c>SalaryLevel</c> / <c>SalaryNotch</c> tables.
///
/// Payroll is the single source of truth for salary structure. HR does not define its own grades; it
/// mirrors payroll's so that the many HR foreign keys (positions, salary assignments, staff movements,
/// recruitment offers, benefit policies, job architecture) keep pointing at rows HR owns while the
/// figures behind them come from payroll.
///
/// The payroll module belongs to another developer and is never modified. That means the sync cannot be
/// pushed from payroll's save path — it is pulled by HR, either lazily on read
/// (<see cref="EnsureCurrentAsync"/>) or explicitly (<see cref="ReconcileAsync"/>). Payroll tables are
/// read-only inputs here.
/// </summary>
public interface ISalaryStructureProjectionService
{
    /// <summary>
    /// Runs a full, idempotent projection pass for the tenant.
    /// Safe to call repeatedly: rows are matched on their payroll code and updated in place, so existing
    /// HR foreign keys are never orphaned. Grades and notches that no longer exist in payroll are
    /// deactivated, never deleted.
    /// </summary>
    Task<SalaryStructureProjectionResult> ReconcileAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cheap guard used on HR read paths. Compares a fingerprint of the payroll side against the last
    /// projected fingerprint and short-circuits when nothing has changed; otherwise delegates to
    /// <see cref="ReconcileAsync"/>.
    /// </summary>
    Task<SalaryStructureProjectionResult> EnsureCurrentAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
