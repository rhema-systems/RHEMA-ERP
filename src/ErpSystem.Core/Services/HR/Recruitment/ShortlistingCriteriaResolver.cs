using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// Turns what a caller says a shortlisting criterion accepts into the rows the engine reads —
/// validating the type, refusing a mandatory protected characteristic, and resolving every
/// catalogue id against the tenant's live master so the label is mirrored rather than typed.
/// </summary>
/// <remarks>
/// <para><b>Round 4, lane B.</b> This lived inside <c>JobVacancyService</c>, where only a criterion
/// being SAVED against a vacancy could reach it. The talent-pool ad-hoc screen
/// (<c>POST api/talent-pool/screen</c>) asks the same question of criteria that are never stored —
/// "who in the pool would match this?" — and it must refuse the same nonsense the vacancy refuses:
/// a mandatory Gender, a numeric criterion with no bound, a list criterion with no values, an area
/// that is not on the tree. A second validator would answer some of those differently within a
/// release.</para>
///
/// <para>The rows it returns are unattached <see cref="JobShortlistingCriteriaValue"/> entities.
/// <c>JobVacancyService</c> persists them; the screen scores against them in memory and throws them
/// away.</para>
/// </remarks>
public interface IShortlistingCriteriaResolver
{
    /// <inheritdoc cref="ShortlistingCriteriaResolver.ResolveValuesAsync"/>
    Task<List<JobShortlistingCriteriaValue>> ResolveValuesAsync(
        JobShortlistingCriteriaType type, bool isMandatory, List<ShortlistingCriteriaValueInputDto>? inputs,
        string? legacyRequiredValue, decimal? minValue, decimal? maxValue,
        Guid? legacySkillId, Guid? legacyQualificationId, Guid tenantId);
}

public sealed class ShortlistingCriteriaResolver : IShortlistingCriteriaResolver
{
    private readonly IGenericRepository<Skill> _skillMasterRepository;
    private readonly IGenericRepository<Qualification> _qualificationMasterRepository;
    private readonly IGenericRepository<Certification> _certificationMasterRepository;
    private readonly IGenericRepository<Language> _languageMasterRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ShortlistingCriteriaResolver(
        IGenericRepository<Skill> skillMasterRepository,
        IGenericRepository<Qualification> qualificationMasterRepository,
        IGenericRepository<Certification> certificationMasterRepository,
        IGenericRepository<Language> languageMasterRepository,
        IUnitOfWork unitOfWork)
    {
        _skillMasterRepository = skillMasterRepository;
        _qualificationMasterRepository = qualificationMasterRepository;
        _certificationMasterRepository = certificationMasterRepository;
        _languageMasterRepository = languageMasterRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Round 3, lane K: the rules a criterion must satisfy before it is stored, and the accepted
    /// values resolved against their catalogues. Register row R-8 (values from the setups) and R-5
    /// fixes 2 and 8 (a blank value list passed everyone; a mandatory Other disqualified nobody),
    /// decision D-7 (Gender and Age may never be mandatory).
    /// </summary>
    /// <remarks>
    /// <para>The shape table (<see cref="ShortlistingCriteriaShapes"/>) is the single source of what a
    /// type needs. A catalogue id must be the tenant's live row and its name is mirrored into the
    /// label; a gender must be a member of the enum (or "Any"); text is trimmed. Duplicates collapse.
    /// A legacy caller that sends only the comma-separated <c>RequiredValue</c> (or a
    /// <c>RequiredSkillId</c> / <c>RequiredQualificationId</c>) gets the same rows derived from it,
    /// so the old shape keeps working and the new column fills for everyone.</para>
    /// </remarks>
    public async Task<List<JobShortlistingCriteriaValue>> ResolveValuesAsync(
        JobShortlistingCriteriaType type, bool isMandatory, List<ShortlistingCriteriaValueInputDto>? inputs,
        string? legacyRequiredValue, decimal? minValue, decimal? maxValue,
        Guid? legacySkillId, Guid? legacyQualificationId, Guid tenantId)
    {
        RequireScorableCriterion(type);
        var shape = ShortlistingCriteriaShapes.Of(type)!;

        if (isMandatory && !shape.AllowsMandatory)
            throw new InvalidOperationException(shape.MandatoryRefusal ?? $"A {shape.Label} criterion cannot be mandatory.");

        if (shape.IsNumeric)
        {
            if (minValue is null && maxValue is null)
                throw new InvalidOperationException($"A {shape.Label} criterion needs a minimum or a maximum — without a bound it measures nothing.");
            return new List<JobShortlistingCriteriaValue>();
        }

        var kind = shape.ValueKind ?? ShortlistingValueKind.Text;
        var candidates = new List<(Guid? Id, string? Label)>();
        if (inputs is not null)
        {
            candidates.AddRange(inputs.Select(i => (i.ReferenceId, i.Label)));
        }
        else
        {
            // Legacy shape: the comma-separated list, plus the single catalogue id the old columns carried.
            if (!string.IsNullOrWhiteSpace(legacyRequiredValue))
                candidates.AddRange(legacyRequiredValue
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(l => ((Guid?)null, (string?)l)));
            if (kind == ShortlistingValueKind.Skill && legacySkillId is { } sid) candidates.Add((sid, null));
            if (kind == ShortlistingValueKind.Qualification && legacyQualificationId is { } qid) candidates.Add((qid, null));
        }

        var master = kind switch
        {
            ShortlistingValueKind.Skill => (await _skillMasterRepository.FindAsync(x => x.TenantId == tenantId && !x.IsDeleted)).ToDictionary(x => x.Id, x => x.Name),
            ShortlistingValueKind.Qualification => (await _qualificationMasterRepository.FindAsync(x => x.TenantId == tenantId && !x.IsDeleted)).ToDictionary(x => x.Id, x => x.Name),
            ShortlistingValueKind.Certification => (await _certificationMasterRepository.FindAsync(x => x.TenantId == tenantId && !x.IsDeleted)).ToDictionary(x => x.Id, x => x.Name),
            ShortlistingValueKind.Language => (await _languageMasterRepository.FindAsync(x => x.TenantId == tenantId && !x.IsDeleted)).ToDictionary(x => x.Id, x => x.Name),
            // Round 4, lane A. Active areas only: a criterion may not be written against an area
            // that has been retired, though one written earlier keeps working — the evaluator tests
            // the stored id against the candidate's path and never re-reads the catalogue.
            ShortlistingValueKind.GeoArea => await _unitOfWork.Repository<ErpSystem.Core.Entities.Reference.GeoArea>()
                .GetQueryable()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.IsActive)
                .ToDictionaryAsync(a => a.Id, a => a.Name),
            // Round 4, lane Q. Active rungs only, on the same terms as an area: a criterion written
            // earlier against a rung since retired keeps working, because the evaluator reads the
            // rank from the whole ladder, retired rungs included.
            ShortlistingValueKind.QualificationLevel => await _unitOfWork.Repository<QualificationLevel>()
                .GetQueryable()
                .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.IsActive)
                .ToDictionaryAsync(l => l.Id, l => l.Name),
            _ => new Dictionary<Guid, string>(),
        };

        var rows = new List<JobShortlistingCriteriaValue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, rawLabel) in candidates)
        {
            Guid? referenceId = null;
            string label;
            if (id is { } key)
            {
                if (kind is ShortlistingValueKind.Text or ShortlistingValueKind.Gender)
                    throw new InvalidOperationException($"A {shape.Label} criterion takes typed values, not a catalogue id.");
                if (!master.TryGetValue(key, out var name))
                    throw new InvalidOperationException(kind switch
                    {
                        ShortlistingValueKind.GeoArea => "The area chosen is not on the tenant's active geography tree. Pick one from the cascade.",
                        ShortlistingValueKind.QualificationLevel => "The level chosen is not an active level on the qualification ladder. Pick one from the list.",
                        _ => $"The {shape.Label.ToLowerInvariant()} chosen is not in the catalogue. Pick one from the list.",
                    });
                referenceId = key;
                label = name;
            }
            else
            {
                label = rawLabel?.Trim() ?? string.Empty;
                if (label.Length == 0) continue;
                // Round 4, lane Q. A typed "Bachelor's" is exactly what the criterion stopped
                // matching on: the level is compared by RANK, so only a rung has one.
                if (kind == ShortlistingValueKind.QualificationLevel)
                    throw new InvalidOperationException(
                        "An Education level criterion takes a level from the qualification ladder, not typed text. Pick the minimum level.");
                if (kind == ShortlistingValueKind.Gender)
                {
                    if (label.Equals("any", StringComparison.OrdinalIgnoreCase)) label = "Any";
                    else if (Enum.TryParse<Gender>(label, ignoreCase: true, out var gender) && Enum.IsDefined(typeof(Gender), gender)) label = gender.ToString();
                    else throw new InvalidOperationException($"'{label}' is not a gender the register knows. Tick the genders the role is open to.");
                }
            }
            var dedupeKey = referenceId?.ToString() ?? $"text:{label}";
            if (!seen.Add(dedupeKey)) continue;
            rows.Add(new JobShortlistingCriteriaValue
            {
                TenantId = tenantId, Kind = kind, ReferenceId = referenceId, Label = label, SortOrder = rows.Count,
            });
        }

        if (rows.Count == 0 && shape.RequiresValues)
            throw new InvalidOperationException(
                $"A {shape.Label} criterion needs at least one accepted value — with none it passes every candidate and measures nothing.");

        // Round 4, lane Q: the first single-valued shape. "At least Bachelor's AND at least HND"
        // has no meaning, and the evaluator would silently read only the first.
        if (!shape.IsList && rows.Count > 1)
            throw new InvalidOperationException(
                $"A {shape.Label} criterion takes one value, the minimum. Remove the others.");
        return rows;
    }

    /// <summary>
    /// The label list mirrored onto the parent, so rows and readers that only know the text still
    /// agree. A LEGACY caller (no <c>values</c> on the payload) keeps its text exactly as sent — the
    /// rows are derived from it, the text is not rewritten from them; lane 5b asserts the echo, and a
    /// legacy single catalogue id must not append its name to what the caller typed.
    /// </summary>
    public static string? MirrorLabels(JobShortlistingCriteriaType type, List<JobShortlistingCriteriaValue> values, string? legacyRequiredValue, bool legacyShape)
    {
        var shape = ShortlistingCriteriaShapes.Of(type);
        if (shape?.IsNumeric == true || legacyShape) return string.IsNullOrWhiteSpace(legacyRequiredValue) ? null : legacyRequiredValue.Trim();
        if (values.Count == 0) return null;
        var joined = string.Join(", ", values.Select(v => v.Label));
        return joined.Length <= 500 ? joined : joined[..500];
    }

    /// <summary>
    /// Refuses a shortlisting criterion whose <see cref="JobShortlistingCriteriaType"/> is not a
    /// defined member.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>[Required]</c> does not catch this. The property is a non-nullable enum, so a payload
    /// that omits <c>type</c> binds to <c>0</c> and <c>RequiredAttribute</c> sees a value; the enum
    /// starts at <c>1</c>. A criterion stored with <c>0</c> reaches
    /// <c>JobApplicationService</c>'s scoring switch at its <c>default:</c> arm — *"Unknown / Other
    /// — default pass with neutral score"* — so it passes every candidate and discriminates
    /// between none of them. That is exactly what the only screen that creates criteria was doing
    /// (lane 5b; ledger § E2 finding 1), and a screen fix alone would leave the hole open to any
    /// other caller.
    /// </remarks>
    public static void RequireScorableCriterion(JobShortlistingCriteriaType type)
    {
        if (!Enum.IsDefined(typeof(JobShortlistingCriteriaType), type))
            throw new InvalidOperationException(
                $"'{(int)type}' is not a shortlisting criterion type. A criterion with no type is scored as "
                + "Unknown, which passes every candidate — choose what the criterion measures.");
    }
}
