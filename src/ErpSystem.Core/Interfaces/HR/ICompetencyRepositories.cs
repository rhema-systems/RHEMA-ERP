using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// COMPETENCY
// ============================================================================

#region Competency

/// <summary>
/// Repository for the <see cref="Competency"/> master list.
/// </summary>
public interface ICompetencyRepository : IGenericRepository<Competency>
{
    /// <summary>Returns a competency by its business code (case-insensitive, tenant-scoped).</summary>
    Task<Competency?> GetByCodeAsync(string code);

    /// <summary>Returns all competencies filtered by category, ordered by name.</summary>
    Task<IEnumerable<Competency>> GetByCategoryAsync(CompetencyCategory category);

    /// <summary>Returns only active competencies, ordered by category then name.</summary>
    Task<IEnumerable<Competency>> GetActiveAsync();

    /// <summary>
    /// Returns a fully-loaded competency including its skill indicators (with skill nav),
    /// and summary counts for positions and employees.
    /// </summary>
    Task<Competency?> GetWithFullDetailsAsync(Guid id);

    /// <summary>
    /// Returns competencies that are required by the given position's
    /// <see cref="PositionCompetency"/> records.
    /// </summary>
    Task<IEnumerable<Competency>> GetByPositionAsync(Guid positionId);

    /// <summary>
    /// Returns competencies that have been assessed for the given employee
    /// via <see cref="EmployeeCompetency"/> records.
    /// </summary>
    Task<IEnumerable<Competency>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Checks whether a code already exists (optionally excluding one record for update validation).</summary>
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null);
}

#endregion

// ============================================================================
// COMPETENCY SKILL INDICATOR
// ============================================================================

#region CompetencySkillIndicator

/// <summary>
/// Repository for <see cref="CompetencySkillIndicator"/> — the mapping from a
/// skill (at a minimum level) to the competency it evidences.
/// </summary>
public interface ICompetencySkillIndicatorRepository : IGenericRepository<CompetencySkillIndicator>
{
    /// <summary>
    /// Returns all skill indicators for a competency, with <c>Skill</c> navigation loaded.
    /// </summary>
    Task<IEnumerable<CompetencySkillIndicator>> GetByCompetencyIdAsync(Guid competencyId);

    /// <summary>
    /// Returns all competencies that a given skill contributes evidence to,
    /// with <c>Competency</c> navigation loaded. Useful for impact analysis when a
    /// skill is modified.
    /// </summary>
    Task<IEnumerable<CompetencySkillIndicator>> GetBySkillIdAsync(Guid skillId);

    /// <summary>
    /// Returns the specific indicator record linking a competency to a skill,
    /// or null if no such mapping exists.
    /// </summary>
    Task<CompetencySkillIndicator?> GetByCompetencyAndSkillAsync(Guid competencyId, Guid skillId);

    /// <summary>
    /// Returns indicators where the minimum skill level is at or above
    /// <paramref name="minimumLevel"/>. Useful for gap analysis filtering.
    /// </summary>
    Task<IEnumerable<CompetencySkillIndicator>> GetByMinimumSkillLevelAsync(SkillLevel minimumLevel);
}

#endregion

// ============================================================================
// POSITION COMPETENCY
// ============================================================================

#region PositionCompetency

/// <summary>
/// Repository for <see cref="PositionCompetency"/> — the required proficiency level
/// for a competency on a position definition.
/// </summary>
public interface IPositionCompetencyRepository : IGenericRepository<PositionCompetency>
{
    /// <summary>
    /// Returns all competency requirements for a position, with <c>Competency</c> navigation loaded,
    /// ordered by competency category then name.
    /// </summary>
    Task<IEnumerable<PositionCompetency>> GetByPositionIdAsync(Guid positionId);

    /// <summary>
    /// Returns all positions that require the given competency, with <c>Position</c> navigation loaded.
    /// Useful for impact analysis when a competency definition changes.
    /// </summary>
    Task<IEnumerable<PositionCompetency>> GetByCompetencyIdAsync(Guid competencyId);

    /// <summary>
    /// Returns the specific requirement record linking a position to a competency,
    /// or null if none exists.
    /// </summary>
    Task<PositionCompetency?> GetByPositionAndCompetencyAsync(Guid positionId, Guid competencyId);

    /// <summary>
    /// Returns position competency requirements filtered by required proficiency level
    /// at or above <paramref name="minimumLevel"/>.
    /// </summary>
    Task<IEnumerable<PositionCompetency>> GetByMinimumRequiredLevelAsync(int minimumLevel);

    /// <summary>
    /// Removes all existing competency requirements for a position and replaces
    /// them with the provided set in a single operation (bulk-set support).
    /// </summary>
    Task BulkReplaceForPositionAsync(Guid positionId, IEnumerable<PositionCompetency> newRequirements);
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY
// ============================================================================

#region EmployeeCompetency

/// <summary>
/// Repository for <see cref="EmployeeCompetency"/> — the current assessed competency
/// level for an employee. Each employee–competency pair has exactly one record here;
/// history is tracked in <see cref="EmployeeCompetencyHistory"/>.
/// </summary>
public interface IEmployeeCompetencyRepository : IGenericRepository<EmployeeCompetency>
{
    /// <summary>
    /// Returns all current competency assessments for an employee,
    /// with <c>Competency</c> navigation loaded, ordered by category then name.
    /// </summary>
    Task<IEnumerable<EmployeeCompetency>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>
    /// Returns all current assessments for a given competency across all employees,
    /// with <c>Employee</c> navigation loaded. Useful for workforce analytics.
    /// </summary>
    Task<IEnumerable<EmployeeCompetency>> GetByCompetencyIdAsync(Guid competencyId);

    /// <summary>
    /// Returns the specific assessment record for an employee–competency pair,
    /// or null if the competency has not yet been assessed for this employee.
    /// </summary>
    Task<EmployeeCompetency?> GetByEmployeeAndCompetencyAsync(Guid employeeId, Guid competencyId);

    /// <summary>
    /// Returns a fully-loaded assessment record including <c>AssessedBy</c>, <c>Competency</c>,
    /// <c>Employee</c>, and the full <c>History</c> collection ordered newest-first.
    /// </summary>
    Task<EmployeeCompetency?> GetWithHistoryAsync(Guid id);

    /// <summary>
    /// Returns all current assessments for a given employee where the assessed
    /// proficiency level is at or above <paramref name="minimumLevel"/>.
    /// </summary>
    Task<IEnumerable<EmployeeCompetency>> GetByEmployeeAndMinimumLevelAsync(Guid employeeId, int minimumLevel);

    /// <summary>
    /// Returns all current assessment records for all employees in a position,
    /// filtered to competencies required by that position. Used to compute
    /// workforce readiness against a position's competency profile.
    /// </summary>
    Task<IEnumerable<EmployeeCompetency>> GetByPositionCompetenciesAsync(Guid positionId);

    /// <summary>
    /// Returns employees who have been assessed above the required level
    /// for all competencies of a given position — i.e., potential candidates.
    /// </summary>
    Task<IEnumerable<EmployeeCompetency>> GetQualifiedEmployeesForPositionAsync(Guid positionId);

    /// <summary>
    /// Returns assessments that fall below the required proficiency level for
    /// the employee's own position competency requirements — i.e., active gaps.
    /// </summary>
    Task<IEnumerable<EmployeeCompetency>> GetGapsForEmployeeAsync(Guid employeeId);

    /// <summary>
    /// Returns assessments whose <c>AssessmentDate</c> predates
    /// <paramref name="olderThan"/>, flagging records due for re-assessment.
    /// </summary>
    Task<IEnumerable<EmployeeCompetency>> GetAssessmentsOlderThanAsync(DateTime olderThan);
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY HISTORY
// ============================================================================

#region EmployeeCompetencyHistory

/// <summary>
/// Repository for <see cref="EmployeeCompetencyHistory"/> — the immutable audit trail
/// of competency assessments per employee–competency pair.
/// Records are written by the service layer and are never modified via the API.
/// </summary>
public interface IEmployeeCompetencyHistoryRepository : IGenericRepository<EmployeeCompetencyHistory>
{
    /// <summary>
    /// Returns all history entries for a specific <see cref="EmployeeCompetency"/> record,
    /// ordered from newest to oldest assessment date.
    /// </summary>
    Task<IEnumerable<EmployeeCompetencyHistory>> GetByEmployeeCompetencyIdAsync(Guid employeeCompetencyId);

    /// <summary>
    /// Returns all history records for an employee across all competencies,
    /// ordered from newest to oldest. Useful for auditing an employee's
    /// full assessment timeline.
    /// </summary>
    Task<IEnumerable<EmployeeCompetencyHistory>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>
    /// Returns all history records for a specific competency across all employees,
    /// ordered from newest to oldest. Useful for understanding how a competency
    /// has been assessed organisation-wide over time.
    /// </summary>
    Task<IEnumerable<EmployeeCompetencyHistory>> GetByCompetencyIdAsync(Guid competencyId);

    /// <summary>
    /// Returns the most recent history snapshot for a given employee–competency pair.
    /// Returns null if no history exists yet (i.e. the record has never been updated).
    /// </summary>
    Task<EmployeeCompetencyHistory?> GetLatestAsync(Guid employeeCompetencyId);
}

#endregion
