using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// COMPETENCY SERVICE
// ============================================================================

#region Competency Service

public interface ICompetencyService
{
    // Queries
    Task<CompetencyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CompetencyDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompetencyDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<CompetencyDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<CompetencyDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompetencyDto>> GetByCategoryAsync(CompetencyCategory category, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompetencyDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<CompetencyLookupDto>> GetLookupListAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<CompetencyDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompetencyDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    // CRUD
    Task<CompetencyDto> CreateAsync(CreateCompetencyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<CompetencyDto> UpdateAsync(UpdateCompetencyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// COMPETENCY SKILL INDICATOR SERVICE
// ============================================================================

#region Competency Skill Indicator Service

public interface ICompetencySkillIndicatorService
{
    // Queries
    Task<CompetencySkillIndicatorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompetencySkillIndicatorDto>> GetByCompetencyIdAsync(Guid competencyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompetencySkillIndicatorDto>> GetBySkillIdAsync(Guid skillId, CancellationToken cancellationToken = default);
    Task<CompetencySkillIndicatorDto?> GetByCompetencyAndSkillAsync(Guid competencyId, Guid skillId, CancellationToken cancellationToken = default);

    // CRUD
    Task<CompetencySkillIndicatorDto> CreateAsync(CreateCompetencySkillIndicatorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<CompetencySkillIndicatorDto> UpdateAsync(UpdateCompetencySkillIndicatorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// POSITION COMPETENCY SERVICE
// ============================================================================

#region Position Competency Service

public interface IPositionCompetencyService
{
    // Queries
    Task<PositionCompetencyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PositionCompetencyDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PositionCompetencyDto>> GetByCompetencyIdAsync(Guid competencyId, CancellationToken cancellationToken = default);
    Task<PositionCompetencyDto?> GetByPositionAndCompetencyAsync(Guid positionId, Guid competencyId, CancellationToken cancellationToken = default);

    // CRUD
    Task<PositionCompetencyDto> CreateAsync(CreatePositionCompetencyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<PositionCompetencyDto> UpdateAsync(UpdatePositionCompetencyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces the entire competency requirement set for a position.
    /// Any existing entries not present in <paramref name="bulkSetDto"/> are removed (soft-deleted).
    /// </summary>
    Task<IEnumerable<PositionCompetencyDto>> BulkSetForPositionAsync(BulkSetPositionCompetenciesDto bulkSetDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY SERVICE
// ============================================================================

#region Employee Competency Service

public interface IEmployeeCompetencyService
{
    // Queries
    Task<EmployeeCompetencyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeCompetencyDetailDto> GetWithHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeCompetencyDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeCompetencyDto>> GetByCompetencyIdAsync(Guid competencyId, CancellationToken cancellationToken = default);
    Task<EmployeeCompetencyDto?> GetByEmployeeAndCompetencyAsync(Guid employeeId, Guid competencyId, CancellationToken cancellationToken = default);

    // CRUD
    Task<EmployeeCompetencyDto> CreateAsync(CreateEmployeeCompetencyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-assesses an employee's competency level. Automatically snapshots the current
    /// values to <c>EmployeeCompetencyHistory</c> before applying the update.
    /// </summary>
    Task<EmployeeCompetencyDto> UpdateAsync(UpdateEmployeeCompetencyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Analytics
    /// <summary>
    /// Returns a full competency gap summary for an employee measured against
    /// the requirements of their current assigned position.
    /// </summary>
    /// <summary>
    /// Where the organisation is short against the competencies its positions require — the
    /// training-needs view, aggregated across everyone in a position that requires each competency.
    /// </summary>
    Task<IEnumerable<OrganisationCompetencyGapDto>> GetOrganisationGapsAsync(
        CancellationToken cancellationToken = default);

    Task<EmployeePositionCompetencyGapSummaryDto> GetGapsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a complete competency profile for an employee, including all
    /// current assessments and summary statistics.
    /// </summary>
    Task<EmployeeCompetencyProfileDto> GetEmployeeProfileAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all assessments whose assessment date is older than
    /// <paramref name="monthsOld"/> months, flagging records due for re-assessment.
    /// Defaults to 12 months.
    /// </summary>
    Task<IEnumerable<EmployeeCompetencyDto>> GetStaleAssessmentsAsync(int monthsOld = 12, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current assessments for employees who meet or exceed all
    /// competency requirements for the given position.
    /// </summary>
    Task<IEnumerable<EmployeeCompetencyDto>> GetQualifiedEmployeesForPositionAsync(Guid positionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch re-assessment: applies multiple competency level changes in a single call.
    /// For existing records (<c>EmployeeCompetencyId</c> set) the current state is
    /// snapshotted to history before the update is applied.
    /// For first-time assessments (<c>CompetencyId</c> set, <c>EmployeeCompetencyId</c> null)
    /// a new record is created.
    /// </summary>
    Task<BatchAssessmentResultDto> BatchAssessAsync(
        EmployeeCompetencyAssessmentUpdateDto dto,
        Guid tenantId,
        Guid assessorUserId,
        CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY HISTORY SERVICE
// ============================================================================

#region Employee Competency History Service

/// <summary>
/// Read-only service for the immutable competency assessment audit trail.
/// History records are created internally by <see cref="IEmployeeCompetencyService.UpdateAsync"/>
/// and are never written or modified via the API.
/// </summary>
public interface IEmployeeCompetencyHistoryService
{
    Task<EmployeeCompetencyHistoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeCompetencyHistoryDto>> GetByEmployeeCompetencyIdAsync(Guid employeeCompetencyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeCompetencyHistoryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeCompetencyHistoryDto>> GetByCompetencyIdAsync(Guid competencyId, CancellationToken cancellationToken = default);
    Task<EmployeeCompetencyHistoryDto?> GetLatestAsync(Guid employeeCompetencyId, CancellationToken cancellationToken = default);
}

#endregion
