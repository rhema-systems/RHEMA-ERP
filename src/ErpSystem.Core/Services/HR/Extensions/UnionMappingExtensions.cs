using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

public static class UnionMappingExtensions
{
    #region Union

    public static UnionDto ToDto(this Union entity)
    {
        return new UnionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            ContactPerson = entity.ContactPerson,
            ContactEmail = entity.ContactEmail,
            ContactPhone = entity.ContactPhone,
            IsActive = entity.IsActive,
            AgreementCount = entity.Agreements?.Count ?? 0,
            Agreements = entity.Agreements?.Select(a => a.ToDto()).ToList() ?? new List<CollectiveBargainingAgreementDto>(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static Union ToEntity(this CreateUnionDto dto)
    {
        return new Union
        {
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            ContactPerson = dto.ContactPerson,
            ContactEmail = dto.ContactEmail,
            ContactPhone = dto.ContactPhone,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateUnionDto dto, Union entity)
    {
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.ContactPerson = dto.ContactPerson;
        entity.ContactEmail = dto.ContactEmail;
        entity.ContactPhone = dto.ContactPhone;
        entity.IsActive = dto.IsActive;
    }

    public static List<UnionDto> ToDtoList(this IEnumerable<Union> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region CollectiveBargainingAgreement

    public static CollectiveBargainingAgreementDto ToDto(this CollectiveBargainingAgreement entity)
    {
        return new CollectiveBargainingAgreementDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UnionId = entity.UnionId,
            UnionName = entity.Union?.Name,
            ReferenceNumber = entity.ReferenceNumber,
            Title = entity.Title,
            EffectiveDate = entity.EffectiveDate,
            ExpiryDate = entity.ExpiryDate,
            Summary = entity.Summary,
            DocumentReference = entity.DocumentReference,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CollectiveBargainingAgreement ToEntity(this CreateCollectiveBargainingAgreementDto dto)
    {
        return new CollectiveBargainingAgreement
        {
            UnionId = dto.UnionId,
            ReferenceNumber = dto.ReferenceNumber,
            Title = dto.Title,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            Summary = dto.Summary,
            DocumentReference = dto.DocumentReference,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateCollectiveBargainingAgreementDto dto, CollectiveBargainingAgreement entity)
    {
        entity.ReferenceNumber = dto.ReferenceNumber;
        entity.Title = dto.Title;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.Summary = dto.Summary;
        entity.DocumentReference = dto.DocumentReference;
        entity.IsActive = dto.IsActive;
    }

    public static List<CollectiveBargainingAgreementDto> ToDtoList(this IEnumerable<CollectiveBargainingAgreement> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion
}
