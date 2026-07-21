using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingProgramGroupService : ITrainingProgramGroupService
{
    private readonly IGenericRepository<TrainingProgramGroup> _groupRepository;
    private readonly IGenericRepository<TrainingProgram> _programRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingProgramGroupService> _logger;

    public TrainingProgramGroupService(
        IGenericRepository<TrainingProgramGroup> groupRepository,
        IGenericRepository<TrainingProgram> programRepository,
        IUnitOfWork unitOfWork,
        ILogger<TrainingProgramGroupService> logger)
    {
        _groupRepository = groupRepository;
        _programRepository = programRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<TrainingProgramGroupDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = _groupRepository.GetQueryable();
        if (activeOnly)
            query = query.Where(g => g.IsActive);

        var groups = await query
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var counts = await _programRepository.GetQueryable()
            .Where(p => p.ProgramGroupId != null)
            .GroupBy(p => p.ProgramGroupId!.Value)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dtos = groups.Select(g => g.ToDto()).ToList();
        foreach (var dto in dtos)
            dto.ProgramsCount = counts.FirstOrDefault(c => c.GroupId == dto.Id)?.Count ?? 0;

        return dtos;
    }

    public async Task<TrainingProgramGroupDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _groupRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Training program group with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<TrainingProgramGroupDto> CreateAsync(CreateTrainingProgramGroupDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var duplicate = await _groupRepository.GetQueryable()
            .AnyAsync(g => g.Code == dto.Code, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A training program group with code '{dto.Code}' already exists.");

        var entity = dto.ToEntity(tenantId, createdByUserId);
        await _groupRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program group created: {Code} - {Name}", entity.Code, entity.Name);
        return entity.ToDto();
    }

    public async Task<TrainingProgramGroupDto> UpdateAsync(Guid id, UpdateTrainingProgramGroupDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _groupRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Training program group with ID '{id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        await _groupRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program group updated: {Id}", id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _groupRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Training program group with ID '{id}' not found.");

        var inUse = await _programRepository.GetQueryable()
            .AnyAsync(p => p.ProgramGroupId == id, cancellationToken);
        if (inUse)
            throw new InvalidOperationException("This group cannot be deleted because it is assigned to one or more training programs. Deactivate it instead.");

        await _groupRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program group deleted: {Id}", id);
        return true;
    }
}
