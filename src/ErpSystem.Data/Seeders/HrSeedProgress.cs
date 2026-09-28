namespace ErpSystem.Data.Seeders;

/// <summary>
/// Where one HR seed step stands, read from its "already seeded?" probe without running it — what
/// the Developer Test Data screen lists under each tier.
/// </summary>
/// <param name="AlwaysRuns">
/// The step has no skip probe because it is a reconciliation, safe to repeat (number sequences).
/// It is never "present"; it simply runs on every pass.
/// </param>
public sealed record HrSeedStepState(string Name, bool Present, bool AlwaysRuns = false);

/// <summary>What happened to one HR seed step on a run.</summary>
public sealed record HrSeedStepOutcome(string Name, HrSeedStepResult Result, string? Error = null);

public enum HrSeedStepResult
{
    Ran,
    Skipped,
    Failed,
}
