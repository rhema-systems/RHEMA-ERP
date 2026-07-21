using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE repositories — Contractor SHE Management (H) and Training (I).
// ============================================================================

// ============================================================================
// H. CONTRACTOR SHE MANAGEMENT
// ============================================================================

public interface ISheContractorRepository : IGenericRepository<SheContractor>
{
    Task<SheContractor?> GetByCodeAsync(string contractorCode);
    Task<SheContractor?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<SheContractor>> GetAllSummaryAsync();
    Task<IEnumerable<SheContractor>> GetByStatusAsync(SheContractorStatus status);
    Task<IEnumerable<SheContractor>> GetActiveAsync();

    /// <summary>Returns approved contractors whose pre-qualification expires within the specified number of days.</summary>
    Task<IEnumerable<SheContractor>> GetExpiringPreQualificationAsync(int daysAhead = 30);

    /// <summary>Returns contractors that have at least one open non-compliance notice.</summary>
    Task<IEnumerable<SheContractor>> GetWithOpenNonCompliancesAsync();
}

public interface ISheContractorInspectionRepository : IGenericRepository<SheContractorInspection>
{
    Task<SheContractorInspection?> GetByNumberAsync(string inspectionNumber);
    Task<IEnumerable<SheContractorInspection>> GetByContractorIdAsync(Guid contractorId);
}

public interface ISheContractorNonComplianceRepository : IGenericRepository<SheContractorNonCompliance>
{
    Task<SheContractorNonCompliance?> GetByNumberAsync(string noticeNumber);
    Task<IEnumerable<SheContractorNonCompliance>> GetByContractorIdAsync(Guid contractorId);
    Task<IEnumerable<SheContractorNonCompliance>> GetByStatusAsync(SheNonComplianceStatus status);
    Task<IEnumerable<SheContractorNonCompliance>> GetOpenAsync();

    /// <summary>Returns open notices whose rectification deadline has passed.</summary>
    Task<IEnumerable<SheContractorNonCompliance>> GetOverdueAsync();

    Task<IEnumerable<SheContractorNonCompliance>> GetRepeatViolationsAsync();
}

public interface ISheContractorDocumentRepository : IGenericRepository<SheContractorDocument>
{
    Task<IEnumerable<SheContractorDocument>> GetByContractorIdAsync(Guid contractorId);

    /// <summary>Returns documents whose expiry date falls within the specified number of days.</summary>
    Task<IEnumerable<SheContractorDocument>> GetExpiringAsync(int daysAhead = 30);

    Task<IEnumerable<SheContractorDocument>> GetUnverifiedAsync();
}

// ============================================================================
// I. SHE TRAINING & AWARENESS
// ============================================================================

public interface ISheTrainingPlanRepository : IGenericRepository<SheTrainingPlan>
{
    Task<SheTrainingPlan?> GetByNumberAsync(string planNumber);
    /// <summary>Returns the plan with its programs loaded.</summary>
    Task<SheTrainingPlan?> GetWithProgramsAsync(Guid id);
    Task<IEnumerable<SheTrainingPlan>> GetByYearAsync(int year);
    Task<IEnumerable<SheTrainingPlan>> GetByStatusAsync(SheTrainingPlanStatus status);
}

public interface ISheTrainingProgramRepository : IGenericRepository<SheTrainingProgram>
{
    Task<SheTrainingProgram?> GetByCodeAsync(string programCode);
    /// <summary>Returns the program with its attendance register loaded.</summary>
    Task<SheTrainingProgram?> GetWithAttendancesAsync(Guid id);
    Task<IEnumerable<SheTrainingProgram>> GetByPlanIdAsync(Guid planId);
    Task<IEnumerable<SheTrainingProgram>> GetByStatusAsync(SheTrainingStatus status);
    Task<IEnumerable<SheTrainingProgram>> GetByCategoryAsync(SheTrainingCategory category);
    Task<IEnumerable<SheTrainingProgram>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);

    /// <summary>Returns planned/scheduled programs whose scheduled date falls within the specified number of days.</summary>
    Task<IEnumerable<SheTrainingProgram>> GetUpcomingAsync(int daysAhead = 30);
}

public interface ISheTrainingAttendanceRepository : IGenericRepository<SheTrainingAttendance>
{
    Task<IEnumerable<SheTrainingAttendance>> GetByProgramIdAsync(Guid programId);
    Task<IEnumerable<SheTrainingAttendance>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Returns attendance records whose certificate expires within the specified number of days.</summary>
    Task<IEnumerable<SheTrainingAttendance>> GetExpiringCertificatesAsync(int daysAhead = 30);
}
