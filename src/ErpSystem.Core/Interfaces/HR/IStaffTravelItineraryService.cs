using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 2: ITINERARY SERVICE
// ============================================================================

#region Staff Travel Itinerary Service

public interface IStaffTravelItineraryService
{
    // Itinerary
    Task<StaffTravelItineraryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelItinerarySummaryDto>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelItineraryDto?> GetCurrentVersionAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelItineraryDto> CreateAsync(CreateStaffTravelItineraryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelItineraryDto> UpdateAsync(UpdateStaffTravelItineraryDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> SetCurrentVersionAsync(Guid itineraryId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Legs
    Task<StaffTravelItineraryLegDto> AddLegAsync(CreateStaffTravelItineraryLegDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelItineraryLegDto>> GetLegsAsync(Guid itineraryId, CancellationToken cancellationToken = default);
    Task<StaffTravelItineraryLegDto> GetLegByIdAsync(Guid legId, CancellationToken cancellationToken = default);
    Task<StaffTravelItineraryLegDto> UpdateLegAsync(UpdateStaffTravelItineraryLegDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteLegAsync(Guid legId, CancellationToken cancellationToken = default);

    // Activities
    Task<StaffTravelItineraryActivityDto> AddActivityAsync(CreateStaffTravelItineraryActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelItineraryActivityDto>> GetActivitiesAsync(Guid legId, CancellationToken cancellationToken = default);
    Task<StaffTravelItineraryActivityDto> UpdateActivityAsync(UpdateStaffTravelItineraryActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteActivityAsync(Guid activityId, CancellationToken cancellationToken = default);
}

#endregion
