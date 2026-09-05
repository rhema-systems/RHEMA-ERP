using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

public static class CompanyProfileMappingExtensions
{
    public static CompanyProfileDto ToDto(this CompanyProfile entity)
    {
        return new CompanyProfileDto
        {
            Id        = entity.Id,
            TenantId  = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,

            LegalName                   = entity.LegalName,
            TradingName                 = entity.TradingName,
            LegalForm                   = entity.LegalForm,
            RegistrationNumber          = entity.RegistrationNumber,
            DateOfIncorporation         = entity.DateOfIncorporation,
            CountryOfIncorporationId    = entity.CountryOfIncorporationId,
            CountryOfIncorporationName  = entity.CountryOfIncorporation?.Name,

            TaxIdentificationNumber     = entity.TaxIdentificationNumber,
            VatNumber                   = entity.VatNumber,
            SsnitEmployerNumber         = entity.SsnitEmployerNumber,
            OtherStatutoryRegistrations = entity.OtherStatutoryRegistrations,

            RegisteredAddress           = entity.RegisteredAddress,
            DigitalAddress              = entity.DigitalAddress,
            City                        = entity.City,
            Region                      = entity.Region,
            CountryId                   = entity.CountryId,
            GeoAreaId                   = entity.GeoAreaId,
            CountryName                 = entity.Country?.Name,
            PostalCode                  = entity.PostalCode,
            PhonePrimary                = entity.PhonePrimary,
            HrEmail                     = entity.HrEmail,
            GeneralEmail                = entity.GeneralEmail,
            Website                     = entity.Website,

            DefaultSignatoryName        = entity.DefaultSignatoryName,
            DefaultSignatoryTitle       = entity.DefaultSignatoryTitle,
            SignatureImageUrl           = entity.SignatureImageUrl,
            CompanySealImageUrl         = entity.CompanySealImageUrl,
            LogoUrl                     = entity.LogoUrl,
            OfferAcceptanceInstructions = entity.OfferAcceptanceInstructions,
            DocumentFooterText          = entity.DocumentFooterText,
        };
    }

    /// <summary>Applies editable fields from an update command onto an existing entity.</summary>
    public static void ApplyUpdate(this CompanyProfile entity, UpdateCompanyProfileDto dto)
    {
        entity.LegalName                   = (dto.LegalName ?? string.Empty).Trim();
        entity.TradingName                 = dto.TradingName?.Trim();
        entity.LegalForm                   = dto.LegalForm;
        entity.RegistrationNumber          = dto.RegistrationNumber?.Trim();
        entity.DateOfIncorporation         = dto.DateOfIncorporation;
        entity.CountryOfIncorporationId    = dto.CountryOfIncorporationId;

        entity.TaxIdentificationNumber     = dto.TaxIdentificationNumber?.Trim();
        entity.VatNumber                   = dto.VatNumber?.Trim();
        entity.SsnitEmployerNumber         = dto.SsnitEmployerNumber?.Trim();
        entity.OtherStatutoryRegistrations = dto.OtherStatutoryRegistrations?.Trim();

        entity.RegisteredAddress           = dto.RegisteredAddress?.Trim();
        entity.DigitalAddress              = dto.DigitalAddress?.Trim();
        entity.City                        = dto.City?.Trim();
        entity.Region                      = dto.Region?.Trim();
        entity.CountryId                   = dto.CountryId;
        entity.GeoAreaId                   = dto.GeoAreaId;
        entity.PostalCode                  = dto.PostalCode?.Trim();
        entity.PhonePrimary                = dto.PhonePrimary?.Trim();
        entity.HrEmail                     = dto.HrEmail?.Trim();
        entity.GeneralEmail                = dto.GeneralEmail?.Trim();
        entity.Website                     = dto.Website?.Trim();

        entity.DefaultSignatoryName        = dto.DefaultSignatoryName?.Trim();
        entity.DefaultSignatoryTitle       = dto.DefaultSignatoryTitle?.Trim();
        // ⚠ SignatureImageUrl and CompanySealImageUrl are legacy read-only — see the update DTO.
        // A seal is an instrument of authority; it is uploaded through the gate and versioned, not
        // typed as a path on a profile edit.
        entity.LogoUrl                     = dto.LogoUrl?.Trim();
        entity.OfferAcceptanceInstructions = dto.OfferAcceptanceInstructions?.Trim();
        entity.DocumentFooterText          = dto.DocumentFooterText?.Trim();
    }
}
