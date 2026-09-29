using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// PNDCL 187's First and Third Schedules, as a tenant's starting compensation schedule (round 5, lane
/// K-II-b).
/// </summary>
/// <remarks>
/// <para>⚠ <b>Copied from <c>docs/HR/catalogues/HR-WORKMENS-COMPENSATION-SCHEDULES.md</c>, which was read
/// against the Act's primary text</b> (Parliament's revised edition, K-II-0). Change a figure there
/// first, with its source, then here. A secondary copy was wrong in three places — do not refresh this
/// list from one.</para>
///
/// <para>These are DEFAULTS. Loading them adds only the rows a tenant does not already have (matched
/// on schedule and injury), so an administrator's edits survive a second load.</para>
///
/// <para><c>ArmOrHand</c> marks the rows the Third Schedule's dominance rule applies to — an injury to the
/// arm or hand the employee does not favour is rated at ninety percent. The rows for both hands, all
/// fingers and thumbs, or a one-armed employee's remaining arm are not marked: they are total either
/// way.</para>
/// </remarks>
public static class IncapacityScheduleDefaults
{
    public const string FirstScheduleSource = "PNDCL 187, First Schedule (s.8)";
    public const string ThirdScheduleSource = "PNDCL 187, Third Schedule (s.6)";

    public static readonly IReadOnlyList<(IncapacityScheduleKind Kind, string Injury, decimal Percentage, bool ArmOrHand)> Rows =
        new (IncapacityScheduleKind, string, decimal, bool)[]
        {
            // ── First Schedule — disfiguring injuries (s.8): the most that may be assessed ──
            (IncapacityScheduleKind.Disfigurement, "Mutilation or amputation of one ear", 15m, false),
            (IncapacityScheduleKind.Disfigurement, "Deformity of the hand through the loss of all the three phalanges of a finger and the metacarpals of the hand", 20m, false),
            (IncapacityScheduleKind.Disfigurement, "Mutilation or amputation of nose", 30m, false),
            (IncapacityScheduleKind.Disfigurement, "Conspicuous deformity of face generally", 50m, false),
            (IncapacityScheduleKind.Disfigurement, "Conspicuous deformity of external appearance generally, other than face", 40m, false),
            (IncapacityScheduleKind.Disfigurement, "Functional loss of genital organs", 85m, false),

            // ── Third Schedule — incapacity (s.6) ──
            (IncapacityScheduleKind.Incapacity, "Loss of two limbs", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of both hands or of all fingers and thumbs", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of both feet", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Total loss of sight", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Total paralysis", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Injuries resulting in being permanently bed-ridden", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Any other injury causing permanent total disablement", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of arm at shoulder", 100m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of arm between elbow and shoulder", 80m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of arm at elbow", 70m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of arm between wrist and elbow", 70m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of four fingers and thumb of one hand", 70m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of four fingers of one hand", 50m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of thumb — both phalanges", 35m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of thumb — phalanx", 10m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of index finger — three phalanges", 15m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of index finger — two phalanges", 10m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of index finger — one phalanx", 6m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of middle finger — three phalanges", 10m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of middle finger — two phalanges", 6m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of middle finger — one phalanx", 4m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of ring finger — three phalanges", 6m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of ring finger — two phalanges", 5m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of ring finger — one phalanx", 3m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of little finger — three phalanges", 5m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of little finger — two phalanges", 4m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of little finger — one phalanx", 3m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of metacarpals — first or second (additional)", 4m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of metacarpals — third, fourth or fifth (additional)", 3m, true),
            (IncapacityScheduleKind.Incapacity, "Loss of leg — at or above knee", 75m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of leg — below knee", 60m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of foot", 40m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of toes — all on one foot", 20m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of toe — great, both phalanges", 10m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of toe — great, one phalanx", 3m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of toe — other than great", 2m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of sight — of one eye", 40m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of hearing of one ear", 15m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of remaining eye by one-eyed employee", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Total loss of hearing", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of remaining arm by one-armed employee", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of remaining leg by one-legged employee", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of mental capacity", 100m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of upper or lower central incisor", 3m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of upper or lower incisor", 2m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of upper or lower canine", 2m, false),
            (IncapacityScheduleKind.Incapacity, "Loss of any one posterior tooth, that is to say, premolar or molar", 1m, false),
            (IncapacityScheduleKind.Incapacity, "Fracture of upper or lower jaw", 25m, false),
        };

    public static string SourceFor(IncapacityScheduleKind kind) =>
        kind == IncapacityScheduleKind.Disfigurement ? FirstScheduleSource : ThirdScheduleSource;
}
