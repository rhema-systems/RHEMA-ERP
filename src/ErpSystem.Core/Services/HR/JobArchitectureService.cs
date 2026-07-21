using ErpSystem.Core.DTOs.HR;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobArchitectureService> _logger;

    public JobArchitectureService(
        IJobFamilyRepository familyRepo,
        IJobSubFamilyRepository subFamilyRepo,
        IJobLevelRepository levelRepo,
        IUnitOfWork unitOfWork,
        ILogger<JobArchitectureService> logger)
    {
        _familyRepo = familyRepo;
        _subFamilyRepo = subFamilyRepo;
        _levelRepo = levelRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Families ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<JobFamilyDto>> GetFamiliesAsync(CancellationToken ct = default)
        => (await _familyRepo.GetAllWithSubFamiliesAsync()).ToDtoList();

    public async Task<IEnumerable<JobFamilyDto>> GetActiveFamiliesAsync(CancellationToken ct = default)
        => (await _familyRepo.GetActiveAsync()).ToDtoList();

    public async Task<JobFamilyDto> CreateFamilyAsync(CreateJobFamilyDto dto, CancellationToken ct = default)
    {
        var e = dto.ToEntity();
        await _familyRepo.AddAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<JobFamilyDto> UpdateFamilyAsync(UpdateJobFamilyDto dto, CancellationToken ct = default)
    {
        var e = await _familyRepo.GetByIdAsync(dto.Id) ?? throw new ArgumentException("Job family not found");
        dto.UpdateEntity(e);
        await _familyRepo.UpdateAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<bool> DeleteFamilyAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _familyRepo.GetByIdAsync(id) ?? throw new ArgumentException("Job family not found");
        await _familyRepo.DeleteAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    // ── Sub-families ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<JobSubFamilyDto>> GetSubFamiliesAsync(Guid familyId, CancellationToken ct = default)
        => (await _subFamilyRepo.GetByFamilyIdAsync(familyId)).ToDtoList();

    public async Task<IEnumerable<JobSubFamilyDto>> GetActiveSubFamiliesAsync(CancellationToken ct = default)
        => (await _subFamilyRepo.GetActiveAsync()).ToDtoList();

    public async Task<JobSubFamilyDto> CreateSubFamilyAsync(CreateJobSubFamilyDto dto, CancellationToken ct = default)
    {
        var e = dto.ToEntity();
        await _subFamilyRepo.AddAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<JobSubFamilyDto> UpdateSubFamilyAsync(UpdateJobSubFamilyDto dto, CancellationToken ct = default)
    {
        var e = await _subFamilyRepo.GetByIdAsync(dto.Id) ?? throw new ArgumentException("Sub-family not found");
        dto.UpdateEntity(e);
        await _subFamilyRepo.UpdateAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<bool> DeleteSubFamilyAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _subFamilyRepo.GetByIdAsync(id) ?? throw new ArgumentException("Sub-family not found");
        await _subFamilyRepo.DeleteAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    // ── Levels ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<JobLevelDto>> GetLevelsAsync(CancellationToken ct = default)
        => (await _levelRepo.GetAllOrderedAsync()).ToDtoList();

    public async Task<IEnumerable<JobLevelDto>> GetActiveLevelsAsync(CancellationToken ct = default)
        => (await _levelRepo.GetActiveAsync()).ToDtoList();

    public async Task<JobLevelDto> CreateLevelAsync(CreateJobLevelDto dto, CancellationToken ct = default)
    {
        var e = dto.ToEntity();
        await _levelRepo.AddAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<JobLevelDto> UpdateLevelAsync(UpdateJobLevelDto dto, CancellationToken ct = default)
    {
        var e = await _levelRepo.GetByIdAsync(dto.Id) ?? throw new ArgumentException("Job level not found");
        dto.UpdateEntity(e);
        await _levelRepo.UpdateAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _levelRepo.GetByIdAsync(id) ?? throw new ArgumentException("Job level not found");
        await _levelRepo.DeleteAsync(e);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
