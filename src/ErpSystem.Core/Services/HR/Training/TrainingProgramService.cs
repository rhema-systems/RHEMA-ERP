using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingProgramService : ITrainingProgramService
{
    private readonly ITrainingProgramRepository _programRepository;
    private readonly ITrainingMaterialRepository _materialRepository;
    private readonly ITrainingProgramCompetencyRepository _competencyRepository;
    private readonly ITrainingProgramSkillRepository _programSkillRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingProgramService> _logger;

    public TrainingProgramService(
        ITrainingProgramRepository programRepository,
        ITrainingMaterialRepository materialRepository,
        ITrainingProgramCompetencyRepository competencyRepository,
        ITrainingProgramSkillRepository programSkillRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainingProgramService> logger)
    {
        _programRepository = programRepository;
        _materialRepository = materialRepository;
        _competencyRepository = competencyRepository;
        _programSkillRepository = programSkillRepository;
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

    // A row owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<TrainingProgram> GetOwnedProgramAsync(Guid id)
    {
        var entity = await _programRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training program with ID '{id}' not found.");
        return entity;
    }

    private async Task<TrainingMaterial> GetOwnedMaterialAsync(Guid id)
    {
        var entity = await _materialRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training material with ID '{id}' not found.");
        return entity;
    }

    private async Task<TrainingProgramCompetency> GetOwnedCompetencyAsync(Guid id)
    {
        var entity = await _competencyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Program competency link with ID '{id}' not found.");
        return entity;
    }

    private async Task<TrainingProgramSkill> GetOwnedProgramSkillAsync(Guid id)
    {
        var entity = await _programSkillRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Program skill link with ID '{id}' not found.");
        return entity;
    }

    // ── Program queries ───────────────────────────────────────────────────────

    public async Task<TrainingProgramDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _programRepository.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Training program with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingProgramDto?> GetByProgramCodeAsync(string programCode, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _programRepository.GetByProgramCodeAsync(programCode);

        // Program codes are unique per tenant, so a match owned by another tenant is reported as no match.
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<TrainingProgramSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var entities = await _programRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.CategoryOption)
            .Include(p => p.ProgramGroup)
            .OrderBy(p => p.ProgramName)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<TrainingProgramSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = _programRepository.GetQueryable().Where(p => p.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(p => p.CategoryOption)
            .Include(p => p.ProgramGroup)
            .OrderBy(p => p.ProgramName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TrainingProgramSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<TrainingProgramSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var entities = await _programRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.CategoryOption)
            .Include(p => p.ProgramGroup)
            .Where(p => p.IsActive)
            .OrderBy(p => p.ProgramName)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingProgramSummaryDto>> GetByCategoryAsync(Guid categoryOptionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programRepository.GetByCategoryAsync(categoryOptionId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingProgramSummaryDto>> GetByTypeAsync(TrainingType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programRepository.GetByTypeAsync(type);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingProgramSummaryDto>> GetWithCertificateAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programRepository.GetWithCertificateAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Program CRUD ──────────────────────────────────────────────────────────

    public async Task<TrainingProgramDto> CreateAsync(CreateTrainingProgramDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        var duplicate = await _programRepository.GetQueryable()
            .AnyAsync(p => p.TenantId == current && p.ProgramCode == createDto.ProgramCode, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A training program with code '{createDto.ProgramCode}' already exists.");

        var entity = createDto.ToEntity(current, createdByUserId);

        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program created: {ProgramCode} — {Name}", entity.ProgramCode, entity.ProgramName);

        return entity.ToDto();
    }

    public async Task<TrainingProgramDto> UpdateAsync(UpdateTrainingProgramDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program updated: {ProgramCode}", entity.ProgramCode);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(id);

        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program deleted: {ProgramCode}", entity.ProgramCode);

        return true;
    }

    // ── Material sub-operations ───────────────────────────────────────────────

    public async Task<TrainingMaterialDto> AddMaterialAsync(CreateTrainingMaterialDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedProgramAsync(createDto.ProgramId);

        var entity = createDto.ToEntity(current, createdByUserId);

        await _materialRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Material '{MaterialName}' added to program {ProgramId}", createDto.MaterialName, createDto.ProgramId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingMaterialDto>> GetMaterialsAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _materialRepository.GetByProgramIdAsync(programId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainingMaterialDto> UpdateMaterialAsync(UpdateTrainingMaterialDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMaterialAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _materialRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteMaterialAsync(Guid materialId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMaterialAsync(materialId);

        await _materialRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Competency sub-operations ──────────────────────────────────────────────

    public async Task<TrainingProgramCompetencyDto> AddCompetencyAsync(CreateTrainingProgramCompetencyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedProgramAsync(createDto.ProgramId);

        var entity = createDto.ToEntity(current, createdByUserId);

        await _competencyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingProgramCompetencyDto>> GetCompetenciesAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _competencyRepository.GetByProgramIdAsync(programId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteCompetencyAsync(Guid programCompetencyId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCompetencyAsync(programCompetencyId);

        await _competencyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Skill sub-operations ──────────────────────────────────────────────────

    public async Task<TrainingProgramSkillDto> AddSkillAsync(CreateTrainingProgramSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedProgramAsync(createDto.ProgramId);

        var entity = createDto.ToEntity(current, createdByUserId);

        await _programSkillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingProgramSkillDto>> GetSkillsAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programSkillRepository.GetByProgramIdAsync(programId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteSkillAsync(Guid programSkillId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramSkillAsync(programSkillId);

        await _programSkillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
