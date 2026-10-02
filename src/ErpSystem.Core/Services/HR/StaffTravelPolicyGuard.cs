using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The travel policy caps that apply to one trip, resolved from the organisation's own policy
/// rather than declared by the caller.
/// </summary>
/// <param name="PolicyId">Null when no policy covers this traveller and date — see the guard.</param>
/// <param name="MaxSingleTripBudget">
/// The most one trip may be estimated at, checked when the request is submitted (lane 1, decision
/// D-1). Null when the policy sets none — the policy form stores "no limit" as 0.
/// </param>
/// <param name="CurrencyCode">
/// The currency the policy's money limits are set in. Null on a policy written before migration
/// batch 1 gave policies a currency; the caller then reads the limits in the base currency, which is
/// what the batch backfilled.
/// </param>
/// <param name="AdvanceBookingDaysFlight">Days before departure a flight is to be booked (lane 4, D-1); 0 sets none.</param>
/// <param name="AdvanceBookingDaysHotel">Days before check-in a hotel is to be booked; 0 sets none.</param>
/// <param name="PreferredVendorMandatory">Every booking names its supplier (lane 4, D-1).</param>
public sealed record TravelPolicyCaps(
    Guid? PolicyId,
    string? PolicyName,
    FlightCabinClass? MaxFlightClass,
    decimal? MaxHotelRatePerNight,
    decimal? MaxSingleTripBudget = null,
    string? CurrencyCode = null,
    int? VersionNumber = null,
    int AdvanceBookingDaysFlight = 0,
    int AdvanceBookingDaysHotel = 0,
    bool PreferredVendorMandatory = false)
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
/// is derived from the two countries. A booking does not get a second opinion. (⚠ That sentence was
/// false until the travel final closure's lane 1: the request took the payload's word for it, so a
/// domestic trip declared international bought the international caps. The request service now sets
/// it from the countries on every create, edit and submission.)</para>
///
/// <para><b>Who may approve a breach (lane 4, D-8).</b> The exception is an act of authority, and not the
/// booker's: a breaching booking is saved <i>awaiting authorisation</i> and cannot be confirmed or ticketed until
/// a travel administrator other than the one who asked for it authorises it (<c>StaffTravelBookingService</c>).
/// Before lane 4 the guard granted the exception in the same request when the caller held
/// <c>HR.Travel.Admin</c> — safe only while HR did not hold it, which D-3 ends. The guard now only describes the
/// breach.</para>
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
    private readonly IHrAudienceResolver _audience;

    public StaffTravelPolicyGuard(IStaffTravelPolicyRepository policies, IEmployeeRepository employees, IHrAudienceResolver audience)
    {
        _policies = policies;
        _employees = employees;
        _audience = audience;
    }

    /// <summary>
    /// The caps in force for this trip: the most specific current policy covering the traveller's
    /// staff level and organisation unit (or a unit above it) on the departure date.
    /// </summary>
    public Task<TravelPolicyCaps> ResolveAsync(
        StaffTravelRequest request, CancellationToken cancellationToken = default)
        => ResolveForAsync(request.TenantId, request.EmployeeId, request.OrganizationUnitId,
            request.TravelStartDate, request.IsInternational, cancellationToken);

    /// <summary>
    /// The same resolution for a trip that is not saved yet — the request form's policy preview
    /// (finding T-16).
    /// </summary>
    /// <remarks>
    /// <b>The unit is the traveller's, and its ancestors count (lane 4, O-5).</b> The guard resolved on the unit the
    /// request named, matched exactly: a directorate's policy did not cover its departments, and a unit on the request
    /// could buy a laxer policy. Lane 1 made the request carry the traveller's own unit; the guard now reads it off the
    /// traveller as well (a traveller who has moved since is checked where they are), using
    /// <paramref name="organizationUnitId"/> only for a traveller with none, and walks up the tree, nearest first.
    /// </remarks>
    public async Task<TravelPolicyCaps> ResolveForAsync(
        Guid tenantId, Guid employeeId, Guid? organizationUnitId, DateOnly departure, bool isInternational,
        CancellationToken cancellationToken = default)
    {
        // The staff level lives on the traveller's POSITION, not on the employee, so it needs the
        // detail read. A traveller with no position simply resolves to the org-wide policy.
        Guid? staffLevelId = null;
        var unitId = organizationUnitId;
        var employee = await _employees.GetByIdWithDetailsAsync(employeeId);
        if (employee is not null && employee.TenantId == tenantId)
        {
            staffLevelId = employee.Position?.StaffLevelId;
            unitId = employee.OrganizationUnitId ?? organizationUnitId;
        }

        var chain = unitId is Guid unit
            ? await _audience.UnitAncestryAsync(tenantId, unit, cancellationToken)
            : Array.Empty<Guid>();
        var candidates = await _policies.GetApplicablePoliciesAsync(staffLevelId, chain, departure);

        // ⚠ Two filters, both load-bearing.
        //
        // TENANT: the repository is unscoped, like the rest of this area's reads — without this,
        // another tenant's policy would decide what this one may book.
        //
        // APPROVED: a policy is a draft until someone with `HR.Travel.Admin` signs it. `ApprovedById`
        // had no writer at all when the caps were first enforced, which meant any holder of
        // `HR.Travel.Write` could author the rule constraining everyone's travel spending and have
        // it bind immediately. An unapproved policy caps nothing.
        var policy = candidates.FirstOrDefault(
            p => p.TenantId == tenantId && p.ApprovedById is not null);
        if (policy is null) return TravelPolicyCaps.None;

        return new TravelPolicyCaps(
            policy.Id,
            policy.PolicyName,
            isInternational ? policy.MaxFlightClassInternational : policy.MaxFlightClassDomestic,
            isInternational ? policy.MaxHotelRateInternational : policy.MaxHotelRateDomestic,
            policy.MaxSingleTripBudget > 0m ? policy.MaxSingleTripBudget : null,
            string.IsNullOrWhiteSpace(policy.CurrencyCode) ? null : policy.CurrencyCode.Trim(),
            policy.VersionNumber,
            Math.Max(0, policy.AdvanceBookingDaysFlight),
            Math.Max(0, policy.AdvanceBookingDaysHotel),
            policy.PreferredVendorMandatory);
    }

    /// <summary>
    /// What a cabin class above the policy's cap breaches, in words; null within the cap or with no policy.
    /// </summary>
    /// <remarks>
    /// <para>The enum is ordered Economy(1) → PremiumEconomy(2) → Business(3) → First(4), so "above the cap" is a
    /// numeric comparison. That ordering is load-bearing; do not renumber it.</para>
    ///
    /// <para><b>Lane 4, D-8: a breach is described, not decided here.</b> These checks returned "authorised" when
    /// the caller held <c>HR.Travel.Admin</c> and asked for an exception in the same request — so once HR holds
    /// Admin (D-3) the clerk booking over the cap would tick their own breach. The booking service now saves a
    /// breaching booking awaiting authorisation, and a different administrator decides it.</para>
    /// </remarks>
    public static string? FlightClassBreach(FlightCabinClass bookedClass, TravelPolicyCaps caps)
        => caps.MaxFlightClass is FlightCabinClass cap && bookedClass > cap
            ? $"{Spaced(bookedClass)} is above the {caps.PolicyName} cap of {Spaced(cap)}"
            : null;

    /// <summary>
    /// What a nightly rate above the policy's cap breaches; null within the cap or with no cap.
    /// </summary>
    /// <remarks>
    /// The rate is compared <b>in the policy's currency</b> (lane 4, C3/T-9): the caller converts the booked rate
    /// through <see cref="HrCurrencyBridge"/> and passes both. The cap was a bare number compared with a rate in
    /// whatever currency the booking carried — a USD 300 room passed a GHS 1,000 cap.
    /// </remarks>
    /// <param name="rateInPolicyCurrency">The booked nightly rate, converted into <paramref name="policyCurrency"/>.</param>
    /// <param name="bookedRate">The rate as booked, for the message — "USD 300.00 (GHS 3,750.00)".</param>
    public static string? HotelRateBreach(
        decimal rateInPolicyCurrency, string policyCurrency, string bookedRate, TravelPolicyCaps caps)
        => caps.MaxHotelRatePerNight is decimal cap && cap > 0m && rateInPolicyCurrency > cap
            ? $"a nightly rate of {bookedRate} is above the {caps.PolicyName} cap of {policyCurrency} {cap:N2}"
            : null;

    /// <summary>
    /// What booking later than the policy asks breaches (lane 4, D-1: <c>AdvanceBookingDaysFlight/Hotel</c> had no
    /// reader); null when booked in time or when the policy sets no notice.
    /// </summary>
    /// <param name="daysAhead">Days between the day the booking was made and the departure (or check-in).</param>
    /// <param name="what">"flight" or "hotel".</param>
    public static string? AdvanceBookingBreach(int daysAhead, int required, string what, TravelPolicyCaps caps)
    {
        if (required <= 0 || daysAhead >= required) return null;
        var before = what == "hotel" ? "check-in" : "departure";
        var when = daysAhead switch
        {
            < 0 => $"{-daysAhead} day{(daysAhead == -1 ? "" : "s")} after {before}",
            0 => $"on the day of {before}",
            _ => $"{daysAhead} day{(daysAhead == 1 ? "" : "s")} before {before}",
        };
        return $"the {what} was booked {when}; {caps.PolicyName} asks for {required} days' notice";
    }

    private static string Spaced(FlightCabinClass cabin)
        => System.Text.RegularExpressions.Regex.Replace(cabin.ToString(), "(?<!^)([A-Z])", " $1");
}
