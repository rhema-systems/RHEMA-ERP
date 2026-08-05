using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE services — Contractor SHE Management (H) and Training (I).
// ============================================================================

// ============================================================================
// H. CONTRACTOR SERVICE
// ============================================================================

public interface ISheContractorService
{
    Task<SheContractorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheContractorDto?> GetByCodeAsync(string contractorCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheContractorSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SheContractorSummaryDto>> GetByStatusAsync(SheContractorStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheContractorSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SheContractorSummaryDto>> GetExpiringPreQualificationAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheContractorSummaryDto>> GetWithOpenNonCompliancesAsync(CancellationToken cancellationToken = default);

    Task<SheContractorDto> CreateAsync(CreateSheContractorDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheContractorDto> UpdateAsync(UpdateSheContractorDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> PreQualifyAsync(PreQualifySheContractorDto dto, Guid userId, CancellationToken cancellationToken = default);

    // Inductions
    Task<SheContractorInductionDto> AddInductionAsync(CreateSheContractorInductionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheContractorInductionDto> UpdateInductionAsync(UpdateSheContractorInductionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteInductionAsync(Guid inductionId, CancellationToken cancellationToken = default);

    // SHE inspections
    Task<IEnumerable<SheContractorInspectionDto>> GetInspectionsAsync(Guid contractorId, CancellationToken cancellationToken = default);
    Task<SheContractorInspectionDto> AddInspectionAsync(CreateSheContractorInspectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheContractorInspectionDto> UpdateInspectionAsync(UpdateSheContractorInspectionDto dto, Guid userId, CancellationToken cancellationToken = default);

    // Non-compliances
    Task<IEnumerable<SheContractorNonComplianceDto>> GetNonCompliancesAsync(Guid contractorId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheContractorNonComplianceDto>> GetOpenNonCompliancesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SheContractorNonComplianceDto>> GetOverdueNonCompliancesAsync(CancellationToken cancellationToken = default);
    Task<SheContractorNonComplianceDto> AddNonComplianceAsync(CreateSheContractorNonComplianceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheContractorNonComplianceDto> UpdateNonComplianceAsync(UpdateSheContractorNonComplianceDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CloseNonComplianceAsync(CloseSheContractorNonComplianceDto dto, Guid userId, CancellationToken cancellationToken = default);

    // Documents
    Task<IEnumerable<SheContractorDocumentDto>> GetDocumentsAsync(Guid contractorId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheContractorDocumentDto>> GetExpiringDocumentsAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheContractorDocumentDto>> GetUnverifiedDocumentsAsync(CancellationToken cancellationToken = default);
    Task<SheContractorDocumentDto> AddDocumentAsync(CreateSheContractorDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> VerifyDocumentAsync(VerifySheContractorDocumentDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}

// ============================================================================
// I. SHE TRAINING SERVICE  (plans, programs, attendance)
// ============================================================================

public interface ISheTrainingService
{
    // Plans
    Task<SheTrainingPlanDto> GetPlanAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheTrainingPlanDto>> GetPlansByYearAsync(int year, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheTrainingPlanDto>> GetPlansByStatusAsync(SheTrainingPlanStatus status, CancellationToken cancellationToken = default);
    Task<SheTrainingPlanDto> CreatePlanAsync(CreateSheTrainingPlanDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheTrainingPlanDto> UpdatePlanAsync(UpdateSheTrainingPlanDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default);

    // Programs
    Task<SheTrainingProgramDto> GetProgramAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheTrainingProgramSummaryDto>> GetProgramsByPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheTrainingProgramSummaryDto>> GetProgramsByStatusAsync(SheTrainingStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheTrainingProgramSummaryDto>> GetProgramsByCategoryAsync(SheTrainingCategory category, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheTrainingProgramSummaryDto>> GetUpcomingProgramsAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<SheTrainingProgramDto> CreateProgramAsync(CreateSheTrainingProgramDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheTrainingProgramDto> UpdateProgramAsync(UpdateSheTrainingProgramDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteProgramAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> EvaluateProgramAsync(EvaluateSheTrainingProgramDto dto, Guid userId, CancellationToken cancellationToken = default);

    // Attendance
    Task<IEnumerable<SheTrainingAttendanceDto>> GetAttendancesByProgramAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheTrainingAttendanceDto>> GetAttendancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheTrainingAttendanceDto>> GetExpiringCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<SheTrainingAttendanceDto> AddAttendanceAsync(CreateSheTrainingAttendanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheTrainingAttendanceDto> UpdateAttendanceAsync(UpdateSheTrainingAttendanceDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttendanceAsync(Guid attendanceId, CancellationToken cancellationToken = default);
}
