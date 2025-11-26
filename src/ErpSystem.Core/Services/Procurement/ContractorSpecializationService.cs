using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class ContractorSpecializationService : IContractorSpecializationService
{
    private readonly IContractorSpecializationRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ContractorSpecializationService> _logger;

    public ContractorSpecializationService(
        IContractorSpecializationRepository repository,
        ICurrentUserProvider currentUserProvider,
        ILogger<ContractorSpecializationService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<ContractorSpecializationDto?> GetByIdAsync(Guid id)
    {
        var specialization = await _repository.GetByIdAsync(id);
        return specialization == null ? null : MapToDto(specialization);
    }

    public async Task<IEnumerable<ContractorSpecializationDto>> GetAllAsync()
    {
        var specializations = await _repository.GetAllAsync();
        return specializations.Select(MapToDto);
    }

    public async Task<IEnumerable<ContractorSpecializationDto>> GetAllSpecializationsAsync()
    {
        var specializations = await _repository.GetAllSpecializationsAsync();
        return specializations.Select(MapToDto);
    }

    public async Task<ContractorSpecializationDto> CreateAsync(CreateContractorSpecializationDto dto)
    {
        // Validate unique code
        var isUnique = await IsSpecializationCodeUniqueAsync(dto.SpecializationCode);
        if (!isUnique)
        {
            throw new InvalidOperationException($"Specialization code '{dto.SpecializationCode}' already exists.");
        }

        var specialization = new ContractorSpecialization
        {
            SpecializationCode = dto.SpecializationCode,
            SpecializationName = dto.SpecializationName,
            Description = dto.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId
        };

        var created = await _repository.CreateAsync(specialization);
        _logger.LogInformation("Created contractor specialization {SpecializationCode} - {SpecializationName}",
            created.SpecializationCode, created.SpecializationName);

        return MapToDto(created);
    }

    public async Task<ContractorSpecializationDto> UpdateAsync(Guid id, UpdateContractorSpecializationDto dto)
    {
        var specialization = await _repository.GetByIdAsync(id);
        if (specialization == null)
        {
            throw new InvalidOperationException($"Contractor specialization with ID {id} not found.");
        }

        specialization.SpecializationName = dto.SpecializationName;
        specialization.Description = dto.Description;
        specialization.IsActive = dto.IsActive;
        specialization.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(specialization);
        _logger.LogInformation("Updated contractor specialization {SpecializationCode} - {SpecializationName}",
            updated.SpecializationCode, updated.SpecializationName);

        return MapToDto(updated);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
        _logger.LogInformation("Deleted contractor specialization with ID {SpecializationId}", id);
    }

    public async Task<IEnumerable<ContractorSpecializationDto>> GetActiveSpecializationsAsync()
    {
        var specializations = await _repository.GetActiveSpecializationsAsync();
        return specializations.Select(MapToDto);
    }

    public async Task<ContractorSpecializationDto?> GetByCodeAsync(string specializationCode)
    {
        var specialization = await _repository.GetByCodeAsync(specializationCode);
        return specialization == null ? null : MapToDto(specialization);
    }

    public async Task<bool> IsSpecializationCodeUniqueAsync(string specializationCode, Guid? excludeId = null)
    {
        return await _repository.IsSpecializationCodeUniqueAsync(specializationCode, excludeId);
    }

    private static ContractorSpecializationDto MapToDto(ContractorSpecialization specialization)
    {
        return new ContractorSpecializationDto
        {
            Id = specialization.Id,
            SpecializationCode = specialization.SpecializationCode,
            SpecializationName = specialization.SpecializationName,
            Description = specialization.Description,
            IsActive = specialization.IsActive,
            DisplayOrder = 0 // Default value, can be enhanced later
        };
    }
}
