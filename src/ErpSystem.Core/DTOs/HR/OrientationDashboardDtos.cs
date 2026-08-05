using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// ORIENTATION — DASHBOARD DTOs
// Aggregated, read-only metrics surfaced by GET /api/orientation-dashboard.
// ============================================================================

public class OrientationDashboardDto
{
    // Programs
    public int TotalPrograms { get; set; }
    public int ActivePrograms { get; set; }
    public int DraftPrograms { get; set; }

    // Enrollments
    public int TotalEnrollments { get; set; }
    public int NotStartedEnrollments { get; set; }
    public int InProgressEnrollments { get; set; }
    public int CompletedEnrollments { get; set; }
    public int OverdueEnrollments { get; set; }

    /// <summary>Completed / total enrollments, as a 0–100 percentage.</summary>
    public decimal OverallCompletionRate { get; set; }

    // Delivery & artifacts
    public int UpcomingSessions { get; set; }
    public int ExpiringCertificates { get; set; }

    // Breakdowns
    public List<OrientationStatusCountDto> ProgramsByStatus { get; set; } = new();
    public List<OrientationCompletionCountDto> EnrollmentsByCompletionStatus { get; set; } = new();

    // Spotlights
    public List<EmployeeOrientationSummaryDto> OverdueList { get; set; } = new();
    public List<OrientationSessionSummaryDto> UpcomingSessionList { get; set; } = new();
    public List<OrientationCertificateDto> ExpiringCertificateList { get; set; } = new();
}

public class OrientationStatusCountDto
{
    public OrientationProgramStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int Count { get; set; }
}

public class OrientationCompletionCountDto
{
    public OrientationCompletionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int Count { get; set; }
}
