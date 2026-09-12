namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — DASHBOARD
// Aggregated KPI counts for the Safety, Health & Environment landing dashboard.
// ============================================================================

public class SheDashboardDto
{
    // Incidents
    public int OpenIncidents { get; set; }
    public int LostTimeInjuries { get; set; }
    public int IncidentsRequiringInvestigation { get; set; }
    public int OverdueCorrectiveActions { get; set; }

    // Hazards & risk assessments
    public int HazardsDueForReview { get; set; }
    public int HighRiskHazards { get; set; }
    public int RiskAssessmentsExpiring { get; set; }
    public int RiskAssessmentsDueForReview { get; set; }

    // Inspections
    public int InspectionsDue { get; set; }
    public int OpenInspectionFindings { get; set; }

    // Permits
    public int ActivePermits { get; set; }
    public int ExpiringPermits { get; set; }
    public int SuspendedPermits { get; set; }

    // Equipment & PPE
    public int EquipmentDueForInspection { get; set; }
    public int EquipmentExpiringCertification { get; set; }
    public int EquipmentOutOfService { get; set; }
    public int PpeBelowReorder { get; set; }

    // Environment (slice 17 — Part D core)
    /// <summary>FR-ENV-019 — rendered red on the dashboard.</summary>
    public int ExpiredEnvironmentalPermits { get; set; }
    public int EnvironmentalPermitsExpiringSoon { get; set; }
    public int OpenEnvironmentalIncidents { get; set; }
    public int MonitoringSchedulesDue { get; set; }
    public int RegulatoryUpdatesOpen { get; set; }
    /// <summary>FR-ENV-029 — the sustainability KPI presence on the main dashboard.</summary>
    public int SustainabilityInitiativesActive { get; set; }
}
