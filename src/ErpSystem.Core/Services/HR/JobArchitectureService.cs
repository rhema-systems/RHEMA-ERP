using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class JobArchitectureService : IJobArchitectureService
{
    private readonly IJobFamilyRepository _familyRepo;
    private readonly IJobSubFamilyRepository _subFamilyRepo;
    private readonly IJobLevelRepository _levelRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobArchitectureService> _logger;

    public JobArchitectureService(
        IJobFamilyRepository familyRepo,
        IJobSubFamilyRepository subFamilyRepo,
        IJobLevelRepository levelRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<JobArchitectureService> logger)
    {
        _familyRepo = familyRepo;
        _subFamilyRepo = subFamilyRepo;
        _levelRepo = levelRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private async Task<JobFamily> GetOwnedFamilyAsync(Guid id)
    {
        var entity = await _familyRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Job family not found");
        return entity;
    }

    private async Task<JobSubFamily> GetOwnedSubFamilyAsync(Guid id)
    {
        var entity = await _subFamilyRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Sub-family not found");
        return entity;
    }

    private async Task<CareerLevel> GetOwnedLevelAsync(Guid id)
    {
        var entity = await _levelRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Job level not found");
        return entity;
    }

    // ── Integrity ────────────────────────────────────────────────────────────
    //
    // ⚠ None of this existed. The three tables carry no unique index beyond their primary key, and
    // the service validated nothing, so two job families could share the code "ENG", two career
    // levels could sit at rank 3, and deleting a family took its sub-families out of every list
    // while leaving job descriptions pointing at it. A vocabulary whose codes are not unique is not
    // a vocabulary.
    //
    // Enforced in the service rather than by a unique index, deliberately: an index does not know
    // about the soft delete (the area-13 trap), so a deleted "ENG" would block a new one forever.
    // Scoping the check to live rows gets the rule without that consequence.

    private async Task RequireFamilyCodeFreeAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var clash = await _familyRepo.GetQueryable()
            .AnyAsync(f => f.TenantId == tenantId && !f.IsDeleted
                        && f.Code.ToLower() == code.ToLower()
                        && (exceptId == null || f.Id != exceptId), ct);
        if (clash)
            throw JobArchitectureException.Conflict($"A job family with code '{code}' already exists.");
    }

    private async Task RequireSubFamilyCodeFreeAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var clash = await _subFamilyRepo.GetQueryable()
            .AnyAsync(f => f.TenantId == tenantId && !f.IsDeleted
                        && f.Code.ToLower() == code.ToLower()
                        && (exceptId == null || f.Id != exceptId), ct);
        if (clash)
            throw JobArchitectureException.Conflict($"A job sub-family with code '{code}' already exists.");
    }

    private async Task RequireLevelCodeAndRankFreeAsync(string code, int rank, Guid? exceptId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var live = _levelRepo.GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && (exceptId == null || l.Id != exceptId));

        if (await live.AnyAsync(l => l.Code.ToLower() == code.ToLower(), ct))
            throw JobArchitectureException.Conflict($"A career level with code '{code}' already exists.");

        // Rank is what orders the ladder. Two levels at the same rank make "more senior than" an
        // unanswerable question, which is worse than a duplicate name.
        var atRank = await live.FirstOrDefaultAsync(l => l.Rank == rank, ct);
        if (atRank != null)
            throw JobArchitectureException.Conflict(
                $"Rank {rank} is already held by '{atRank.Name}'. Career level ranks order the ladder, so each must be distinct.");
    }

    /// <summary>Refuses to remove a classification that job descriptions are still filed under.</summary>
    /// <remarks>
    /// A soft delete hides the row from every list but leaves the foreign key intact, so those job
    /// descriptions keep resolving a name HR can no longer see or edit — the classification becomes
    /// unmaintainable rather than going away. Refusing, and saying how many records are affected,
    /// lets HR reclassify them first.
    /// </remarks>
    private async Task RequireNotInUseAsync(
        System.Linq.Expressions.Expression<Func<JobDescription, bool>> predicate,
        string what, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var inUse = await _unitOfWork.Repository<JobDescription>().GetQueryable()
            .Where(jd => jd.TenantId == tenantId && !jd.IsDeleted)
            .Where(predicate)
            .CountAsync(ct);
        if (inUse > 0)
            throw JobArchitectureException.Conflict(
                $"{inUse} job description(s) are still classified under this {what}. Reclassify them before removing it.");
    }

    // ── Families ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<JobFamilyDto>> GetFamiliesAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var items = await _familyRepo.GetAllWithSubFamiliesAsync();
        return items.Where(f => f.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<JobFamilyDto>> GetActiveFamiliesAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var items = await _familyRepo.GetActiveAsync();
        return items.Where(f => f.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobFamilyDto> CreateFamilyAsync(CreateJobFamilyDto dto, CancellationToken ct = default)
    {
        await RequireFamilyCodeFreeAsync(dto.Code, null, ct);
        var e = dto.ToEntity();
        e.TenantId = GetTenantId();
        await _familyRepo.AddAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<JobFamilyDto> UpdateFamilyAsync(UpdateJobFamilyDto dto, CancellationToken ct = default)
    {
        var e = await GetOwnedFamilyAsync(dto.Id);
        await RequireFamilyCodeFreeAsync(dto.Code, dto.Id, ct);
        dto.UpdateEntity(e);
        await _familyRepo.UpdateAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<bool> DeleteFamilyAsync(Guid id, CancellationToken ct = default)
    {
        var e = await GetOwnedFamilyAsync(id);
        var tenantId = GetTenantId();

        // ⚠ BOTH obstacles reported together, not the first one found. A family in real use usually
        // has both sub-families and job descriptions under it, and reporting one at a time makes
        // the user clear four sub-families only to be told about eleven job descriptions. One
        // refusal that states the whole job is the difference between a rule and an obstacle course.
        var blockers = new List<string>();

        var liveSubFamilies = await _subFamilyRepo.GetQueryable()
            .CountAsync(sf => sf.TenantId == tenantId && !sf.IsDeleted && sf.JobFamilyId == id, ct);
        if (liveSubFamilies > 0)
            blockers.Add($"{liveSubFamilies} sub-{(liveSubFamilies == 1 ? "family" : "families")}");

        var inUse = await _unitOfWork.Repository<JobDescription>().GetQueryable()
            .CountAsync(jd => jd.TenantId == tenantId && !jd.IsDeleted && jd.JobFamilyId == id, ct);
        if (inUse > 0)
            blockers.Add($"{inUse} job description(s) still classified under it");

        if (blockers.Count > 0)
            throw JobArchitectureException.Conflict(
                $"This job family still has {string.Join(" and ", blockers)}. Move or reclassify them first.");

        await _familyRepo.DeleteAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    // ── Sub-families ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<JobSubFamilyDto>> GetSubFamiliesAsync(Guid familyId, CancellationToken ct = default)
    {
        await GetOwnedFamilyAsync(familyId);
        var tenantId = GetTenantId();
        var items = await _subFamilyRepo.GetByFamilyIdAsync(familyId);
        return items.Where(s => s.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<JobSubFamilyDto>> GetActiveSubFamiliesAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var items = await _subFamilyRepo.GetActiveAsync();
        return items.Where(s => s.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobSubFamilyDto> CreateSubFamilyAsync(CreateJobSubFamilyDto dto, CancellationToken ct = default)
    {
        await GetOwnedFamilyAsync(dto.JobFamilyId);
        await RequireSubFamilyCodeFreeAsync(dto.Code, null, ct);
        var e = dto.ToEntity();
        e.TenantId = GetTenantId();
        await _subFamilyRepo.AddAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<JobSubFamilyDto> UpdateSubFamilyAsync(UpdateJobSubFamilyDto dto, CancellationToken ct = default)
    {
        var e = await GetOwnedSubFamilyAsync(dto.Id);

        // ⚠ A sub-family cannot change families. Until the DTO carried one this was not a rule, it
        // was an accident of shape: the family in the body was simply not read, so the caller got
        // 200 and nothing moved. Refusing says what silence could not — that re-filing a sub-family
        // reclassifies every job description under it, and that is not an edit.
        if (dto.JobFamilyId.HasValue && dto.JobFamilyId.Value != e.JobFamilyId)
            throw JobArchitectureException.Invalid(
                "A sub-family cannot be moved to another job family. Create it under the family it belongs to, " +
                "and reclassify the job descriptions filed under this one.");

        await RequireSubFamilyCodeFreeAsync(dto.Code, dto.Id, ct);
        dto.UpdateEntity(e);
        await _subFamilyRepo.UpdateAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<bool> DeleteSubFamilyAsync(Guid id, CancellationToken ct = default)
    {
        var e = await GetOwnedSubFamilyAsync(id);
        await RequireNotInUseAsync(jd => jd.JobSubFamilyId == id, "sub-family", ct);
        await _subFamilyRepo.DeleteAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    // ── Levels ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<JobLevelDto>> GetLevelsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var items = await _levelRepo.GetAllOrderedAsync();
        return items.Where(l => l.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<JobLevelDto>> GetActiveLevelsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var items = await _levelRepo.GetActiveAsync();
        return items.Where(l => l.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobLevelDto> CreateLevelAsync(CreateJobLevelDto dto, CancellationToken ct = default)
    {
        await RequireLevelCodeAndRankFreeAsync(dto.Code, dto.Rank, null, ct);
        var e = dto.ToEntity();
        e.TenantId = GetTenantId();
        await _levelRepo.AddAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<JobLevelDto> UpdateLevelAsync(UpdateJobLevelDto dto, CancellationToken ct = default)
    {
        var e = await GetOwnedLevelAsync(dto.Id);
        await RequireLevelCodeAndRankFreeAsync(dto.Code, dto.Rank, dto.Id, ct);
        dto.UpdateEntity(e);
        await _levelRepo.UpdateAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default)
    {
        var e = await GetOwnedLevelAsync(id);
        await RequireNotInUseAsync(jd => jd.JobLevelId == id, "career level", ct);
        await _levelRepo.DeleteAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
