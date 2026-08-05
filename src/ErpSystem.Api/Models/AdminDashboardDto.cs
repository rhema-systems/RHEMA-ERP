namespace ErpSystem.Api.Models;

/// <summary>
/// Aggregated KPIs and recent activity for the admin/HR home dashboard.
/// All figures are scoped to the caller's tenant via the global query filter.
/// </summary>
public class AdminDashboardDto
{
    // ── Headline KPIs ─────────────────────────────────────────────────────────
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }
    public int NewHiresThisMonth { get; set; }
    public int PendingLeaveRequests { get; set; }
    public int OpenVacancies { get; set; }
    public int ActiveApplications { get; set; }
    public int PendingRequisitions { get; set; }
    public int ActiveAppraisals { get; set; }
    public int UpcomingTrainings { get; set; }

    // ── Workforce breakdown ───────────────────────────────────────────────────
    public int FullTimeEmployees { get; set; }
    public int PartTimeEmployees { get; set; }
    public int InactiveEmployees { get; set; }

    // ── Recent activity ───────────────────────────────────────────────────────
    public List<RecentHireDto> RecentHires { get; set; } = new();
}

public class RecentHireDto
{
    public string Name { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public DateOnly? DateEmployed { get; set; }
}
