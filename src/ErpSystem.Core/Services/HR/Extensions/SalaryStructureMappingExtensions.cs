using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

/// <summary>
/// Explicit, hand-written mapping extensions for the Salary Structure module.
/// 
/// Design goals:
/// - Predictable and side-effect free mappings
/// - No AutoMapper / no reflection
/// - Defensive null handling (throws <see cref="ArgumentNullException"/>)
/// - Detail/read DTOs include ordered nested collections
/// - Create/Update DTOs map to flat entity shapes (no collection overwrites)
/// </summary>
public static class SalaryStructureMappingExtensions
{
    #region SalaryGrade

    /// <summary>
    /// Maps a <see cref="SalaryGrade"/> entity to a lightweight <see cref="SalaryGradeDto"/>.
    /// Intended for lists and references.
    /// </summary>
    /// <param name="entity">The source entity.</param>
    /// <returns>A mapped <see cref="SalaryGradeDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static SalaryGradeDto ToDto(this SalaryGrade entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new SalaryGradeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            MinSalary = entity.MinSalary,
            MaxSalary = entity.MaxSalary,
            IsActive = entity.IsActive,
            EffectiveDate = entity.EffectiveDate,
            EndDate = entity.EndDate,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedById = entity.CreatedById,
            LastModifiedById = entity.LastModifiedById,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy
        };
    }

    /// <summary>
    /// Maps a <see cref="SalaryGrade"/> entity to a <see cref="SalaryGradeDetailDto"/>.
    /// Includes the nested <see cref="SalaryLevelDto"/> collection ordered by <c>Sequence</c>.
    /// </summary>
    /// <param name="entity">The source entity.</param>
    /// <returns>A mapped <see cref="SalaryGradeDetailDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static SalaryGradeDetailDto ToDetailDto(this SalaryGrade entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new SalaryGradeDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            MinSalary = entity.MinSalary,
            MaxSalary = entity.MaxSalary,
            IsActive = entity.IsActive,
            EffectiveDate = entity.EffectiveDate,
            EndDate = entity.EndDate,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedById = entity.CreatedById,
            LastModifiedById = entity.LastModifiedById,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            Levels = (entity.Levels ?? new List<SalaryLevel>())
                .OrderBy(l => l.Sequence)
                .Select(l => l.ToDto())
                .ToList()
        };
    }

    /// <summary>
    /// Maps a <see cref="CreateSalaryGradeDto"/> to a new <see cref="SalaryGrade"/> entity.
    /// The caller injects the tenant identifier; audit fields are intentionally not set here.
    /// </summary>
    /// <param name="dto">The create DTO.</param>
    /// <param name="tenantId">Tenant identifier to apply to the entity (authoritative).</param>
    /// <returns>A new <see cref="SalaryGrade"/> entity.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is null.</exception>
    public static SalaryGrade ToEntity(this CreateSalaryGradeDto dto, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new SalaryGrade
        {
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            MinSalary = dto.MinSalary,
            MaxSalary = dto.MaxSalary,
            IsActive = dto.IsActive,
            EffectiveDate = dto.EffectiveDate,
            EndDate = dto.EndDate
        };
    }

    /// <summary>
    /// Updates an existing <see cref="SalaryGrade"/> entity in-place from an <see cref="UpdateSalaryGradeDto"/>.
    /// Does not overwrite <c>TenantId</c> or navigation collections.
    /// </summary>
    /// <param name="dto">The update DTO.</param>
    /// <param name="entity">The existing entity to update.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> or <paramref name="entity"/> is null.</exception>
    public static void UpdateEntity(this UpdateSalaryGradeDto dto, SalaryGrade entity)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(entity);

        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.MinSalary = dto.MinSalary;
        entity.MaxSalary = dto.MaxSalary;
        entity.IsActive = dto.IsActive;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.EndDate = dto.EndDate;
    }

    #endregion

    #region SalaryLevel

    /// <summary>
    /// Maps a <see cref="SalaryLevel"/> entity to a lightweight <see cref="SalaryLevelDto"/>.
    /// Intended for lists and references.
    /// </summary>
    /// <param name="entity">The source entity.</param>
    /// <returns>A mapped <see cref="SalaryLevelDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static SalaryLevelDto ToDto(this SalaryLevel entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new SalaryLevelDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SalaryGradeId = entity.SalaryGradeId,
            Code = entity.Code,
            Name = entity.Name,
            MinSalary = entity.MinSalary,
            MidSalary = entity.MidSalary,
            MaxSalary = entity.MaxSalary,
            Sequence = entity.Sequence,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedById = entity.CreatedById,
            LastModifiedById = entity.LastModifiedById,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy
        };
    }

    /// <summary>
    /// Maps a <see cref="SalaryLevel"/> entity to a <see cref="SalaryLevelDetailDto"/>.
    /// Includes the nested <see cref="SalaryNotchDto"/> collection ordered by <c>NotchNumber</c>.
    /// </summary>
    /// <param name="entity">The source entity.</param>
    /// <returns>A mapped <see cref="SalaryLevelDetailDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static SalaryLevelDetailDto ToDetailDto(this SalaryLevel entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new SalaryLevelDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SalaryGradeId = entity.SalaryGradeId,
            Code = entity.Code,
            Name = entity.Name,
            MinSalary = entity.MinSalary,
            MidSalary = entity.MidSalary,
            MaxSalary = entity.MaxSalary,
            Sequence = entity.Sequence,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedById = entity.CreatedById,
            LastModifiedById = entity.LastModifiedById,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            Notches = (entity.Notches ?? new List<SalaryNotch>())
                .OrderBy(n => n.NotchNumber)
                .Select(n => n.ToDto())
                .ToList()
        };
    }

    /// <summary>
    /// Maps a <see cref="CreateSalaryLevelDto"/> to a new <see cref="SalaryLevel"/> entity.
    /// The caller injects the tenant identifier; audit fields are intentionally not set here.
    /// </summary>
    /// <param name="dto">The create DTO.</param>
    /// <param name="tenantId">Tenant identifier to apply to the entity (authoritative).</param>
    /// <returns>A new <see cref="SalaryLevel"/> entity.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is null.</exception>
    public static SalaryLevel ToEntity(this CreateSalaryLevelDto dto, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new SalaryLevel
        {
            TenantId = tenantId,
            SalaryGradeId = dto.SalaryGradeId,
            Code = dto.Code,
            Name = dto.Name,
            MinSalary = dto.MinSalary,
            MidSalary = dto.MidSalary,
            MaxSalary = dto.MaxSalary,
            Sequence = dto.Sequence,
            IsActive = dto.IsActive
        };
    }

    /// <summary>
    /// Updates an existing <see cref="SalaryLevel"/> entity in-place from an <see cref="UpdateSalaryLevelDto"/>.
    /// Does not overwrite <c>TenantId</c> or navigation collections.
    /// </summary>
    /// <param name="dto">The update DTO.</param>
    /// <param name="entity">The existing entity to update.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> or <paramref name="entity"/> is null.</exception>
    public static void UpdateEntity(this UpdateSalaryLevelDto dto, SalaryLevel entity)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(entity);

        entity.SalaryGradeId = dto.SalaryGradeId;
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.MinSalary = dto.MinSalary;
        entity.MidSalary = dto.MidSalary;
        entity.MaxSalary = dto.MaxSalary;
        entity.Sequence = dto.Sequence;
        entity.IsActive = dto.IsActive;
    }

    #endregion

    #region SalaryNotch

    /// <summary>
    /// Maps a <see cref="SalaryNotch"/> entity to a <see cref="SalaryNotchDto"/>.
    /// Intended for lists and references.
    /// </summary>
    /// <param name="entity">The source entity.</param>
    /// <returns>A mapped <see cref="SalaryNotchDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static SalaryNotchDto ToDto(this SalaryNotch entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new SalaryNotchDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SalaryLevelId = entity.SalaryLevelId,
            NotchNumber = entity.NotchNumber,
            SalaryAmount = entity.SalaryAmount,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedById = entity.CreatedById,
            LastModifiedById = entity.LastModifiedById,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy
        };
    }

    /// <summary>
    /// Maps a <see cref="CreateSalaryNotchDto"/> to a new <see cref="SalaryNotch"/> entity.
    /// The caller injects the tenant identifier; audit fields are intentionally not set here.
    /// </summary>
    /// <param name="dto">The create DTO.</param>
    /// <param name="tenantId">Tenant identifier to apply to the entity (authoritative).</param>
    /// <returns>A new <see cref="SalaryNotch"/> entity.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is null.</exception>
    public static SalaryNotch ToEntity(this CreateSalaryNotchDto dto, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new SalaryNotch
        {
            TenantId = tenantId,
            SalaryLevelId = dto.SalaryLevelId,
            NotchNumber = dto.NotchNumber,
            SalaryAmount = dto.SalaryAmount,
            IsActive = dto.IsActive
        };
    }

    /// <summary>
    /// Updates an existing <see cref="SalaryNotch"/> entity in-place from an <see cref="UpdateSalaryNotchDto"/>.
    /// Does not overwrite <c>TenantId</c>.
    /// </summary>
    /// <param name="dto">The update DTO.</param>
    /// <param name="entity">The existing entity to update.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> or <paramref name="entity"/> is null.</exception>
    public static void UpdateEntity(this UpdateSalaryNotchDto dto, SalaryNotch entity)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(entity);

        entity.SalaryLevelId = dto.SalaryLevelId;
        entity.NotchNumber = dto.NotchNumber;
        entity.SalaryAmount = dto.SalaryAmount;
        entity.IsActive = dto.IsActive;
    }

    #endregion

    #region Collection Mappings

    /// <summary>
    /// Maps a collection of <see cref="SalaryLevel"/> entities to a collection of <see cref="SalaryLevelDto"/>.
    /// 
    /// Note: This method preserves the enumeration order of the input collection.
    /// Ordering requirements (e.g., by Sequence) are applied by the calling mapping where necessary.
    /// </summary>
    /// <param name="entities">The source entities.</param>
    /// <returns>Mapped DTOs in the same order as the source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entities"/> is null.</exception>
    public static ICollection<SalaryLevelDto> ToDtoList(this ICollection<SalaryLevel> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        if (entities.Count == 0)
        {
            return new List<SalaryLevelDto>();
        }

        var list = new List<SalaryLevelDto>(entities.Count);
        foreach (var entity in entities)
        {
            list.Add(entity.ToDto());
        }

        return list;
    }

    /// <summary>
    /// Maps a collection of <see cref="SalaryNotch"/> entities to a collection of <see cref="SalaryNotchDto"/>.
    /// 
    /// Note: This method preserves the enumeration order of the input collection.
    /// Ordering requirements (e.g., by NotchNumber) are applied by the calling mapping where necessary.
    /// </summary>
    /// <param name="entities">The source entities.</param>
    /// <returns>Mapped DTOs in the same order as the source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entities"/> is null.</exception>
    public static ICollection<SalaryNotchDto> ToDtoList(this ICollection<SalaryNotch> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        if (entities.Count == 0)
        {
            return new List<SalaryNotchDto>();
        }

        var list = new List<SalaryNotchDto>(entities.Count);
        foreach (var entity in entities)
        {
            list.Add(entity.ToDto());
        }

        return list;
    }

    #endregion
}
