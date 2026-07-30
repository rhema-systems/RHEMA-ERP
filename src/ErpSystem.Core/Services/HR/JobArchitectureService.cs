using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
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
            throw new ArgumentException("Job family not found");
        return entity;
    }

    private async Task<JobSubFamily> GetOwnedSubFamilyAsync(Guid id)
    {
        var entity = await _subFamilyRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Sub-family not found");
        return entity;
    }

    private async Task<CareerLevel> GetOwnedLevelAsync(Guid id)
    {
        var entity = await _levelRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Job level not found");
        return entity;
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
        var e = dto.ToEntity();
        e.TenantId = GetTenantId();
        await _familyRepo.AddAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<JobFamilyDto> UpdateFamilyAsync(UpdateJobFamilyDto dto, CancellationToken ct = default)
    {
        var e = await GetOwnedFamilyAsync(dto.Id);
        dto.UpdateEntity(e);
        await _familyRepo.UpdateAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<bool> DeleteFamilyAsync(Guid id, CancellationToken ct = default)
    {
        var e = await GetOwnedFamilyAsync(id);
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
        var e = dto.ToEntity();
        e.TenantId = GetTenantId();
        await _subFamilyRepo.AddAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<JobSubFamilyDto> UpdateSubFamilyAsync(UpdateJobSubFamilyDto dto, CancellationToken ct = default)
    {
        var e = await GetOwnedSubFamilyAsync(dto.Id);
        dto.UpdateEntity(e);
        await _subFamilyRepo.UpdateAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<bool> DeleteSubFamilyAsync(Guid id, CancellationToken ct = default)
    {
        var e = await GetOwnedSubFamilyAsync(id);
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
        var e = dto.ToEntity();
        e.TenantId = GetTenantId();
        await _levelRepo.AddAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<JobLevelDto> UpdateLevelAsync(UpdateJobLevelDto dto, CancellationToken ct = default)
    {
        var e = await GetOwnedLevelAsync(dto.Id);
        dto.UpdateEntity(e);
        await _levelRepo.UpdateAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default)
    {
        var e = await GetOwnedLevelAsync(id);
        await _levelRepo.DeleteAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
