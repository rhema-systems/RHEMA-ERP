using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>One rung of the tenant's qualification ladder, as the scoring engine reads it.</summary>
public readonly record struct QualificationRung(Guid Id, string Name, int Rank, bool IsActive);

/// <summary>
/// The tenant's qualification ladder (<see cref="QualificationLevel"/>), read once per scoring run
/// or per save — round 4, lane Q.
/// </summary>
/// <remarks>
/// ⚠ It holds RETIRED rungs too. A rung may be retired after criteria and qualifications name it,
/// and the engine must still know its rank; otherwise a criterion written last year stops being
/// answerable the day somebody tidies the ladder. Only an active rung may be CHOSEN — that is
/// <see cref="QualificationLevelRules.Resolve"/>'s rule, not this class's.
/// </remarks>
public sealed class QualificationLadder
{
    private readonly IReadOnlyDictionary<Guid, QualificationRung> _rungs;

    public QualificationLadder(IEnumerable<QualificationRung> rungs) =>
        _rungs = rungs.ToDictionary(r => r.Id);

    public static readonly QualificationLadder Empty = new(Array.Empty<QualificationRung>());

    public bool TryGet(Guid id, out QualificationRung rung) => _rungs.TryGetValue(id, out rung);

    /// <summary>Whether any rung can be chosen at all — a tenant may not have built a ladder.</summary>
    public bool HasActiveRungs => _rungs.Values.Any(r => r.IsActive);

    public static async Task<QualificationLadder> LoadAsync(
        IUnitOfWork unitOfWork, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rungs = await unitOfWork.Repository<QualificationLevel>().GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted)
            .Select(l => new QualificationRung(l.Id, l.Name, l.Rank, l.IsActive))
            .ToListAsync(cancellationToken);
        return new QualificationLadder(rungs);
    }
}

/// <summary>
/// The rules a candidate qualification's level must satisfy at both doors — HR's candidate record
/// and the careers profile (round 4, lane Q; decision Q-D1).
/// </summary>
public static class QualificationLevelRules
{
    /// <summary>
    /// Validates the level a candidate qualification states, and returns the one to store.
    /// </summary>
    /// <param name="type">The qualification's kind. Only Education must have a level.</param>
    /// <param name="stated">The rung the caller sent; null when none was stated.</param>
    /// <param name="catalogueLevelId">The catalogue entry's rung, when the qualification was picked from the catalogue.</param>
    /// <param name="current">The rung the row stores today, on an update; null for a new row.</param>
    /// <param name="ladder">The tenant's ladder, retired rungs included.</param>
    /// <param name="qualificationName">What the qualification is called, so a refusal names it.</param>
    /// <returns>
    /// The stated rung. Null leaves the level to follow the catalogue entry's, which is what makes a
    /// catalogue pick carry its level without the caller restating it.
    /// </returns>
    /// <remarks>
    /// <para>⚠ The Education requirement is on the EFFECTIVE level — stated, or else the catalogue
    /// entry's — so a catalogue pick that already sits on a rung needs nothing more.</para>
    /// <para>⚠ A tenant with no active rung has no level to give, so the requirement is waived
    /// there. Refusing every Education row for want of a choice that does not exist would lock
    /// candidates out of their own profile.</para>
    /// <para>A retired rung already on the row may stay — the same rule lane M set for a vendor
    /// picked before it was blacklisted — but it cannot be newly chosen.</para>
    /// </remarks>
    public static Guid? Resolve(
        QualificationType type, Guid? stated, Guid? catalogueLevelId, Guid? current,
        QualificationLadder ladder, string? qualificationName)
    {
        if (stated is { } id)
        {
            if (!ladder.TryGet(id, out var rung))
                throw new InvalidOperationException(
                    $"The level chosen for '{Named(qualificationName)}' is not on this organisation's qualification ladder. Pick one from the list.");
            if (!rung.IsActive && id != current)
                throw new InvalidOperationException(
                    $"'{rung.Name}' has been retired from the qualification ladder. Pick a current level for '{Named(qualificationName)}'.");
        }

        var effective = stated ?? catalogueLevelId;
        if (type == QualificationType.Education && effective is null && ladder.HasActiveRungs)
            throw new InvalidOperationException(
                $"Choose the level of '{Named(qualificationName)}': Bachelor's degree, Master's, HND and so on. "
                + "An education qualification needs its level, because shortlisting compares levels rather than names.");

        return stated;
    }

    private static string Named(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "this qualification" : name.Trim();
}
