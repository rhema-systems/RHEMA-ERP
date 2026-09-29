using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainerService : ITrainerService
{
    private readonly ITrainerProfileRepository _profileRepository;
    private readonly ITrainerSkillRepository _skillRepository;
    private readonly ITrainerAvailabilityRepository _availabilityRepository;
    private readonly IGenericRepository<TrainingVendor> _vendorRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainerService> _logger;

    public TrainerService(
        ITrainerProfileRepository profileRepository,
        ITrainerSkillRepository skillRepository,
        ITrainerAvailabilityRepository availabilityRepository,
        IGenericRepository<TrainingVendor> vendorRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainerService> logger)
    {
        _profileRepository = profileRepository;
        _skillRepository = skillRepository;
        _availabilityRepository = availabilityRepository;
        _vendorRepository = vendorRepository;
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
    private async Task<TrainerProfile> GetOwnedProfileAsync(Guid id)
    {
        var entity = await _profileRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Trainer profile with ID '{id}' not found.");
        return entity;
    }

    private async Task<TrainerSkill> GetOwnedSkillAsync(Guid id)
    {
        var entity = await _skillRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Trainer skill with ID '{id}' not found.");
        return entity;
    }

    private async Task<TrainerAvailability> GetOwnedAvailabilityAsync(Guid id)
    {
        var entity = await _availabilityRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Trainer availability with ID '{id}' not found.");
        return entity;
    }

    // ── Trainer profile queries ───────────────────────────────────────────────

    public async Task<TrainerProfileDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _profileRepository.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Trainer profile with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainerProfileSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _profileRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainerProfileSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _profileRepository.GetActiveTrainersAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainerProfileSummaryDto>> GetByVendorIdAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _profileRepository.GetByVendorIdAsync(vendorId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainerProfileSummaryDto>> GetAvailableForDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _profileRepository.GetAvailableForDateRangeAsync(from, to);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Trainer profile CRUD ─────────────────────────────────────────────────

    public async Task<TrainerProfileDto> CreateAsync(CreateTrainerProfileDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        if (createDto.VendorId.HasValue && createDto.VendorId != Guid.Empty)
        {
            var vendor = await _vendorRepository.GetByIdAsync(createDto.VendorId.Value);
            if (vendor == null || vendor.TenantId != current)
                throw new ArgumentException($"Training vendor with ID '{createDto.VendorId}' not found.");
        }

        var entity = createDto.ToEntity(current, createdByUserId);

        await _profileRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trainer profile created: {Name}", entity.Name);

        // Employee/Vendor navs are unloaded on the just-created tracked instance, so a direct
        // entity.ToDto() would blank EmployeeName/VendorName even though the FK ids are correct.
        // Reload through GetByIdAsync (GetWithFullDetailsAsync) instead.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainerProfileDto> UpdateAsync(UpdateTrainerProfileDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProfileAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _profileRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trainer profile updated: {Name}", entity.Name);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProfileAsync(id);

        // Round 4, lane M3: orientation sessions can name a vendor's trainer as their facilitator.
        if (await _unitOfWork.WhyStillBookedAsync(entity.TenantId, null, entity.Id, entity.Name,
                "make the trainer inactive, which keeps their record and stops them being picked", cancellationToken) is { } booked)
            throw new InvalidOperationException(booked);

        await _profileRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trainer profile deleted: {Name}", entity.Name);

        return true;
    }

    // ── Skill sub-operations ─────────────────────────────────────────────────

    public async Task<TrainerSkillDto> AddSkillAsync(CreateTrainerSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedProfileAsync(createDto.TrainerProfileId);

        var entity = createDto.ToEntity(current, createdByUserId);

        await _skillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Skill added to trainer profile {TrainerProfileId}", createDto.TrainerProfileId);

        // Skill nav is unloaded on the just-created tracked instance, so entity.ToDto() would blank
        // SkillName. Reload with the include instead.
        return await ReloadSkillDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<IEnumerable<TrainerSkillDto>> GetSkillsAsync(Guid trainerProfileId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _skillRepository.GetByTrainerProfileIdAsync(trainerProfileId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainerSkillDto> UpdateSkillAsync(UpdateTrainerSkillDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSkillAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _skillRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReloadSkillDtoAsync(entity.Id, cancellationToken);
    }

    private async Task<TrainerSkillDto> ReloadSkillDtoAsync(Guid skillId, CancellationToken cancellationToken)
    {
        var reloaded = await _skillRepository.GetQueryable()
            .Include(s => s.Skill)
            .FirstAsync(s => s.Id == skillId, cancellationToken);
        return reloaded.ToDto();
    }

    public async Task<bool> DeleteSkillAsync(Guid trainerSkillId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSkillAsync(trainerSkillId);

        await _skillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Availability sub-operations ──────────────────────────────────────────

    public async Task<TrainerAvailabilityDto> AddAvailabilityAsync(CreateTrainerAvailabilityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedProfileAsync(createDto.TrainerProfileId);

        var entity = createDto.ToEntity(current, createdByUserId);

        await _availabilityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Availability added to trainer profile {TrainerProfileId}", createDto.TrainerProfileId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainerAvailabilityDto>> GetAvailabilityAsync(Guid trainerProfileId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _availabilityRepository.GetByTrainerProfileIdAsync(trainerProfileId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainerAvailabilityDto> UpdateAvailabilityAsync(UpdateTrainerAvailabilityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAvailabilityAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _availabilityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAvailabilityAsync(Guid availabilityId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAvailabilityAsync(availabilityId);

        await _availabilityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
