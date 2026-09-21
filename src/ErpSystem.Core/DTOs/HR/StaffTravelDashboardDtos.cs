using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// STAFF TRAVEL — DASHBOARD DTOs
// ----------------------------------------------------------------------------
// Aggregated, read-only metrics surfaced by GET /api/staff-travel/requests/dashboard.
// *Name fields are computed enum labels, matching the summary-DTO convention.
// ============================================================================

public class StaffTravelDashboardDto
{
    // Headline counts
    public int TotalRequests { get; set; }
    public int DraftCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int ApprovedCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int RejectedCount { get; set; }
    public int CancelledCount { get; set; }

    // Scope / risk
    public int InternationalCount { get; set; }
    public int DomesticCount { get; set; }
    public int HighRiskCount { get; set; }
    public int UpcomingTripCount { get; set; }

    // Cost (active requests only — excludes cancelled/rejected)
    public decimal TotalEstimatedCost { get; set; }
    public decimal TotalApprovedBudget { get; set; }

    // Breakdowns
    public List<StaffTravelStatusCountDto> ByStatus { get; set; } = new();
    public List<StaffTravelTypeCountDto> ByTravelType { get; set; } = new();
    public List<StaffTravelMonthlyCountDto> MonthlyTrend { get; set; } = new();

    /// <summary>
    /// Cost split by the currency each trip was costed in. ⚠ The two scalar totals above add
    /// currencies together and are only meaningful when a tenant travels in one — see
    /// <see cref="StaffTravelCurrencyTotalDto"/>.
    /// </summary>
    public List<StaffTravelCurrencyTotalDto> CostByCurrency { get; set; } = new();

    // Spotlights
    public List<StaffTravelRequestSummaryDto> PendingApprovals { get; set; } = new();
    public List<StaffTravelRequestSummaryDto> UpcomingTrips { get; set; } = new();
    public List<StaffTravelRequestSummaryDto> RecentRequests { get; set; } = new();
}

public class StaffTravelStatusCountDto
{
    public StaffTravelRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int Count { get; set; }
}

public class StaffTravelTypeCountDto
{
    public StaffTravelType TravelType { get; set; }
    public string TravelTypeName => TravelType.ToString();
    public int Count { get; set; }
}

public class StaffTravelMonthlyCountDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// Travel cost for one currency, on the dashboard.
/// </summary>
/// <remarks>
/// ⚠ <b>This exists because the scalar totals beside it add currencies together.</b>
/// <c>TotalEstimatedCost</c> and <c>TotalApprovedBudget</c> sum <c>EstimatedTotalCost</c> across
/// every request regardless of the currency each was costed in, so 5,000 GHS and 5,000 USD become
/// "10,000" of nothing. On a headline figure that is worse than showing no number at all.
///
/// <para>The totals are <b>not</b> converted to a base currency here, deliberately. Travel does not
/// invent a rate (slice 6), Finance's conversion is currently inverted
/// (<c>docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md</c> §2), and a converted headline would be
/// confidently wrong rather than visibly incomplete. A screen should show the single figure when a
/// tenant travels in one currency and this breakdown when it does not.</para>
/// </remarks>
public class StaffTravelCurrencyTotalDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal EstimatedTotal { get; set; }
    public decimal ApprovedBudget { get; set; }
    public int RequestCount { get; set; }
}
