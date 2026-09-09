using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ═════════════════════════════════════════════════════════════════════════════
//  DEMO FEEDBACK ROUND 2, LANE C3 — THE THREE NAMED-SET MASTERS (plan § 6.4)
//
//  Benefit group, skill set, certification set. One shape, written out three
//  times rather than shared through a generic base: EF cannot reliably
//  translate interface property access on a generic entity parameter, and a
//  base class that compiles but throws at query time on one of three
//  subclasses is worse than the repetition it saves.
//
//  What is NOT here: how a position's attached sets combine with its individual
//  rows. That is one rule for all three and lives in PositionNamedSetService.
// ═════════════════════════════════════════════════════════════════════════════

#region Benefit groups

public interface IBenefitGroupService
{
    Task<IEnumerable<BenefitGroupDto>> GetAllAsync(bool activeOnly = false, string? search = null, CancellationToken ct = default);
    Task<BenefitGroupDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<BenefitGroupDto> CreateAsync(CreateBenefitGroupDto dto, CancellationToken ct = default);
    Task<BenefitGroupDto> UpdateAsync(UpdateBenefitGroupDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<BenefitGroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken ct = default);
    Task<BenefitGroupMemberDto> AddMemberAsync(Guid groupId, BenefitGroupMemberInputDto dto, CancellationToken ct = default);
    Task<BenefitGroupMemberDto> UpdateMemberAsync(Guid groupId, Guid memberId, BenefitGroupMemberInputDto dto, CancellationToken ct = default);
    Task<bool> RemoveMemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default);
}

public class BenefitGroupService : IBenefitGroupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<BenefitGroupService> _logger;

    public BenefitGroupService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUser, ILogger<BenefitGroupService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private static string Trim(string? v) => string.IsNullOrWhiteSpace(v) ? string.Empty : v.Trim();
    private static string? TrimOrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private IQueryable<BenefitGroup> Groups(Guid tenantId) =>
        _unitOfWork.Repository<BenefitGroup>().GetQueryable().Where(g => g.TenantId == tenantId && !g.IsDeleted);

    private IQueryable<BenefitGroupMember> Members(Guid tenantId) =>
        _unitOfWork.Repository<BenefitGroupMember>().GetQueryable()
            .Include(m => m.Policy)
            .Where(m => m.TenantId == tenantId && !m.IsDeleted);

    private async Task<BenefitGroup> RequireAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        await Groups(tenantId).FirstOrDefaultAsync(g => g.Id == id, ct)
        ?? throw new ArgumentException($"Benefit group with ID '{id}' not found.");

    private async Task RequireUnusedAsync(Guid tenantId, string name, string? code, Guid? exceptId, CancellationToken ct)
    {
        var n = Trim(name);
        if (await Groups(tenantId).AnyAsync(g => g.Name == n && (exceptId == null || g.Id != exceptId), ct))
            throw new InvalidOperationException($"A benefit group named '{n}' already exists.");

        var c = TrimOrNull(code);
        if (c != null && await Groups(tenantId).AnyAsync(g => g.Code == c && (exceptId == null || g.Id != exceptId), ct))
            throw new InvalidOperationException($"A benefit group with code '{c}' already exists.");
    }

    public async Task<IEnumerable<BenefitGroupDto>> GetAllAsync(bool activeOnly = false, string? search = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = Groups(tenantId);
        if (activeOnly) query = query.Where(g => g.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(g => g.Name.Contains(term) || (g.Code != null && g.Code.Contains(term)));
        }

        var rows = await query.OrderBy(g => g.Name).ToListAsync(ct);
        return await WithCountsAsync(tenantId, rows, ct);
    }

    public async Task<BenefitGroupDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var row = await RequireAsync(tenantId, id, ct);
        var dto = (await WithCountsAsync(tenantId, new[] { row }, ct)).Single();
        dto.Members = (await GetMembersAsync(id, ct)).ToList();
        return dto;
    }

    private async Task<List<BenefitGroupDto>> WithCountsAsync(Guid tenantId, IReadOnlyCollection<BenefitGroup> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var memberCounts = await _unitOfWork.Repository<BenefitGroupMember>().GetQueryable()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && ids.Contains(m.BenefitGroupId))
            .GroupBy(m => m.BenefitGroupId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var positionCounts = await _unitOfWork.Repository<EmployeePositionBenefitGroup>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && ids.Contains(a.BenefitGroupId))
            .GroupBy(a => a.BenefitGroupId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        return rows.Select(r => new BenefitGroupDto
        {
            Id = r.Id,
            TenantId = r.TenantId,
            Name = r.Name,
            Code = r.Code,
            Description = r.Description,
            IsActive = r.IsActive,
            MemberCount = memberCounts.FirstOrDefault(x => x.Key == r.Id)?.Count ?? 0,
            PositionCount = positionCounts.FirstOrDefault(x => x.Key == r.Id)?.Count ?? 0,
            CreatedAt = r.CreatedAt,
            CreatedBy = r.CreatedBy ?? string.Empty,
            UpdatedAt = r.UpdatedAt,
            UpdatedBy = r.UpdatedBy,
        }).ToList();
    }

    public async Task<BenefitGroupDto> CreateAsync(CreateBenefitGroupDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await RequireUnusedAsync(tenantId, dto.Name, dto.Code, null, ct);

        var entity = new BenefitGroup
        {
            TenantId = tenantId,
            Name = Trim(dto.Name),
            Code = TrimOrNull(dto.Code),
            Description = TrimOrNull(dto.Description),
            IsActive = dto.IsActive,
        }.StampCreated(_currentUser);

        await _unitOfWork.Repository<BenefitGroup>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Benefit group created: {Id} ({Name})", entity.Id, entity.Name);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<BenefitGroupDto> UpdateAsync(UpdateBenefitGroupDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, dto.Id, ct);
        await RequireUnusedAsync(tenantId, dto.Name, dto.Code, dto.Id, ct);

        entity.Name = Trim(dto.Name);
        entity.Code = TrimOrNull(dto.Code);
        entity.Description = TrimOrNull(dto.Description);
        entity.IsActive = dto.IsActive;
        entity.StampUpdated(_currentUser);

        await _unitOfWork.Repository<BenefitGroup>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, ct);

        // A group a post is standing on cannot be deleted out from under it — retire it instead,
        // which stops it being offered and leaves every post that holds it working.
        var positions = await _unitOfWork.Repository<EmployeePositionBenefitGroup>().GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.BenefitGroupId == id, ct);
        if (positions > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' is attached to {positions} position{(positions == 1 ? "" : "s")}, so it cannot be deleted. "
                + "Detach it from those positions first, or set it inactive to stop it being offered.");

        foreach (var m in await Members(tenantId).Where(m => m.BenefitGroupId == id).ToListAsync(ct))
        {
            m.IsDeleted = true;
            m.DeletedAt = DateTime.UtcNow;
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Benefit group deleted (soft): {Id}", id);
        return true;
    }

    public async Task<IEnumerable<BenefitGroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        _ = await RequireAsync(tenantId, groupId, ct);
        return await Members(tenantId)
            .Where(m => m.BenefitGroupId == groupId)
            .OrderBy(m => m.Policy.PolicyName)
            .Select(m => new BenefitGroupMemberDto
            {
                Id = m.Id,
                BenefitGroupId = m.BenefitGroupId,
                PolicyId = m.PolicyId,
                PolicyName = m.Policy.PolicyName,
                PolicyCode = m.Policy.PolicyCode,
                PolicyIsActive = m.Policy.IsActive,
                CreatedAt = m.CreatedAt,
                CreatedBy = m.CreatedBy ?? string.Empty,
                UpdatedAt = m.UpdatedAt,
                UpdatedBy = m.UpdatedBy,
            })
            .ToListAsync(ct);
    }

    private async Task RequireActivePolicyAsync(Guid tenantId, Guid policyId, CancellationToken ct)
    {
        var policy = await _unitOfWork.Repository<BenefitPolicy>().GetQueryable()
                         .FirstOrDefaultAsync(p => p.Id == policyId && p.TenantId == tenantId && !p.IsDeleted, ct)
                     ?? throw new ArgumentException($"Benefit policy with ID '{policyId}' not found.");
        if (!policy.IsActive)
            throw new InvalidOperationException($"'{policy.PolicyName}' is retired, so it cannot be added to a group.");
    }

    public async Task<BenefitGroupMemberDto> AddMemberAsync(Guid groupId, BenefitGroupMemberInputDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var group = await RequireAsync(tenantId, groupId, ct);
        await RequireActivePolicyAsync(tenantId, dto.PolicyId, ct);

        if (await Members(tenantId).AnyAsync(m => m.BenefitGroupId == groupId && m.PolicyId == dto.PolicyId, ct))
            throw new InvalidOperationException($"That benefit is already in '{group.Name}'.");

        var entity = new BenefitGroupMember
        {
            TenantId = tenantId,
            BenefitGroupId = groupId,
            PolicyId = dto.PolicyId,
        }.StampCreated(_currentUser);

        await _unitOfWork.Repository<BenefitGroupMember>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetMembersAsync(groupId, ct)).First(m => m.Id == entity.Id);
    }

    public async Task<BenefitGroupMemberDto> UpdateMemberAsync(Guid groupId, Guid memberId, BenefitGroupMemberInputDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var group = await RequireAsync(tenantId, groupId, ct);
        var entity = await _unitOfWork.Repository<BenefitGroupMember>().GetQueryable()
                         .FirstOrDefaultAsync(m => m.Id == memberId && m.BenefitGroupId == groupId && m.TenantId == tenantId && !m.IsDeleted, ct)
                     ?? throw new ArgumentException($"Benefit group member with ID '{memberId}' not found.");

        if (entity.PolicyId != dto.PolicyId)
        {
            await RequireActivePolicyAsync(tenantId, dto.PolicyId, ct);
            if (await Members(tenantId).AnyAsync(m => m.BenefitGroupId == groupId && m.PolicyId == dto.PolicyId && m.Id != memberId, ct))
                throw new InvalidOperationException($"That benefit is already in '{group.Name}'.");
            entity.PolicyId = dto.PolicyId;
        }

        entity.StampUpdated(_currentUser);
        await _unitOfWork.Repository<BenefitGroupMember>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetMembersAsync(groupId, ct)).First(m => m.Id == entity.Id);
    }

    public async Task<bool> RemoveMemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        _ = await RequireAsync(tenantId, groupId, ct);
        var entity = await _unitOfWork.Repository<BenefitGroupMember>().GetQueryable()
                         .FirstOrDefaultAsync(m => m.Id == memberId && m.BenefitGroupId == groupId && m.TenantId == tenantId && !m.IsDeleted, ct)
                     ?? throw new ArgumentException($"Benefit group member with ID '{memberId}' not found.");

        // ⚠ Deliberately NOT refused when positions hold the group (§ 6.4.2 rule 6): editing a
        // set's membership after posts have attached it is allowed. A post left holding a
        // redundant individual row is not broken — its next save trips the duplicate rule, and
        // the effective read marks the row meanwhile.
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

#region Skill sets

public interface ISkillSetService
{
    Task<IEnumerable<SkillSetDto>> GetAllAsync(bool activeOnly = false, string? search = null, CancellationToken ct = default);
    Task<SkillSetDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<SkillSetDto> CreateAsync(CreateSkillSetDto dto, CancellationToken ct = default);
    Task<SkillSetDto> UpdateAsync(UpdateSkillSetDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<SkillSetMemberDto>> GetMembersAsync(Guid setId, CancellationToken ct = default);
    Task<SkillSetMemberDto> AddMemberAsync(Guid setId, SkillSetMemberInputDto dto, CancellationToken ct = default);
    Task<SkillSetMemberDto> UpdateMemberAsync(Guid setId, Guid memberId, SkillSetMemberInputDto dto, CancellationToken ct = default);
    Task<bool> RemoveMemberAsync(Guid setId, Guid memberId, CancellationToken ct = default);
}

public class SkillSetService : ISkillSetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<SkillSetService> _logger;

    public SkillSetService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUser, ILogger<SkillSetService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private static string Trim(string? v) => string.IsNullOrWhiteSpace(v) ? string.Empty : v.Trim();
    private static string? TrimOrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private IQueryable<SkillSet> Sets(Guid tenantId) =>
        _unitOfWork.Repository<SkillSet>().GetQueryable().Where(s => s.TenantId == tenantId && !s.IsDeleted);

    private IQueryable<SkillSetMember> Members(Guid tenantId) =>
        _unitOfWork.Repository<SkillSetMember>().GetQueryable()
            .Include(m => m.Skill)
            .Where(m => m.TenantId == tenantId && !m.IsDeleted);

    private async Task<SkillSet> RequireAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        await Sets(tenantId).FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new ArgumentException($"Skill set with ID '{id}' not found.");

    private async Task RequireUnusedAsync(Guid tenantId, string name, string? code, Guid? exceptId, CancellationToken ct)
    {
        var n = Trim(name);
        if (await Sets(tenantId).AnyAsync(s => s.Name == n && (exceptId == null || s.Id != exceptId), ct))
            throw new InvalidOperationException($"A skill set named '{n}' already exists.");

        var c = TrimOrNull(code);
        if (c != null && await Sets(tenantId).AnyAsync(s => s.Code == c && (exceptId == null || s.Id != exceptId), ct))
            throw new InvalidOperationException($"A skill set with code '{c}' already exists.");
    }

    public async Task<IEnumerable<SkillSetDto>> GetAllAsync(bool activeOnly = false, string? search = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = Sets(tenantId);
        if (activeOnly) query = query.Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.Name.Contains(term) || (s.Code != null && s.Code.Contains(term)));
        }

        var rows = await query.OrderBy(s => s.Name).ToListAsync(ct);
        return await WithCountsAsync(tenantId, rows, ct);
    }

    public async Task<SkillSetDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var row = await RequireAsync(tenantId, id, ct);
        var dto = (await WithCountsAsync(tenantId, new[] { row }, ct)).Single();
        dto.Members = (await GetMembersAsync(id, ct)).ToList();
        return dto;
    }

    private async Task<List<SkillSetDto>> WithCountsAsync(Guid tenantId, IReadOnlyCollection<SkillSet> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var memberCounts = await _unitOfWork.Repository<SkillSetMember>().GetQueryable()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && ids.Contains(m.SkillSetId))
            .GroupBy(m => m.SkillSetId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var positionCounts = await _unitOfWork.Repository<PositionSkillSet>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && ids.Contains(a.SkillSetId))
            .GroupBy(a => a.SkillSetId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        return rows.Select(r => new SkillSetDto
        {
            Id = r.Id,
            TenantId = r.TenantId,
            Name = r.Name,
            Code = r.Code,
            Description = r.Description,
            IsActive = r.IsActive,
            MemberCount = memberCounts.FirstOrDefault(x => x.Key == r.Id)?.Count ?? 0,
            PositionCount = positionCounts.FirstOrDefault(x => x.Key == r.Id)?.Count ?? 0,
            CreatedAt = r.CreatedAt,
            CreatedBy = r.CreatedBy ?? string.Empty,
            UpdatedAt = r.UpdatedAt,
            UpdatedBy = r.UpdatedBy,
        }).ToList();
    }

    public async Task<SkillSetDto> CreateAsync(CreateSkillSetDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await RequireUnusedAsync(tenantId, dto.Name, dto.Code, null, ct);

        var entity = new SkillSet
        {
            TenantId = tenantId,
            Name = Trim(dto.Name),
            Code = TrimOrNull(dto.Code),
            Description = TrimOrNull(dto.Description),
            IsActive = dto.IsActive,
        }.StampCreated(_currentUser);

        await _unitOfWork.Repository<SkillSet>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Skill set created: {Id} ({Name})", entity.Id, entity.Name);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<SkillSetDto> UpdateAsync(UpdateSkillSetDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, dto.Id, ct);
        await RequireUnusedAsync(tenantId, dto.Name, dto.Code, dto.Id, ct);

        entity.Name = Trim(dto.Name);
        entity.Code = TrimOrNull(dto.Code);
        entity.Description = TrimOrNull(dto.Description);
        entity.IsActive = dto.IsActive;
        entity.StampUpdated(_currentUser);

        await _unitOfWork.Repository<SkillSet>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, ct);

        var positions = await _unitOfWork.Repository<PositionSkillSet>().GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.SkillSetId == id, ct);
        if (positions > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' is attached to {positions} position{(positions == 1 ? "" : "s")}, so it cannot be deleted. "
                + "Detach it from those positions first, or set it inactive to stop it being offered.");

        foreach (var m in await Members(tenantId).Where(m => m.SkillSetId == id).ToListAsync(ct))
        {
            m.IsDeleted = true;
            m.DeletedAt = DateTime.UtcNow;
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Skill set deleted (soft): {Id}", id);
        return true;
    }

    public async Task<IEnumerable<SkillSetMemberDto>> GetMembersAsync(Guid setId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        _ = await RequireAsync(tenantId, setId, ct);
        return await Members(tenantId)
            .Where(m => m.SkillSetId == setId)
            .OrderByDescending(m => m.Priority).ThenBy(m => m.Skill.Name)
            .Select(m => new SkillSetMemberDto
            {
                Id = m.Id,
                SkillSetId = m.SkillSetId,
                SkillId = m.SkillId,
                SkillName = m.Skill.Name,
                SkillCategory = m.Skill.Category,
                SkillIsActive = m.Skill.IsActive,
                RequiredLevel = m.RequiredLevel,
                IsRequired = m.IsRequired,
                Priority = m.Priority,
                CreatedAt = m.CreatedAt,
                CreatedBy = m.CreatedBy ?? string.Empty,
                UpdatedAt = m.UpdatedAt,
                UpdatedBy = m.UpdatedBy,
            })
            .ToListAsync(ct);
    }

    private async Task RequireActiveSkillAsync(Guid tenantId, Guid skillId, CancellationToken ct)
    {
        var skill = await _unitOfWork.Repository<Skill>().GetQueryable()
                        .FirstOrDefaultAsync(s => s.Id == skillId && s.TenantId == tenantId && !s.IsDeleted, ct)
                    ?? throw new ArgumentException($"Skill with ID '{skillId}' not found.");
        if (!skill.IsActive)
            throw new InvalidOperationException($"'{skill.Name}' is retired, so it cannot be added to a set.");
    }

    public async Task<SkillSetMemberDto> AddMemberAsync(Guid setId, SkillSetMemberInputDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var set = await RequireAsync(tenantId, setId, ct);
        await RequireActiveSkillAsync(tenantId, dto.SkillId, ct);

        if (await Members(tenantId).AnyAsync(m => m.SkillSetId == setId && m.SkillId == dto.SkillId, ct))
            throw new InvalidOperationException($"That skill is already in '{set.Name}'.");

        var entity = new SkillSetMember
        {
            TenantId = tenantId,
            SkillSetId = setId,
            SkillId = dto.SkillId,
            RequiredLevel = dto.RequiredLevel,
            IsRequired = dto.IsRequired,
            Priority = dto.Priority,
        }.StampCreated(_currentUser);

        await _unitOfWork.Repository<SkillSetMember>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetMembersAsync(setId, ct)).First(m => m.Id == entity.Id);
    }

    public async Task<SkillSetMemberDto> UpdateMemberAsync(Guid setId, Guid memberId, SkillSetMemberInputDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var set = await RequireAsync(tenantId, setId, ct);
        var entity = await _unitOfWork.Repository<SkillSetMember>().GetQueryable()
                         .FirstOrDefaultAsync(m => m.Id == memberId && m.SkillSetId == setId && m.TenantId == tenantId && !m.IsDeleted, ct)
                     ?? throw new ArgumentException($"Skill set member with ID '{memberId}' not found.");

        if (entity.SkillId != dto.SkillId)
        {
            await RequireActiveSkillAsync(tenantId, dto.SkillId, ct);
            if (await Members(tenantId).AnyAsync(m => m.SkillSetId == setId && m.SkillId == dto.SkillId && m.Id != memberId, ct))
                throw new InvalidOperationException($"That skill is already in '{set.Name}'.");
            entity.SkillId = dto.SkillId;
        }

        entity.RequiredLevel = dto.RequiredLevel;
        entity.IsRequired = dto.IsRequired;
        entity.Priority = dto.Priority;
        entity.StampUpdated(_currentUser);

        await _unitOfWork.Repository<SkillSetMember>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetMembersAsync(setId, ct)).First(m => m.Id == entity.Id);
    }

    public async Task<bool> RemoveMemberAsync(Guid setId, Guid memberId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        _ = await RequireAsync(tenantId, setId, ct);
        var entity = await _unitOfWork.Repository<SkillSetMember>().GetQueryable()
                         .FirstOrDefaultAsync(m => m.Id == memberId && m.SkillSetId == setId && m.TenantId == tenantId && !m.IsDeleted, ct)
                     ?? throw new ArgumentException($"Skill set member with ID '{memberId}' not found.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

#region Certification sets

public interface ICertificationSetService
{
    Task<IEnumerable<CertificationSetDto>> GetAllAsync(bool activeOnly = false, string? search = null, CancellationToken ct = default);
    Task<CertificationSetDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CertificationSetDto> CreateAsync(CreateCertificationSetDto dto, CancellationToken ct = default);
    Task<CertificationSetDto> UpdateAsync(UpdateCertificationSetDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<CertificationSetMemberDto>> GetMembersAsync(Guid setId, CancellationToken ct = default);
    Task<CertificationSetMemberDto> AddMemberAsync(Guid setId, CertificationSetMemberInputDto dto, CancellationToken ct = default);
    Task<CertificationSetMemberDto> UpdateMemberAsync(Guid setId, Guid memberId, CertificationSetMemberInputDto dto, CancellationToken ct = default);
    Task<bool> RemoveMemberAsync(Guid setId, Guid memberId, CancellationToken ct = default);
}

public class CertificationSetService : ICertificationSetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<CertificationSetService> _logger;

    public CertificationSetService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUser, ILogger<CertificationSetService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private static string Trim(string? v) => string.IsNullOrWhiteSpace(v) ? string.Empty : v.Trim();
    private static string? TrimOrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private IQueryable<CertificationSet> Sets(Guid tenantId) =>
        _unitOfWork.Repository<CertificationSet>().GetQueryable().Where(s => s.TenantId == tenantId && !s.IsDeleted);

    private IQueryable<CertificationSetMember> Members(Guid tenantId) =>
        _unitOfWork.Repository<CertificationSetMember>().GetQueryable()
            .Include(m => m.Certification).ThenInclude(c => c.CertifyingBody)
            .Where(m => m.TenantId == tenantId && !m.IsDeleted);

    private async Task<CertificationSet> RequireAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        await Sets(tenantId).FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new ArgumentException($"Certification set with ID '{id}' not found.");

    private async Task RequireUnusedAsync(Guid tenantId, string name, string? code, Guid? exceptId, CancellationToken ct)
    {
        var n = Trim(name);
        if (await Sets(tenantId).AnyAsync(s => s.Name == n && (exceptId == null || s.Id != exceptId), ct))
            throw new InvalidOperationException($"A certification set named '{n}' already exists.");

        var c = TrimOrNull(code);
        if (c != null && await Sets(tenantId).AnyAsync(s => s.Code == c && (exceptId == null || s.Id != exceptId), ct))
            throw new InvalidOperationException($"A certification set with code '{c}' already exists.");
    }

    public async Task<IEnumerable<CertificationSetDto>> GetAllAsync(bool activeOnly = false, string? search = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = Sets(tenantId);
        if (activeOnly) query = query.Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.Name.Contains(term) || (s.Code != null && s.Code.Contains(term)));
        }

        var rows = await query.OrderBy(s => s.Name).ToListAsync(ct);
        return await WithCountsAsync(tenantId, rows, ct);
    }

    public async Task<CertificationSetDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var row = await RequireAsync(tenantId, id, ct);
        var dto = (await WithCountsAsync(tenantId, new[] { row }, ct)).Single();
        dto.Members = (await GetMembersAsync(id, ct)).ToList();
        return dto;
    }

    private async Task<List<CertificationSetDto>> WithCountsAsync(Guid tenantId, IReadOnlyCollection<CertificationSet> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var memberCounts = await _unitOfWork.Repository<CertificationSetMember>().GetQueryable()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && ids.Contains(m.CertificationSetId))
            .GroupBy(m => m.CertificationSetId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var positionCounts = await _unitOfWork.Repository<PositionCertificationSet>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && ids.Contains(a.CertificationSetId))
            .GroupBy(a => a.CertificationSetId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        return rows.Select(r => new CertificationSetDto
        {
            Id = r.Id,
            TenantId = r.TenantId,
            Name = r.Name,
            Code = r.Code,
            Description = r.Description,
            IsActive = r.IsActive,
            MemberCount = memberCounts.FirstOrDefault(x => x.Key == r.Id)?.Count ?? 0,
            PositionCount = positionCounts.FirstOrDefault(x => x.Key == r.Id)?.Count ?? 0,
            CreatedAt = r.CreatedAt,
            CreatedBy = r.CreatedBy ?? string.Empty,
            UpdatedAt = r.UpdatedAt,
            UpdatedBy = r.UpdatedBy,
        }).ToList();
    }

    public async Task<CertificationSetDto> CreateAsync(CreateCertificationSetDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await RequireUnusedAsync(tenantId, dto.Name, dto.Code, null, ct);

        var entity = new CertificationSet
        {
            TenantId = tenantId,
            Name = Trim(dto.Name),
            Code = TrimOrNull(dto.Code),
            Description = TrimOrNull(dto.Description),
            IsActive = dto.IsActive,
        }.StampCreated(_currentUser);

        await _unitOfWork.Repository<CertificationSet>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Certification set created: {Id} ({Name})", entity.Id, entity.Name);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<CertificationSetDto> UpdateAsync(UpdateCertificationSetDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, dto.Id, ct);
        await RequireUnusedAsync(tenantId, dto.Name, dto.Code, dto.Id, ct);

        entity.Name = Trim(dto.Name);
        entity.Code = TrimOrNull(dto.Code);
        entity.Description = TrimOrNull(dto.Description);
        entity.IsActive = dto.IsActive;
        entity.StampUpdated(_currentUser);

        await _unitOfWork.Repository<CertificationSet>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, ct);

        var positions = await _unitOfWork.Repository<PositionCertificationSet>().GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.CertificationSetId == id, ct);
        if (positions > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' is attached to {positions} position{(positions == 1 ? "" : "s")}, so it cannot be deleted. "
                + "Detach it from those positions first, or set it inactive to stop it being offered.");

        foreach (var m in await Members(tenantId).Where(m => m.CertificationSetId == id).ToListAsync(ct))
        {
            m.IsDeleted = true;
            m.DeletedAt = DateTime.UtcNow;
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Certification set deleted (soft): {Id}", id);
        return true;
    }

    public async Task<IEnumerable<CertificationSetMemberDto>> GetMembersAsync(Guid setId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        _ = await RequireAsync(tenantId, setId, ct);
        return await Members(tenantId)
            .Where(m => m.CertificationSetId == setId)
            .OrderBy(m => m.Certification.Name)
            .Select(m => new CertificationSetMemberDto
            {
                Id = m.Id,
                CertificationSetId = m.CertificationSetId,
                CertificationId = m.CertificationId,
                CertificationName = m.Certification.Name,
                CertificationCode = m.Certification.Code,
                CertifyingBodyName = m.Certification.CertifyingBody.Name,
                CertificationIsActive = m.Certification.IsActive,
                IsMandatory = m.IsMandatory,
                CreatedAt = m.CreatedAt,
                CreatedBy = m.CreatedBy ?? string.Empty,
                UpdatedAt = m.UpdatedAt,
                UpdatedBy = m.UpdatedBy,
            })
            .ToListAsync(ct);
    }

    private async Task RequireActiveCertificationAsync(Guid tenantId, Guid certificationId, CancellationToken ct)
    {
        var cert = await _unitOfWork.Repository<Certification>().GetQueryable()
                       .FirstOrDefaultAsync(c => c.Id == certificationId && c.TenantId == tenantId && !c.IsDeleted, ct)
                   ?? throw new ArgumentException($"Certification with ID '{certificationId}' not found.");
        if (!cert.IsActive)
            throw new InvalidOperationException($"'{cert.Name}' is retired, so it cannot be added to a set.");
    }

    public async Task<CertificationSetMemberDto> AddMemberAsync(Guid setId, CertificationSetMemberInputDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var set = await RequireAsync(tenantId, setId, ct);
        await RequireActiveCertificationAsync(tenantId, dto.CertificationId, ct);

        if (await Members(tenantId).AnyAsync(m => m.CertificationSetId == setId && m.CertificationId == dto.CertificationId, ct))
            throw new InvalidOperationException($"That credential is already in '{set.Name}'.");

        var entity = new CertificationSetMember
        {
            TenantId = tenantId,
            CertificationSetId = setId,
            CertificationId = dto.CertificationId,
            IsMandatory = dto.IsMandatory,
        }.StampCreated(_currentUser);

        await _unitOfWork.Repository<CertificationSetMember>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetMembersAsync(setId, ct)).First(m => m.Id == entity.Id);
    }

    public async Task<CertificationSetMemberDto> UpdateMemberAsync(Guid setId, Guid memberId, CertificationSetMemberInputDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var set = await RequireAsync(tenantId, setId, ct);
        var entity = await _unitOfWork.Repository<CertificationSetMember>().GetQueryable()
                         .FirstOrDefaultAsync(m => m.Id == memberId && m.CertificationSetId == setId && m.TenantId == tenantId && !m.IsDeleted, ct)
                     ?? throw new ArgumentException($"Certification set member with ID '{memberId}' not found.");

        if (entity.CertificationId != dto.CertificationId)
        {
            await RequireActiveCertificationAsync(tenantId, dto.CertificationId, ct);
            if (await Members(tenantId).AnyAsync(m => m.CertificationSetId == setId && m.CertificationId == dto.CertificationId && m.Id != memberId, ct))
                throw new InvalidOperationException($"That credential is already in '{set.Name}'.");
            entity.CertificationId = dto.CertificationId;
        }

        entity.IsMandatory = dto.IsMandatory;
        entity.StampUpdated(_currentUser);

        await _unitOfWork.Repository<CertificationSetMember>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetMembersAsync(setId, ct)).First(m => m.Id == entity.Id);
    }

    public async Task<bool> RemoveMemberAsync(Guid setId, Guid memberId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        _ = await RequireAsync(tenantId, setId, ct);
        var entity = await _unitOfWork.Repository<CertificationSetMember>().GetQueryable()
                         .FirstOrDefaultAsync(m => m.Id == memberId && m.CertificationSetId == setId && m.TenantId == tenantId && !m.IsDeleted, ct)
                     ?? throw new ArgumentException($"Certification set member with ID '{memberId}' not found.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion
