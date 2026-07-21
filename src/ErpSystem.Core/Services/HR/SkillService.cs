using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service implementation for skill operations.
/// </summary>
public sealed class SkillService : ISkillService
{
    private readonly ISkillRepository _skillRepository;
    private readonly ILogger<SkillService> _logger;

    public SkillService(ISkillRepository skillRepository, ILogger<SkillService> logger)
    {
        _skillRepository = skillRepository;
        _logger = logger;
    }

    public async Task<SkillDto?> GetByIdAsync(Guid id)
    {
        var skill = await _skillRepository.GetByIdAsync(id);
        return skill == null ? null : MapToDto(skill);
    }

    public async Task<IEnumerable<SkillDto>> GetAllAsync()
    {
        var items = await _skillRepository.GetAllAsync();
        return items.OrderBy(x => x.Name).Select(MapToDto);
    }

    public async Task<IEnumerable<SkillDto>> GetActiveSkillsAsync()
    {
        var items = await _skillRepository.GetActiveSkillsAsync();
        return items.Select(MapToDto);
    }

    public async Task<IEnumerable<SkillDto>> GetByCategoryAsync(string category)
    {
        var items = await _skillRepository.GetByCategoryAsync(category);
        return items.Select(MapToDto);
    }

    public async Task<SkillDto?> GetByNameAsync(string name)
    {
        var skill = await _skillRepository.GetByNameAsync(name);
        return skill == null ? null : MapToDto(skill);
    }

    public async Task<SkillDto> CreateSkillAsync(CreateSkillDto createDto)
    {
        if (await _skillRepository.NameExistsAsync(createDto.Name))
        {
            throw new InvalidOperationException($"Skill name '{createDto.Name}' already exists.");
        }

        var entity = new Skill
        {
            Name = createDto.Name,
            Description = createDto.Description,
            Category = createDto.Category,
            RequiresCertification = createDto.RequiresCertification,
            IsActive = createDto.IsActive
        };

        await _skillRepository.AddAsync(entity);
        await _skillRepository.SaveChangesAsync();

        _logger.LogInformation("Skill created: {SkillId} ({Name})", entity.Id, entity.Name);

        return MapToDto(entity);
    }

    public async Task<SkillDto> UpdateSkillAsync(Guid id, CreateSkillDto updateDto)
    {
        var entity = await _skillRepository.GetByIdAsync(id);
        if (entity == null)
        {
            throw new InvalidOperationException($"Skill with ID {id} not found.");
        }

        if (!string.Equals(entity.Name, updateDto.Name, StringComparison.Ordinal) && await _skillRepository.NameExistsAsync(updateDto.Name))
        {
            throw new InvalidOperationException($"Skill name '{updateDto.Name}' already exists.");
        }

        entity.Name = updateDto.Name;
        entity.Description = updateDto.Description;
        entity.Category = updateDto.Category;
        entity.RequiresCertification = updateDto.RequiresCertification;
        entity.IsActive = updateDto.IsActive;

        await _skillRepository.UpdateAsync(entity);
        await _skillRepository.SaveChangesAsync();

        _logger.LogInformation("Skill updated: {SkillId} ({Name})", entity.Id, entity.Name);

        return MapToDto(entity);
    }

    public async Task<bool> DeleteSkillAsync(Guid id)
    {
        await _skillRepository.DeleteAsync(id);
        await _skillRepository.SaveChangesAsync();

        _logger.LogInformation("Skill deleted (soft): {SkillId}", id);
        return true;
    }

    public Task<bool> NameExistsAsync(string name)
        => _skillRepository.NameExistsAsync(name);

    public async Task<IEnumerable<string>> GetSkillCategoriesAsync()
    {
        var all = await _skillRepository.GetAllAsync();
        return all
            .Select(s => s.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c)
            .Select(c => c!)
            .ToList();
    }

    private static SkillDto MapToDto(Skill entity)
    {
        return new SkillDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            RequiresCertification = entity.RequiresCertification,
            IsActive = entity.IsActive
        };
    }
}
