using System;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

/// <summary>
/// Explicit, hand-written mapping extensions for the Staff Level module.
///
/// Design goals:
/// - Predictable, side-effect free mappings
/// - No AutoMapper / no reflection
/// - Defensive null handling (throws <see cref="ArgumentNullException"/>)
/// - No navigation entity exposure (no direct mapping of EmployeePositions)
/// </summary>
public static class StaffLevelMappingExtensions
{
    #region StaffLevel

    /// <summary>
    /// Maps a <see cref="StaffLevel"/> entity to a read/display <see cref="StaffLevelDto"/>.
    /// </summary>
    /// <param name="entity">The source entity.</param>
    /// <returns>A mapped <see cref="StaffLevelDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static StaffLevelDto ToDto(this StaffLevel entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new StaffLevelDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Rank = entity.Rank,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    /// <summary>
    /// Maps a <see cref="StaffLevel"/> entity to an optimized <see cref="StaffLevelListDto"/>.
    /// Intended for table/list views.
    /// </summary>
    /// <param name="entity">The source entity.</param>
    /// <returns>A mapped <see cref="StaffLevelListDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static StaffLevelListDto ToListDto(this StaffLevel entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new StaffLevelListDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Rank = entity.Rank,
            IsActive = entity.IsActive
        };
    }

    /// <summary>
    /// Maps a <see cref="StaffLevel"/> entity to a <see cref="StaffLevelDetailDto"/>.
    /// Includes computed fields such as <see cref="StaffLevelDetailDto.EmployeePositionCount"/>.
    /// </summary>
    /// <param name="entity">The source entity.</param>
    /// <returns>A mapped <see cref="StaffLevelDetailDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static StaffLevelDetailDto ToDetailDto(this StaffLevel entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new StaffLevelDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Rank = entity.Rank,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            EmployeePositionCount = entity.EmployeePositions?.Count ?? 0
        };
    }

    /// <summary>
    /// Maps a <see cref="CreateStaffLevelDto"/> to a new <see cref="StaffLevel"/> entity.
    ///
    /// Tenant and audit fields are intentionally not set here.
    /// </summary>
    /// <param name="dto">The source create DTO.</param>
    /// <returns>A new <see cref="StaffLevel"/> entity populated from the DTO.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is null.</exception>
    public static StaffLevel ToEntity(this CreateStaffLevelDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new StaffLevel
        {
            Name = dto.Name,
            Code = dto.Code,
            Rank = dto.Rank,
            Description = dto.Description
        };
    }

    /// <summary>
    /// Applies a <see cref="UpdateStaffLevelDto"/> onto an existing <see cref="StaffLevel"/> entity.
    ///
    /// This method updates only mutable business fields and preserves identity, tenant, and audit fields.
    /// </summary>
    /// <param name="dto">The source update DTO.</param>
    /// <param name="entity">The existing entity instance to update.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> or <paramref name="entity"/> is null.</exception>
    public static void UpdateEntity(this UpdateStaffLevelDto dto, StaffLevel entity)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(entity);

        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.Rank = dto.Rank;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
    }

    #endregion
}
