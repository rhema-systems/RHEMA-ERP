using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 4: BOOKINGS
// ============================================================================

#region Staff Travel Flight Booking

public interface IStaffTravelFlightBookingRepository : IGenericRepository<StaffTravelFlightBooking>
{
    /// <summary>Returns all flight bookings for a request, with vendor loaded.</summary>
    Task<IEnumerable<StaffTravelFlightBooking>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns a flight booking with its ordered segments loaded.</summary>
    Task<StaffTravelFlightBooking?> GetWithSegmentsAsync(Guid id);

    /// <summary>Flight with its segments and vendor, scoped to the tenant — for reloading a write.</summary>
    Task<StaffTravelFlightBooking?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns flight bookings filtered by status.</summary>
    Task<IEnumerable<StaffTravelFlightBooking>> GetByStatusAsync(TravelBookingStatus status);

    /// <summary>Returns flight bookings placed with the given vendor.</summary>
    Task<IEnumerable<StaffTravelFlightBooking>> GetByVendorIdAsync(Guid vendorId);
}

#endregion

#region Staff Travel Flight Segment

public interface IStaffTravelFlightSegmentRepository : IGenericRepository<StaffTravelFlightSegment>
{
    /// <summary>Returns the segments of a flight booking ordered by sequence.</summary>
    Task<IEnumerable<StaffTravelFlightSegment>> GetByBookingIdAsync(Guid flightBookingId);
}

#endregion

#region Staff Travel Hotel Booking

public interface IStaffTravelHotelBookingRepository : IGenericRepository<StaffTravelHotelBooking>
{
    /// <summary>The hotel with its country and vendor, scoped to the tenant — for reloading a write (F-12).</summary>
    Task<StaffTravelHotelBooking?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all hotel bookings for a request, with vendor loaded.</summary>
    Task<IEnumerable<StaffTravelHotelBooking>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns hotel bookings filtered by status.</summary>
    Task<IEnumerable<StaffTravelHotelBooking>> GetByStatusAsync(TravelBookingStatus status);

    /// <summary>Returns hotel bookings placed with the given vendor.</summary>
    Task<IEnumerable<StaffTravelHotelBooking>> GetByVendorIdAsync(Guid vendorId);
}

#endregion

#region Staff Travel Ground Transport

public interface IStaffTravelGroundTransportRepository : IGenericRepository<StaffTravelGroundTransport>
{
    /// <summary>The ground transport with its vendor, scoped to the tenant — for reloading a write (F-12).</summary>
    Task<StaffTravelGroundTransport?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all ground transport arrangements for a request.</summary>
    Task<IEnumerable<StaffTravelGroundTransport>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns ground transport arrangements placed with the given vendor.</summary>
    Task<IEnumerable<StaffTravelGroundTransport>> GetByVendorIdAsync(Guid vendorId);
}

#endregion

#region Staff Travel Car Rental Booking

public interface IStaffTravelCarRentalBookingRepository : IGenericRepository<StaffTravelCarRentalBooking>
{
    /// <summary>The car rental with its vendor, scoped to the tenant — for reloading a write (F-12).</summary>
    Task<StaffTravelCarRentalBooking?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all car rental bookings for a request, with vendor loaded.</summary>
    Task<IEnumerable<StaffTravelCarRentalBooking>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns car rental bookings placed with the given vendor.</summary>
    Task<IEnumerable<StaffTravelCarRentalBooking>> GetByVendorIdAsync(Guid vendorId);
}

#endregion
