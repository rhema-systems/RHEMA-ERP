using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Application.HR.Extensions;

public static class LocationStructureMappingExtensions
{
    #region LocationStructure Mappings

    public static LocationStructureDto ToDto(this LocationStructure entity)
    {
        return new LocationStructureDto
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

    public static LocationStructureSummaryDto ToSummaryDto(this LocationStructure entity)
    {
        return new LocationStructureSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive
        };
    }

    public static LocationStructureDetailDto ToDetailDto(this LocationStructure entity)
    {
        return new LocationStructureDetailDto
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
            LevelCount = entity.LocationLevels?.Count ?? 0,
            LocationCount = entity.Locations?.Count ?? 0,
            Levels = entity.LocationLevels?.Select(l => l.ToSummaryDto()).ToList() ?? new List<LocationLevelSummaryDto>()
        };
    }

    public static LocationStructure ToEntity(this CreateLocationStructureDto dto)
    {
        return new LocationStructure
        {
            Name = dto.Name,
            Code = dto.Code,
            IsDefault = dto.IsDefault,
            Description = dto.Description,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateLocationStructureDto dto, LocationStructure entity)
    {
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.IsDefault = dto.IsDefault;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
    }

    public static List<LocationStructureDto> ToDtoList(this IEnumerable<LocationStructure> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<LocationStructureSummaryDto> ToSummaryDtoList(this IEnumerable<LocationStructure> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region LocationLevel Mappings

    public static LocationLevelDto ToDto(this LocationLevel entity)
    {
        return new LocationLevelDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            LevelNumber = entity.LevelNumber,
            RequiresAddress = entity.RequiresAddress,
            RequiresContactInfo = entity.RequiresContactInfo,
            AllowsEmployeeAssignment = entity.AllowsEmployeeAssignment,
            IsRootLevel = entity.IsRootLevel,
            IsLocked = entity.IsLocked,
            IsActive = entity.IsActive,
            StructureId = entity.StructureId,
            StructureName = entity.Structure?.Name,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static LocationLevelSummaryDto ToSummaryDto(this LocationLevel entity)
    {
        return new LocationLevelSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            LevelNumber = entity.LevelNumber,
            IsRootLevel = entity.IsRootLevel,
            IsActive = entity.IsActive
        };
    }

    public static LocationLevelDetailDto ToDetailDto(this LocationLevel entity)
    {
        return new LocationLevelDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            LevelNumber = entity.LevelNumber,
            RequiresAddress = entity.RequiresAddress,
            RequiresContactInfo = entity.RequiresContactInfo,
            AllowsEmployeeAssignment = entity.AllowsEmployeeAssignment,
            IsRootLevel = entity.IsRootLevel,
            IsLocked = entity.IsLocked,
            IsActive = entity.IsActive,
            StructureId = entity.StructureId,
            StructureName = entity.Structure?.Name,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            LocationCount = entity.Locations?.Count ?? 0
        };
    }

    public static LocationLevel ToEntity(this CreateLocationLevelDto dto)
    {
        return new LocationLevel
        {
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            LevelNumber = dto.LevelNumber,
            RequiresAddress = dto.RequiresAddress,
            RequiresContactInfo = dto.RequiresContactInfo,
            AllowsEmployeeAssignment = dto.AllowsEmployeeAssignment,
            IsActive = dto.IsActive,
            StructureId = dto.StructureId
        };
    }

    public static void UpdateEntity(this UpdateLocationLevelDto dto, LocationLevel entity)
    {
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.Description = dto.Description;
        entity.LevelNumber = dto.LevelNumber;
        entity.RequiresAddress = dto.RequiresAddress;
        entity.RequiresContactInfo = dto.RequiresContactInfo;
        entity.AllowsEmployeeAssignment = dto.AllowsEmployeeAssignment;
        entity.IsActive = dto.IsActive;
        entity.StructureId = dto.StructureId;
    }

    public static List<LocationLevelDto> ToDtoList(this IEnumerable<LocationLevel> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<LocationLevelSummaryDto> ToSummaryDtoList(this IEnumerable<LocationLevel> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region Location Mappings

    public static LocationDto ToDto(this Location entity)
    {
        return new LocationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            StructureId = entity.StructureId,
            StructureName = entity.Structure?.Name,
            LocationLevelId = entity.LocationLevelId,
            LevelName = entity.LocationLevel?.Name,
            ParentLocationId = entity.ParentLocationId,
            ParentLocationName = entity.ParentLocation?.Name,
            AddressLine1 = entity.AddressLine1,
            AddressLine2 = entity.AddressLine2,
            City = entity.City,
            PostalCode = entity.PostalCode,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name,
            DigitalAddress = entity.DigitalAddress,
            Phone = entity.Phone,
            Email = entity.Email,
            Website = entity.Website,
            FaxNumber = entity.FaxNumber,
            Sequence = entity.Sequence,
            Path = entity.Path,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static LocationSummaryDto ToSummaryDto(this Location entity)
    {
        return new LocationSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            LevelName = entity.LocationLevel?.Name,
            City = entity.City,
            CountryName = entity.Country?.Name,
            IsActive = entity.IsActive
        };
    }

    public static LocationDetailDto ToDetailDto(this Location entity)
    {
        return new LocationDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            StructureId = entity.StructureId,
            StructureName = entity.Structure?.Name,
            LocationLevelId = entity.LocationLevelId,
            LevelName = entity.LocationLevel?.Name,
            ParentLocationId = entity.ParentLocationId,
            ParentLocationName = entity.ParentLocation?.Name,
            AddressLine1 = entity.AddressLine1,
            AddressLine2 = entity.AddressLine2,
            City = entity.City,
            PostalCode = entity.PostalCode,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name,
            DigitalAddress = entity.DigitalAddress,
            Phone = entity.Phone,
            Email = entity.Email,
            Website = entity.Website,
            FaxNumber = entity.FaxNumber,
            Sequence = entity.Sequence,
            Path = entity.Path,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ChildLocationCount = entity.ChildLocations?.Count ?? 0,
            EmployeeCount = entity.Employees?.Count ?? 0,
            ContactCount = entity.LocationContacts?.Count ?? 0,
            Contacts = entity.LocationContacts?.Select(c => c.ToSummaryDto()).ToList() ?? new List<LocationContactSummaryDto>()
        };
    }

    public static LocationTreeDto ToTreeDto(this Location entity)
    {
        return new LocationTreeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            LocationLevelId = entity.LocationLevelId,
            LevelName = entity.LocationLevel?.Name ?? string.Empty,
            ParentLocationId = entity.ParentLocationId,
            City = entity.City,
            CountryName = entity.Country?.Name,
            Path = entity.Path,
            Sequence = entity.Sequence,
            IsActive = entity.IsActive,
            IsLeaf = !(entity.ChildLocations?.Any() ?? false),
            Depth = entity.Path.Split('/').Length - 1,
            Children = entity.ChildLocations?.Select(c => c.ToTreeDto()).ToList() ?? new List<LocationTreeDto>()
        };
    }

    public static LocationHierarchyDto ToHierarchyDto(this Location entity)
    {
        return new LocationHierarchyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            LocationLevelId = entity.LocationLevelId,
            LevelName = entity.LocationLevel?.Name ?? string.Empty,
            LevelNumber = entity.LocationLevel?.LevelNumber ?? 0,
            ParentLocationId = entity.ParentLocationId,
            ParentLocationName = entity.ParentLocation?.Name,
            City = entity.City,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name,
            Path = entity.Path,
            Sequence = entity.Sequence,
            Depth = entity.Path.Split('/').Length - 1,
            IsLeaf = !(entity.ChildLocations?.Any() ?? false),
            ChildLocationCount = entity.ChildLocations?.Count ?? 0,
            EmployeeCount = entity.Employees?.Count ?? 0,
            IsActive = entity.IsActive,
            Children = entity.ChildLocations?.Select(c => c.ToHierarchyDto()).ToList() ?? new List<LocationHierarchyDto>()
        };
    }

    public static Location ToEntity(this CreateLocationDto dto)
    {
        return new Location
        {
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            StructureId = dto.StructureId,
            LocationLevelId = dto.LocationLevelId,
            ParentLocationId = dto.ParentLocationId,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            PostalCode = dto.PostalCode,
            CountryId = dto.CountryId,
            DigitalAddress = dto.DigitalAddress,
            Phone = dto.Phone,
            Email = dto.Email,
            Website = dto.Website,
            FaxNumber = dto.FaxNumber,
            Sequence = dto.Sequence,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateLocationDto dto, Location entity)
    {
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.Description = dto.Description;
        entity.StructureId = dto.StructureId;
        entity.LocationLevelId = dto.LocationLevelId;
        entity.ParentLocationId = dto.ParentLocationId;
        entity.AddressLine1 = dto.AddressLine1;
        entity.AddressLine2 = dto.AddressLine2;
        entity.City = dto.City;
        entity.PostalCode = dto.PostalCode;
        entity.CountryId = dto.CountryId;
        entity.DigitalAddress = dto.DigitalAddress;
        entity.Phone = dto.Phone;
        entity.Email = dto.Email;
        entity.Website = dto.Website;
        entity.FaxNumber = dto.FaxNumber;
        entity.Sequence = dto.Sequence;
        entity.IsActive = dto.IsActive;
    }

    public static List<LocationDto> ToDtoList(this IEnumerable<Location> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<LocationSummaryDto> ToSummaryDtoList(this IEnumerable<Location> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public static List<LocationTreeDto> ToTreeDtoList(this IEnumerable<Location> entities)
    {
        return entities.Select(e => e.ToTreeDto()).ToList();
    }

    #endregion

    #region LocationContact Mappings

    public static LocationContactDto ToDto(this LocationContact entity)
    {
        return new LocationContactDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            ContactName = entity.ContactName,
            Phone = entity.Phone,
            Email = entity.Email,
            IsPrimary = entity.IsPrimary,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static LocationContactSummaryDto ToSummaryDto(this LocationContact entity)
    {
        return new LocationContactSummaryDto
        {
            Id = entity.Id,
            ContactName = entity.ContactName,
            EmployeeName = entity.Employee?.FullName,
            Phone = entity.Phone,
            Email = entity.Email,
            IsPrimary = entity.IsPrimary
        };
    }

    public static LocationContactDetailDto ToDetailDto(this LocationContact entity)
    {
        return new LocationContactDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            ContactName = entity.ContactName,
            Phone = entity.Phone,
            Email = entity.Email,
            IsPrimary = entity.IsPrimary,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeCode = entity.Employee?.EmployeeNumber,
            EmployeeDepartment = entity.Employee?.OrganizationUnit?.Name
        };
    }

    public static LocationContact ToEntity(this CreateLocationContactDto dto)
    {
        return new LocationContact
        {
            LocationId = dto.LocationId,
            EmployeeId = dto.EmployeeId,
            ContactName = dto.ContactName,
            Phone = dto.Phone,
            Email = dto.Email,
            IsPrimary = dto.IsPrimary
        };
    }

    public static void UpdateEntity(this UpdateLocationContactDto dto, LocationContact entity)
    {
        entity.LocationId = dto.LocationId;
        entity.EmployeeId = dto.EmployeeId;
        entity.ContactName = dto.ContactName;
        entity.Phone = dto.Phone;
        entity.Email = dto.Email;
        entity.IsPrimary = dto.IsPrimary;
    }

    public static List<LocationContactDto> ToDtoList(this IEnumerable<LocationContact> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<LocationContactSummaryDto> ToSummaryDtoList(this IEnumerable<LocationContact> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion
}
