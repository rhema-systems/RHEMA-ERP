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
