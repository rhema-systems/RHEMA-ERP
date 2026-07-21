using ErpSystem.Core.Entities.HR.StaffTravel;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 2: ITINERARY & LEGS
// ============================================================================

#region Staff Travel Itinerary

public interface IStaffTravelItineraryRepository : IGenericRepository<StaffTravelItinerary>
{
    /// <summary>Returns all itinerary versions for a request, newest version first.</summary>
    Task<IEnumerable<StaffTravelItinerary>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns the current-version itinerary for a request, with legs and activities loaded.</summary>
    Task<StaffTravelItinerary?> GetCurrentVersionAsync(Guid requestId);

    /// <summary>Returns a fully-loaded itinerary including its legs and each leg's activities.</summary>
    Task<StaffTravelItinerary?> GetWithLegsAsync(Guid id);

    /// <summary>Returns the next version number to use for a request's itinerary (max existing + 1).</summary>
    Task<int> GetNextVersionNumberAsync(Guid requestId);
}

#endregion

#region Staff Travel Itinerary Leg

public interface IStaffTravelItineraryLegRepository : IGenericRepository<StaffTravelItineraryLeg>
{
    /// <summary>Returns the legs of an itinerary ordered by sequence, with activities loaded.</summary>
    Task<IEnumerable<StaffTravelItineraryLeg>> GetByItineraryIdAsync(Guid itineraryId);

    /// <summary>Returns a single leg with its activities loaded.</summary>
    Task<StaffTravelItineraryLeg?> GetWithActivitiesAsync(Guid id);
}

#endregion

#region Staff Travel Itinerary Activity

public interface IStaffTravelItineraryActivityRepository : IGenericRepository<StaffTravelItineraryActivity>
{
    /// <summary>Returns the activities of a leg ordered by start time.</summary>
    Task<IEnumerable<StaffTravelItineraryActivity>> GetByLegIdAsync(Guid legId);

    /// <summary>Returns only the mandatory activities of a leg.</summary>
    Task<IEnumerable<StaffTravelItineraryActivity>> GetMandatoryActivitiesAsync(Guid legId);
}

#endregion
