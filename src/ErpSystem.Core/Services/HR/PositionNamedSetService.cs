using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// What a position ACTUALLY requires: its attached named sets unioned with its individual rows,
/// and the one rule that governs the two together (demo feedback round 2, lane C3, plan § 6.4).
/// </summary>
/// <remarks>
/// <para><b>⚠ THE EFFECTIVE READ IS THE POINT, NOT AN EXTRA.</b> Attaching a set writes an
/// attachment row and nothing else — no benefit row, no skill requirement, no certification
/// requirement is materialised. So every existing consumer that read the individual table directly
/// would have gone on seeing only the individual rows, and a benefit attached through a group would
/// have enrolled nobody, a skill through a set would have matched no succession candidate, a
/// credential through a set would have appeared in no compliance report. The feature would have
/// looked finished and done nothing. The three <c>GetEffective…Async</c> reads below are what those
/// consumers now call.</para>
///
/// <para><b>Why virtual and not materialised.</b> Writing the member rows out onto the position at
/// attach time would have kept every consumer working untouched — but § 6.4.2 rule 6 allows a set's
/// membership to be edited after positions have attached it, and materialised copies would then be
/// stale everywhere until something rewrote them. The union is computed on read so that editing a
/// set is immediately true of every post that holds it.</para>
///
/// <para><b>Tenancy.</b> The tenant is taken from the position row itself rather than from the
/// current user, because two callers (the benefit enrolment reconciliation and the expiry sweeps)
/// can run without a user. Pass <c>expectedTenantId</c> from a request-scoped caller and a position
/// belonging to another tenant is refused rather than read.</para>
/// </remarks>
public interface IPositionNamedSetService
{
    Task<IReadOnlyList<EffectiveBenefitDto>> GetEffectiveBenefitsAsync(Guid positionId, Guid? expectedTenantId = null, CancellationToken ct = default);
    Task<IReadOnlyList<EffectiveSkillDto>> GetEffectiveSkillsAsync(Guid positionId, Guid? expectedTenantId = null, CancellationToken ct = default);
    Task<IReadOnlyList<EffectiveCertificationDto>> GetEffectiveCertificationsAsync(Guid positionId, Guid? expectedTenantId = null, CancellationToken ct = default);

    /// <summary>The three attachment lists for the position read.</summary>
    Task<PositionAttachedSets> GetAttachedAsync(Guid positionId, Guid? expectedTenantId = null, CancellationToken ct = default);

    /// <summary>
    /// The rule of § 6.4.2, for all three kinds at once, BEFORE anything is written. A null set
    /// list means "not carried by this save", and the sets already attached are used instead.
    /// </summary>
    Task ValidateAsync(Guid tenantId, Guid? positionId, NamedSetRuleInput input, CancellationToken ct = default);

    /// <summary>
    /// How many DISTINCT credentials a post would get from its certification sets — the ones the
    /// save carries, or the ones already attached when it carries none.
    /// </summary>
    /// <remarks>
    /// ⚠ Exists because lane C2's rule — a post whose certification switch is on must name at least
    /// one credential — counted the INDIVIDUAL requirement rows only. Once a set could provide
    /// them, a post whose credentials came entirely from a set was refused for naming nothing while
    /// naming a whole regulatory bundle. Found by the demo scenario, not by the harness.
    /// </remarks>
    Task<int> CountCredentialsFromSetsAsync(Guid tenantId, Guid? positionId, IReadOnlyList<Guid>? desiredSetIds, CancellationToken ct = default);

    /// <summary>
    /// Replace-set of the three attachment tables. Null leaves that kind as it is; an empty list
    /// detaches every set of that kind.
    /// </summary>
    Task SyncAsync(EmployeePosition position, ICollection<Guid>? benefitGroupIds, ICollection<Guid>? skillSetIds,
                   ICollection<Guid>? certificationSetIds, CancellationToken ct = default);
}

/// <summary>The three attachment lists, so one call fills the position DTO.</summary>
public sealed class PositionAttachedSets
{
    public List<AttachedSetDto> BenefitGroups { get; set; } = new();
    public List<AttachedSetDto> SkillSets { get; set; } = new();
    public List<AttachedSetDto> CertificationSets { get; set; } = new();
}

/// <summary>
/// What a position save is asking for. Duplicates are deliberately PRESERVED in these lists —
/// refusing a duplicate is rule 5, and a list that had already been deduplicated could not.
/// </summary>
public sealed record NamedSetRuleInput(
    IReadOnlyList<Guid>? BenefitPolicyIds,
    IReadOnlyList<Guid>? BenefitGroupIds,
    IReadOnlyList<Guid>? SkillIds,
    IReadOnlyList<Guid>? SkillSetIds,
    IReadOnlyList<Guid>? CertificationIds,
    IReadOnlyList<Guid>? CertificationSetIds);

public class PositionNamedSetService : IPositionNamedSetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<PositionNamedSetService> _logger;

    public PositionNamedSetService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUser, ILogger<PositionNamedSetService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Tenancy
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    private async Task<Guid> TenantOfPositionAsync(Guid positionId, Guid? expectedTenantId, CancellationToken ct)
    {
        var row = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
                      .AsNoTracking()
                      .Where(p => p.Id == positionId && !p.IsDeleted)
                      .Select(p => new { p.TenantId })
                      .FirstOrDefaultAsync(ct)
                  ?? throw new ArgumentException($"Position with ID '{positionId}' not found.");

        if (expectedTenantId.HasValue && expectedTenantId.Value != Guid.Empty && row.TenantId != expectedTenantId.Value)
            throw new ArgumentException($"Position with ID '{positionId}' not found.");

        return row.TenantId;
    }

    private static EffectiveSourceDto Individual() =>
        new() { Kind = "Individual", Label = "Individual" };

    private static EffectiveSourceDto FromSet(Guid id, string name, string? code) =>
        new() { Kind = "Set", SetId = id, SetName = name, SetCode = code, Label = $"Set: {code ?? name}" };

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Effective benefits
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<EffectiveBenefitDto>> GetEffectiveBenefitsAsync(
        Guid positionId, Guid? expectedTenantId = null, CancellationToken ct = default)
    {
        var tenantId = await TenantOfPositionAsync(positionId, expectedTenantId, ct);

        var individuals = await _unitOfWork.Repository<EmployeePositionBenefit>().GetQueryable()
            .AsNoTracking()
            .Include(x => x.BenefitPolicy)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.PositionId == positionId)
            .ToListAsync(ct);

        // A retired GROUP still contributes to a post that already holds it — retiring stops it
        // being offered, it does not silently strip entitlements from people.
        var groups = await _unitOfWork.Repository<EmployeePositionBenefitGroup>().GetQueryable()
            .AsNoTracking()
            .Include(a => a.BenefitGroup).ThenInclude(g => g.Members.Where(m => !m.IsDeleted)).ThenInclude(m => m.Policy)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == positionId)
            .ToListAsync(ct);

        var byPolicy = new Dictionary<Guid, EffectiveBenefitDto>();

        foreach (var row in individuals)
        {
            var line = Line(row.PolicyId, row.BenefitPolicy?.PolicyName, row.BenefitPolicy?.PolicyCode, row.BenefitPolicy?.IsActive ?? false);
            line.PositionAmount = row.PositionAmount;
            line.ExpiryDate = row.ExpiryDate;
            line.PositionBenefitId = row.Id;
            line.Sources.Add(Individual());
        }

        foreach (var attachment in groups)
        {
            var group = attachment.BenefitGroup;
            foreach (var member in group.Members)
            {
                var line = Line(member.PolicyId, member.Policy?.PolicyName, member.Policy?.PolicyCode, member.Policy?.IsActive ?? false);
                line.Sources.Add(FromSet(group.Id, group.Name, group.Code));
            }
        }

        return byPolicy.Values.OrderBy(x => x.PolicyName).ToList();

        EffectiveBenefitDto Line(Guid policyId, string? name, string? code, bool active)
        {
            if (!byPolicy.TryGetValue(policyId, out var line))
            {
                line = new EffectiveBenefitDto
                {
                    PolicyId = policyId,
                    PolicyName = name ?? string.Empty,
                    PolicyCode = code,
                    PolicyIsActive = active,
                };
                byPolicy[policyId] = line;
            }
            return line;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Effective skills
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<EffectiveSkillDto>> GetEffectiveSkillsAsync(
        Guid positionId, Guid? expectedTenantId = null, CancellationToken ct = default)
    {
        var tenantId = await TenantOfPositionAsync(positionId, expectedTenantId, ct);

        var individuals = await _unitOfWork.Repository<PositionSkillRequirement>().GetQueryable()
            .AsNoTracking()
            .Include(x => x.Skill)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.PositionId == positionId)
            .ToListAsync(ct);

        var sets = await _unitOfWork.Repository<PositionSkillSet>().GetQueryable()
            .AsNoTracking()
            .Include(a => a.SkillSet).ThenInclude(s => s.Members.Where(m => !m.IsDeleted)).ThenInclude(m => m.Skill)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == positionId)
            .ToListAsync(ct);

        var bySkill = new Dictionary<Guid, EffectiveSkillDto>();

        foreach (var row in individuals)
        {
            var line = Line(row.SkillId, row.Skill?.Name, row.Skill?.Category, row.Skill?.RequiresCertification ?? false);
            Strengthen(line, row.RequiredLevel, row.IsRequired, row.Priority);
            line.PositionSkillRequirementId = row.Id;
            line.Sources.Add(Individual());
        }

        foreach (var attachment in sets)
        {
            var set = attachment.SkillSet;
            foreach (var member in set.Members)
            {
                var line = Line(member.SkillId, member.Skill?.Name, member.Skill?.Category, member.Skill?.RequiresCertification ?? false);
                Strengthen(line, member.RequiredLevel, member.IsRequired, member.Priority);
                line.Sources.Add(FromSet(set.Id, set.Name, set.Code));
            }
        }

        return bySkill.Values
            .OrderByDescending(x => x.Priority).ThenBy(x => x.SkillName)
            .ToList();

        EffectiveSkillDto Line(Guid skillId, string? name, string? category, bool requiresCertification)
        {
            if (!bySkill.TryGetValue(skillId, out var line))
            {
                line = new EffectiveSkillDto
                {
                    SkillId = skillId,
                    SkillName = name ?? string.Empty,
                    SkillCategory = category,
                    RequiresCertification = requiresCertification,
                    RequiredLevel = SkillLevel.Beginner,
                    IsRequired = false,
                    Priority = 0,
                };
                bySkill[skillId] = line;
            }
            return line;
        }

        // ⚠ Where two sources ask for the same skill differently, the STRONGEST wins. A post does
        // not need a skill less because a second set asked for less of it, and "preferred" in one
        // set cannot demote "required" in another.
        static void Strengthen(EffectiveSkillDto line, SkillLevel level, bool isRequired, int priority)
        {
            if (level > line.RequiredLevel) line.RequiredLevel = level;
            if (isRequired) line.IsRequired = true;
            if (priority > line.Priority) line.Priority = priority;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Effective certifications
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<EffectiveCertificationDto>> GetEffectiveCertificationsAsync(
        Guid positionId, Guid? expectedTenantId = null, CancellationToken ct = default)
    {
        var tenantId = await TenantOfPositionAsync(positionId, expectedTenantId, ct);

        var individuals = await _unitOfWork.Repository<PositionCertificationRequirement>().GetQueryable()
            .AsNoTracking()
            .Include(x => x.Certification).ThenInclude(c => c.CertifyingBody)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.PositionId == positionId)
            .ToListAsync(ct);

        var sets = await _unitOfWork.Repository<PositionCertificationSet>().GetQueryable()
            .AsNoTracking()
            .Include(a => a.CertificationSet).ThenInclude(s => s.Members.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.Certification).ThenInclude(c => c.CertifyingBody)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == positionId)
            .ToListAsync(ct);

        var byCert = new Dictionary<Guid, EffectiveCertificationDto>();

        foreach (var row in individuals)
        {
            var line = Line(row.CertificationId, row.Certification);
            if (row.IsMandatory) line.IsMandatory = true;
            line.PositionCertificationRequirementId = row.Id;
            line.Sources.Add(Individual());
        }

        foreach (var attachment in sets)
        {
            var set = attachment.CertificationSet;
            foreach (var member in set.Members)
            {
                var line = Line(member.CertificationId, member.Certification);
                if (member.IsMandatory) line.IsMandatory = true;
                line.Sources.Add(FromSet(set.Id, set.Name, set.Code));
            }
        }

        return byCert.Values.OrderBy(x => x.CertificationName).ToList();

        EffectiveCertificationDto Line(Guid certificationId, Certification? cert)
        {
            if (!byCert.TryGetValue(certificationId, out var line))
            {
                line = new EffectiveCertificationDto
                {
                    CertificationId = certificationId,
                    CertificationName = cert?.Name ?? string.Empty,
                    CertificationCode = cert?.Code,
                    CertifyingBodyName = cert?.CertifyingBody?.Name ?? string.Empty,
                    CertificationIsActive = cert?.IsActive ?? false,
                    IsMandatory = false,
                };
                byCert[certificationId] = line;
            }
            return line;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Attachments
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<PositionAttachedSets> GetAttachedAsync(Guid positionId, Guid? expectedTenantId = null, CancellationToken ct = default)
    {
        var tenantId = await TenantOfPositionAsync(positionId, expectedTenantId, ct);

        var benefits = await _unitOfWork.Repository<EmployeePositionBenefitGroup>().GetQueryable()
            .AsNoTracking()
            .Include(a => a.BenefitGroup)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == positionId)
            .Select(a => new AttachedSetDto
            {
                Id = a.Id,
                SetId = a.BenefitGroupId,
                Name = a.BenefitGroup.Name,
                Code = a.BenefitGroup.Code,
                IsActive = a.BenefitGroup.IsActive,
                MemberCount = a.BenefitGroup.Members.Count(m => !m.IsDeleted),
            })
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        var skills = await _unitOfWork.Repository<PositionSkillSet>().GetQueryable()
            .AsNoTracking()
            .Include(a => a.SkillSet)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == positionId)
            .Select(a => new AttachedSetDto
            {
                Id = a.Id,
                SetId = a.SkillSetId,
                Name = a.SkillSet.Name,
                Code = a.SkillSet.Code,
                IsActive = a.SkillSet.IsActive,
                MemberCount = a.SkillSet.Members.Count(m => !m.IsDeleted),
            })
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        var certifications = await _unitOfWork.Repository<PositionCertificationSet>().GetQueryable()
            .AsNoTracking()
            .Include(a => a.CertificationSet)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == positionId)
            .Select(a => new AttachedSetDto
            {
                Id = a.Id,
                SetId = a.CertificationSetId,
                Name = a.CertificationSet.Name,
                Code = a.CertificationSet.Code,
                IsActive = a.CertificationSet.IsActive,
                MemberCount = a.CertificationSet.Members.Count(m => !m.IsDeleted),
            })
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return new PositionAttachedSets { BenefitGroups = benefits, SkillSets = skills, CertificationSets = certifications };
    }

    public async Task<int> CountCredentialsFromSetsAsync(
        Guid tenantId, Guid? positionId, IReadOnlyList<Guid>? desiredSetIds, CancellationToken ct = default)
    {
        var setIds = desiredSetIds?.Distinct().ToList();
        if (setIds is null)
        {
            if (positionId is not { } pid) return 0;
            setIds = await _unitOfWork.Repository<PositionCertificationSet>().GetQueryable()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == pid)
                .Select(a => a.CertificationSetId)
                .ToListAsync(ct);
        }

        if (setIds.Count == 0) return 0;

        return await _unitOfWork.Repository<CertificationSetMember>().GetQueryable()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && setIds.Contains(m.CertificationSetId))
            .Select(m => m.CertificationId)
            .Distinct()
            .CountAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  THE RULE (§ 6.4.2), stated once for all three kinds
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task ValidateAsync(Guid tenantId, Guid? positionId, NamedSetRuleInput input, CancellationToken ct = default)
    {
        // ── Benefits ────────────────────────────────────────────────────────────────────────
        await OneKindAsync(
            noun: "benefit",
            setNoun: "benefit group",
            individualIds: input.BenefitPolicyIds,
            setIds: input.BenefitGroupIds,
            currentSetIdsAsync: async () => positionId is not { } pid
                ? new List<Guid>()
                : await _unitOfWork.Repository<EmployeePositionBenefitGroup>().GetQueryable()
                    .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == pid)
                    .Select(a => a.BenefitGroupId).ToListAsync(ct),
            setNamesAsync: async ids => await _unitOfWork.Repository<BenefitGroup>().GetQueryable()
                .Where(g => g.TenantId == tenantId && !g.IsDeleted && ids.Contains(g.Id))
                .Select(g => new SetHead(g.Id, g.Name, g.IsActive)).ToListAsync(ct),
            membersAsync: async ids => await _unitOfWork.Repository<BenefitGroupMember>().GetQueryable()
                .Where(m => m.TenantId == tenantId && !m.IsDeleted && ids.Contains(m.BenefitGroupId))
                .Select(m => new SetMemberRef(m.BenefitGroupId, m.PolicyId)).ToListAsync(ct),
            itemNamesAsync: async ids => await _unitOfWork.Repository<BenefitPolicy>().GetQueryable()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted && ids.Contains(p.Id))
                .Select(p => new ItemHead(p.Id, p.PolicyName)).ToListAsync(ct));

        // ── Skills ──────────────────────────────────────────────────────────────────────────
        await OneKindAsync(
            noun: "skill",
            setNoun: "skill set",
            individualIds: input.SkillIds,
            setIds: input.SkillSetIds,
            currentSetIdsAsync: async () => positionId is not { } pid
                ? new List<Guid>()
                : await _unitOfWork.Repository<PositionSkillSet>().GetQueryable()
                    .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == pid)
                    .Select(a => a.SkillSetId).ToListAsync(ct),
            setNamesAsync: async ids => await _unitOfWork.Repository<SkillSet>().GetQueryable()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && ids.Contains(s.Id))
                .Select(s => new SetHead(s.Id, s.Name, s.IsActive)).ToListAsync(ct),
            membersAsync: async ids => await _unitOfWork.Repository<SkillSetMember>().GetQueryable()
                .Where(m => m.TenantId == tenantId && !m.IsDeleted && ids.Contains(m.SkillSetId))
                .Select(m => new SetMemberRef(m.SkillSetId, m.SkillId)).ToListAsync(ct),
            itemNamesAsync: async ids => await _unitOfWork.Repository<Skill>().GetQueryable()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && ids.Contains(s.Id))
                .Select(s => new ItemHead(s.Id, s.Name)).ToListAsync(ct));

        // ── Certifications ──────────────────────────────────────────────────────────────────
        await OneKindAsync(
            noun: "credential",
            setNoun: "certification set",
            individualIds: input.CertificationIds,
            setIds: input.CertificationSetIds,
            currentSetIdsAsync: async () => positionId is not { } pid
                ? new List<Guid>()
                : await _unitOfWork.Repository<PositionCertificationSet>().GetQueryable()
                    .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == pid)
                    .Select(a => a.CertificationSetId).ToListAsync(ct),
            setNamesAsync: async ids => await _unitOfWork.Repository<CertificationSet>().GetQueryable()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && ids.Contains(s.Id))
                .Select(s => new SetHead(s.Id, s.Name, s.IsActive)).ToListAsync(ct),
            membersAsync: async ids => await _unitOfWork.Repository<CertificationSetMember>().GetQueryable()
                .Where(m => m.TenantId == tenantId && !m.IsDeleted && ids.Contains(m.CertificationSetId))
                .Select(m => new SetMemberRef(m.CertificationSetId, m.CertificationId)).ToListAsync(ct),
            itemNamesAsync: async ids => await _unitOfWork.Repository<Certification>().GetQueryable()
                .Where(c => c.TenantId == tenantId && !c.IsDeleted && ids.Contains(c.Id))
                .Select(c => new ItemHead(c.Id, c.Name)).ToListAsync(ct));
    }

    private sealed record SetHead(Guid Id, string Name, bool IsActive);
    private sealed record SetMemberRef(Guid SetId, Guid ItemId);
    private sealed record ItemHead(Guid Id, string Name);

    private static async Task OneKindAsync(
        string noun,
        string setNoun,
        IReadOnlyList<Guid>? individualIds,
        IReadOnlyList<Guid>? setIds,
        Func<Task<List<Guid>>> currentSetIdsAsync,
        Func<List<Guid>, Task<List<SetHead>>> setNamesAsync,
        Func<List<Guid>, Task<List<SetMemberRef>>> membersAsync,
        Func<List<Guid>, Task<List<ItemHead>>> itemNamesAsync)
    {
        // Rule 5 — a duplicate individual row is REFUSED, not collapsed. This is what closes X-1
        // and X-2: the old GroupBy(...).First() kept the first silently, so a second row naming the
        // same skill with a different required level simply vanished on reload.
        if (individualIds is { Count: > 0 })
        {
            var dupes = individualIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0)
            {
                var names = await itemNamesAsync(dupes);
                var label = Describe(dupes, names);
                throw new InvalidOperationException(
                    $"The same {noun} is listed twice ({label}). Each {noun} appears once — remove the duplicate row, "
                    + $"or change it to a different {noun}.");
            }
        }

        // The same, for the sets themselves.
        if (setIds is { Count: > 0 })
        {
            var dupes = setIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0)
            {
                var heads = await setNamesAsync(dupes);
                var label = string.Join(", ", dupes.Select(id => heads.FirstOrDefault(h => h.Id == id)?.Name ?? id.ToString()));
                throw new InvalidOperationException($"The same {setNoun} is attached twice ({label}). Attach it once.");
            }
        }

        // Which sets are in force for this save: the ones it carries, else the ones already there.
        var effectiveSetIds = setIds is null
            ? await currentSetIdsAsync()
            : setIds.Distinct().ToList();

        if (effectiveSetIds.Count > 0)
        {
            var heads = await setNamesAsync(effectiveSetIds);

            // A set the save names that does not exist for this tenant.
            var unknown = effectiveSetIds.Where(id => heads.All(h => h.Id != id)).ToList();
            if (unknown.Count > 0)
                throw new ArgumentException($"No {setNoun} was found with ID '{unknown[0]}'.");

            // A RETIRED set may stay attached where it already is, but cannot be newly attached —
            // retiring is meant to stop it being offered, not to break the posts already on it.
            if (setIds is not null)
            {
                var already = (await currentSetIdsAsync()).ToHashSet();
                var retiredAndNew = heads.Where(h => !h.IsActive && !already.Contains(h.Id)).ToList();
                if (retiredAndNew.Count > 0)
                    throw new InvalidOperationException(
                        $"The {setNoun} '{retiredAndNew[0].Name}' is retired, so it cannot be attached. "
                        + "Reactivate it first, or choose another.");
            }

            // Rules 2 and 3 — one check, because at save time both arrive together and there is no
            // way to tell which of the two the user added. The message names the row AND the set
            // and says which to remove, which answers both directions.
            if (individualIds is { Count: > 0 })
            {
                var members = await membersAsync(effectiveSetIds);
                var clash = individualIds.Distinct()
                    .Select(id => new { ItemId = id, Sets = members.Where(m => m.ItemId == id).Select(m => m.SetId).ToList() })
                    .Where(x => x.Sets.Count > 0)
                    .ToList();

                if (clash.Count > 0)
                {
                    var names = await itemNamesAsync(clash.Select(c => c.ItemId).ToList());
                    var first = clash[0];
                    var itemName = names.FirstOrDefault(n => n.Id == first.ItemId)?.Name ?? first.ItemId.ToString();
                    var setName = heads.FirstOrDefault(h => h.Id == first.Sets[0])?.Name ?? first.Sets[0].ToString();
                    var more = clash.Count > 1 ? $" ({clash.Count - 1} other{(clash.Count == 2 ? "" : "s")} likewise.)" : string.Empty;
                    throw new InvalidOperationException(
                        $"The {noun} '{itemName}' is already provided by the {setNoun} '{setName}'; remove the individual row.{more}");
                }
            }
        }

        static string Describe(List<Guid> ids, List<ItemHead> names) =>
            string.Join(", ", ids.Select(id => names.FirstOrDefault(n => n.Id == id)?.Name ?? id.ToString()));
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Sync — replace-set on the three attachment tables
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task SyncAsync(EmployeePosition position, ICollection<Guid>? benefitGroupIds, ICollection<Guid>? skillSetIds,
                                ICollection<Guid>? certificationSetIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        var tenantId = position.TenantId;

        var positionId = position.Id;

        await SyncOneAsync<EmployeePositionBenefitGroup>(
            benefitGroupIds,
            a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == positionId,
            a => a.BenefitGroupId,
            setId => new EmployeePositionBenefitGroup { TenantId = tenantId, PositionId = positionId, BenefitGroupId = setId },
            ct);

        await SyncOneAsync<PositionSkillSet>(
            skillSetIds,
            a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == positionId,
            a => a.SkillSetId,
            setId => new PositionSkillSet { TenantId = tenantId, PositionId = positionId, SkillSetId = setId },
            ct);

        await SyncOneAsync<PositionCertificationSet>(
            certificationSetIds,
            a => a.TenantId == tenantId && !a.IsDeleted && a.PositionId == positionId,
            a => a.CertificationSetId,
            setId => new PositionCertificationSet { TenantId = tenantId, PositionId = positionId, CertificationSetId = setId },
            ct);
    }

    /// <summary>
    /// ⚠ A plain add, no revive. The attachment tables' unique indexes carry
    /// <c>HasFilter("[IsDeleted] = 0")</c>, so a soft-deleted row does not block re-attaching —
    /// unlike <c>PositionSkillRequirement</c> and <c>EmployeePositionBenefit</c>, whose unfiltered
    /// indexes are exactly why their syncs have to hunt for a deleted row and resurrect it.
    /// </summary>
    /// <remarks>
    /// ⚠ <paramref name="forThisPosition"/> is built by the caller, where the CONCRETE entity type
    /// is known. Writing the predicate inside this generic method instead would have put an
    /// interface member access in the expression tree, which EF cannot map to a column — the same
    /// trap that kept the three master services from sharing a base class.
    /// </remarks>
    private async Task SyncOneAsync<TAttachment>(
        ICollection<Guid>? desired,
        System.Linq.Expressions.Expression<Func<TAttachment, bool>> forThisPosition,
        Func<TAttachment, Guid> setIdOf,
        Func<Guid, TAttachment> make,
        CancellationToken ct)
        where TAttachment : TenantEntity
    {
        if (desired is null) return;   // not carried by this save: leave as it is

        var wanted = desired.Distinct().ToList();
        var repo = _unitOfWork.Repository<TAttachment>();

        var live = await repo.GetQueryable().Where(forThisPosition).ToListAsync(ct);

        foreach (var row in live.Where(r => !wanted.Contains(setIdOf(r))))
        {
            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
        }

        var have = live.Where(r => !r.IsDeleted).Select(setIdOf).ToHashSet();
        foreach (var setId in wanted.Where(id => !have.Contains(id)))
        {
            await repo.AddAsync(make(setId).StampCreated(_currentUser));
        }
    }
}
