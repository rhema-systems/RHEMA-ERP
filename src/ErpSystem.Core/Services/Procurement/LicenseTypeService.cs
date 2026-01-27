using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class LicenseTypeService : ILicenseTypeService
{
    private readonly ILicenseTypeRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<LicenseTypeService> _logger;

    public LicenseTypeService(
        ILicenseTypeRepository repository,
        ICurrentUserProvider currentUserProvider,
        ILogger<LicenseTypeService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<LicenseTypeDto?> GetByIdAsync(Guid id)
    {
        var licenseType = await _repository.GetByIdAsync(id);
        return licenseType == null ? null : MapToDto(licenseType);
    }

    public async Task<IEnumerable<LicenseTypeDto>> GetAllAsync()
    {
        var licenseTypes = await _repository.GetAllAsync();
        return licenseTypes.Select(MapToDto);
    }

    public async Task<IEnumerable<LicenseTypeDto>> GetAllLicenseTypesAsync()
    {
        var licenseTypes = await _repository.GetAllLicenseTypesAsync();
        return licenseTypes.Select(MapToDto);
    }

    public async Task<LicenseTypeDto> CreateAsync(CreateLicenseTypeDto dto)
    {
        // Validate unique code
        var isUnique = await IsLicenseCodeUniqueAsync(dto.LicenseCode);
        if (!isUnique)
        {
            throw new InvalidOperationException($"License code '{dto.LicenseCode}' already exists.");
        }

        var licenseType = new LicenseType
        {
            LicenseCode = dto.LicenseCode,
            LicenseName = dto.LicenseName,
            Description = dto.Description,
            ValidityPeriodMonths = dto.ValidityPeriodMonths,
            IsMandatory = dto.IsMandatory,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId
        };

        var created = await _repository.CreateAsync(licenseType);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Created license type {LicenseCode} - {LicenseName}",
            created.LicenseCode, created.LicenseName);

        return MapToDto(created);
    }

    public async Task<LicenseTypeDto> UpdateAsync(Guid id, UpdateLicenseTypeDto dto)
    {
        var licenseType = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException($"License type with ID {id} not found.");
        licenseType.LicenseName = dto.LicenseName;
        licenseType.Description = dto.Description;
        licenseType.IsMandatory = dto.IsMandatory;
        licenseType.ValidityPeriodMonths = dto.ValidityPeriodMonths;
        licenseType.IsActive = dto.IsActive;
        licenseType.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(licenseType);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Updated license type {LicenseCode} - {LicenseName}",
            updated.LicenseCode, updated.LicenseName);

        return MapToDto(updated);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Deleted license type with ID {LicenseTypeId}", id);
    }

    public async Task<IEnumerable<LicenseTypeDto>> GetActiveLicenseTypesAsync()
    {
        var licenseTypes = await _repository.GetActiveLicenseTypesAsync();
        return licenseTypes.Select(MapToDto);
    }

    public async Task<IEnumerable<LicenseTypeDto>> GetMandatoryLicenseTypesAsync(string applicableTo)
    {
        var licenseTypes = await _repository.GetMandatoryLicenseTypesAsync(applicableTo);
        return licenseTypes.Select(MapToDto);
    }

    public async Task<IEnumerable<LicenseTypeDto>> GetLicenseTypesByApplicabilityAsync(string applicableTo)
    {
        var licenseTypes = await _repository.GetLicenseTypesByApplicabilityAsync(applicableTo);
        return licenseTypes.Select(MapToDto);
    }

    public async Task<LicenseTypeDto?> GetByCodeAsync(string licenseCode)
    {
        var licenseType = await _repository.GetByCodeAsync(licenseCode);
        return licenseType == null ? null : MapToDto(licenseType);
    }

    public async Task<bool> IsLicenseCodeUniqueAsync(string licenseCode, Guid? excludeId = null)
    {
        return await _repository.IsLicenseCodeUniqueAsync(licenseCode, excludeId);
    }

    private static LicenseTypeDto MapToDto(LicenseType licenseType)
    {
        return new LicenseTypeDto
        {
            Id = licenseType.Id,
            LicenseCode = licenseType.LicenseCode,
            LicenseName = licenseType.LicenseName,
            Description = licenseType.Description,
            ApplicableTo = "Both", // Default value since entity doesn't have this field
            IsMandatory = licenseType.IsMandatory,
            ValidityPeriodMonths = licenseType.ValidityPeriodMonths,
            RequiresRenewal = licenseType.ValidityPeriodMonths.HasValue && licenseType.ValidityPeriodMonths.Value > 0,
            RenewalReminderDays = 30, // Default value
            IsActive = licenseType.IsActive
        };
    }
}
