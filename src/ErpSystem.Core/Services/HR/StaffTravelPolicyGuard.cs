using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The travel policy caps that apply to one trip, resolved from the organisation's own policy
/// rather than declared by the caller.
/// </summary>
/// <param name="PolicyId">Null when no policy covers this traveller and date — see the guard.</param>
public sealed record TravelPolicyCaps(
    Guid? PolicyId,
    string? PolicyName,
    FlightCabinClass? MaxFlightClass,
    decimal? MaxHotelRatePerNight)
{
    public static readonly TravelPolicyCaps None = new(null, null, null, null);

    public bool HasPolicy => PolicyId is not null;
}

/// <summary>
/// Enforces the travel policy's spend caps at the point a booking is written.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Every booking DTO carried <c>PolicyAllowedClass</c> /
/// <c>PolicyMaxRatePerNight</c> and a <c>ClassExceptionApproved</c> / <c>RateExceptionApproved</c>
/// boolean — all four supplied by the caller — and <c>StaffTravelBookingService</c> contained
/// <b>zero references to any of them</b>. So a caller could book First class, declare that the
/// policy allowed First, tick their own exception, and nothing refused it. The real caps were on
/// <see cref="StaffTravelPolicy"/> the whole time
/// (<c>MaxFlightClassDomestic/International</c>, <c>MaxHotelRateDomestic/International</c>), and
/// <c>GetApplicablePoliciesAsync</c> already resolved the right policy — it was simply never
/// called from a booking path. This is the "gate that gates nothing" shape: the code reads as
/// built precisely because so much of it mentions the policy.</para>
///
/// <para><b>Domestic or international is the request's answer, not the booking's.</b> The policy
/// splits both caps that way, and the trip already knows: <c>StaffTravelRequest.IsInternational</c>
/// is derived from the two countries. A booking does not get a second opinion.</para>
///
/// <para><b>Who may approve a breach.</b> The exception flag is not a field the booking form fills
/// in; it is an act of authority. It may only be set by a caller holding
/// <c>HR.Travel.Admin</c> — which HR deliberately does <i>not</i> hold — so a travel clerk with
/// Write cannot approve their own breach. The service takes that decision as an explicit
/// parameter rather than reading claims itself, because a service that quietly inspects the
/// caller's identity is the thing that made these fields spoofable in the first place.</para>
///
/// <para><b>No policy means no cap, and that is deliberate.</b> A tenant that has configured no
/// travel policy is not thereby forbidden from booking travel — it simply has no rule to breach,
/// and <see cref="TravelPolicyCaps.HasPolicy"/> is false. Refusing every booking until someone
/// writes a policy would make the area unusable on a fresh tenant, which is how unmaintained
/// reference data has repeatedly turned a rule into an obstacle elsewhere in HR.</para>
/// </remarks>
public sealed class StaffTravelPolicyGuard
{
    private readonly IStaffTravelPolicyRepository _policies;
    private readonly IEmployeeRepository _employees;

    public StaffTravelPolicyGuard(IStaffTravelPolicyRepository policies, IEmployeeRepository employees)
    {
        _policies = policies;
        _employees = employees;
    }

    /// <summary>
    /// The caps in force for this trip: the most specific current policy covering the traveller's
    /// staff level and organisation unit on the departure date.
    /// </summary>
    public async Task<TravelPolicyCaps> ResolveAsync(
        StaffTravelRequest request, CancellationToken cancellationToken = default)
    {
        // The staff level lives on the traveller's POSITION, not on the employee, so it needs the
        // detail read. A traveller with no position simply resolves to the org-wide policy.
        Guid? staffLevelId = null;
        var employee = await _employees.GetByIdWithDetailsAsync(request.EmployeeId);
        if (employee is not null && employee.TenantId == request.TenantId)
            staffLevelId = employee.Position?.StaffLevelId;

        var candidates = await _policies.GetApplicablePoliciesAsync(
            staffLevelId, request.OrganizationUnitId, request.TravelStartDate);

        // ⚠ The repository is unscoped, like the rest of this area's reads — filter here rather
        // than letting another tenant's policy decide what this one may book.
        var policy = candidates.FirstOrDefault(p => p.TenantId == request.TenantId);
        if (policy is null) return TravelPolicyCaps.None;

        return new TravelPolicyCaps(
            policy.Id,
            policy.PolicyName,
            request.IsInternational ? policy.MaxFlightClassInternational : policy.MaxFlightClassDomestic,
            request.IsInternational ? policy.MaxHotelRateInternational : policy.MaxHotelRateDomestic);
    }

    /// <summary>
    /// Refuses a cabin class above the policy cap unless an authorised caller has approved the
    /// exception. Returns the exception flag to store — never the caller's claim of it.
    /// </summary>
    /// <remarks>
    /// The enum is ordered Economy(1) → PremiumEconomy(2) → Business(3) → First(4), so "above the
    /// cap" is a numeric comparison. That ordering is load-bearing; do not renumber it.
    /// </remarks>
    public bool RequireFlightClassWithinPolicy(
        FlightCabinClass bookedClass,
        TravelPolicyCaps caps,
        bool exceptionRequested,
        bool callerMayApproveExceptions)
    {
        if (caps.MaxFlightClass is not FlightCabinClass cap) return false;
        if (bookedClass <= cap) return false;

        if (!exceptionRequested)
            throw new InvalidOperationException(
                $"{bookedClass} exceeds the {caps.PolicyName} cap of {cap} for this trip. " +
                "An approved policy exception is required to book above the cap.");

        if (!callerMayApproveExceptions)
            throw new UnauthorizedAccessException(
                $"Approving a booking above the {caps.PolicyName} cap of {cap} requires travel " +
                "administrator rights. Ask a travel administrator to authorise the exception.");

        return true;
    }

    /// <summary>
    /// Refuses a nightly rate above the policy cap unless an authorised caller has approved it.
    /// </summary>
    /// <remarks>
    /// ⚠ The cap and the rate are compared <b>as numbers in whatever currency each carries</b>.
    /// <c>StaffTravelPolicy</c> stores no currency alongside <c>MaxHotelRateDomestic/International</c>,
    /// so there is nothing to convert from; comparing a USD rate against a GHS cap would be wrong in
    /// the other direction. The comparison is therefore only sound while policy caps and bookings
    /// are expressed in the same currency. Recorded rather than silently assumed — giving the policy
    /// a currency is the fix, and it is a schema change beyond this slice.
    /// </remarks>
    public bool RequireHotelRateWithinPolicy(
        decimal ratePerNight,
        TravelPolicyCaps caps,
        bool exceptionRequested,
        bool callerMayApproveExceptions)
    {
        if (caps.MaxHotelRatePerNight is not decimal cap || cap <= 0m) return false;
        if (ratePerNight <= cap) return false;

        if (!exceptionRequested)
            throw new InvalidOperationException(
                $"A nightly rate of {ratePerNight:N2} exceeds the {caps.PolicyName} cap of " +
                $"{cap:N2} for this trip. An approved policy exception is required.");

        if (!callerMayApproveExceptions)
            throw new UnauthorizedAccessException(
                $"Approving a nightly rate above the {caps.PolicyName} cap of {cap:N2} requires " +
                "travel administrator rights. Ask a travel administrator to authorise the exception.");

        return true;
    }
}
