using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainerService : ITrainerService
{
    private readonly ITrainerProfileRepository _profileRepository;
    private readonly ITrainerSkillRepository _skillRepository;
    private readonly ITrainerAvailabilityRepository _availabilityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainerService> _logger;

    public TrainerService(
        ITrainerProfileRepository profileRepository,
        ITrainerSkillRepository skillRepository,
        ITrainerAvailabilityRepository availabilityRepository,
        IUnitOfWork unitOfWork,
        ILogger<TrainerService> logger)
    {
        _profileRepository = profileRepository;
        _skillRepository = skillRepository;
        _availabilityRepository = availabilityRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Trainer profile queries ───────────────────────────────────────────────

    public async Task<TrainerProfileDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _profileRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Trainer profile with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainerProfileSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _profileRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainerProfileSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _profileRepository.GetActiveTrainersAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainerProfileSummaryDto>> GetByVendorIdAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        var entities = await _profileRepository.GetByVendorIdAsync(vendorId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainerProfileSummaryDto>> GetAvailableForDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var entities = await _profileRepository.GetAvailableForDateRangeAsync(from, to);
        return entities.ToSummaryDtoList();
    }

    // ── Trainer profile CRUD ─────────────────────────────────────────────────

    public async Task<TrainerProfileDto> CreateAsync(CreateTrainerProfileDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _profileRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trainer profile created: {Name}", entity.Name);

        return entity.ToDto();
    }

    public async Task<TrainerProfileDto> UpdateAsync(UpdateTrainerProfileDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _profileRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Trainer profile with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _profileRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trainer profile updated: {Name}", entity.Name);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _profileRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Trainer profile with ID '{id}' not found.");

        await _profileRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trainer profile deleted: {Name}", entity.Name);

        return true;
    }

    // ── Skill sub-operations ─────────────────────────────────────────────────

    public async Task<TrainerSkillDto> AddSkillAsync(CreateTrainerSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.GetByIdAsync(createDto.TrainerProfileId);

        if (profile == null)
            throw new ArgumentException($"Trainer profile with ID '{createDto.TrainerProfileId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _skillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Skill added to trainer profile {TrainerProfileId}", createDto.TrainerProfileId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainerSkillDto>> GetSkillsAsync(Guid trainerProfileId, CancellationToken cancellationToken = default)
    {
        var entities = await _skillRepository.GetByTrainerProfileIdAsync(trainerProfileId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainerSkillDto> UpdateSkillAsync(UpdateTrainerSkillDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _skillRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Trainer skill with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _skillRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteSkillAsync(Guid trainerSkillId, CancellationToken cancellationToken = default)
    {
        var entity = await _skillRepository.GetByIdAsync(trainerSkillId);

        if (entity == null)
            throw new ArgumentException($"Trainer skill with ID '{trainerSkillId}' not found.");

        await _skillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Availability sub-operations ──────────────────────────────────────────

    public async Task<TrainerAvailabilityDto> AddAvailabilityAsync(CreateTrainerAvailabilityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.GetByIdAsync(createDto.TrainerProfileId);

        if (profile == null)
            throw new ArgumentException($"Trainer profile with ID '{createDto.TrainerProfileId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _availabilityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Availability added to trainer profile {TrainerProfileId}", createDto.TrainerProfileId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainerAvailabilityDto>> GetAvailabilityAsync(Guid trainerProfileId, CancellationToken cancellationToken = default)
    {
        var entities = await _availabilityRepository.GetByTrainerProfileIdAsync(trainerProfileId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainerAvailabilityDto> UpdateAvailabilityAsync(UpdateTrainerAvailabilityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _availabilityRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Trainer availability with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _availabilityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAvailabilityAsync(Guid availabilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _availabilityRepository.GetByIdAsync(availabilityId);

        if (entity == null)
            throw new ArgumentException($"Trainer availability with ID '{availabilityId}' not found.");

        await _availabilityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
