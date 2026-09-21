using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The certification model (demo feedback round 2, lane C2, plan § 6.3): the catalogue under the
/// certifying body, the skill and position links that consume it, what an employee holds, and
/// the compliance read that sets one against the other.
/// </summary>
public interface ICertificationService
{
    // ── Catalogue ────────────────────────────────────────────────────────────
    Task<IEnumerable<CertificationDto>> GetCertificationsAsync(Guid? certifyingBodyId = null, bool activeOnly = false, string? search = null, CancellationToken ct = default);
    Task<CertificationDto> GetCertificationAsync(Guid id, CancellationToken ct = default);
    Task<CertificationDto> CreateCertificationAsync(CreateCertificationDto dto, CancellationToken ct = default);
    Task<CertificationDto> UpdateCertificationAsync(UpdateCertificationDto dto, CancellationToken ct = default);
    Task<bool> DeleteCertificationAsync(Guid id, CancellationToken ct = default);

    // ── Skill links ──────────────────────────────────────────────────────────
    Task<IEnumerable<SkillCertificationDto>> GetSkillCertificationsAsync(Guid skillId, CancellationToken ct = default);
    /// <summary>
    /// Replaces a skill's accepted credentials with <paramref name="desired"/>. Null leaves them as
    /// they are (a save that did not carry the set); an empty set clears them. Enforces the rule:
    /// a skill that requires certification names at least one credential.
    /// </summary>
    Task SyncSkillCertificationsAsync(Skill skill, ICollection<SkillCertificationInputDto>? desired, CancellationToken ct = default);

    // ── Position requirements ────────────────────────────────────────────────
    Task<IEnumerable<PositionCertificationRequirementDto>> GetPositionRequirementsAsync(Guid positionId, CancellationToken ct = default);
    /// <summary>
    /// Replaces a position's required credentials with <paramref name="desired"/> (the whole set,
    /// like its skills and benefits). Enforces the rule: a position with either switch on names at
    /// least one credential.
    /// </summary>
    /// <param name="setProvidedCount">
    /// How many credentials the post's attached certification SETS provide (round 2, lane C3a).
    /// ⚠ The "a switched-on post must name a credential" rule below counts these too: a post whose
    /// whole regulatory bundle arrives through a set names plenty, just not row by row.
    /// </param>
    Task SyncPositionRequirementsAsync(EmployeePosition position, ICollection<CreatePositionCertificationRequirementDto>? desired, CancellationToken ct = default, int setProvidedCount = 0);

    // ── Employee credentials ─────────────────────────────────────────────────
    Task<IEnumerable<EmployeeCertificationDto>> GetEmployeeCertificationsAsync(Guid employeeId, CancellationToken ct = default);
    Task<EmployeeCertificationDto> GetEmployeeCertificationAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeCertificationDto> AddEmployeeCertificationAsync(CreateEmployeeCertificationDto dto, CancellationToken ct = default);
    Task<EmployeeCertificationDto> UpdateEmployeeCertificationAsync(UpdateEmployeeCertificationDto dto, CancellationToken ct = default);
    Task<EmployeeCertificationDto> RevokeEmployeeCertificationAsync(Guid id, RevokeEmployeeCertificationDto dto, CancellationToken ct = default);
    /// <summary><paramref name="verifierEmployeeId"/> comes from the token, never from a DTO.</summary>
    Task<EmployeeCertificationDto> VerifyEmployeeCertificationAsync(Guid id, Guid? verifierEmployeeId, CancellationToken ct = default);
    Task<bool> RemoveEmployeeCertificationAsync(Guid id, CancellationToken ct = default);
    /// <summary>The raw row, for the evidence download's entitlement check.</summary>
    Task<EmployeeCertification?> GetEmployeeCertificationEntityAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeCertificationDto> AttachEvidenceAsync(Guid id, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId, string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default);

    // ── Compliance ───────────────────────────────────────────────────────────
    Task<EmployeeCertificationComplianceDto> GetEmployeeComplianceAsync(Guid employeeId, CancellationToken ct = default);
}

public class CertificationService : ICertificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPositionNamedSetService _namedSets;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<CertificationService> _logger;

    public CertificationService(
        IUnitOfWork unitOfWork,
        IPositionNamedSetService namedSets,
        ICurrentUserProvider currentUserProvider,
        ILogger<CertificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _namedSets = namedSets;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter
    // and TenantId auto-stamp are inert. Every read and write here scopes to the tenant itself.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.Date);

    private static string Trim(string? v) => string.IsNullOrWhiteSpace(v) ? string.Empty : v.Trim();
    private static string? TrimOrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    // ═══════════════════════════════════════════════════════════════════════
    //  Catalogue
    // ═══════════════════════════════════════════════════════════════════════

    private IQueryable<Certification> Catalogue(Guid tenantId) =>
        _unitOfWork.Repository<Certification>().GetQueryable()
            .Include(c => c.CertifyingBody)
            .Where(c => c.TenantId == tenantId && !c.IsDeleted);

    public async Task<IEnumerable<CertificationDto>> GetCertificationsAsync(
        Guid? certifyingBodyId = null, bool activeOnly = false, string? search = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = Catalogue(tenantId);
        if (certifyingBodyId.HasValue) query = query.Where(c => c.CertifyingBodyId == certifyingBodyId.Value);
        if (activeOnly) query = query.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Code != null && c.Code.Contains(term))
                                     || c.CertifyingBody.Name.Contains(term));
        }

        var rows = await query
            .OrderBy(c => c.CertifyingBody.Name).ThenBy(c => c.Name)
            .ToListAsync(ct);

        return await WithCountsAsync(tenantId, rows, ct);
    }

    public async Task<CertificationDto> GetCertificationAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var row = await Catalogue(tenantId).FirstOrDefaultAsync(c => c.Id == id, ct)
                  ?? throw new ArgumentException($"Certification with ID '{id}' not found.");
        return (await WithCountsAsync(tenantId, new[] { row }, ct)).Single();
    }

    public async Task<CertificationDto> CreateCertificationAsync(CreateCertificationDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var body = await RequireBodyAsync(tenantId, dto.CertifyingBodyId, ct);
        await RequireUnusedNameAndCodeAsync(tenantId, dto.CertifyingBodyId, dto.Name, dto.Code, null, body.Name, ct);

        var entity = new Certification
        {
            TenantId = tenantId,
            CertifyingBodyId = dto.CertifyingBodyId,
            Name = Trim(dto.Name),
            Code = TrimOrNull(dto.Code),
            Kind = dto.Kind,
            Description = TrimOrNull(dto.Description),
            ValidityMonths = dto.ValidityMonths,
            RenewalRequired = dto.RenewalRequired,
            ExpiryNotificationLeadDays = dto.ExpiryNotificationLeadDays,
            IsActive = dto.IsActive,
        }.StampCreated(_currentUserProvider);

        await _unitOfWork.Repository<Certification>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Certification created: {Id} ({Name}) under body {BodyId}", entity.Id, entity.Name, entity.CertifyingBodyId);
        return await GetCertificationAsync(entity.Id, ct);
    }

    public async Task<CertificationDto> UpdateCertificationAsync(UpdateCertificationDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<Certification>().GetQueryable()
                         .FirstOrDefaultAsync(c => c.Id == dto.Id && c.TenantId == tenantId && !c.IsDeleted, ct)
                     ?? throw new ArgumentException($"Certification with ID '{dto.Id}' not found.");
        var body = await RequireBodyAsync(tenantId, dto.CertifyingBodyId, ct);

        // ⚠ Moving a credential to another body is refused once anyone holds it: the holder's
        // certificate names the body that issued it, and the row would then say otherwise.
        if (entity.CertifyingBodyId != dto.CertifyingBodyId)
        {
            var held = await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
                .AnyAsync(e => e.TenantId == tenantId && !e.IsDeleted && e.CertificationId == entity.Id, ct);
            if (held)
                throw new InvalidOperationException(
                    $"'{entity.Name}' is already held by at least one employee, so it cannot be moved to another certifying body. Retire it and create the credential under the right body instead.");
        }

        await RequireUnusedNameAndCodeAsync(tenantId, dto.CertifyingBodyId, dto.Name, dto.Code, dto.Id, body.Name, ct);

        entity.CertifyingBodyId = dto.CertifyingBodyId;
        entity.Name = Trim(dto.Name);
        entity.Code = TrimOrNull(dto.Code);
        entity.Kind = dto.Kind;
        entity.Description = TrimOrNull(dto.Description);
        entity.ValidityMonths = dto.ValidityMonths;
        entity.RenewalRequired = dto.RenewalRequired;
        entity.ExpiryNotificationLeadDays = dto.ExpiryNotificationLeadDays;
        entity.IsActive = dto.IsActive;
        entity.StampUpdated(_currentUserProvider);

        await _unitOfWork.Repository<Certification>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetCertificationAsync(entity.Id, ct);
    }

    public async Task<bool> DeleteCertificationAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<Certification>().GetQueryable()
                         .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, ct)
                     ?? throw new ArgumentException($"Certification with ID '{id}' not found.");

        var skills = await _unitOfWork.Repository<SkillCertification>().GetQueryable()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.CertificationId == id, ct);
        var positions = await _unitOfWork.Repository<PositionCertificationRequirement>().GetQueryable()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.CertificationId == id, ct);
        var holders = await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.CertificationId == id, ct);

        if (skills + positions + holders > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' is cited by {skills} skill(s), {positions} position(s) and {holders} employee record(s), so it cannot be removed. "
                + "Make it inactive instead — it stays off new records and the existing ones keep their credential.");

        await _unitOfWork.Repository<Certification>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<CertifyingBody> RequireBodyAsync(Guid tenantId, Guid bodyId, CancellationToken ct)
    {
        var body = await _unitOfWork.Repository<CertifyingBody>().GetQueryable()
            .FirstOrDefaultAsync(b => b.Id == bodyId && b.TenantId == tenantId && !b.IsDeleted, ct);
        if (body == null)
            throw new ArgumentException($"Certifying body with ID '{bodyId}' not found.");
        if (!body.IsActive)
            throw new InvalidOperationException($"'{body.Name}' is retired; a credential cannot be filed under it.");
        return body;
    }

    private async Task RequireUnusedNameAndCodeAsync(Guid tenantId, Guid bodyId, string name, string? code, Guid? exceptId, string bodyName, CancellationToken ct)
    {
        var trimmedName = Trim(name);
        var trimmedCode = TrimOrNull(code);
        var query = _unitOfWork.Repository<Certification>().GetQueryable()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.CertifyingBodyId == bodyId
                        && (exceptId == null || c.Id != exceptId));

        if (await query.AnyAsync(c => c.Name == trimmedName, ct))
            throw new InvalidOperationException($"'{bodyName}' already issues a credential called '{trimmedName}'.");
        if (trimmedCode != null && await query.AnyAsync(c => c.Code == trimmedCode, ct))
            throw new InvalidOperationException($"'{bodyName}' already has a credential coded '{trimmedCode}'.");
    }

    private async Task<List<CertificationDto>> WithCountsAsync(Guid tenantId, IReadOnlyCollection<Certification> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return new List<CertificationDto>();
        var ids = rows.Select(r => r.Id).ToList();

        var skillCounts = await _unitOfWork.Repository<SkillCertification>().GetQueryable()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.CertificationId))
            .GroupBy(x => x.CertificationId).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.N, ct);
        var positionCounts = await _unitOfWork.Repository<PositionCertificationRequirement>().GetQueryable()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.CertificationId))
            .GroupBy(x => x.CertificationId).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.N, ct);
        var holderCounts = await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.CertificationId))
            .GroupBy(x => x.CertificationId).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.N, ct);

        return rows.Select(c => new CertificationDto
        {
            Id = c.Id,
            TenantId = c.TenantId,
            CertifyingBodyId = c.CertifyingBodyId,
            CertifyingBodyName = c.CertifyingBody?.Name ?? string.Empty,
            CertifyingBodyAbbreviation = c.CertifyingBody?.Abbreviation,
            Name = c.Name,
            Code = c.Code,
            Kind = c.Kind,
            Description = c.Description,
            ValidityMonths = c.ValidityMonths,
            RenewalRequired = c.RenewalRequired,
            ExpiryNotificationLeadDays = c.ExpiryNotificationLeadDays,
            IsActive = c.IsActive,
            SkillCount = skillCounts.GetValueOrDefault(c.Id),
            PositionCount = positionCounts.GetValueOrDefault(c.Id),
            HolderCount = holderCounts.GetValueOrDefault(c.Id),
            CreatedAt = c.CreatedAt,
            CreatedBy = c.CreatedBy ?? string.Empty,
            UpdatedAt = c.UpdatedAt,
            UpdatedBy = c.UpdatedBy,
        }).ToList();
    }

    /// <summary>Every id in <paramref name="ids"/> is an active credential of this tenant.</summary>
    private async Task<Dictionary<Guid, Certification>> RequireActiveCertificationsAsync(Guid tenantId, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0) return new Dictionary<Guid, Certification>();

        var found = await _unitOfWork.Repository<Certification>().GetQueryable()
            .Include(c => c.CertifyingBody)
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && wanted.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var missing = wanted.Where(id => !found.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException($"Certification with ID '{missing[0]}' not found.");

        var retired = found.Values.FirstOrDefault(c => !c.IsActive);
        if (retired != null)
            throw new InvalidOperationException($"'{retired.Name}' is retired and cannot be required or accepted on new records.");

        return found;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Skill links
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<SkillCertificationDto>> GetSkillCertificationsAsync(Guid skillId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<SkillCertification>().GetQueryable()
            .Include(x => x.Certification).ThenInclude(c => c.CertifyingBody)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.SkillId == skillId)
            .OrderByDescending(x => x.IsMandatory).ThenBy(x => x.Certification.Name)
            .Select(x => new SkillCertificationDto
            {
                Id = x.Id,
                CertificationId = x.CertificationId,
                CertificationName = x.Certification.Name,
                Kind = x.Certification.Kind,
                CertifyingBodyId = x.Certification.CertifyingBodyId,
                CertifyingBodyName = x.Certification.CertifyingBody.Name,
                IsMandatory = x.IsMandatory,
                Notes = x.Notes,
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The same replace-set shape as the position's skills, read through the filter so a link that
    /// was removed and added back revives its row instead of tripping the unique index.
    /// </remarks>
    public async Task SyncSkillCertificationsAsync(Skill skill, ICollection<SkillCertificationInputDto>? desired, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var repo = _unitOfWork.Repository<SkillCertification>();

        var existingLive = await repo.GetQueryable()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.SkillId == skill.Id)
            .CountAsync(ct);

        if (desired == null)
        {
            // A save that did not carry the set. The rule still holds against what is stored.
            if (skill.RequiresCertification && existingLive == 0)
                throw new InvalidOperationException(
                    $"'{skill.Name}' requires certification, so say which credential evidences it — add at least one accepted certification.");
            return;
        }

        var desiredDistinct = desired
            .Where(x => x.CertificationId != Guid.Empty)
            .GroupBy(x => x.CertificationId)
            .Select(g => g.First())
            .ToList();

        if (skill.RequiresCertification && desiredDistinct.Count == 0)
            throw new InvalidOperationException(
                $"'{skill.Name}' requires certification, so say which credential evidences it — add at least one accepted certification.");

        await RequireActiveCertificationsAsync(tenantId, desiredDistinct.Select(x => x.CertificationId), ct);

        var all = await repo.GetQueryableIncludingDeleted(x => x.TenantId == tenantId && x.SkillId == skill.Id)
            .ToListAsync(ct);
        var byCertification = all.GroupBy(x => x.CertificationId).ToDictionary(g => g.Key, g => g.First());

        foreach (var row in desiredDistinct)
        {
            if (byCertification.TryGetValue(row.CertificationId, out var entity))
            {
                entity.IsDeleted = false;
                entity.DeletedAt = null;
                entity.IsMandatory = row.IsMandatory;
                entity.Notes = TrimOrNull(row.Notes);
                entity.StampUpdated(_currentUserProvider);
                await repo.UpdateAsync(entity);
            }
            else
            {
                await repo.AddAsync(new SkillCertification
                {
                    TenantId = tenantId,
                    SkillId = skill.Id,
                    CertificationId = row.CertificationId,
                    IsMandatory = row.IsMandatory,
                    Notes = TrimOrNull(row.Notes),
                }.StampCreated(_currentUserProvider));
            }
        }

        var keep = desiredDistinct.Select(x => x.CertificationId).ToHashSet();
        foreach (var entity in all.Where(x => !x.IsDeleted && !keep.Contains(x.CertificationId)))
        {
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            await repo.UpdateAsync(entity);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Position requirements
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<PositionCertificationRequirementDto>> GetPositionRequirementsAsync(Guid positionId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<PositionCertificationRequirement>().GetQueryable()
            .Include(x => x.Certification).ThenInclude(c => c.CertifyingBody)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.PositionId == positionId)
            .OrderByDescending(x => x.IsMandatory).ThenBy(x => x.Certification.Name)
            .Select(x => new PositionCertificationRequirementDto
            {
                Id = x.Id,
                CertificationId = x.CertificationId,
                CertificationName = x.Certification.Name,
                Kind = x.Certification.Kind,
                CertifyingBodyId = x.Certification.CertifyingBodyId,
                CertifyingBodyName = x.Certification.CertifyingBody.Name,
                IsMandatory = x.IsMandatory,
                Notes = x.Notes,
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task SyncPositionRequirementsAsync(EmployeePosition position, ICollection<CreatePositionCertificationRequirementDto>? desired, CancellationToken ct = default, int setProvidedCount = 0)
    {
        var tenantId = GetTenantId();
        var repo = _unitOfWork.Repository<PositionCertificationRequirement>();
        var switchedOn = position.RequiresCertification || position.RequiresLicense;

        if (desired == null)
        {
            var existingLive = await repo.GetQueryable()
                .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.PositionId == position.Id, ct);
            if (switchedOn && existingLive == 0 && setProvidedCount == 0)
                throw new InvalidOperationException(
                    $"'{position.Title}' requires a certification or licence, so say which one — add at least one required credential, or turn the switch off.");
            return;
        }

        var desiredDistinct = desired
            .Where(x => x.CertificationId != Guid.Empty)
            .GroupBy(x => x.CertificationId)
            .Select(g => g.First())
            .ToList();

        // ⚠ The switches were bare bools with no consumer (register row P-2). They stay as the
        // user's statement of intent; these rows are what the statement means, and a statement
        // with nothing behind it is refused rather than stored.
        if (switchedOn && desiredDistinct.Count == 0 && setProvidedCount == 0)
            throw new InvalidOperationException(
                $"'{position.Title}' requires a certification or licence, so say which one — add at least one required credential, or turn the switch off.");

        await RequireActiveCertificationsAsync(tenantId, desiredDistinct.Select(x => x.CertificationId), ct);

        var all = await repo.GetQueryableIncludingDeleted(x => x.TenantId == tenantId && x.PositionId == position.Id)
            .ToListAsync(ct);
        var byCertification = all.GroupBy(x => x.CertificationId).ToDictionary(g => g.Key, g => g.First());

        foreach (var row in desiredDistinct)
        {
            if (byCertification.TryGetValue(row.CertificationId, out var entity))
            {
                entity.IsDeleted = false;
                entity.DeletedAt = null;
                entity.IsMandatory = row.IsMandatory;
                entity.Notes = TrimOrNull(row.Notes);
                entity.StampUpdated(_currentUserProvider);
                await repo.UpdateAsync(entity);
            }
            else
            {
                await repo.AddAsync(new PositionCertificationRequirement
                {
                    TenantId = tenantId,
                    PositionId = position.Id,
                    CertificationId = row.CertificationId,
                    IsMandatory = row.IsMandatory,
                    Notes = TrimOrNull(row.Notes),
                }.StampCreated(_currentUserProvider));
            }
        }

        var keep = desiredDistinct.Select(x => x.CertificationId).ToHashSet();
        foreach (var entity in all.Where(x => !x.IsDeleted && !keep.Contains(x.CertificationId)))
        {
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            await repo.UpdateAsync(entity);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Employee credentials
    // ═══════════════════════════════════════════════════════════════════════

    private IQueryable<EmployeeCertification> Credentials(Guid tenantId) =>
        _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
            .Include(x => x.Certification).ThenInclude(c => c.CertifyingBody)
            .Include(x => x.VerifiedBy)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

    private async Task<int> PolicyLeadDaysAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _unitOfWork.Repository<CompanyHrPolicySettings>().GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => (int?)s.CertificationExpiryLeadDays)
            .FirstOrDefaultAsync(ct);
        return settings is > 0 ? settings.Value : 60;
    }

    /// <summary>The state a credential is in today. Only Revoked is stored; the rest are dates.</summary>
    internal static EmployeeCertificationStatus StatusOf(EmployeeCertification e, int leadDays, DateOnly today)
    {
        if (e.IsRevoked) return EmployeeCertificationStatus.Revoked;
        if (!e.ExpiresOn.HasValue) return EmployeeCertificationStatus.Valid;
        var days = e.ExpiresOn.Value.DayNumber - today.DayNumber;
        if (days < 0) return EmployeeCertificationStatus.Expired;
        var lead = e.Certification?.ExpiryNotificationLeadDays ?? leadDays;
        return days <= lead ? EmployeeCertificationStatus.ExpiringSoon : EmployeeCertificationStatus.Valid;
    }

    private static EmployeeCertificationDto MapCredential(EmployeeCertification e, int leadDays, DateOnly today) => new()
    {
        Id = e.Id,
        EmployeeId = e.EmployeeId,
        CertificationId = e.CertificationId,
        CertificationName = e.Certification?.Name ?? string.Empty,
        Kind = e.Certification?.Kind ?? CertificationKind.Certification,
        CertifyingBodyId = e.Certification?.CertifyingBodyId ?? Guid.Empty,
        CertifyingBodyName = e.Certification?.CertifyingBody?.Name ?? string.Empty,
        CertificateNumber = e.CertificateNumber,
        IssuedOn = e.IssuedOn,
        ExpiresOn = e.ExpiresOn,
        Status = StatusOf(e, leadDays, today),
        DaysUntilExpiry = e.ExpiresOn.HasValue ? e.ExpiresOn.Value.DayNumber - today.DayNumber : null,
        IsRevoked = e.IsRevoked,
        RevokedOn = e.RevokedOn,
        RevocationReason = e.RevocationReason,
        HasEvidence = e.EvidenceFileUploadRecordId.HasValue || e.EvidenceDocumentRecordId.HasValue,
        EvidenceFileName = e.EvidenceFileName,
        EvidenceFileSizeBytes = e.EvidenceFileSizeBytes,
        IsVerified = e.IsVerified,
        VerifiedById = e.VerifiedById,
        VerifiedByName = e.VerifiedBy == null ? null : $"{e.VerifiedBy.FirstName} {e.VerifiedBy.LastName}".Trim(),
        VerifiedOn = e.VerifiedOn,
        Notes = e.Notes,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };

    public async Task<IEnumerable<EmployeeCertificationDto>> GetEmployeeCertificationsAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var lead = await PolicyLeadDaysAsync(tenantId, ct);
        var today = Today();
        var rows = await Credentials(tenantId)
            .Where(x => x.EmployeeId == employeeId)
            .OrderBy(x => x.IsRevoked).ThenBy(x => x.ExpiresOn ?? DateOnly.MaxValue).ThenBy(x => x.Certification.Name)
            .ToListAsync(ct);
        return rows.Select(r => MapCredential(r, lead, today)).ToList();
    }

    public async Task<EmployeeCertificationDto> GetEmployeeCertificationAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var row = await Credentials(tenantId).FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new ArgumentException($"Employee certification with ID '{id}' not found.");
        return MapCredential(row, await PolicyLeadDaysAsync(tenantId, ct), Today());
    }

    public async Task<EmployeeCertificationDto> AddEmployeeCertificationAsync(CreateEmployeeCertificationDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var employeeExists = await _unitOfWork.Repository<Employee>().GetQueryable()
            .AnyAsync(e => e.Id == dto.EmployeeId && e.TenantId == tenantId && !e.IsDeleted, ct);
        if (!employeeExists)
            throw new ArgumentException($"Employee with ID '{dto.EmployeeId}' not found.");

        var certification = (await RequireActiveCertificationsAsync(tenantId, new[] { dto.CertificationId }, ct))[dto.CertificationId];

        var number = TrimOrNull(dto.CertificateNumber);
        if (number != null)
        {
            var dup = await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
                .AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.EmployeeId == dto.EmployeeId
                               && x.CertificationId == dto.CertificationId && x.CertificateNumber == number, ct);
            if (dup)
                throw new InvalidOperationException($"This employee already holds '{certification.Name}' with certificate number '{number}'.");
        }

        var expires = ResolveExpiry(dto.IssuedOn, dto.ExpiresOn, certification);
        if (dto.IssuedOn.HasValue && expires.HasValue && expires.Value < dto.IssuedOn.Value)
            throw new InvalidOperationException("A credential cannot expire before it was issued.");

        var entity = new EmployeeCertification
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            CertificationId = dto.CertificationId,
            CertificateNumber = number,
            IssuedOn = dto.IssuedOn,
            ExpiresOn = expires,
            Notes = TrimOrNull(dto.Notes),
        }.StampCreated(_currentUserProvider);

        await _unitOfWork.Repository<EmployeeCertification>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Employee certification recorded: {Id} ({Certification}) for employee {EmployeeId}", entity.Id, certification.Name, dto.EmployeeId);
        return await GetEmployeeCertificationAsync(entity.Id, ct);
    }

    /// <summary>
    /// The stated expiry, else issue + the catalogue's validity, else none. The catalogue's
    /// validity is a DEFAULT, not a rule: a body may have issued this one for longer.
    /// </summary>
    private static DateOnly? ResolveExpiry(DateOnly? issuedOn, DateOnly? expiresOn, Certification certification)
    {
        if (expiresOn.HasValue) return expiresOn;
        if (issuedOn.HasValue && certification.ValidityMonths is > 0)
            return issuedOn.Value.AddMonths(certification.ValidityMonths.Value);
        return null;
    }

    public async Task<EmployeeCertificationDto> UpdateEmployeeCertificationAsync(UpdateEmployeeCertificationDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await Credentials(tenantId).FirstOrDefaultAsync(x => x.Id == dto.Id, ct)
                     ?? throw new ArgumentException($"Employee certification with ID '{dto.Id}' not found.");

        var number = TrimOrNull(dto.CertificateNumber);
        if (number != null && number != entity.CertificateNumber)
        {
            var dup = await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
                .AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.Id != entity.Id && x.EmployeeId == entity.EmployeeId
                               && x.CertificationId == entity.CertificationId && x.CertificateNumber == number, ct);
            if (dup)
                throw new InvalidOperationException($"This employee already holds '{entity.Certification.Name}' with certificate number '{number}'.");
        }

        var expires = ResolveExpiry(dto.IssuedOn, dto.ExpiresOn, entity.Certification);
        if (dto.IssuedOn.HasValue && expires.HasValue && expires.Value < dto.IssuedOn.Value)
            throw new InvalidOperationException("A credential cannot expire before it was issued.");

        entity.CertificateNumber = number;
        entity.IssuedOn = dto.IssuedOn;
        entity.ExpiresOn = expires;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.StampUpdated(_currentUserProvider);

        await _unitOfWork.Repository<EmployeeCertification>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetEmployeeCertificationAsync(entity.Id, ct);
    }

    public async Task<EmployeeCertificationDto> RevokeEmployeeCertificationAsync(Guid id, RevokeEmployeeCertificationDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await Credentials(tenantId).FirstOrDefaultAsync(x => x.Id == id, ct)
                     ?? throw new ArgumentException($"Employee certification with ID '{id}' not found.");
        if (entity.IsRevoked)
            throw new InvalidOperationException("This credential is already revoked.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("Say why the credential was revoked.");

        entity.IsRevoked = true;
        entity.RevokedOn = dto.RevokedOn ?? Today();
        entity.RevocationReason = dto.Reason.Trim();
        entity.StampUpdated(_currentUserProvider);

        await _unitOfWork.Repository<EmployeeCertification>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Employee certification revoked: {Id}", id);
        return await GetEmployeeCertificationAsync(entity.Id, ct);
    }

    public async Task<EmployeeCertificationDto> VerifyEmployeeCertificationAsync(Guid id, Guid? verifierEmployeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await Credentials(tenantId).FirstOrDefaultAsync(x => x.Id == id, ct)
                     ?? throw new ArgumentException($"Employee certification with ID '{id}' not found.");
        if (entity.IsRevoked)
            throw new InvalidOperationException("A revoked credential cannot be verified.");
        // ⚠ Verifying your own credential is not verification.
        if (verifierEmployeeId.HasValue && verifierEmployeeId.Value == entity.EmployeeId)
            throw new InvalidOperationException("An employee cannot verify their own credential.");

        entity.IsVerified = true;
        entity.VerifiedById = verifierEmployeeId;
        entity.VerifiedOn = DateTime.UtcNow;
        entity.StampUpdated(_currentUserProvider);

        await _unitOfWork.Repository<EmployeeCertification>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetEmployeeCertificationAsync(entity.Id, ct);
    }

    public async Task<bool> RemoveEmployeeCertificationAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
                         .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, ct)
                     ?? throw new ArgumentException($"Employee certification with ID '{id}' not found.");

        // A skill row pointing at it would be left pointing at nothing; unlink first.
        var evidencing = await _unitOfWork.Repository<EmployeeSkill>().GetQueryable()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.EmployeeCertificationId == id)
            .ToListAsync(ct);
        foreach (var skill in evidencing)
        {
            skill.EmployeeCertificationId = null;
            await _unitOfWork.Repository<EmployeeSkill>().UpdateAsync(skill);
        }

        await _unitOfWork.Repository<EmployeeCertification>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EmployeeCertification?> GetEmployeeCertificationEntityAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, ct);
    }

    public async Task<EmployeeCertificationDto> AttachEvidenceAsync(
        Guid id, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
                         .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, ct)
                     ?? throw new ArgumentException($"Employee certification with ID '{id}' not found.");

        entity.EvidenceFileUploadRecordId = fileUploadRecordId;
        entity.EvidenceDocumentRecordId = documentRecordId;
        entity.EvidenceDocumentVersionId = documentVersionId;
        entity.EvidenceFileName = fileName;
        entity.EvidenceMimeType = mimeType;
        entity.EvidenceFileSizeBytes = fileSizeBytes;
        entity.StampUpdated(_currentUserProvider);

        await _unitOfWork.Repository<EmployeeCertification>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetEmployeeCertificationAsync(entity.Id, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Compliance
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// ⚠ <c>Employee.PositionId</c> is a NON-NULLABLE Guid, so "no position" is Guid.Empty — the
    /// same trap the document compliance read records. An employee with no position has no
    /// requirements and is compliant by definition; their held credentials are still counted.
    /// </remarks>
    public async Task<EmployeeCertificationComplianceDto> GetEmployeeComplianceAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .AsNoTracking()
            .Where(e => e.Id == employeeId && e.TenantId == tenantId)
            .Select(e => new
            {
                e.Id,
                Name = (e.FirstName + " " + e.LastName).Trim(),
                e.PositionId,
                PositionTitle = e.Position != null ? e.Position.Title : null,
                RequiresCertification = e.Position != null && e.Position.RequiresCertification,
                RequiresLicense = e.Position != null && e.Position.RequiresLicense,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var lead = await PolicyLeadDaysAsync(tenantId, ct);
        var today = Today();
        var held = await Credentials(tenantId).Where(x => x.EmployeeId == employeeId).ToListAsync(ct);

        var result = new EmployeeCertificationComplianceDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            PositionId = employee.PositionId == Guid.Empty ? null : employee.PositionId,
            PositionTitle = employee.PositionTitle,
            RequiresCertification = employee.RequiresCertification,
            RequiresLicense = employee.RequiresLicense,
            ExpiringSoonCount = held.Count(h => StatusOf(h, lead, today) == EmployeeCertificationStatus.ExpiringSoon),
            ExpiredCount = held.Count(h => StatusOf(h, lead, today) == EmployeeCertificationStatus.Expired),
        };

        if (employee.PositionId == Guid.Empty)
        {
            result.IsCompliant = true;
            return result;
        }

        // ⚠ Round 2, lane C3 — the EFFECTIVE requirement, not the individual table. A credential
        // the post requires through an attached certification set is as required as one listed
        // individually; reading the table directly would have reported a person compliant while
        // they were missing everything a set asked of them.
        var requirements = (await _namedSets.GetEffectiveCertificationsAsync(employee.PositionId, tenantId, ct))
            .OrderByDescending(r => r.IsMandatory).ThenBy(r => r.CertificationName)
            .ToList();

        var requiredIds = requirements.Select(r => r.CertificationId).ToList();
        var kinds = await _unitOfWork.Repository<Certification>().GetQueryable()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && requiredIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Kind, ct);

        foreach (var r in requirements)
        {
            // The BEST credential the employee holds for this requirement: a valid one over an
            // expiring one over an expired one over a revoked one.
            var best = held
                .Where(h => h.CertificationId == r.CertificationId)
                .Select(h => new { Row = h, Status = StatusOf(h, lead, today) })
                .OrderBy(x => x.Status)
                .FirstOrDefault();

            var line = new EmployeeCertificationComplianceLineDto
            {
                CertificationId = r.CertificationId,
                CertificationName = r.CertificationName,
                CertifyingBodyName = r.CertifyingBodyName,
                Kind = kinds.TryGetValue(r.CertificationId, out var k) ? k : default,
                IsMandatory = r.IsMandatory,
            };

            if (best == null)
            {
                line.Status = "Missing";
            }
            else
            {
                line.EmployeeCertificationId = best.Row.Id;
                line.ExpiresOn = best.Row.ExpiresOn;
                line.DaysUntilExpiry = best.Row.ExpiresOn.HasValue ? best.Row.ExpiresOn.Value.DayNumber - today.DayNumber : null;
                line.Status = best.Status switch
                {
                    EmployeeCertificationStatus.Valid => "Held",
                    EmployeeCertificationStatus.ExpiringSoon => "ExpiringSoon",
                    EmployeeCertificationStatus.Expired => "Expired",
                    _ => "Revoked",
                };
                // Expiring soon still satisfies — it is a warning, not a gap. Expired and revoked do not.
                line.IsSatisfied = best.Status is EmployeeCertificationStatus.Valid or EmployeeCertificationStatus.ExpiringSoon;
            }

            result.Lines.Add(line);
        }

        result.MandatoryCount = result.Lines.Count(l => l.IsMandatory);
        result.MandatorySatisfiedCount = result.Lines.Count(l => l.IsMandatory && l.IsSatisfied);
        result.IsCompliant = result.MandatorySatisfiedCount == result.MandatoryCount;
        return result;
    }
}
