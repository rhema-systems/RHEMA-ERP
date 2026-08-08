using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Application.HR.Extensions;

/// <summary>
/// Explicit, hand-written mapping extensions for the Competency module:
/// <see cref="Competency"/>, <see cref="CompetencySkillIndicator"/>,
/// <see cref="PositionCompetency"/>, <see cref="EmployeeCompetency"/>,
/// and <see cref="EmployeeCompetencyHistory"/>.
///
/// Design goals:
/// - No AutoMapper / no reflection
/// - Defensive null handling on all navigation properties
/// - History records are read-only from the API perspective; no ToEntity/UpdateEntity here
/// </summary>
public static class CompetencyMappingExtensions
{
    // ========================================================================
    // COMPETENCY
    // ========================================================================

    #region Competency

    /// <summary>Maps a <see cref="Competency"/> to its full read DTO.</summary>
    public static CompetencyDto ToDto(this Competency entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new CompetencyDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            CompetencyCategory = entity.CompetencyCategory,
            ProficiencyScaleMax = entity.ProficiencyScaleMax,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    /// <summary>
    /// Maps a <see cref="Competency"/> to a detailed read DTO that includes
    /// associated skill indicators and summary counts.
    /// Requires <c>SkillIndicators</c>, <c>PositionCompetencies</c>, and
    /// <c>EmployeeCompetencies</c> to be loaded via the repository.
    /// </summary>
    public static CompetencyDetailDto ToDetailDto(this Competency entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new CompetencyDetailDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            CompetencyCategory = entity.CompetencyCategory,
            ProficiencyScaleMax = entity.ProficiencyScaleMax,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            SkillIndicators = entity.SkillIndicators.Select(i => i.ToDto()).ToList(),
            PositionCount = entity.PositionCompetencies?.Count ?? 0,
            EmployeeCount = entity.EmployeeCompetencies?.Count ?? 0,
        };
    }

    /// <summary>Maps a <see cref="Competency"/> to a lightweight lookup DTO for dropdowns.</summary>
    public static CompetencyLookupDto ToLookupDto(this Competency entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new CompetencyLookupDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            CompetencyCategory = entity.CompetencyCategory,
            ProficiencyScaleMax = entity.ProficiencyScaleMax,
        };
    }

    /// <summary>Maps a <see cref="CreateCompetencyDto"/> to a new <see cref="Competency"/> entity.</summary>
    public static Competency ToEntity(this CreateCompetencyDto dto, Guid tenantId, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new Competency
        {
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            CompetencyCategory = dto.CompetencyCategory,
            ProficiencyScaleMax = dto.ProficiencyScaleMax,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    /// <summary>
    /// Applies a <see cref="UpdateCompetencyDto"/> onto an existing <see cref="Competency"/> entity.
    /// Identity, tenant, and audit creation fields are preserved.
    /// </summary>
    public static void UpdateEntity(this Competency entity, UpdateCompetencyDto dto, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.CompetencyCategory = dto.CompetencyCategory;
        entity.ProficiencyScaleMax = dto.ProficiencyScaleMax;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    /// <summary>Projects a sequence of competencies to summary DTOs.</summary>
    public static IEnumerable<CompetencyDto> ToDtoList(this IEnumerable<Competency> entities)
        => entities.Select(e => e.ToDto());

    /// <summary>Projects a sequence of competencies to lookup DTOs.</summary>
    public static IEnumerable<CompetencyLookupDto> ToLookupDtoList(this IEnumerable<Competency> entities)
        => entities.Select(e => e.ToLookupDto());

    #endregion

    // ========================================================================
    // COMPETENCY SKILL INDICATOR
    // ========================================================================

    #region CompetencySkillIndicator

    /// <summary>Maps a <see cref="CompetencySkillIndicator"/> to its read DTO.</summary>
    public static CompetencySkillIndicatorDto ToDto(this CompetencySkillIndicator entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new CompetencySkillIndicatorDto
        {
            Id = entity.Id,
            CompetencyId = entity.CompetencyId,
            CompetencyName = entity.Competency?.Name ?? string.Empty,
            SkillId = entity.SkillId,
            SkillName = entity.Skill?.Name ?? string.Empty,
            SkillCategory = entity.Skill?.Category,
            MinimumSkillLevelRequired = entity.MinimumSkillLevelRequired,
            Rationale = entity.Rationale,
        };
    }

    /// <summary>Maps a <see cref="CreateCompetencySkillIndicatorDto"/> to a new entity.</summary>
    public static CompetencySkillIndicator ToEntity(this CreateCompetencySkillIndicatorDto dto, Guid tenantId, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new CompetencySkillIndicator
        {
            TenantId = tenantId,
            CompetencyId = dto.CompetencyId,
            SkillId = dto.SkillId,
            MinimumSkillLevelRequired = dto.MinimumSkillLevelRequired,
            Rationale = dto.Rationale,
            CreatedBy = userId.ToString(),
        };
    }

    /// <summary>
    /// Applies an <see cref="UpdateCompetencySkillIndicatorDto"/> to an existing indicator.
    /// The CompetencyId/SkillId pairing is immutable and must not be changed here.
    /// </summary>
    public static void UpdateEntity(this CompetencySkillIndicator entity, UpdateCompetencySkillIndicatorDto dto, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        entity.MinimumSkillLevelRequired = dto.MinimumSkillLevelRequired;
        entity.Rationale = dto.Rationale;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // POSITION COMPETENCY
    // ========================================================================

    #region PositionCompetency

    /// <summary>Maps a <see cref="PositionCompetency"/> to its read DTO.</summary>
    public static PositionCompetencyDto ToDto(this PositionCompetency entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new PositionCompetencyDto
        {
            Id = entity.Id,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            CompetencyId = entity.CompetencyId,
            CompetencyCode = entity.Competency?.Code ?? string.Empty,
            CompetencyName = entity.Competency?.Name ?? string.Empty,
            CompetencyCategory = entity.Competency?.CompetencyCategory ?? default,
            ProficiencyScaleMax = entity.Competency?.ProficiencyScaleMax ?? 5,
            RequiredProficiencyLevel = entity.RequiredProficiencyLevel,
            Notes = entity.Notes,
        };
    }

    /// <summary>Maps a <see cref="CreatePositionCompetencyDto"/> to a new entity.</summary>
    public static PositionCompetency ToEntity(this CreatePositionCompetencyDto dto, Guid tenantId, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PositionCompetency
        {
            TenantId = tenantId,
            PositionId = dto.PositionId,
            CompetencyId = dto.CompetencyId,
            RequiredProficiencyLevel = dto.RequiredProficiencyLevel,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    /// <summary>
    /// Applies an <see cref="UpdatePositionCompetencyDto"/> to an existing position competency.
    /// PositionId and CompetencyId are immutable.
    /// </summary>
    public static void UpdateEntity(this PositionCompetency entity, UpdatePositionCompetencyDto dto, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        entity.RequiredProficiencyLevel = dto.RequiredProficiencyLevel;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    /// <summary>
    /// Converts a <see cref="PositionCompetencyInputDto"/> (from a bulk-set operation)
    /// to a new <see cref="PositionCompetency"/> entity for the given position.
    /// </summary>
    public static PositionCompetency ToEntity(this PositionCompetencyInputDto dto, Guid positionId, Guid tenantId, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PositionCompetency
        {
            TenantId = tenantId,
            PositionId = positionId,
            CompetencyId = dto.CompetencyId,
            RequiredProficiencyLevel = dto.RequiredProficiencyLevel,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    /// <summary>Projects a sequence of position competencies to read DTOs.</summary>
    public static IEnumerable<PositionCompetencyDto> ToDtoList(this IEnumerable<PositionCompetency> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // EMPLOYEE COMPETENCY
    // ========================================================================

    #region EmployeeCompetency

    /// <summary>Maps an <see cref="EmployeeCompetency"/> to its current-assessment read DTO.</summary>
    public static EmployeeCompetencyDto ToDto(this EmployeeCompetency entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new EmployeeCompetencyDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            CompetencyId = entity.CompetencyId,
            CompetencyCode = entity.Competency?.Code ?? string.Empty,
            CompetencyName = entity.Competency?.Name ?? string.Empty,
            CompetencyCategory = entity.Competency?.CompetencyCategory ?? default,
            ProficiencyScaleMax = entity.Competency?.ProficiencyScaleMax ?? 5,
            CurrentProficiencyLevel = entity.CurrentProficiencyLevel,
            AssessmentDate = entity.AssessmentDate,
            AssessedById = entity.AssessedById,
            AssessedByName = entity.AssessedBy?.FullName,
            AssessmentMethod = entity.AssessmentMethod,
            EvidenceNotes = entity.EvidenceNotes,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    /// <summary>
    /// Maps an <see cref="EmployeeCompetency"/> to a detailed read DTO that includes
    /// the full assessment history. Requires <c>History</c> to be loaded.
    /// </summary>
    public static EmployeeCompetencyDetailDto ToDetailDto(this EmployeeCompetency entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new EmployeeCompetencyDetailDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            CompetencyId = entity.CompetencyId,
            CompetencyCode = entity.Competency?.Code ?? string.Empty,
            CompetencyName = entity.Competency?.Name ?? string.Empty,
            CompetencyCategory = entity.Competency?.CompetencyCategory ?? default,
            ProficiencyScaleMax = entity.Competency?.ProficiencyScaleMax ?? 5,
            CurrentProficiencyLevel = entity.CurrentProficiencyLevel,
            AssessmentDate = entity.AssessmentDate,
            AssessedById = entity.AssessedById,
            AssessedByName = entity.AssessedBy?.FullName,
            AssessmentMethod = entity.AssessmentMethod,
            EvidenceNotes = entity.EvidenceNotes,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            History = entity.History.Select(h => h.ToDto()).OrderByDescending(h => h.AssessmentDate).ToList(),
        };
    }

    /// <summary>Maps a <see cref="CreateEmployeeCompetencyDto"/> to a new entity.</summary>
    public static EmployeeCompetency ToEntity(this CreateEmployeeCompetencyDto dto, Guid tenantId, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new EmployeeCompetency
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            CompetencyId = dto.CompetencyId,
            CurrentProficiencyLevel = dto.CurrentProficiencyLevel,
            AssessmentDate = dto.AssessmentDate,
            AssessedById = dto.AssessedById,
            AssessmentMethod = dto.AssessmentMethod,
            EvidenceNotes = dto.EvidenceNotes,
            CreatedBy = userId.ToString(),
        };
    }

    /// <summary>
    /// Applies an <see cref="UpdateEmployeeCompetencyDto"/> to an existing record.
    /// The caller is responsible for snapshotting the current values to
    /// <see cref="EmployeeCompetencyHistory"/> BEFORE calling this method.
    /// </summary>
    public static void UpdateEntity(this EmployeeCompetency entity, UpdateEmployeeCompetencyDto dto, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        entity.CurrentProficiencyLevel = dto.CurrentProficiencyLevel;
        entity.AssessmentDate = dto.AssessmentDate;
        entity.AssessedById = dto.AssessedById;
        entity.AssessmentMethod = dto.AssessmentMethod;
        entity.EvidenceNotes = dto.EvidenceNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    /// <summary>Projects a sequence of employee competencies to read DTOs.</summary>
    public static IEnumerable<EmployeeCompetencyDto> ToDtoList(this IEnumerable<EmployeeCompetency> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // EMPLOYEE COMPETENCY HISTORY
    // ========================================================================

    #region EmployeeCompetencyHistory

    /// <summary>
    /// Maps an <see cref="EmployeeCompetencyHistory"/> record to its immutable read DTO.
    /// History records are never created or modified via API callers.
    /// </summary>
    public static EmployeeCompetencyHistoryDto ToDto(this EmployeeCompetencyHistory entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new EmployeeCompetencyHistoryDto
        {
            Id = entity.Id,
            EmployeeCompetencyId = entity.EmployeeCompetencyId,
            ProficiencyLevel = entity.ProficiencyLevel,
            AssessmentDate = entity.AssessmentDate,
            AssessedById = entity.AssessedById,
            AssessedByName = entity.AssessedBy?.FullName,
            AssessmentMethod = entity.AssessmentMethod,
            EvidenceNotes = entity.EvidenceNotes,
            ChangeReason = entity.ChangeReason,
            RecordedAt = entity.RecordedAt,
            RecordedById = entity.RecordedById,
            RecordedByName = entity.RecordedBy?.FullName,
        };
    }

    /// <summary>
    /// Builds a new <see cref="EmployeeCompetencyHistory"/> snapshot from the
    /// current state of an <see cref="EmployeeCompetency"/> before it is updated.
    /// Called by the service layer immediately before applying an update.
    /// </summary>
    public static EmployeeCompetencyHistory ToHistorySnapshot(
        this EmployeeCompetency entity,
        string changeReason,
        Guid tenantId,
        Guid recordedById)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new EmployeeCompetencyHistory
        {
            TenantId = tenantId,
            EmployeeCompetencyId = entity.Id,
            ProficiencyLevel = entity.CurrentProficiencyLevel,
            AssessmentDate = entity.AssessmentDate,
            AssessedById = entity.AssessedById,
            AssessmentMethod = entity.AssessmentMethod,
            EvidenceNotes = entity.EvidenceNotes,
            ChangeReason = changeReason,
            RecordedAt = DateTime.UtcNow,
            RecordedById = recordedById,
            CreatedBy = recordedById.ToString(),
        };
    }

    #endregion
}
