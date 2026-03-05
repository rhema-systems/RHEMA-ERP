using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Application.HR.Extensions;

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
            ChangedAt = entity.CreatedAt
        };

        // Determine change type
        if (entity.PreviousParentId != entity.NewParentId)
        {
            dto.ChangeType = "Restructure";
        }
        else if (entity.PreviousHeadEmployeeId != entity.NewHeadEmployeeId)
        {
            dto.ChangeType = "Leadership Change";
        }
        else
        {
            dto.ChangeType = "Other";
        }

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
