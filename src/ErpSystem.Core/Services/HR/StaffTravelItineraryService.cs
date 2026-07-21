using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 2: ITINERARY SERVICE
// ============================================================================

#region Staff Travel Itinerary Service

public class StaffTravelItineraryService : IStaffTravelItineraryService
{
    private readonly IStaffTravelItineraryRepository _itineraryRepository;
    private readonly IStaffTravelItineraryLegRepository _legRepository;
    private readonly IStaffTravelItineraryActivityRepository _activityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelItineraryService> _logger;

    public StaffTravelItineraryService(
        IStaffTravelItineraryRepository itineraryRepository,
        IStaffTravelItineraryLegRepository legRepository,
        IStaffTravelItineraryActivityRepository activityRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelItineraryService> logger)
    {
        _itineraryRepository = itineraryRepository;
        _legRepository = legRepository;
        _activityRepository = activityRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ---- Itinerary ---------------------------------------------------------

    public async Task<StaffTravelItineraryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _itineraryRepository.GetWithLegsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Itinerary with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelItinerarySummaryDto>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _itineraryRepository.GetByRequestIdAsync(requestId)).Select(i => i.ToSummaryDto()).ToList();

    public async Task<StaffTravelItineraryDto?> GetCurrentVersionAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await _itineraryRepository.GetCurrentVersionAsync(requestId);
        return entity?.ToDto();
    }

    public async Task<StaffTravelItineraryDto> CreateAsync(CreateStaffTravelItineraryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.VersionNumber = await _itineraryRepository.GetNextVersionNumberAsync(createDto.StaffTravelRequestId);

        if (createDto.IsCurrentVersion)
            await DemoteExistingVersionsAsync(createDto.StaffTravelRequestId, createdByUserId, cancellationToken);

        await _itineraryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Itinerary v{Version} created for request {RequestId}", entity.VersionNumber, entity.StaffTravelRequestId);
        return (await _itineraryRepository.GetWithLegsAsync(entity.Id))!.ToDto();
    }

    public async Task<StaffTravelItineraryDto> UpdateAsync(UpdateStaffTravelItineraryDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _itineraryRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Itinerary with ID '{updateDto.Id}' not found.");

        if (updateDto.IsCurrentVersion && !entity.IsCurrentVersion)
            await DemoteExistingVersionsAsync(entity.StaffTravelRequestId, updatedByUserId, cancellationToken);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _itineraryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _itineraryRepository.GetWithLegsAsync(entity.Id))!.ToDto();
    }

    public async Task<bool> SetCurrentVersionAsync(Guid itineraryId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _itineraryRepository.GetByIdAsync(itineraryId);
        if (entity == null)
            throw new ArgumentException($"Itinerary with ID '{itineraryId}' not found.");

        await DemoteExistingVersionsAsync(entity.StaffTravelRequestId, updatedByUserId, cancellationToken);

        entity.IsCurrentVersion = true;
        entity.UpdatedBy = updatedByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        await _itineraryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _itineraryRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Itinerary with ID '{id}' not found.");

        await _itineraryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Legs --------------------------------------------------------------

    public async Task<StaffTravelItineraryLegDto> AddLegAsync(CreateStaffTravelItineraryLegDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _legRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelItineraryLegDto>> GetLegsAsync(Guid itineraryId, CancellationToken cancellationToken = default)
        => (await _legRepository.GetByItineraryIdAsync(itineraryId)).Select(l => l.ToDto()).ToList();

    public async Task<StaffTravelItineraryLegDto> GetLegByIdAsync(Guid legId, CancellationToken cancellationToken = default)
    {
        var entity = await _legRepository.GetWithActivitiesAsync(legId);
        if (entity == null)
            throw new ArgumentException($"Itinerary leg with ID '{legId}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffTravelItineraryLegDto> UpdateLegAsync(UpdateStaffTravelItineraryLegDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _legRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Itinerary leg with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _legRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteLegAsync(Guid legId, CancellationToken cancellationToken = default)
    {
        var entity = await _legRepository.GetByIdAsync(legId);
        if (entity == null)
            throw new ArgumentException($"Itinerary leg with ID '{legId}' not found.");

        await _legRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Activities --------------------------------------------------------

    public async Task<StaffTravelItineraryActivityDto> AddActivityAsync(CreateStaffTravelItineraryActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _activityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelItineraryActivityDto>> GetActivitiesAsync(Guid legId, CancellationToken cancellationToken = default)
        => (await _activityRepository.GetByLegIdAsync(legId)).Select(a => a.ToDto()).ToList();

    public async Task<StaffTravelItineraryActivityDto> UpdateActivityAsync(UpdateStaffTravelItineraryActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _activityRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Itinerary activity with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _activityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        var entity = await _activityRepository.GetByIdAsync(activityId);
        if (entity == null)
            throw new ArgumentException($"Itinerary activity with ID '{activityId}' not found.");

        await _activityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Helpers -----------------------------------------------------------

    private async Task DemoteExistingVersionsAsync(Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _itineraryRepository.GetByRequestIdAsync(requestId);
        foreach (var current in existing.Where(i => i.IsCurrentVersion))
        {
            current.IsCurrentVersion = false;
            current.UpdatedBy = userId.ToString();
            current.UpdatedAt = DateTime.UtcNow;
            await _itineraryRepository.UpdateAsync(current);
        }
    }
}

#endregion
