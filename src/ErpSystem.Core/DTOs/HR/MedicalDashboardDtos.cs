using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// MEDICAL — DASHBOARD DTOs
// ----------------------------------------------------------------------------
// Aggregated, read-only metrics surfaced by GET /api/medical/dashboard.
// *Name fields are computed enum labels, matching the summary-DTO convention.
// ============================================================================

public class MedicalDashboardDto
{
    // Claims
    public int TotalClaims { get; set; }
    public int PendingClaims { get; set; }
    public int FlaggedClaims { get; set; }
    public int ApprovedClaims { get; set; }
    public int PaidClaims { get; set; }
    public decimal TotalReimbursedAmount { get; set; }
    public decimal PendingClaimsAmount { get; set; }

    // Insurance
    public int ActivePolicies { get; set; }
    public int ExpiringPolicies { get; set; }
    public int OverduePremiums { get; set; }
    public decimal OverduePremiumAmount { get; set; }

    // Clinical
    public int PendingPreAuthorizations { get; set; }
    public int PendingReferrals { get; set; }
    public int UpcomingAppointments { get; set; }

    // Employee health
    public int ExamsDue { get; set; }

    // Breakdowns
    public List<MedicalClaimStatusCountDto> ClaimsByStatus { get; set; } = new();
    public List<MedicalMonthlyClaimCountDto> MonthlyClaimTrend { get; set; } = new();

    // Spotlights
    public List<MedicalClaimSpotlightDto> RecentClaims { get; set; } = new();
    public List<MedicalClaimSpotlightDto> PendingApprovalClaims { get; set; } = new();
    public List<MedicalAppointmentSpotlightDto> UpcomingAppointmentList { get; set; } = new();
}

public class MedicalClaimStatusCountDto
{
    public ClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int Count { get; set; }
}

public class MedicalMonthlyClaimCountDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class MedicalClaimSpotlightDto
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public MedicalExpenseType ExpenseType { get; set; }
    public string ExpenseTypeName => ExpenseType.ToString();
    public ClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal AmountRequested { get; set; }
    public DateTime ClaimDate { get; set; }
}

public class MedicalAppointmentSpotlightDto
{
    public Guid Id { get; set; }
    public string AppointmentNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public DateTime AppointmentDateTime { get; set; }
    public MedicalAppointmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
}
