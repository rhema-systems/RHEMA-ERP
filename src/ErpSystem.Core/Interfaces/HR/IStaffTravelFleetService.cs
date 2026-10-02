using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// What a company-vehicle leg asks of Fleet (travel final closure, lane 6, slice 6a). A trip's own fleet trip is made,
/// changed, submitted and cancelled through Fleet's services; Fleet's code is not changed (D-12).
/// </summary>
/// <remarks>On a change, a null vehicle or driver keeps the fleet trip's own.</remarks>
public sealed record StaffTravelFleetReservation(
    Guid? VehicleAssetId, Guid? DriverEmployeeId, DateTime Start, DateTime End,
    string? Origin, string? Destination, Guid? DestinationId, string? Purpose, string? Notes);

public interface IStaffTravelFleetService
{
    /// <summary>The vehicles, drivers and destinations a trip's leg chooses from, each saying why it is not available.</summary>
    Task<StaffTravelFleetOptionsDto> GetOptionsAsync(
        Guid requestId, DateTime? from = null, DateTime? to = null, Guid? excludeFleetTripId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Checks the reservation (FX-2, FX-5, the destination), creates the fleet trip and submits it when Fleet
    /// publishes an approval route (D-27). Returns the fleet trip's id.</summary>
    Task<Guid> ReserveAsync(StaffTravelRequest request, StaffTravelFleetReservation reservation, CancellationToken cancellationToken = default);

    /// <summary>Changes a reservation while Fleet allows: a draft or rejected trip wholly, an approved one only its driver.</summary>
    Task UpdateReservationAsync(
        StaffTravelRequest request, Guid fleetTripId, StaffTravelFleetReservation reservation, CancellationToken cancellationToken = default);

    /// <summary>Cancels a fleet trip that is not out — refused once dispatched; nothing to do once completed or cancelled.</summary>
    Task CancelReservationAsync(Guid fleetTripId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the request's undispatched fleet trips and marks their legs Cancelled — the trip's cancel and its
    /// <i>Request change</i> (FX-3, D2). The legs are tracked; the caller saves. Returns how many.
    /// </summary>
    Task<int> CancelForRequestAsync(Guid tenantId, Guid requestId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Fills a company-vehicle leg's vehicle, plate, driver, Fleet's status and times — and the leg's status,
    /// read from the fleet trip (no copies of Fleet's facts).</summary>
    Task DescribeAsync(IReadOnlyCollection<StaffTravelGroundTransportDto> legs, CancellationToken cancellationToken = default);
}
