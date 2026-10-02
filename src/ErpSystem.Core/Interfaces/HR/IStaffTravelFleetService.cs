using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

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

    // ---- Slice 6b: fuel on claims (D-30, D-31, D-32) ----

    /// <summary>A trip's company-vehicle trips, with the fuel Fleet already logs on each — what a fuel expense chooses from.</summary>
    Task<StaffTravelFleetFuelOptionsDto> GetFuelOptionsAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A claim line's fleet trip and litres (D-30, S4): only a fuel expense names them; on a trip with a live company
    /// vehicle and no car rental a fuel expense must; the trip is the request's own and live, the fill inside it. When
    /// <paramref name="askAboutDuplicates"/>, a fill on a day Fleet already logs fuel for that trip needs
    /// <paramref name="duplicateReason"/> (D-32) — returned as the internal note to keep; null otherwise.
    /// </summary>
    Task<string?> CheckFuelLineAsync(
        StaffTravelRequest request, TravelExpenseCategory category, DateOnly expenseDate, Guid? fleetTripId, decimal? litres,
        string? duplicateReason, bool askAboutDuplicates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a paid fuel expense in Fleet (D-31): the litres claimed and the amount paid, in Finance's base currency, on
    /// the fleet trip's vehicle. Fleet writes its own cost entry. Returns Fleet's fuel record id.
    /// </summary>
    Task<Guid> RecordClaimFuelAsync(
        Guid fleetTripId, DateOnly fuelledOn, decimal litres, decimal amountPaid, string? merchant, string reference, string notes,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a fuel record a payment wrote, with its cost entry — a voided payment (D-31).</summary>
    Task RemoveClaimFuelAsync(Guid fleetFuelTransactionId, CancellationToken cancellationToken = default);
}
