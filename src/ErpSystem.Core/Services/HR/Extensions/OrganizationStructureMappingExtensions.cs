using System.Linq.Expressions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Application.HR.Extensions;

/// <summary>
/// The two things the organisation-unit change log records, named once.
/// </summary>
/// <remarks>
/// <para>Added in areas 19-23 slice 5. The classification itself is older: <c>ToDetailDto</c> has
/// derived it since the port, and <b>nothing in the repository has ever called that mapper</b>, so
/// the rule sat where no read could reach it. Lifting it here puts the log's own vocabulary in one
/// place — the DTO, the register's filter and the screens' badges all read from this.</para>
///
/// <para>⚠ <see cref="Classify"/> runs in memory over a loaded entity and <see cref="Predicate"/>
/// must run in SQL, so the same rule genuinely has to be written twice. They are kept adjacent
/// deliberately, and the slice-5 harness asserts they agree — a filtered page must contain exactly
/// the rows the classifier gives that type — because two statements of one rule is precisely the
/// shape that drifts.</para>
/// </remarks>
public static class OrganizationUnitChangeTypes
{
    /// <summary>The unit reported somewhere new.</summary>
    public const string Restructure = "Restructure";

    /// <summary>The unit got a different head, or lost the one it had.</summary>
    public const string LeadershipChange = "Leadership Change";

    /// <summary>Neither — a row no current writer produces, kept so a legacy row still classifies.</summary>
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = new[] { Restructure, LeadershipChange, Other };

    /// <summary>In-memory classification of a loaded row.</summary>
    public static string Classify(OrganizationUnitHistory entity)
    {
        if (entity.PreviousParentId != entity.NewParentId)
            return Restructure;
        if (entity.PreviousHeadEmployeeId != entity.NewHeadEmployeeId)
            return LeadershipChange;
        return Other;
    }

    /// <summary>
    /// The SQL twin of <see cref="Classify"/>, for the register's change-type filter.
    /// </summary>
    /// <remarks>
    /// EF Core rewrites <c>a != b</c> and <c>a == b</c> over nullable columns to C# semantics, so
    /// two null parent ids compare equal here exactly as they do in <see cref="Classify"/>. Written
    /// as an <c>Expression</c> rather than a method group for the reason slice 4 found the hard way:
    /// a static predicate called inside <c>Where</c> compiles, reads correctly and throws at runtime.
    /// </remarks>
    public static Expression<Func<OrganizationUnitHistory, bool>> Predicate(string changeType) => changeType switch
    {
        Restructure => h => h.PreviousParentId != h.NewParentId,
        LeadershipChange => h => h.PreviousParentId == h.NewParentId
                                 && h.PreviousHeadEmployeeId != h.NewHeadEmployeeId,
        Other => h => h.PreviousParentId == h.NewParentId
                      && h.PreviousHeadEmployeeId == h.NewHeadEmployeeId,
        _ => throw new ArgumentOutOfRangeException(nameof(changeType), changeType, "Unknown change type."),
    };

    /// <summary>
    /// Resolves a caller-supplied change type onto one of the three constants, tolerating case and
    /// the spaceless form a query string is likely to carry. Returns false for anything else, so the
    /// endpoint can refuse rather than quietly ignore the filter.
    /// </summary>
    public static bool TryResolve(string? value, out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var needle = value.Replace(" ", string.Empty).Trim();
        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Replace(" ", string.Empty), needle, StringComparison.OrdinalIgnoreCase))
            {
                resolved = candidate;
                return true;
            }
        }

        return false;
    }
}

public static class OrganizationStructureMappingExtensions
{
    #region OrganizationStructure Mappings

    public static OrganizationStructureDto ToDto(this OrganizationStructure entity)
    {
        return new OrganizationStructureDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            IsDefault = entity.IsDefault,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static OrganizationStructureSummaryDto ToSummaryDto(this OrganizationStructure entity)
    {
        return new OrganizationStructureSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive
        };
    }

    public static OrganizationStructureDetailDto ToDetailDto(this OrganizationStructure entity)
    {
        return new OrganizationStructureDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            IsDefault = entity.IsDefault,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            LevelCount = entity.Levels?.Count ?? 0,
            UnitCount = entity.Levels?.Sum(l => l.OrganizationUnits?.Count ?? 0) ?? 0,
            Levels = entity.Levels?.Select(l => l.ToSummaryDto()).ToList() ?? new List<OrganizationLevelSummaryDto>()
        };
    }

    public static OrganizationStructure ToEntity(this CreateOrganizationStructureDto dto)
    {
        return new OrganizationStructure
        {
            Name = dto.Name,
            Code = dto.Code,
            IsDefault = dto.IsDefault,
            Description = dto.Description,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateOrganizationStructureDto dto, OrganizationStructure entity)
    {
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.IsDefault = dto.IsDefault;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
    }

    public static List<OrganizationStructureDto> ToDtoList(this IEnumerable<OrganizationStructure> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<OrganizationStructureSummaryDto> ToSummaryDtoList(this IEnumerable<OrganizationStructure> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region OrganizationLevel Mappings

    public static OrganizationLevelDto ToDto(this OrganizationLevel entity)
    {
        return new OrganizationLevelDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            LevelNumber = entity.LevelNumber,
            IsRootLevel = entity.IsRootLevel,
            RequiresHead = entity.RequiresHead,
            AllowsDirectEmployees = entity.AllowsDirectEmployees,
            IsLocked = entity.IsLocked,
            IsActive = entity.IsActive,
            StructureId = entity.StructureId,
            StructureName = entity.OrganizationStructure?.Name,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static OrganizationLevelSummaryDto ToSummaryDto(this OrganizationLevel entity)
    {
        return new OrganizationLevelSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            LevelNumber = entity.LevelNumber,
            IsRootLevel = entity.IsRootLevel,
            IsActive = entity.IsActive
        };
    }

    public static OrganizationLevelDetailDto ToDetailDto(this OrganizationLevel entity)
    {
        return new OrganizationLevelDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            LevelNumber = entity.LevelNumber,
            IsRootLevel = entity.IsRootLevel,
            RequiresHead = entity.RequiresHead,
            AllowsDirectEmployees = entity.AllowsDirectEmployees,
            IsLocked = entity.IsLocked,
            IsActive = entity.IsActive,
            StructureId = entity.StructureId,
            StructureName = entity.OrganizationStructure?.Name,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            UnitCount = entity.OrganizationUnits?.Count ?? 0
        };
    }

    public static OrganizationLevel ToEntity(this CreateOrganizationLevelDto dto)
    {
        return new OrganizationLevel
        {
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            LevelNumber = dto.LevelNumber,
            RequiresHead = dto.RequiresHead,
            AllowsDirectEmployees = dto.AllowsDirectEmployees,
            IsActive = dto.IsActive,
            StructureId = dto.StructureId
        };
    }

    public static void UpdateEntity(this UpdateOrganizationLevelDto dto, OrganizationLevel entity)
    {
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.Description = dto.Description;
        entity.LevelNumber = dto.LevelNumber;
        entity.RequiresHead = dto.RequiresHead;
        entity.AllowsDirectEmployees = dto.AllowsDirectEmployees;
        entity.IsActive = dto.IsActive;
        entity.StructureId = dto.StructureId;
    }

    public static List<OrganizationLevelDto> ToDtoList(this IEnumerable<OrganizationLevel> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<OrganizationLevelSummaryDto> ToSummaryDtoList(this IEnumerable<OrganizationLevel> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region OrganizationUnit Mappings

    public static OrganizationUnitDto ToDto(this OrganizationUnit entity)
    {
        return new OrganizationUnitDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            AccountCode = entity.AccountCode,
            Description = entity.Description,
            OrganizationLevelId = entity.OrganizationLevelId,
            LevelName = entity.OrganizationLevel?.Name,
            ParentUnitId = entity.ParentUnitId,
            ParentUnitName = entity.ParentUnit?.Name,
            HeadEmployeeId = entity.HeadEmployeeId,
            HeadEmployeeName = entity.HeadEmployee?.FullName,
            Sequence = entity.Sequence,
            Path = entity.Path,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static OrganizationUnitSummaryDto ToSummaryDto(this OrganizationUnit entity)
    {
        return new OrganizationUnitSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            OrganizationLevelId = entity.OrganizationLevelId,
            LevelName = entity.OrganizationLevel?.Name,
            ParentUnitId = entity.ParentUnitId,
            ParentUnitName = entity.ParentUnit?.Name,
            IsActive = entity.IsActive
        };
    }

    public static OrganizationUnitDetailDto ToDetailDto(this OrganizationUnit entity)
    {
        return new OrganizationUnitDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            AccountCode = entity.AccountCode,
            Description = entity.Description,
            OrganizationLevelId = entity.OrganizationLevelId,
            LevelName = entity.OrganizationLevel?.Name,
            ParentUnitId = entity.ParentUnitId,
            ParentUnitName = entity.ParentUnit?.Name,
            HeadEmployeeId = entity.HeadEmployeeId,
            HeadEmployeeName = entity.HeadEmployee?.FullName,
            Sequence = entity.Sequence,
            Path = entity.Path,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            HierarchyPath = entity.HierarchyPath,
            ChildCount = entity.ChildCount,
            EmployeeCount = entity.EmployeeCount,
            Depth = entity.Path.Split('/').Length - 1
        };
    }

    public static OrganizationUnitTreeDto ToTreeDto(this OrganizationUnit entity)
    {
        return new OrganizationUnitTreeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            OrganizationLevelId = entity.OrganizationLevelId,
            LevelName = entity.OrganizationLevel?.Name ?? string.Empty,
            ParentUnitId = entity.ParentUnitId,
            HeadEmployeeId = entity.HeadEmployeeId,
            HeadEmployeeName = entity.HeadEmployee?.FullName,
            Sequence = entity.Sequence,
            Path = entity.Path,
            IsActive = entity.IsActive,
            HasChildren = entity.ChildUnits?.Any() ?? false,
            Depth = entity.Path.Split('/').Length - 1,
            Children = entity.ChildUnits?.Select(c => c.ToTreeDto()).ToList() ?? new List<OrganizationUnitTreeDto>()
        };
    }

    public static OrganizationUnitHierarchyDto ToHierarchyDto(this OrganizationUnit entity)
    {
        return new OrganizationUnitHierarchyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            OrganizationLevelId = entity.OrganizationLevelId,
            LevelName = entity.OrganizationLevel?.Name ?? string.Empty,
            LevelNumber = entity.OrganizationLevel?.LevelNumber ?? 0,
            ParentUnitId = entity.ParentUnitId,
            ParentUnitName = entity.ParentUnit?.Name,
            HeadEmployeeId = entity.HeadEmployeeId,
            HeadEmployeeName = entity.HeadEmployee?.FullName,
            Path = entity.Path,
            HierarchyPath = entity.HierarchyPath,
            Sequence = entity.Sequence,
            Depth = entity.Path.Split('/').Length - 1,
            HasChildren = entity.ChildUnits?.Any() ?? false,
            ChildCount = entity.ChildCount,
            EmployeeCount = entity.EmployeeCount,
            IsActive = entity.IsActive,
            Children = entity.ChildUnits?.Select(c => c.ToHierarchyDto()).ToList() ?? new List<OrganizationUnitHierarchyDto>()
        };
    }

    public static OrganizationUnit ToEntity(this CreateOrganizationUnitDto dto)
    {
        return new OrganizationUnit
        {
            Name = dto.Name,
            Code = dto.Code,
            AccountCode = dto.AccountCode,
            Description = dto.Description,
            OrganizationLevelId = dto.OrganizationLevelId,
            ParentUnitId = dto.ParentUnitId,
            HeadEmployeeId = dto.HeadEmployeeId,
            Sequence = dto.Sequence,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateOrganizationUnitDto dto, OrganizationUnit entity)
    {
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.AccountCode = dto.AccountCode;
        entity.Description = dto.Description;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.ParentUnitId = dto.ParentUnitId;
        entity.HeadEmployeeId = dto.HeadEmployeeId;
        entity.Sequence = dto.Sequence;
        entity.IsActive = dto.IsActive;
    }

    public static List<OrganizationUnitDto> ToDtoList(this IEnumerable<OrganizationUnit> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<OrganizationUnitSummaryDto> ToSummaryDtoList(this IEnumerable<OrganizationUnit> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public static List<OrganizationUnitTreeDto> ToTreeDtoList(this IEnumerable<OrganizationUnit> entities)
    {
        return entities.Select(e => e.ToTreeDto()).ToList();
    }

    #endregion

    #region OrganizationUnitHistory Mappings

    public static OrganizationUnitHistoryDto ToDto(this OrganizationUnitHistory entity)
    {
        return new OrganizationUnitHistoryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PreviousParentId = entity.PreviousParentId,
            PreviousParentName = null, // Would need to load separately if needed
            NewParentId = entity.NewParentId,
            NewParentName = null, // Would need to load separately if needed
            PreviousHeadEmployeeId = entity.PreviousHeadEmployeeId,
            PreviousHeadEmployeeName = null, // Would need to load separately if needed
            NewHeadEmployeeId = entity.NewHeadEmployeeId,
            NewHeadEmployeeName = null, // Would need to load separately if needed
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            ChangeReason = entity.ChangeReason,
            ChangeType = OrganizationUnitChangeTypes.Classify(entity),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static OrganizationUnitHistorySummaryDto ToSummaryDto(this OrganizationUnitHistory entity)
    {
        return new OrganizationUnitHistorySummaryDto
        {
            Id = entity.Id,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            ChangeReason = entity.ChangeReason
        };
    }

    public static OrganizationUnitHistoryDetailDto ToDetailDto(this OrganizationUnitHistory entity)
    {
        var dto = new OrganizationUnitHistoryDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PreviousParentId = entity.PreviousParentId,
            NewParentId = entity.NewParentId,
            PreviousHeadEmployeeId = entity.PreviousHeadEmployeeId,
            NewHeadEmployeeId = entity.NewHeadEmployeeId,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            ChangeReason = entity.ChangeReason,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ChangedBy = entity.CreatedBy,
            ChangedAt = entity.CreatedAt,
            // One definition, in OrganizationUnitChangeTypes. This mapper used to hold the only copy
            // of the rule and no read ever reached it.
            ChangeType = OrganizationUnitChangeTypes.Classify(entity)
        };

        return dto;
    }

    public static List<OrganizationUnitHistoryDto> ToDtoList(this IEnumerable<OrganizationUnitHistory> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<OrganizationUnitHistorySummaryDto> ToSummaryDtoList(this IEnumerable<OrganizationUnitHistory> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion
}
