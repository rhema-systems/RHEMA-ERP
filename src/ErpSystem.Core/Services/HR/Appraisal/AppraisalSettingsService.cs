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

#region Appraisal Settings

public class AppraisalSettingsService : IAppraisalSettingsService
{
    private readonly IGenericRepository<AppraisalSettings> _settingsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalSettingsService> _logger;

    public AppraisalSettingsService(
        IGenericRepository<AppraisalSettings> settingsRepository,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalSettingsService> logger)
    {
        _settingsRepository = settingsRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AppraisalSettingsDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _settingsRepository.GetByIdAsync(id);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal settings with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalSettingsDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _settingsRepository.GetAllAsync();
        return entities.ToDtoList();
    }

    public async Task<PagedResult<AppraisalSettingsDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _settingsRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query.OrderByDescending(s => s.CreatedAt)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync(cancellationToken);

        return new PagedResult<AppraisalSettingsDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalSettingsDto> CreateAsync(CreateAppraisalSettingsDto createDto, CancellationToken cancellationToken = default)
    {
        // Unique name check
        var nameExists = await _settingsRepository.GetQueryable(s => s.SettingsName == createDto.SettingsName)
            .AnyAsync(cancellationToken);
        if (nameExists)
            throw new InvalidOperationException($"An appraisal settings named '{createDto.SettingsName}' already exists.");

        // Zero out weights for disabled evaluators, then validate
        if (!createDto.RequireSelfEvaluation) createDto.SelfEvaluationWeight = 0;
        if (!createDto.RequirePeerReviews) createDto.PeerEvaluationWeight = 0;
        if (!createDto.RequireManagerEvaluation) createDto.ManagerEvaluationWeight = 0;

        if (!createDto.RequireSelfEvaluation && !createDto.RequirePeerReviews && !createDto.RequireManagerEvaluation)
            throw new InvalidOperationException("At least one evaluation type (self, peer, or manager) must be enabled.");

        var totalWeight = createDto.SelfEvaluationWeight + createDto.PeerEvaluationWeight + createDto.ManagerEvaluationWeight;
        if (Math.Abs(totalWeight - 1.0m) > 0.005m)
            throw new InvalidOperationException($"Total evaluation weights must equal 1.0. Current total: {totalWeight:F2}");

        var entity = createDto.ToEntity();

        await _settingsRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal settings created successfully: {settingsId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<AppraisalSettingsDto> UpdateAsync(UpdateAppraisalSettingsDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _settingsRepository.GetByIdAsync(updateDto.Id);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal settings with ID '{updateDto.Id}' not found.");

        // Unique name check (exclude self)
        var nameExists = await _settingsRepository.GetQueryable(s => s.SettingsName == updateDto.SettingsName && s.Id != updateDto.Id)
            .AnyAsync(cancellationToken);
        if (nameExists)
            throw new InvalidOperationException($"An appraisal settings named '{updateDto.SettingsName}' already exists.");

        // Zero out weights for disabled evaluators, then validate
        if (!updateDto.RequireSelfEvaluation) updateDto.SelfEvaluationWeight = 0;
        if (!updateDto.RequirePeerReviews) updateDto.PeerEvaluationWeight = 0;
        if (!updateDto.RequireManagerEvaluation) updateDto.ManagerEvaluationWeight = 0;

        if (!updateDto.RequireSelfEvaluation && !updateDto.RequirePeerReviews && !updateDto.RequireManagerEvaluation)
            throw new InvalidOperationException("At least one evaluation type (self, peer, or manager) must be enabled.");

        var totalWeight = updateDto.SelfEvaluationWeight + updateDto.PeerEvaluationWeight + updateDto.ManagerEvaluationWeight;
        if (Math.Abs(totalWeight - 1.0m) > 0.005m)
            throw new InvalidOperationException($"Total evaluation weights must equal 1.0. Current total: {totalWeight:F2}");

        updateDto.UpdateEntity(entity);

        await _settingsRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal settings updated successfully: {settingsId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _settingsRepository.GetByIdAsync(id);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal settings with ID '{id}' not found.");

        // Check if settings are being used by any cycles
        var cyclesCount = await _settingsRepository.GetQueryable()
            .Where(s => s.Id == id)
            .SelectMany(s => s.AppraisalCycles)
            .CountAsync(cancellationToken);

        if (cyclesCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete appraisal settings that are being used by {cyclesCount} cycle(s).");
        }

        await _settingsRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal settings deleted: {settingsId}", id);

        return true;
    }

    public async Task<AppraisalSettingsDto?> GetDefaultSettingsAsync(CancellationToken cancellationToken = default)
    {
        // Return the most recently created settings as default
        var entity = await _settingsRepository.GetQueryable()
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity?.ToDto();
    }

    public async Task<bool> ValidateWeightsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _settingsRepository.GetByIdAsync(id);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal settings with ID '{id}' not found.");

        var totalWeight = entity.SelfEvaluationWeight + entity.PeerEvaluationWeight + entity.ManagerEvaluationWeight;
        var isValid = Math.Abs(totalWeight - 1.0m) <= 0.01m;

        if (!isValid)
        {
            _logger.LogWarning("Appraisal settings {settingsId} has invalid weights. Total: {totalWeight}", id, totalWeight);
        }

        return isValid;
    }
}

#endregion Appraisal Settings

