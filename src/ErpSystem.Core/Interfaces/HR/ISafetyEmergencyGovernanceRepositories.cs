using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE repositories — Emergency (M), Regulatory (N), Signage (O), KPI (P),
// Committee & Meetings (Q) and Return-to-Work (R).
// ============================================================================

// ============================================================================
// M. EMERGENCY PREPAREDNESS & RESPONSE
// ============================================================================

public interface IEmergencyPlanRepository : IGenericRepository<EmergencyPlan>
{
    Task<EmergencyPlan?> GetByNumberAsync(string planNumber);
    Task<EmergencyPlan?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<EmergencyPlan>> GetAllSummaryAsync();
    Task<IEnumerable<EmergencyPlan>> GetByTypeAsync(SheEmergencyType type);
    Task<IEnumerable<EmergencyPlan>> GetActiveAsync();

    /// <summary>Returns active plans whose next review date falls within the specified number of days.</summary>
    Task<IEnumerable<EmergencyPlan>> GetDueForReviewAsync(int daysAhead = 30);
}

public interface IEmergencyDrillRepository : IGenericRepository<EmergencyDrill>
{
    Task<EmergencyDrill?> GetByNumberAsync(string drillNumber);
    Task<IEnumerable<EmergencyDrill>> GetByPlanIdAsync(Guid planId);
    Task<IEnumerable<EmergencyDrill>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);

    /// <summary>Returns drills whose next scheduled date falls within the specified number of days.</summary>
    Task<IEnumerable<EmergencyDrill>> GetUpcomingAsync(int daysAhead = 30);
}

public interface IEmergencyResponseTeamRepository : IGenericRepository<EmergencyResponseTeam>
{
    Task<IEnumerable<EmergencyResponseTeam>> GetByPlanIdAsync(Guid planId);
    Task<IEnumerable<EmergencyResponseTeam>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Returns active team members whose certificate expires within the specified number of days.</summary>
    Task<IEnumerable<EmergencyResponseTeam>> GetExpiringCertificatesAsync(int daysAhead = 30);
}

// ============================================================================
// N. REGULATORY COMPLIANCE REGISTER
// ============================================================================

public interface ISheRegulatoryObligationRepository : IGenericRepository<SheRegulatoryObligation>
{
    Task<SheRegulatoryObligation?> GetByCodeAsync(string obligationCode);
    /// <summary>Returns the obligation with its evidence records loaded.</summary>
    Task<SheRegulatoryObligation?> GetWithEvidenceAsync(Guid id);
    Task<IEnumerable<SheRegulatoryObligation>> GetAllSummaryAsync();
    Task<IEnumerable<SheRegulatoryObligation>> GetByDomainAsync(SheRegulatoryDomain domain);
    Task<IEnumerable<SheRegulatoryObligation>> GetByComplianceStatusAsync(SheComplianceStatus status);
    Task<IEnumerable<SheRegulatoryObligation>> GetByOwnerAsync(Guid ownerId);

    /// <summary>Returns active obligations whose next review date falls within the specified number of days.</summary>
    Task<IEnumerable<SheRegulatoryObligation>> GetDueForReviewAsync(int daysAhead = 30);

    /// <summary>Returns active obligations that are not currently compliant.</summary>
    Task<IEnumerable<SheRegulatoryObligation>> GetNonCompliantAsync();
}

// ============================================================================
// O. SAFETY SIGNAGE REGISTER
// ============================================================================

public interface ISafetySignRepository : IGenericRepository<SafetySign>
{
    Task<SafetySign?> GetByCodeAsync(string signCode);
    Task<IEnumerable<SafetySign>> GetAllSummaryAsync();
    Task<IEnumerable<SafetySign>> GetByLocationAsync(Guid locationId);
    Task<IEnumerable<SafetySign>> GetByTypeAsync(SheSafetySignType type);
    Task<IEnumerable<SafetySign>> GetByStatusAsync(SheSafetySignStatus status);
    Task<IEnumerable<SafetySign>> GetActiveAsync();

    /// <summary>Returns active signs whose next inspection falls within the specified number of days.</summary>
    Task<IEnumerable<SafetySign>> GetDueForInspectionAsync(int daysAhead = 30);
}

// ============================================================================
// P. SHE PERFORMANCE METRICS / KPIs
// ============================================================================

public interface IShePerformanceSnapshotRepository : IGenericRepository<ShePerformanceSnapshot>
{
    Task<ShePerformanceSnapshot?> GetByNumberAsync(string snapshotNumber);

    /// <summary>Returns the snapshot for a specific period (and optional location), or null.</summary>
    Task<ShePerformanceSnapshot?> GetByPeriodAsync(SheSnapshotPeriodType periodType, int year, int? periodNumber, Guid? locationId = null);

    Task<IEnumerable<ShePerformanceSnapshot>> GetByYearAsync(int year);
    Task<IEnumerable<ShePerformanceSnapshot>> GetByLocationAsync(Guid locationId);

    /// <summary>Returns the tenant's most recently prepared snapshot. The tenant filter must sit
    /// inside the query: ordering across all tenants and discarding a foreign winner afterwards
    /// would wrongly report "no snapshot" for a tenant that has them.</summary>
    Task<ShePerformanceSnapshot?> GetLatestAsync(Guid tenantId);
}

// ============================================================================
// Q. SAFETY COMMITTEE & MEETINGS
// ============================================================================

public interface ISafetyCommitteeRepository : IGenericRepository<SafetyCommittee>
{
    /// <summary>Returns the committee with its members loaded.</summary>
    Task<SafetyCommittee?> GetWithMembersAsync(Guid id);
    Task<IEnumerable<SafetyCommittee>> GetActiveAsync();
    Task<IEnumerable<SafetyCommittee>> GetByChairPersonAsync(Guid chairPersonId);
}

public interface ISafetyCommitteeMemberRepository : IGenericRepository<SafetyCommitteeMember>
{
    Task<IEnumerable<SafetyCommitteeMember>> GetByCommitteeIdAsync(Guid committeeId);
    Task<IEnumerable<SafetyCommitteeMember>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<SafetyCommitteeMember>> GetActiveMembersAsync(Guid committeeId);
}

public interface ISafetyMeetingRepository : IGenericRepository<SafetyMeeting>
{
    Task<SafetyMeeting?> GetByNumberAsync(string meetingNumber);
    Task<SafetyMeeting?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<SafetyMeeting>> GetByCommitteeIdAsync(Guid committeeId);
    Task<IEnumerable<SafetyMeeting>> GetByTypeAsync(SheSafetyMeetingType type);
    Task<IEnumerable<SafetyMeeting>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);
}

public interface ISafetyMeetingActionItemRepository : IGenericRepository<SafetyMeetingActionItem>
{
    Task<IEnumerable<SafetyMeetingActionItem>> GetByMeetingIdAsync(Guid meetingId);
    Task<IEnumerable<SafetyMeetingActionItem>> GetByAssigneeAsync(Guid employeeId);
    Task<IEnumerable<SafetyMeetingActionItem>> GetByStatusAsync(SheActionItemStatus status);
    Task<IEnumerable<SafetyMeetingActionItem>> GetOpenAsync();

    /// <summary>Returns open action items whose due date has passed.</summary>
    Task<IEnumerable<SafetyMeetingActionItem>> GetOverdueAsync();
}

// ============================================================================
// R. RETURN-TO-WORK PLANS
// ============================================================================

public interface ISheReturnToWorkPlanRepository : IGenericRepository<SheReturnToWorkPlan>
{
    Task<SheReturnToWorkPlan?> GetByNumberAsync(string planNumber);
    Task<SheReturnToWorkPlan?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<SheReturnToWorkPlan>> GetAllSummaryAsync();
    Task<IEnumerable<SheReturnToWorkPlan>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<SheReturnToWorkPlan>> GetByStatusAsync(SheReturnToWorkStatus status);
    Task<IEnumerable<SheReturnToWorkPlan>> GetByIncidentAsync(Guid safetyIncidentId);
    Task<IEnumerable<SheReturnToWorkPlan>> GetActiveAsync();
}
