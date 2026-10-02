using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// What a travel request's state allows to be hung off it, in one place for the services that hang
/// things off it — bookings, budgets, advances and claims.
/// </summary>
/// <remarks>
/// <para><b>A closed trip takes nothing more</b> (travel final closure, lane 1, decision D-6). Closing
/// says every claim is paid or rejected and every advance settled — the close refuses otherwise — so a
/// booking, budget, advance or claim added afterwards would be money and commitments on a trip the
/// organisation has finished with, invisible to everything that reads "open trips".</para>
///
/// <para>⚠ Deliberately narrow. Lane 3 (the money chain) decides which statuses take an advance or a
/// claim, and lane 5 (bookings) which take a booking; each widens this rather than adding a second
/// rule beside it.</para>
/// </remarks>
public static class StaffTravelRequestGuards
{
    /// <param name="what">What is being added, for the message — "a booking", "an advance".</param>
    public static void RequireOpen(StaffTravelRequest request, string what)
    {
        if (request.Status == StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException(
                $"Travel request {request.RequestNumber} is closed, so {what} cannot be added to it.");
    }
}
