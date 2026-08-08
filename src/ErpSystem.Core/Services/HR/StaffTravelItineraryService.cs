using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelItineraryService> _logger;

    public StaffTravelItineraryService(
        IStaffTravelItineraryRepository itineraryRepository,
        IStaffTravelItineraryLegRepository legRepository,
        IStaffTravelItineraryActivityRepository activityRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelItineraryService> logger)
    {
        _itineraryRepository = itineraryRepository;
        _legRepository = legRepository;
        _activityRepository = activityRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffTravelItinerary> GetOwnedItineraryAsync(Guid id)
    {
        var entity = await _itineraryRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Itinerary with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelItineraryLeg> GetOwnedLegAsync(Guid id)
    {
        var entity = await _legRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Itinerary leg with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelItineraryActivity> GetOwnedActivityAsync(Guid id)
    {
        var entity = await _activityRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Itinerary activity with ID '{id}' not found.");
        return entity;
    }

    // ---- Itinerary ---------------------------------------------------------

    public async Task<StaffTravelItineraryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _itineraryRepository.GetWithLegsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Itinerary with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelItinerarySummaryDto>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _itineraryRepository.GetByRequestIdAsync(requestId))
            .Where(i => i.TenantId == tenantId)
            .Select(i => i.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelItineraryDto?> GetCurrentVersionAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _itineraryRepository.GetCurrentVersionAsync(requestId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<StaffTravelItineraryDto> CreateAsync(CreateStaffTravelItineraryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.VersionNumber = await GetNextVersionNumberAsync(createDto.StaffTravelRequestId, tenantId);

        if (createDto.IsCurrentVersion)
            await DemoteExistingVersionsAsync(createDto.StaffTravelRequestId, createdByUserId, cancellationToken);

        await _itineraryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Itinerary v{Version} created for request {RequestId}", entity.VersionNumber, entity.StaffTravelRequestId);

        var refreshed = await _itineraryRepository.GetWithLegsAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Itinerary with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<StaffTravelItineraryDto> UpdateAsync(UpdateStaffTravelItineraryDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItineraryAsync(updateDto.Id);

        if (updateDto.IsCurrentVersion && !entity.IsCurrentVersion)
            await DemoteExistingVersionsAsync(entity.StaffTravelRequestId, updatedByUserId, cancellationToken);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _itineraryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _itineraryRepository.GetWithLegsAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Itinerary with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<bool> SetCurrentVersionAsync(Guid itineraryId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItineraryAsync(itineraryId);

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
        var entity = await GetOwnedItineraryAsync(id);

        await _itineraryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Legs --------------------------------------------------------------

    public async Task<StaffTravelItineraryLegDto> AddLegAsync(CreateStaffTravelItineraryLegDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedItineraryAsync(createDto.StaffTravelItineraryId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _legRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelItineraryLegDto>> GetLegsAsync(Guid itineraryId, CancellationToken cancellationToken = default)
    {
        await GetOwnedItineraryAsync(itineraryId);
        var tenantId = GetTenantId();
        return (await _legRepository.GetByItineraryIdAsync(itineraryId))
            .Where(l => l.TenantId == tenantId)
            .Select(l => l.ToDto())
            .ToList();
    }

    public async Task<StaffTravelItineraryLegDto> GetLegByIdAsync(Guid legId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _legRepository.GetWithActivitiesAsync(legId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Itinerary leg with ID '{legId}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffTravelItineraryLegDto> UpdateLegAsync(UpdateStaffTravelItineraryLegDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedLegAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _legRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteLegAsync(Guid legId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedLegAsync(legId);
        await _legRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Activities --------------------------------------------------------

    public async Task<StaffTravelItineraryActivityDto> AddActivityAsync(CreateStaffTravelItineraryActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedLegAsync(createDto.StaffTravelItineraryLegId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _activityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelItineraryActivityDto>> GetActivitiesAsync(Guid legId, CancellationToken cancellationToken = default)
    {
        await GetOwnedLegAsync(legId);
        var tenantId = GetTenantId();
        return (await _activityRepository.GetByLegIdAsync(legId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto())
            .ToList();
    }

    public async Task<StaffTravelItineraryActivityDto> UpdateActivityAsync(UpdateStaffTravelItineraryActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActivityAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _activityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActivityAsync(activityId);
        await _activityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Helpers -----------------------------------------------------------

    private async Task<int> GetNextVersionNumberAsync(Guid requestId, Guid tenantId)
    {
        var existing = await _itineraryRepository.GetByRequestIdAsync(requestId);
        var maxVersion = existing
            .Where(i => i.TenantId == tenantId)
            .Select(i => (int?)i.VersionNumber)
            .DefaultIfEmpty(null)
            .Max() ?? 0;
        return maxVersion + 1;
    }

    private async Task DemoteExistingVersionsAsync(Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var existing = await _itineraryRepository.GetByRequestIdAsync(requestId);
        foreach (var current in existing.Where(i => i.TenantId == tenantId && i.IsCurrentVersion))
        {
            current.IsCurrentVersion = false;
            current.UpdatedBy = userId.ToString();
            current.UpdatedAt = DateTime.UtcNow;
            await _itineraryRepository.UpdateAsync(current);
        }
    }
}

#endregion
