using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

public static class IdentificationTypeMappingExtensions
{
    #region IdentificationType

    public static IdentificationTypeDto ToDto(this IdentificationType entity)
    {
        return new IdentificationTypeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            IssuingAuthorityName = entity.IssuingAuthorityName,
            IssuingCountryId = entity.IssuingCountryId,
            IssuingCountryName = entity.IssuingCountry?.Name,
            HasExpiryDate = entity.HasExpiryDate,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static IdentificationType ToEntity(this CreateIdentificationTypeDto dto)
    {
        return new IdentificationType
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            IssuingAuthorityName = dto.IssuingAuthorityName,
            IssuingCountryId = dto.IssuingCountryId,
            HasExpiryDate = dto.HasExpiryDate,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateIdentificationTypeDto dto, IdentificationType entity)
    {
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.Description = dto.Description;
        entity.IssuingAuthorityName = dto.IssuingAuthorityName;
        entity.IssuingCountryId = dto.IssuingCountryId;
        entity.HasExpiryDate = dto.HasExpiryDate;
        entity.IsActive = dto.IsActive;
    }

    public static List<IdentificationTypeDto> ToDtoList(this IEnumerable<IdentificationType> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static IdentificationTypeLookupDto ToLookupDto(this IdentificationType entity)
    {
        return new IdentificationTypeLookupDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            HasExpiryDate = entity.HasExpiryDate,
            IsActive = entity.IsActive
        };
    }

    public static List<IdentificationTypeLookupDto> ToLookupDtoList(this IEnumerable<IdentificationType> entities)
    {
        return entities.Select(e => e.ToLookupDto()).ToList();
    }

    #endregion
}
