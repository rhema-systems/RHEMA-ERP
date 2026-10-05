using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// What an itinerary may do, and when (travel final closure, lane 5, slice 5b — findings D5, O-15, Q3, Q5, decision D-25).
/// </summary>
/// <remarks>
/// <para><b>When a trip is planned.</b> An itinerary was created on a trip in any status, a Closed one included, and its
/// legs and activities changed whenever. A plan is drafted with the request and kept while the trip is open — Draft,
/// Submitted, returned for revision, Approved, under way — and not on one rejected, cancelled, completed or closed.</para>
///
/// <para><b>Status is the server's (D-25).</b> It was the payload's on every edit. A version is a Draft when made;
/// <i>Finalise</i> marks the current version Approved and stamps <c>FinalizedAt</c>; a version a newer one replaces as
/// current is Superseded; the trip's cancel marks the current one Cancelled. PendingReview, Active and Completed stay
/// readable with no writer. A finalised, superseded or cancelled version is the record of a plan — it is not edited,
/// and neither are its legs or activities: a change is a new version.</para>
///
/// <para><b>Days.</b> The day totals were the payload's; they are the trip's own dates — every day from departure to
/// return, the Saturdays and Sundays among them, and the rest working days (public holidays are not subtracted).</para>
/// </remarks>
public static class StaffTravelItineraryRules
{
    public static bool IsPlannable(StaffTravelRequestStatus status)
        => status is StaffTravelRequestStatus.Draft or StaffTravelRequestStatus.Submitted
            or StaffTravelRequestStatus.ReturnedForRevision or StaffTravelRequestStatus.Approved
            or StaffTravelRequestStatus.InProgress;

    /// <summary>A plan is made and changed while the trip is open — from draft to under way.</summary>
    public static void RequirePlannable(StaffTravelRequest request, string what)
    {
        if (IsPlannable(request.Status)) return;
        throw new InvalidOperationException(
            $"Travel request {request.RequestNumber} is {StaffTravelBookingRules.Describe(request.Status)}, so {what} " +
            "cannot be changed — a trip is planned while it is open, from its draft until it is under way.");
    }

    /// <summary>A version still being written — the only kind whose plan changes.</summary>
    public static bool IsEditable(TravelItineraryStatus status)
        => status is TravelItineraryStatus.Draft or TravelItineraryStatus.PendingReview;

    public static void RequireEditable(StaffTravelItinerary itinerary)
    {
        if (IsEditable(itinerary.Status)) return;
        throw new InvalidOperationException(
            $"Version {itinerary.VersionNumber} of the itinerary is {Describe(itinerary.Status)} — the record of a plan, " +
            "not changed. Make a new version to change it.");
    }

    public static string Describe(TravelItineraryStatus status) => status switch
    {
        TravelItineraryStatus.Approved => "finalised",
        TravelItineraryStatus.PendingReview => "pending review",
        _ => status.ToString().ToLowerInvariant(),
    };

    /// <summary>The trip's days: all of them, the weekend ones among them, and the rest.</summary>
    public static (int Travel, int Working, int Weekend) Days(StaffTravelRequest request)
    {
        if (request.TravelEndDate < request.TravelStartDate) return (0, 0, 0);
        var travel = request.TravelEndDate.DayNumber - request.TravelStartDate.DayNumber + 1;
        var weekend = 0;
        for (var d = request.TravelStartDate; d <= request.TravelEndDate; d = d.AddDays(1))
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) weekend++;
        return (travel, travel - weekend, weekend);
    }

    public static void ApplyDays(StaffTravelItinerary itinerary, StaffTravelRequest request)
    {
        var (travel, working, weekend) = Days(request);
        itinerary.TotalTravelDays = travel;
        itinerary.TotalWorkingDays = working;
        itinerary.TotalWeekendDays = weekend;
    }

    /// <summary>
    /// D-24's itinerary half: the trip's cancel marks its current version Cancelled — the plan of a trip that will not
    /// happen. Tracked; the caller saves. Returns whether one was.
    /// </summary>
    public static async Task<bool> CancelWithTripAsync(
        IUnitOfWork unitOfWork, Guid tenantId, Guid requestId, string userId, CancellationToken cancellationToken)
    {
        var current = await unitOfWork.Repository<StaffTravelItinerary>()
            .GetQueryable(i => i.TenantId == tenantId && i.StaffTravelRequestId == requestId && !i.IsDeleted && i.IsCurrentVersion)
            .ToListAsync(cancellationToken);
        foreach (var itinerary in current)
        {
            itinerary.Status = TravelItineraryStatus.Cancelled;
            itinerary.UpdatedAt = DateTime.UtcNow;
            itinerary.UpdatedBy = userId;
        }
        return current.Count > 0;
    }
}
