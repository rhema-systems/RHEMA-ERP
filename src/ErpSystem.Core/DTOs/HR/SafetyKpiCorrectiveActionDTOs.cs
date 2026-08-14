using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — UNIFIED CORRECTIVE-ACTION TRACKER & COMPUTED KPIs (slice 14)
//
// FR-SHE-245: the four LIVE corrective-action stores (incident CAs,
// inspection-hazard actions, equipment-inspection actions, committee meeting
// action items) stay separate tables; these DTOs are the union READ-MODEL that
// gives the spec's one-tracker view. SheHazardCorrectiveAction is deliberately
// NOT a source — it is a hazard→template configuration link with no status,
// due date or assignee, not a live action.
//
// FR-SHE-248/230/232, FR-CON-001: the computed-KPI read models. Every figure
// here is computed from live data — the hand-reported snapshot figures keep
// their existing DTOs.
// ============================================================================

#region Unified corrective-action tracker

/// <summary>Which silo a unified tracker row comes from.</summary>
public enum SheCorrectiveActionSource
{
    Incident = 1,
    Inspection = 2,
    Equipment = 3,
    Committee = 4,
}

/// <summary>
/// Lifecycle stage normalised across the silos' status enums
/// (SheCorrectiveActionStatus and SheActionItemStatus).
/// A stored "Overdue" status maps to Open/InProgress semantics — overdue-ness is
/// recomputed from the due date on every read, not trusted from the column.
/// </summary>
public enum SheUnifiedActionStatus
{
    Open = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}

public class SheUnifiedCorrectiveActionDto
{
    /// <summary>Id of the row in its own silo table.</summary>
    public Guid Id { get; set; }

    public SheCorrectiveActionSource Source { get; set; }
    public string SourceName => Source.ToString();

    /// <summary>Id of the record the action belongs to (incident / inspection / equipment / committee).</summary>
    public Guid ParentId { get; set; }

    /// <summary>Human reference of the parent — incident number, inspection number, equipment number + name, committee + meeting number.</summary>
    public string ParentReference { get; set; } = string.Empty;

    /// <summary>Route of the parent's detail screen, e.g. /hr/safety/incidents/{id}.</summary>
    public string ParentPath { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Normalised priority (Critical/High/Medium/Low). Null for silos that carry no priority (inspection and equipment actions).</summary>
    public SheCorrectiveActionPriority? Priority { get; set; }
    public string? PriorityName => Priority?.ToString();

    public SheUnifiedActionStatus Status { get; set; }
    public string StatusName => Status.ToString();

    /// <summary>The silo's own status value, verbatim, for drill-down display.</summary>
    public string RawStatus { get; set; } = string.Empty;

    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }

    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    /// <summary>Recomputed on read: open/in-progress with a due date before today.</summary>
    public bool IsOverdue { get; set; }

    public int? DaysOverdue { get; set; }

    /// <summary>Escalation tier per SheReminderLadder (1 ≤7d, 2 ≤30d, 3 beyond). 0 when not overdue.</summary>
    public int EscalationTier { get; set; }

    /// <summary>Incident CAs only — whether the action's effectiveness has been verified.</summary>
    public bool? EffectivenessVerified { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class SheUnifiedCorrectiveActionSummaryDto
{
    public int Total { get; set; }
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int Overdue { get; set; }
    public int OverdueTier1 { get; set; }
    public int OverdueTier2 { get; set; }
    public int OverdueTier3 { get; set; }
    public int DueWithin7Days { get; set; }
    public int WithoutDueDate { get; set; }

    public Dictionary<string, SheUnifiedCorrectiveActionSourceSummaryDto> BySource { get; set; } = new();
}

public class SheUnifiedCorrectiveActionSourceSummaryDto
{
    public int Total { get; set; }
    public int Open { get; set; }
    public int Overdue { get; set; }
    public int Completed { get; set; }
}

#endregion

#region Computed KPIs

/// <summary>
/// Every figure the KPI engine can compute from live data for one period +
/// optional location. Frequency rates are null when man-hours are zero/unknown;
/// averages are null when there is nothing to average; PPE compliance is null
/// until the PPE requirement matrix's job-role codes match position codes.
/// </summary>
public class SheComputedKpisDto
{
    public SheSnapshotPeriodType PeriodType { get; set; }
    public string PeriodTypeName => PeriodType.ToString();
    public int Year { get; set; }
    public int? PeriodNumber { get; set; }
    public Guid? LocationId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    /// <summary>The man-hours figure the frequency rates divided by (hand-entered input, echoed back).</summary>
    public long ManHoursUsed { get; set; }

    // Incidents
    public int TotalAccidents { get; set; }
    public int TotalIncidents { get; set; }
    public int TotalNearMisses { get; set; }
    public int TotalDangerousOccurrences { get; set; }
    public int TotalFatalities { get; set; }
    public int TotalLostTimeInjuries { get; set; }
    public int TotalLostDays { get; set; }

    // Frequency rates
    public decimal? LostTimeInjuryFrequencyRate { get; set; }
    public decimal? TotalRecordableIncidentRate { get; set; }
    public decimal? NearMissFrequencyRate { get; set; }

    // Inspections
    public int InspectionsConducted { get; set; }
    public int InspectionsOverdue { get; set; }
    public decimal? AverageInspectionComplianceScore { get; set; }
    public decimal? HousekeepingComplianceRating { get; set; }

    // Corrective actions (union tracker; tenant-wide — the silo rows carry no location)
    public int CorrectiveActionsIssued { get; set; }
    public int CorrectiveActionsCompleted { get; set; }
    public int CorrectiveActionsOverdue { get; set; }
    public decimal? CorrectiveActionClosureRate { get; set; }

    // Training
    public int TrainingProgramsPlanned { get; set; }
    public int TrainingProgramsConducted { get; set; }
    public int TotalTrainingHours { get; set; }
    public decimal? TrainingCompletionRate { get; set; }

    // Contractors
    public int ContractorsOnSite { get; set; }
    public int ContractorInspectionsConducted { get; set; }
    public int ContractorNonComplianceNoticesIssued { get; set; }
    public decimal? ContractorComplianceRate { get; set; }

    // Environment
    public int EnvironmentalIncidents { get; set; }
    public int EnvironmentalIncidentsReportedToEpa { get; set; }
    public decimal? WasteRecyclingRate { get; set; }

    // Emergency drills
    public int EmergencyDrillsConducted { get; set; }
    public decimal? FireDrillObjectivesMetRate { get; set; }

    // PPE (tenant-wide, point-in-time; null until matrix job-role codes match position codes)
    public decimal? PpeComplianceRate { get; set; }
    public int PpeEmployeesAssessed { get; set; }

    // Regulatory (tenant-wide, point-in-time)
    public int RegulatoryObligationsTotal { get; set; }
    public int RegulatoryObligationsCompliant { get; set; }
    public int RegulatoryObligationsNonCompliant { get; set; }
    public int RegulatoryObligationsExpiringSoon { get; set; }
}

/// <summary>Per-organization-unit compliance picture for a period (FR-SHE-230).</summary>
public class SheDepartmentalComplianceDto
{
    public Guid? OrganizationUnitId { get; set; }
    public string OrganizationUnitName { get; set; } = string.Empty;
    public int InspectionsConducted { get; set; }
    public int InspectionsScored { get; set; }
    public decimal? AverageComplianceScore { get; set; }
    public int Incidents { get; set; }
    public int LostTimeInjuries { get; set; }
    public int OpenInspectionFindings { get; set; }
}

/// <summary>Contractor SHE ranking for a period (FR-CON-001). Ordered best-first by average inspection score, nulls last.</summary>
public class SheContractorRankingDto
{
    public Guid ContractorId { get; set; }
    public string ContractorCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int? PreQualificationScore { get; set; }
    public int InspectionsConducted { get; set; }
    public int InspectionsScored { get; set; }
    public decimal? AverageComplianceScore { get; set; }
    public int NonComplianceNoticesIssued { get; set; }
    public int OpenNonCompliances { get; set; }
    public int Rank { get; set; }
}

/// <summary>5×5 likelihood × severity counts over the active hazard register (FR-SHE-232).</summary>
public class SheHazardHeatmapDto
{
    /// <summary>[likelihood-1][severity-1] → count, inherent (uncontrolled) risk.</summary>
    public int[][] Inherent { get; set; } = Array.Empty<int[]>();

    /// <summary>[likelihood-1][severity-1] → count, residual (post-control) risk.</summary>
    public int[][] Residual { get; set; } = Array.Empty<int[]>();

    public int TotalHazards { get; set; }

    /// <summary>Hazards excluded because a factor is outside 1–5 (data-quality signal, not silently dropped).</summary>
    public int ExcludedOutOfRange { get; set; }
}

#endregion
