using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingProgramService> _logger;

    public TrainingProgramService(
        ITrainingProgramRepository programRepository,
        ITrainingMaterialRepository materialRepository,
        ITrainingProgramCompetencyRepository competencyRepository,
        ITrainingProgramSkillRepository programSkillRepository,
        IUnitOfWork unitOfWork,
        ILogger<TrainingProgramService> logger)
    {
        _programRepository = programRepository;
        _materialRepository = materialRepository;
        _competencyRepository = competencyRepository;
        _programSkillRepository = programSkillRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Program queries ───────────────────────────────────────────────────────

    public async Task<TrainingProgramDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training program with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingProgramDto?> GetByProgramCodeAsync(string programCode, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetByProgramCodeAsync(programCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TrainingProgramSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _programRepository.GetQueryable()
            .Include(p => p.CategoryOption)
            .Include(p => p.ProgramGroup)
            .OrderBy(p => p.ProgramName)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<TrainingProgramSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _programRepository.GetQueryable();
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
        var entities = await _programRepository.GetQueryable()
            .Include(p => p.CategoryOption)
            .Include(p => p.ProgramGroup)
            .Where(p => p.IsActive)
            .OrderBy(p => p.ProgramName)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingProgramSummaryDto>> GetByCategoryAsync(Guid categoryOptionId, CancellationToken cancellationToken = default)
    {
        var entities = await _programRepository.GetByCategoryAsync(categoryOptionId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingProgramSummaryDto>> GetByTypeAsync(TrainingType type, CancellationToken cancellationToken = default)
    {
        var entities = await _programRepository.GetByTypeAsync(type);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingProgramSummaryDto>> GetWithCertificateAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _programRepository.GetWithCertificateAsync();
        return entities.ToSummaryDtoList();
    }

    // ── Program CRUD ──────────────────────────────────────────────────────────

    public async Task<TrainingProgramDto> CreateAsync(CreateTrainingProgramDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program created: {ProgramCode} — {Name}", entity.ProgramCode, entity.ProgramName);

        return entity.ToDto();
    }

    public async Task<TrainingProgramDto> UpdateAsync(UpdateTrainingProgramDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Training program with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program updated: {ProgramCode}", entity.ProgramCode);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training program with ID '{id}' not found.");

        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program deleted: {ProgramCode}", entity.ProgramCode);

        return true;
    }

    // ── Material sub-operations ───────────────────────────────────────────────

    public async Task<TrainingMaterialDto> AddMaterialAsync(CreateTrainingMaterialDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var program = await _programRepository.GetByIdAsync(createDto.ProgramId);

        if (program == null)
            throw new ArgumentException($"Training program with ID '{createDto.ProgramId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _materialRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Material '{MaterialName}' added to program {ProgramId}", createDto.MaterialName, createDto.ProgramId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingMaterialDto>> GetMaterialsAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var entities = await _materialRepository.GetByProgramIdAsync(programId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainingMaterialDto> UpdateMaterialAsync(UpdateTrainingMaterialDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _materialRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Training material with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _materialRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteMaterialAsync(Guid materialId, CancellationToken cancellationToken = default)
    {
        var entity = await _materialRepository.GetByIdAsync(materialId);

        if (entity == null)
            throw new ArgumentException($"Training material with ID '{materialId}' not found.");

        await _materialRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Competency sub-operations ──────────────────────────────────────────────

    public async Task<TrainingProgramCompetencyDto> AddCompetencyAsync(CreateTrainingProgramCompetencyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var program = await _programRepository.GetByIdAsync(createDto.ProgramId);

        if (program == null)
            throw new ArgumentException($"Training program with ID '{createDto.ProgramId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _competencyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingProgramCompetencyDto>> GetCompetenciesAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var entities = await _competencyRepository.GetByProgramIdAsync(programId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteCompetencyAsync(Guid programCompetencyId, CancellationToken cancellationToken = default)
    {
        var entity = await _competencyRepository.GetByIdAsync(programCompetencyId);

        if (entity == null)
            throw new ArgumentException($"Program competency link with ID '{programCompetencyId}' not found.");

        await _competencyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Skill sub-operations ──────────────────────────────────────────────────

    public async Task<TrainingProgramSkillDto> AddSkillAsync(CreateTrainingProgramSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var program = await _programRepository.GetByIdAsync(createDto.ProgramId);

        if (program == null)
            throw new ArgumentException($"Training program with ID '{createDto.ProgramId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _programSkillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingProgramSkillDto>> GetSkillsAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var entities = await _programSkillRepository.GetByProgramIdAsync(programId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteSkillAsync(Guid programSkillId, CancellationToken cancellationToken = default)
    {
        var entity = await _programSkillRepository.GetByIdAsync(programSkillId);

        if (entity == null)
            throw new ArgumentException($"Program skill link with ID '{programSkillId}' not found.");

        await _programSkillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
