using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Appraisal Cycle Target

public class AppraisalCycleTargetService : IAppraisalCycleTargetService
{
    private readonly IGenericRepository<AppraisalCycleTarget> _targetRepository;
    private readonly IGenericRepository<AppraisalCycleTargetExclusion> _exclusionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCycleTargetService> _logger;

    public AppraisalCycleTargetService(
        IGenericRepository<AppraisalCycleTarget> targetRepository,
        IGenericRepository<AppraisalCycleTargetExclusion> exclusionRepository,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCycleTargetService> logger)
    {
        _targetRepository = targetRepository;
        _exclusionRepository = exclusionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AppraisalCycleTargetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal cycle target with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalCycleTargetDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var entities = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .Where(t => t.AppraisalCycleId == cycleId)
            .OrderBy(t => t.TargetType)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalCycleTargetDto>> GetByTargetTypeAsync(AppraisalTargetType targetType, CancellationToken cancellationToken = default)
    {
        var entities = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .Where(t => t.TargetType == targetType)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<AppraisalCycleTargetDto> CreateAsync(CreateAppraisalCycleTargetDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate that exactly one target type is specified
        var targetCount = new[] { createDto.OrganizationLevelId, createDto.OrganizationUnitId, createDto.PositionId }
            .Count(id => id.HasValue);

        if (targetCount != 1)
        {
            throw new InvalidOperationException("Exactly one target type must be specified (OrganizationLevel, OrganizationUnit, or Position).");
        }

        // ── Data integrity: no duplicate scope for the same cycle ──────────────
        bool duplicate = createDto.TargetType switch
        {
            AppraisalTargetType.OrganizationLevel =>
                await _targetRepository.ExistsAsync(
                    t => t.AppraisalCycleId == createDto.AppraisalCycleId
                      && t.OrganizationLevelId == createDto.OrganizationLevelId),
            AppraisalTargetType.OrganizationUnit =>
                await _targetRepository.ExistsAsync(
                    t => t.AppraisalCycleId == createDto.AppraisalCycleId
                      && t.OrganizationUnitId == createDto.OrganizationUnitId),
            AppraisalTargetType.Position =>
                await _targetRepository.ExistsAsync(
                    t => t.AppraisalCycleId == createDto.AppraisalCycleId
                      && t.PositionId == createDto.PositionId),
            _ => false
        };

        if (duplicate)
            throw new InvalidOperationException(
                $"A cycle target of type '{createDto.TargetType}' already exists for this scope in the specified appraisal cycle.");

        var entity = createDto.ToEntity();

        await _targetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .FirstOrDefaultAsync(t => t.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Appraisal cycle target created successfully: {targetId}", entity!.Id);

        return entity.ToDto();
    }

    public async Task<AppraisalCycleTargetDto> UpdateAsync(UpdateAppraisalCycleTargetDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .FirstOrDefaultAsync(t => t.Id == updateDto.Id, cancellationToken);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal cycle target with ID '{updateDto.Id}' not found.");

        // Validate that exactly one target type is specified
        var targetCount = new[] { updateDto.OrganizationLevelId, updateDto.OrganizationUnitId, updateDto.PositionId }
            .Count(id => id.HasValue);

        if (targetCount != 1)
        {
            throw new InvalidOperationException("Exactly one target type must be specified.");
        }

        updateDto.UpdateEntity(entity);

        await _targetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle target updated successfully: {targetId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _targetRepository.GetByIdAsync(id);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal cycle target with ID '{id}' not found.");

        await _targetRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle target deleted: {targetId}", id);

        return true;
    }

    public async Task<bool> ValidateTargetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _targetRepository.GetByIdAsync(id);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal cycle target with ID '{id}' not found.");

        // Validate that exactly one target type is specified
        var targetCount = new[] { entity.OrganizationLevelId, entity.OrganizationUnitId, entity.PositionId }
            .Count(id => id.HasValue);

        var isValid = targetCount == 1;

        if (!isValid)
        {
            _logger.LogWarning("Appraisal cycle target {targetId} has invalid configuration. Target count: {targetCount}", id, targetCount);
        }

        return isValid;
    }

    // ─── Exclusions ───────────────────────────────────────────────────────────

    public async Task<AppraisalCycleTargetExclusionDto> AddExclusionAsync(
        Guid targetId, CreateAppraisalCycleTargetExclusionDto dto, CancellationToken cancellationToken = default)
    {
        var targetExists = await _targetRepository.ExistsAsync(t => t.Id == targetId);
        if (!targetExists)
            throw new ArgumentException($"Appraisal cycle target with ID '{targetId}' not found.");

        var entity = dto.ToEntity();
        entity.AppraisalCycleTargetId = targetId;
        entity.IsActive = true;

        await _exclusionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Exclusion added to cycle target {TargetId}: {ExclusionId}", targetId, entity.Id);

        entity = await _exclusionRepository.GetQueryable()
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(e => e.Id == entity.Id, cancellationToken);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<AppraisalCycleTargetExclusionDto>> GetExclusionsAsync(
        Guid targetId, CancellationToken cancellationToken = default)
    {
        var entities = await _exclusionRepository.GetQueryable(e => e.AppraisalCycleTargetId == targetId)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Employee)
            .OrderByDescending(e => e.IsActive)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<AppraisalCycleTargetExclusionDto> UpdateExclusionAsync(
        Guid targetId, UpdateAppraisalCycleTargetExclusionDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _exclusionRepository.GetQueryable()
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(e => e.Id == dto.Id && e.AppraisalCycleTargetId == targetId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Exclusion not found for this cycle target.");

        dto.UpdateEntity(entity);
        await _exclusionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle target exclusion updated: {ExclusionId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> RemoveExclusionAsync(
        Guid targetId, Guid exclusionId, CancellationToken cancellationToken = default)
    {
        var entity = await _exclusionRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == exclusionId && e.AppraisalCycleTargetId == targetId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Exclusion not found for this cycle target.");

        await _exclusionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cycle target exclusion removed: {ExclusionId}", exclusionId);
        return true;
    }
}

#endregion Appraisal Cycle Target

