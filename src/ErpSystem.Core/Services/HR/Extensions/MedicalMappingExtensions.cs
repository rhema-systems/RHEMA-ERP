using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class MedicalMappingExtensions
{
    // ========================================================================
    // PRIVATE HELPERS
    // ========================================================================

    private static void MapAuditFields(BaseEntity entity, BaseDto dto)
    {
        dto.Id = entity.Id;
        dto.CreatedAt = entity.CreatedAt;
        dto.CreatedBy = entity.CreatedBy ?? string.Empty;
        dto.UpdatedAt = entity.UpdatedAt;
        dto.UpdatedBy = entity.UpdatedBy;
    }

    private static string FormatPhysicianName(Physician? physician)
    {
        if (physician == null) return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(physician.Title)) parts.Add(physician.Title);
        parts.Add(physician.FirstName);
        if (!string.IsNullOrWhiteSpace(physician.MiddleName)) parts.Add(physician.MiddleName);
        parts.Add(physician.LastName);
        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string FormatDependentName(EmployeeDependent? dependent)
    {
        if (dependent == null) return string.Empty;

        return string.Join(" ",
            new[] { dependent.FirstName, dependent.MiddleName, dependent.LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string? FormatDependentRelationship(EmployeeDependent? dependent)
    {
        if (dependent == null) return null;

        if (dependent.Relationship == DependentRelationship.Other
            && !string.IsNullOrWhiteSpace(dependent.RelationshipDescription))
            return dependent.RelationshipDescription;

        return dependent.Relationship.ToString();
    }

    // ========================================================================
    // HEALTHCARE FACILITY
    // ========================================================================

    #region HealthcareFacility

    private static T MapHealthcareFacilityCore<T>(HealthcareFacility entity) where T : HealthcareFacilityDto, new()
    {
        var dto = new T();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.FacilityName = entity.FacilityName;
        dto.ShortName = entity.ShortName;
        dto.FacilityCode = entity.FacilityCode;
        dto.FacilityType = entity.FacilityType;
        dto.LicenseNumber = entity.LicenseNumber;
        dto.LicenseExpiryDate = entity.LicenseExpiryDate;
        dto.PhysicalAddress = entity.PhysicalAddress;
        dto.DigitalAddress = entity.DigitalAddress;
        dto.City = entity.City;
        dto.PostalCode = entity.PostalCode;
        dto.CountryId = entity.CountryId;
        dto.GeoAreaId = entity.GeoAreaId;
        dto.CountryName = entity.Country?.Name;
        dto.PrimaryPhone = entity.PrimaryPhone;
        dto.EmergencyPhone = entity.EmergencyPhone;
        dto.Email = entity.Email;
        dto.Website = entity.Website;
        dto.Description = entity.Description;
        dto.HasEmergencyServices = entity.HasEmergencyServices;
        dto.Has24HourService = entity.Has24HourService;
        dto.HasAmbulanceService = entity.HasAmbulanceService;
        dto.HasLaboratory = entity.HasLaboratory;
        dto.HasPharmacy = entity.HasPharmacy;
        dto.AccreditationBody = entity.AccreditationBody;
        dto.AccreditationNumber = entity.AccreditationNumber;
        dto.AccreditationDate = entity.AccreditationDate;
        dto.AccreditationExpiryDate = entity.AccreditationExpiryDate;
        dto.AcceptsNHIS = entity.AcceptsNHIS;
        dto.NHISAccreditationNumber = entity.NHISAccreditationNumber;
        dto.BankId = entity.BankId;
        dto.BankName = entity.Bank?.Name;
        dto.BranchId = entity.BranchId;
        dto.BranchName = entity.Branch?.Name;
        dto.AccountNumber = entity.AccountNumber;
        dto.AccountName = entity.AccountName;
        dto.ContactPersonName = entity.ContactPersonName;
        dto.ContactPersonTitle = entity.ContactPersonTitle;
        dto.ContactPersonPhone = entity.ContactPersonPhone;
        dto.ContactPersonEmail = entity.ContactPersonEmail;
        dto.OperatingHours = entity.OperatingHours;
        dto.OperatingDays = entity.OperatingDays;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static HealthcareFacilityDto ToDto(this HealthcareFacility entity)
        => MapHealthcareFacilityCore<HealthcareFacilityDto>(entity);

    public static HealthcareFacilityDetailDto ToDetailDto(this HealthcareFacility entity)
    {
        var dto = MapHealthcareFacilityCore<HealthcareFacilityDetailDto>(entity);
        dto.Physicians = entity.Physicians.Select(p => p.ToSummaryDto()).ToList();
        // ⚠ Set the facility name explicitly rather than leaving ToDto to read `s.Facility`. It
        // resolves today only because EF fixup wires the navigation back to the entity being mapped
        // — the same accident-of-tracking slice 0 caught on the union agreement. An AsNoTracking read
        // here would silently blank every row.
        dto.Services = entity.Services
            .Select(s => { var svc = s.ToDto(); svc.FacilityName = entity.FacilityName; return svc; })
            .ToList();
        dto.ProviderNetworks = entity.ProviderFacilities.Select(pf => pf.ToSummaryDto()).ToList();
        return dto;
    }

    public static HealthcareFacilitySummaryDto ToSummaryDto(this HealthcareFacility entity)
    {
        return new HealthcareFacilitySummaryDto
        {
            Id = entity.Id,
            FacilityName = entity.FacilityName,
            FacilityCode = entity.FacilityCode,
            FacilityType = entity.FacilityType,
            City = entity.City,
            PrimaryPhone = entity.PrimaryPhone,
            HasEmergencyServices = entity.HasEmergencyServices,
            AcceptsNHIS = entity.AcceptsNHIS,
            IsActive = entity.IsActive,
        };
    }

    public static HealthcareFacility ToEntity(this CreateHealthcareFacilityDto dto, Guid tenantId, Guid userId)
    {
        return new HealthcareFacility
        {
            TenantId = tenantId,
            FacilityName = dto.FacilityName,
            ShortName = dto.ShortName,
            FacilityCode = dto.FacilityCode,
            FacilityType = dto.FacilityType,
            LicenseNumber = dto.LicenseNumber,
            LicenseExpiryDate = dto.LicenseExpiryDate,
            PhysicalAddress = dto.PhysicalAddress,
            DigitalAddress = dto.DigitalAddress,
            City = dto.City,
            PostalCode = dto.PostalCode,
            CountryId = dto.CountryId,
            GeoAreaId = dto.GeoAreaId,
            PrimaryPhone = dto.PrimaryPhone,
            EmergencyPhone = dto.EmergencyPhone,
            Email = dto.Email,
            Website = dto.Website,
            Description = dto.Description,
            HasEmergencyServices = dto.HasEmergencyServices,
            Has24HourService = dto.Has24HourService,
            HasAmbulanceService = dto.HasAmbulanceService,
            HasLaboratory = dto.HasLaboratory,
            HasPharmacy = dto.HasPharmacy,
            AccreditationBody = dto.AccreditationBody,
            AccreditationNumber = dto.AccreditationNumber,
            AccreditationDate = dto.AccreditationDate,
            AccreditationExpiryDate = dto.AccreditationExpiryDate,
            AcceptsNHIS = dto.AcceptsNHIS,
            NHISAccreditationNumber = dto.NHISAccreditationNumber,
            BankId = dto.BankId,
            BranchId = dto.BranchId,
            AccountNumber = dto.AccountNumber,
            AccountName = dto.AccountName,
            ContactPersonName = dto.ContactPersonName,
            ContactPersonTitle = dto.ContactPersonTitle,
            ContactPersonPhone = dto.ContactPersonPhone,
            ContactPersonEmail = dto.ContactPersonEmail,
            OperatingHours = dto.OperatingHours,
            OperatingDays = dto.OperatingDays,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this HealthcareFacility entity, UpdateHealthcareFacilityDto dto, Guid userId)
    {
        entity.FacilityName = dto.FacilityName;
        entity.ShortName = dto.ShortName;
        entity.FacilityCode = dto.FacilityCode;
        entity.FacilityType = dto.FacilityType;
        entity.LicenseNumber = dto.LicenseNumber;
        entity.LicenseExpiryDate = dto.LicenseExpiryDate;
        entity.PhysicalAddress = dto.PhysicalAddress;
        entity.DigitalAddress = dto.DigitalAddress;
        entity.City = dto.City;
        entity.PostalCode = dto.PostalCode;
        entity.CountryId = dto.CountryId;
        entity.GeoAreaId = dto.GeoAreaId;
        entity.PrimaryPhone = dto.PrimaryPhone;
        entity.EmergencyPhone = dto.EmergencyPhone;
        entity.Email = dto.Email;
        entity.Website = dto.Website;
        entity.Description = dto.Description;
        entity.HasEmergencyServices = dto.HasEmergencyServices;
        entity.Has24HourService = dto.Has24HourService;
        entity.HasAmbulanceService = dto.HasAmbulanceService;
        entity.HasLaboratory = dto.HasLaboratory;
        entity.HasPharmacy = dto.HasPharmacy;
        entity.AccreditationBody = dto.AccreditationBody;
        entity.AccreditationNumber = dto.AccreditationNumber;
        entity.AccreditationDate = dto.AccreditationDate;
        entity.AccreditationExpiryDate = dto.AccreditationExpiryDate;
        entity.AcceptsNHIS = dto.AcceptsNHIS;
        entity.NHISAccreditationNumber = dto.NHISAccreditationNumber;
        entity.BankId = dto.BankId;
        entity.BranchId = dto.BranchId;
        entity.AccountNumber = dto.AccountNumber;
        entity.AccountName = dto.AccountName;
        entity.ContactPersonName = dto.ContactPersonName;
        entity.ContactPersonTitle = dto.ContactPersonTitle;
        entity.ContactPersonPhone = dto.ContactPersonPhone;
        entity.ContactPersonEmail = dto.ContactPersonEmail;
        entity.OperatingHours = dto.OperatingHours;
        entity.OperatingDays = dto.OperatingDays;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<HealthcareFacilitySummaryDto> ToSummaryDtoList(this IEnumerable<HealthcareFacility> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<HealthcareFacilityDto> ToDtoList(this IEnumerable<HealthcareFacility> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // PHYSICIAN
    // ========================================================================

    #region Physician

    public static PhysicianDto ToDto(this Physician entity)
    {
        var dto = new PhysicianDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.FirstName = entity.FirstName;
        dto.LastName = entity.LastName;
        dto.MiddleName = entity.MiddleName;
        dto.Title = entity.Title;
        dto.Specialization = entity.Specialization;
        dto.MedicalLicenseNumber = entity.MedicalLicenseNumber;
        dto.LicenseExpiryDate = entity.LicenseExpiryDate;
        dto.PhoneNumber = entity.PhoneNumber;
        dto.Email = entity.Email;
        dto.FacilityId = entity.FacilityId;
        dto.FacilityName = entity.Facility?.FacilityName;
        dto.IsVerified = entity.IsVerified;
        dto.VerificationDate = entity.VerificationDate;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static PhysicianSummaryDto ToSummaryDto(this Physician entity)
    {
        return new PhysicianSummaryDto
        {
            Id = entity.Id,
            FullName = FormatPhysicianName(entity),
            Title = entity.Title,
            Specialization = entity.Specialization,
            PhoneNumber = entity.PhoneNumber,
            FacilityName = entity.Facility?.FacilityName,
            IsActive = entity.IsActive,
            IsVerified = entity.IsVerified,
        };
    }

    public static Physician ToEntity(this CreatePhysicianDto dto, Guid tenantId, Guid userId)
    {
        return new Physician
        {
            TenantId = tenantId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            MiddleName = dto.MiddleName,
            Title = dto.Title,
            Specialization = dto.Specialization,
            MedicalLicenseNumber = dto.MedicalLicenseNumber,
            LicenseExpiryDate = dto.LicenseExpiryDate,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            FacilityId = dto.FacilityId,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this Physician entity, UpdatePhysicianDto dto, Guid userId)
    {
        entity.FirstName = dto.FirstName;
        entity.LastName = dto.LastName;
        entity.MiddleName = dto.MiddleName;
        entity.Title = dto.Title;
        entity.Specialization = dto.Specialization;
        entity.MedicalLicenseNumber = dto.MedicalLicenseNumber;
        entity.LicenseExpiryDate = dto.LicenseExpiryDate;
        entity.PhoneNumber = dto.PhoneNumber;
        entity.Email = dto.Email;
        entity.FacilityId = dto.FacilityId;
        // ⚠ IsVerified is NOT written here (2026-10-07). It was, from a DTO field the physicians
        // screen never sent, so every edit un-verified the licence — and any caller could verify one
        // without the verify endpoint's VerificationDate. Verifying is ApplyTo(VerifyPhysicianDto)'s alone.
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this VerifyPhysicianDto dto, Physician entity, Guid userId)
    {
        entity.IsVerified = true;
        entity.VerificationDate = dto.VerificationDate;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<PhysicianSummaryDto> ToSummaryDtoList(this IEnumerable<Physician> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<PhysicianDto> ToDtoList(this IEnumerable<Physician> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // FACILITY SERVICE
    // ========================================================================

    #region FacilityService

    public static FacilityServiceDto ToDto(this FacilityService entity)
    {
        var dto = new FacilityServiceDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.FacilityId = entity.FacilityId;
        dto.FacilityName = entity.Facility?.FacilityName ?? string.Empty;
        dto.Name = entity.Name;
        dto.ServiceType = entity.ServiceType;
        dto.EstimatedCost = entity.EstimatedCost;
        dto.RequiresAppointment = entity.RequiresAppointment;
        dto.IsEmergencyService = entity.IsEmergencyService;
        dto.RequiresPreAuthorization = entity.RequiresPreAuthorization;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static FacilityService ToEntity(this CreateFacilityServiceDto dto, Guid tenantId, Guid userId)
    {
        return new FacilityService
        {
            TenantId = tenantId,
            FacilityId = dto.FacilityId,
            Name = dto.Name,
            ServiceType = dto.ServiceType,
            EstimatedCost = dto.EstimatedCost,
            RequiresAppointment = dto.RequiresAppointment,
            IsEmergencyService = dto.IsEmergencyService,
            RequiresPreAuthorization = dto.RequiresPreAuthorization,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this FacilityService entity, UpdateFacilityServiceDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.ServiceType = dto.ServiceType;
        entity.EstimatedCost = dto.EstimatedCost;
        entity.RequiresAppointment = dto.RequiresAppointment;
        entity.IsEmergencyService = dto.IsEmergencyService;
        entity.RequiresPreAuthorization = dto.RequiresPreAuthorization;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<FacilityServiceDto> ToDtoList(this IEnumerable<FacilityService> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL INSURANCE PROVIDER
    // ========================================================================

    #region MedicalInsuranceProvider

    private static T MapMedicalInsuranceProviderCore<T>(MedicalInsuranceProvider entity) where T : MedicalInsuranceProviderDto, new()
    {
        var dto = new T();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.Name = entity.Name;
        dto.ShortName = entity.ShortName;
        dto.Code = entity.Code;
        dto.ProviderType = entity.ProviderType;
        dto.LicenseNumber = entity.LicenseNumber;
        dto.LicenseExpiryDate = entity.LicenseExpiryDate;
        dto.Address = entity.Address;
        dto.City = entity.City;
        dto.PostalCode = entity.PostalCode;
        dto.CountryId = entity.CountryId;
        dto.CountryName = entity.Country?.Name;
        dto.PrimaryPhone = entity.PrimaryPhone;
        dto.ClaimsHotline = entity.ClaimsHotline;
        dto.Email = entity.Email;
        dto.ClaimsEmail = entity.ClaimsEmail;
        dto.Website = entity.Website;
        dto.HasOnlinePortal = entity.HasOnlinePortal;
        dto.ClaimsPortalUrl = entity.ClaimsPortalUrl;
        dto.StandardProcessingDays = entity.StandardProcessingDays;
        dto.EmergencyProcessingDays = entity.EmergencyProcessingDays;
        dto.ClaimSubmissionDeadlineDays = entity.ClaimSubmissionDeadlineDays;
        dto.ClaimSubmissionProcess = entity.ClaimSubmissionProcess;
        dto.PreferredPaymentMethod = entity.PreferredPaymentMethod;
        dto.BankId = entity.BankId;
        dto.BankName = entity.Bank?.Name;
        dto.BranchId = entity.BranchId;
        dto.BranchName = entity.Branch?.Name;
        dto.AccountNumber = entity.AccountNumber;
        dto.AccountName = entity.AccountName;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalInsuranceProviderDto ToDto(this MedicalInsuranceProvider entity)
        => MapMedicalInsuranceProviderCore<MedicalInsuranceProviderDto>(entity);

    public static MedicalInsuranceProviderDetailDto ToDetailDto(this MedicalInsuranceProvider entity)
    {
        var dto = MapMedicalInsuranceProviderCore<MedicalInsuranceProviderDetailDto>(entity);
        dto.Plans = entity.Plans.Select(p => p.ToDto()).ToList();
        dto.NetworkFacilities = entity.ProviderFacilities.Select(pf => pf.ToDto()).ToList();
        dto.Documents = entity.Documents.Select(d => d.ToDto()).ToList();
        return dto;
    }

    public static MedicalInsuranceProviderSummaryDto ToSummaryDto(this MedicalInsuranceProvider entity)
    {
        return new MedicalInsuranceProviderSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            ProviderType = entity.ProviderType,
            PrimaryPhone = entity.PrimaryPhone,
            IsActive = entity.IsActive,
            PlanCount = entity.Plans?.Count ?? 0,
        };
    }

    public static MedicalInsuranceProvider ToEntity(this CreateMedicalInsuranceProviderDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalInsuranceProvider
        {
            TenantId = tenantId,
            Name = dto.Name,
            ShortName = dto.ShortName,
            Code = dto.Code,
            ProviderType = dto.ProviderType,
            LicenseNumber = dto.LicenseNumber,
            LicenseExpiryDate = dto.LicenseExpiryDate,
            Address = dto.Address,
            City = dto.City,
            PostalCode = dto.PostalCode,
            CountryId = dto.CountryId,
            PrimaryPhone = dto.PrimaryPhone,
            ClaimsHotline = dto.ClaimsHotline,
            Email = dto.Email,
            ClaimsEmail = dto.ClaimsEmail,
            Website = dto.Website,
            HasOnlinePortal = dto.HasOnlinePortal,
            ClaimsPortalUrl = dto.ClaimsPortalUrl,
            StandardProcessingDays = dto.StandardProcessingDays,
            EmergencyProcessingDays = dto.EmergencyProcessingDays,
            ClaimSubmissionDeadlineDays = dto.ClaimSubmissionDeadlineDays,
            ClaimSubmissionProcess = dto.ClaimSubmissionProcess,
            PreferredPaymentMethod = dto.PreferredPaymentMethod,
            BankId = dto.BankId,
            BranchId = dto.BranchId,
            AccountNumber = dto.AccountNumber,
            AccountName = dto.AccountName,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalInsuranceProvider entity, UpdateMedicalInsuranceProviderDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.ShortName = dto.ShortName;
        entity.Code = dto.Code;
        entity.ProviderType = dto.ProviderType;
        entity.LicenseNumber = dto.LicenseNumber;
        entity.LicenseExpiryDate = dto.LicenseExpiryDate;
        entity.Address = dto.Address;
        entity.City = dto.City;
        entity.PostalCode = dto.PostalCode;
        entity.CountryId = dto.CountryId;
        entity.PrimaryPhone = dto.PrimaryPhone;
        entity.ClaimsHotline = dto.ClaimsHotline;
        entity.Email = dto.Email;
        entity.ClaimsEmail = dto.ClaimsEmail;
        entity.Website = dto.Website;
        entity.HasOnlinePortal = dto.HasOnlinePortal;
        entity.ClaimsPortalUrl = dto.ClaimsPortalUrl;
        entity.StandardProcessingDays = dto.StandardProcessingDays;
        entity.EmergencyProcessingDays = dto.EmergencyProcessingDays;
        entity.ClaimSubmissionDeadlineDays = dto.ClaimSubmissionDeadlineDays;
        entity.ClaimSubmissionProcess = dto.ClaimSubmissionProcess;
        entity.PreferredPaymentMethod = dto.PreferredPaymentMethod;
        entity.BankId = dto.BankId;
        entity.BranchId = dto.BranchId;
        entity.AccountNumber = dto.AccountNumber;
        entity.AccountName = dto.AccountName;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalInsuranceProviderSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalInsuranceProvider> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalInsuranceProviderDto> ToDtoList(this IEnumerable<MedicalInsuranceProvider> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL INSURANCE PLAN
    // ========================================================================

    #region MedicalInsurancePlan

    public static MedicalInsurancePlanDto ToDto(this MedicalInsurancePlan entity)
    {
        var dto = new MedicalInsurancePlanDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.MedicalInsuranceProviderId = entity.MedicalInsuranceProviderId;
        dto.ProviderName = entity.MedicalInsuranceProvider?.Name ?? string.Empty;
        dto.Name = entity.Name;
        dto.Code = entity.Code;
        dto.PlanType = entity.PlanType;
        dto.Description = entity.Description;
        dto.AnnualLimit = entity.AnnualLimit;
        dto.LifetimeLimit = entity.LifetimeLimit;
        dto.OutpatientLimit = entity.OutpatientLimit;
        dto.InpatientLimit = entity.InpatientLimit;
        dto.DentalLimit = entity.DentalLimit;
        dto.OpticalLimit = entity.OpticalLimit;
        dto.MaternityLimit = entity.MaternityLimit;
        dto.MentalHealthLimit = entity.MentalHealthLimit;
        dto.PrescriptionLimit = entity.PrescriptionLimit;
        dto.CoversDependents = entity.CoversDependents;
        dto.MaxDependents = entity.MaxDependents;
        dto.MaxChildAge = entity.MaxChildAge;
        dto.MonthlyPremium = entity.MonthlyPremium;
        dto.AnnualPremium = entity.AnnualPremium;
        dto.EmployerContributionPercent = entity.EmployerContributionPercent;
        dto.EmployeeContributionPercent = entity.EmployeeContributionPercent;
        dto.EffectiveDate = entity.EffectiveDate;
        dto.ExpiryDate = entity.ExpiryDate;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalInsurancePlanSummaryDto ToSummaryDto(this MedicalInsurancePlan entity)
    {
        return new MedicalInsurancePlanSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            ProviderName = entity.MedicalInsuranceProvider?.Name ?? string.Empty,
            PlanType = entity.PlanType,
            AnnualLimit = entity.AnnualLimit,
            IsActive = entity.IsActive,
        };
    }

    public static MedicalInsurancePlan ToEntity(this CreateMedicalInsurancePlanDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalInsurancePlan
        {
            TenantId = tenantId,
            MedicalInsuranceProviderId = dto.MedicalInsuranceProviderId,
            Name = dto.Name,
            Code = dto.Code,
            PlanType = dto.PlanType,
            Description = dto.Description,
            AnnualLimit = dto.AnnualLimit,
            LifetimeLimit = dto.LifetimeLimit,
            OutpatientLimit = dto.OutpatientLimit,
            InpatientLimit = dto.InpatientLimit,
            DentalLimit = dto.DentalLimit,
            OpticalLimit = dto.OpticalLimit,
            MaternityLimit = dto.MaternityLimit,
            MentalHealthLimit = dto.MentalHealthLimit,
            PrescriptionLimit = dto.PrescriptionLimit,
            CoversDependents = dto.CoversDependents,
            MaxDependents = dto.MaxDependents,
            MaxChildAge = dto.MaxChildAge,
            MonthlyPremium = dto.MonthlyPremium,
            AnnualPremium = dto.AnnualPremium,
            EmployerContributionPercent = dto.EmployerContributionPercent,
            EmployeeContributionPercent = dto.EmployeeContributionPercent,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalInsurancePlan entity, UpdateMedicalInsurancePlanDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.PlanType = dto.PlanType;
        entity.Description = dto.Description;
        entity.AnnualLimit = dto.AnnualLimit;
        entity.LifetimeLimit = dto.LifetimeLimit;
        entity.OutpatientLimit = dto.OutpatientLimit;
        entity.InpatientLimit = dto.InpatientLimit;
        entity.DentalLimit = dto.DentalLimit;
        entity.OpticalLimit = dto.OpticalLimit;
        entity.MaternityLimit = dto.MaternityLimit;
        entity.MentalHealthLimit = dto.MentalHealthLimit;
        entity.PrescriptionLimit = dto.PrescriptionLimit;
        entity.CoversDependents = dto.CoversDependents;
        entity.MaxDependents = dto.MaxDependents;
        entity.MaxChildAge = dto.MaxChildAge;
        entity.MonthlyPremium = dto.MonthlyPremium;
        entity.AnnualPremium = dto.AnnualPremium;
        entity.EmployerContributionPercent = dto.EmployerContributionPercent;
        entity.EmployeeContributionPercent = dto.EmployeeContributionPercent;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalInsurancePlanSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalInsurancePlan> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalInsurancePlanDto> ToDtoList(this IEnumerable<MedicalInsurancePlan> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // EMPLOYEE MEDICAL INSURANCE POLICY
    // ========================================================================

    #region EmployeeMedicalInsurancePolicy

    private static T MapEmployeeMedicalInsurancePolicyCore<T>(EmployeeMedicalInsurancePolicy entity)
        where T : EmployeeMedicalInsurancePolicyDto, new()
    {
        var dto = new T();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.EmployeeId = entity.EmployeeId;
        dto.EmployeeName = entity.Employee?.FullName ?? string.Empty;
        dto.EmployeeNumber = entity.Employee?.EmployeeNumber;
        dto.ProviderId = entity.ProviderId;
        dto.ProviderName = entity.MedicalInsuranceProvider?.Name ?? string.Empty;
        dto.PlanId = entity.PlanId;
        dto.PlanName = entity.MedicalInsurancePlan?.Name ?? string.Empty;
        dto.BenefitTierId = entity.BenefitTierId;
        dto.BenefitTierName = entity.BenefitTier?.TierName;
        dto.PolicyNumber = entity.PolicyNumber;
        dto.MembershipNumber = entity.MembershipNumber;
        dto.StartDate = entity.StartDate;
        dto.EndDate = entity.EndDate;
        dto.AnnualLimit = entity.AnnualLimit;
        dto.UtilizedAmount = entity.UtilizedAmount;
        dto.CoversDependents = entity.CoversDependents;
        dto.Status = entity.Status;
        dto.IsActive = entity.IsActive;
        dto.CancellationDate = entity.CancellationDate;
        dto.CancellationReason = entity.CancellationReason;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static EmployeeMedicalInsurancePolicyDto ToDto(this EmployeeMedicalInsurancePolicy entity)
        => MapEmployeeMedicalInsurancePolicyCore<EmployeeMedicalInsurancePolicyDto>(entity);

    public static EmployeeMedicalInsurancePolicyDetailDto ToDetailDto(this EmployeeMedicalInsurancePolicy entity)
    {
        var dto = MapEmployeeMedicalInsurancePolicyCore<EmployeeMedicalInsurancePolicyDetailDto>(entity);
        dto.Dependents = entity.Dependents.Select(d => d.ToDto()).ToList();
        dto.RecentClaims = entity.ExpenseClaims.Select(c => c.ToSummaryDto()).ToList();
        dto.InsuranceClaims = entity.InsuranceClaims.Select(c => c.ToSummaryDto()).ToList();
        dto.PremiumRecords = entity.PremiumRecords.Select(r => r.ToSummaryDto()).ToList();
        return dto;
    }

    public static EmployeeMedicalInsurancePolicySummaryDto ToSummaryDto(this EmployeeMedicalInsurancePolicy entity)
    {
        return new EmployeeMedicalInsurancePolicySummaryDto
        {
            Id = entity.Id,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            ProviderName = entity.MedicalInsuranceProvider?.Name ?? string.Empty,
            PlanName = entity.MedicalInsurancePlan?.Name ?? string.Empty,
            PolicyNumber = entity.PolicyNumber,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            RemainingLimit = entity.RemainingLimit,
            Status = entity.Status,
            IsActive = entity.IsActive,
        };
    }

    public static EmployeeMedicalInsurancePolicy ToEntity(this CreateEmployeeMedicalInsurancePolicyDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeMedicalInsurancePolicy
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            ProviderId = dto.ProviderId,
            PlanId = dto.PlanId,
            BenefitTierId = dto.BenefitTierId,
            PolicyNumber = dto.PolicyNumber,
            MembershipNumber = dto.MembershipNumber,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            AnnualLimit = dto.AnnualLimit,
            UtilizedAmount = 0,
            CoversDependents = dto.CoversDependents,
            Status = MedicalInsurancePolicyStatus.Active,
            IsActive = true,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeMedicalInsurancePolicy entity, UpdateEmployeeMedicalInsurancePolicyDto dto, Guid userId)
    {
        entity.PolicyNumber = dto.PolicyNumber;
        entity.MembershipNumber = dto.MembershipNumber;
        entity.BenefitTierId = dto.BenefitTierId;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.AnnualLimit = dto.AnnualLimit;
        entity.CoversDependents = dto.CoversDependents;
        entity.Status = dto.Status;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this CancelEmployeeMedicalInsurancePolicyDto dto, EmployeeMedicalInsurancePolicy entity, Guid userId)
    {
        entity.Status = MedicalInsurancePolicyStatus.Cancelled;
        entity.IsActive = false;
        entity.CancellationDate = dto.CancellationDate;
        entity.CancellationReason = dto.CancellationReason;
        entity.EndDate ??= dto.CancellationDate;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeMedicalInsurancePolicySummaryDto> ToSummaryDtoList(this IEnumerable<EmployeeMedicalInsurancePolicy> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<EmployeeMedicalInsurancePolicyDto> ToDtoList(this IEnumerable<EmployeeMedicalInsurancePolicy> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL INSURANCE POLICY DEPENDENT
    // ========================================================================

    #region MedicalInsurancePolicyDependent

    public static MedicalInsurancePolicyDependentDto ToDto(this MedicalInsurancePolicyDependent entity)
    {
        var dto = new MedicalInsurancePolicyDependentDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.PolicyId = entity.PolicyId;
        dto.PolicyNumber = entity.Policy?.PolicyNumber ?? string.Empty;
        dto.DependentId = entity.DependentId;
        dto.DependentName = FormatDependentName(entity.Dependent);
        dto.Relationship = FormatDependentRelationship(entity.Dependent);
        dto.MembershipNumber = entity.MembershipNumber;
        dto.CoverageStartDate = entity.CoverageStartDate;
        dto.CoverageEndDate = entity.CoverageEndDate;
        dto.AnnualLimit = entity.AnnualLimit;
        dto.UtilizedAmount = entity.UtilizedAmount;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalInsurancePolicyDependent ToEntity(this AddMedicalInsurancePolicyDependentDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalInsurancePolicyDependent
        {
            TenantId = tenantId,
            PolicyId = dto.PolicyId,
            DependentId = dto.DependentId,
            MembershipNumber = dto.MembershipNumber,
            CoverageStartDate = dto.CoverageStartDate,
            CoverageEndDate = dto.CoverageEndDate,
            AnnualLimit = dto.AnnualLimit,
            UtilizedAmount = 0,
            IsActive = true,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalInsurancePolicyDependent entity, UpdateMedicalInsurancePolicyDependentDto dto, Guid userId)
    {
        entity.MembershipNumber = dto.MembershipNumber;
        entity.CoverageEndDate = dto.CoverageEndDate;
        entity.AnnualLimit = dto.AnnualLimit;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalInsurancePolicyDependentDto> ToDtoList(this IEnumerable<MedicalInsurancePolicyDependent> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL INSURANCE CLAIM
    // ========================================================================

    #region MedicalInsuranceClaim

    public static MedicalInsuranceClaimDto ToDto(this MedicalInsuranceClaim entity)
    {
        var dto = new MedicalInsuranceClaimDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.PolicyId = entity.PolicyId;
        dto.PolicyNumber = entity.Policy?.PolicyNumber ?? string.Empty;
        dto.MedicalExpenseClaimId = entity.MedicalExpenseClaimId;
        dto.MedicalClaimNumber = entity.MedicalExpenseClaim?.ClaimNumber ?? string.Empty;
        dto.InsuranceClaimNumber = entity.InsuranceClaimNumber;
        dto.SubmissionDate = entity.SubmissionDate;
        dto.ClaimedAmount = entity.ClaimedAmount;
        dto.ApprovedAmount = entity.ApprovedAmount;
        dto.PaidAmount = entity.PaidAmount;
        dto.CoPayAmount = entity.CoPayAmount;
        dto.Status = entity.Status;
        dto.ApprovalDate = entity.ApprovalDate;
        dto.RejectionDate = entity.RejectionDate;
        dto.RejectionReason = entity.RejectionReason;
        dto.PaymentDate = entity.PaymentDate;
        dto.PaymentReference = entity.PaymentReference;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalInsuranceClaimSummaryDto ToSummaryDto(this MedicalInsuranceClaim entity)
    {
        return new MedicalInsuranceClaimSummaryDto
        {
            Id = entity.Id,
            InsuranceClaimNumber = entity.InsuranceClaimNumber,
            MedicalClaimNumber = entity.MedicalExpenseClaim?.ClaimNumber ?? string.Empty,
            PolicyNumber = entity.Policy?.PolicyNumber ?? string.Empty,
            SubmissionDate = entity.SubmissionDate,
            ClaimedAmount = entity.ClaimedAmount,
            ApprovedAmount = entity.ApprovedAmount,
            Status = entity.Status,
        };
    }

    public static MedicalInsuranceClaim ToEntity(this CreateMedicalInsuranceClaimDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalInsuranceClaim
        {
            TenantId = tenantId,
            PolicyId = dto.PolicyId,
            MedicalExpenseClaimId = dto.MedicalExpenseClaimId,
            InsuranceClaimNumber = dto.InsuranceClaimNumber,
            SubmissionDate = DateTime.UtcNow,
            ClaimedAmount = dto.ClaimedAmount,
            CoPayAmount = dto.CoPayAmount,
            Status = MedicalInsuranceClaimStatus.Submitted,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void ApplyTo(this UpdateMedicalInsuranceClaimStatusDto dto, MedicalInsuranceClaim entity, Guid userId)
    {
        entity.Status = dto.Status;
        entity.ApprovedAmount = dto.ApprovedAmount;
        entity.RejectionReason = dto.RejectionReason;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;

        if (dto.Status == MedicalInsuranceClaimStatus.Approved
            || dto.Status == MedicalInsuranceClaimStatus.PartiallyApproved)
        {
            entity.ApprovalDate = DateTime.UtcNow;

            // Patient co-pay is the shortfall the insurer did not cover. Computed once if not set by the insurer.
            if (dto.ApprovedAmount.HasValue && !entity.CoPayAmount.HasValue)
            {
                var coPay = entity.ClaimedAmount - dto.ApprovedAmount.Value;
                entity.CoPayAmount = coPay > 0m ? coPay : 0m;
            }
        }

        if (dto.Status == MedicalInsuranceClaimStatus.Rejected)
            entity.RejectionDate = DateTime.UtcNow;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this RecordMedicalInsuranceClaimPaymentDto dto, MedicalInsuranceClaim entity, Guid userId)
    {
        entity.PaidAmount = dto.PaidAmount;
        entity.PaymentReference = dto.PaymentReference;
        entity.PaymentDate = dto.PaymentDate;
        entity.Status = MedicalInsuranceClaimStatus.Paid;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalInsuranceClaimSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalInsuranceClaim> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalInsuranceClaimDto> ToDtoList(this IEnumerable<MedicalInsuranceClaim> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL INSURANCE PROVIDER FACILITY
    // ========================================================================

    #region MedicalInsuranceProviderFacility

    public static MedicalInsuranceProviderFacilityDto ToDto(this MedicalInsuranceProviderFacility entity)
    {
        var dto = new MedicalInsuranceProviderFacilityDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ProviderId = entity.ProviderId;
        dto.ProviderName = entity.MedicalInsuranceProvider?.Name ?? string.Empty;
        dto.FacilityId = entity.FacilityId;
        dto.FacilityName = entity.Facility?.FacilityName ?? string.Empty;
        dto.FacilityCity = entity.Facility?.City;
        dto.EffectiveDate = entity.EffectiveDate;
        dto.ExpiryDate = entity.ExpiryDate;
        dto.IsPreferredProvider = entity.IsPreferredProvider;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalInsuranceProviderFacilitySummaryDto ToSummaryDto(this MedicalInsuranceProviderFacility entity)
    {
        return new MedicalInsuranceProviderFacilitySummaryDto
        {
            Id = entity.Id,
            ProviderName = entity.MedicalInsuranceProvider?.Name ?? string.Empty,
            FacilityName = entity.Facility?.FacilityName ?? string.Empty,
            IsPreferredProvider = entity.IsPreferredProvider,
            IsActive = entity.IsActive,
        };
    }

    public static MedicalInsuranceProviderFacility ToEntity(this AddMedicalInsuranceProviderFacilityDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalInsuranceProviderFacility
        {
            TenantId = tenantId,
            ProviderId = dto.ProviderId,
            FacilityId = dto.FacilityId,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            IsPreferredProvider = dto.IsPreferredProvider,
            IsActive = true,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalInsuranceProviderFacility entity, UpdateMedicalInsuranceProviderFacilityDto dto, Guid userId)
    {
        entity.ExpiryDate = dto.ExpiryDate;
        entity.IsPreferredProvider = dto.IsPreferredProvider;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalInsuranceProviderFacilitySummaryDto> ToSummaryDtoList(this IEnumerable<MedicalInsuranceProviderFacility> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalInsuranceProviderFacilityDto> ToDtoList(this IEnumerable<MedicalInsuranceProviderFacility> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL INSURANCE PROVIDER DOCUMENT
    // ========================================================================

    #region MedicalInsuranceProviderDocument

    public static MedicalInsuranceProviderDocumentDto ToDto(this MedicalInsuranceProviderDocument entity)
    {
        var dto = new MedicalInsuranceProviderDocumentDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ProviderId = entity.ProviderId;
        dto.ProviderName = entity.MedicalInsuranceProvider?.Name ?? string.Empty;
        dto.FileName = entity.FileName;
        dto.FilePath = entity.FilePath;
        dto.FileUploadRecordId = entity.FileUploadRecordId;
        dto.DocumentRecordId = entity.DocumentRecordId;
        dto.DocumentVersionId = entity.DocumentVersionId;
        dto.DocumentType = entity.DocumentType;
        dto.Description = entity.Description;
        dto.UploadDate = entity.UploadDate;
        dto.ExpiryDate = entity.ExpiryDate;
        dto.IsActive = entity.IsActive;
        return dto;
    }

    public static MedicalInsuranceProviderDocument ToEntity(this CreateMedicalInsuranceProviderDocumentDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalInsuranceProviderDocument
        {
            TenantId = tenantId,
            ProviderId = dto.ProviderId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            FileUploadRecordId = dto.FileUploadRecordId,
            DocumentRecordId = dto.DocumentRecordId,
            DocumentVersionId = dto.DocumentVersionId,
            DocumentType = dto.DocumentType,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            ExpiryDate = dto.ExpiryDate,
            IsActive = true,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<MedicalInsuranceProviderDocumentDto> ToDtoList(this IEnumerable<MedicalInsuranceProviderDocument> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL INSURANCE PREMIUM RECORD
    // ========================================================================

    #region MedicalInsurancePremiumRecord

    public static MedicalInsurancePremiumRecordDto ToDto(this MedicalInsurancePremiumRecord entity)
    {
        var dto = new MedicalInsurancePremiumRecordDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ProviderId = entity.ProviderId;
        dto.ProviderName = entity.MedicalInsuranceProvider?.Name ?? string.Empty;
        dto.PlanId = entity.PlanId;
        dto.PlanName = entity.MedicalInsurancePlan?.Name ?? string.Empty;
        dto.PolicyId = entity.PolicyId;
        dto.PolicyNumber = entity.Policy?.PolicyNumber;
        dto.BillingPeriodStart = entity.BillingPeriodStart;
        dto.BillingPeriodEnd = entity.BillingPeriodEnd;
        dto.TotalPremiumAmount = entity.TotalPremiumAmount;
        dto.EmployerContribution = entity.EmployerContribution;
        dto.EmployeeContribution = entity.EmployeeContribution;
        dto.CoveredLivesCount = entity.CoveredLivesCount;
        dto.Status = entity.Status;
        dto.DueDate = entity.DueDate;
        dto.PaymentDate = entity.PaymentDate;
        dto.PaymentReference = entity.PaymentReference;
        dto.PaymentMethod = entity.PaymentMethod;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalInsurancePremiumRecordSummaryDto ToSummaryDto(this MedicalInsurancePremiumRecord entity)
    {
        return new MedicalInsurancePremiumRecordSummaryDto
        {
            Id = entity.Id,
            ProviderName = entity.MedicalInsuranceProvider?.Name ?? string.Empty,
            PlanName = entity.MedicalInsurancePlan?.Name ?? string.Empty,
            BillingPeriodStart = entity.BillingPeriodStart,
            BillingPeriodEnd = entity.BillingPeriodEnd,
            TotalPremiumAmount = entity.TotalPremiumAmount,
            Status = entity.Status,
        };
    }

    public static MedicalInsurancePremiumRecord ToEntity(this CreateMedicalInsurancePremiumRecordDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalInsurancePremiumRecord
        {
            TenantId = tenantId,
            ProviderId = dto.ProviderId,
            PlanId = dto.PlanId,
            PolicyId = dto.PolicyId,
            BillingPeriodStart = dto.BillingPeriodStart,
            BillingPeriodEnd = dto.BillingPeriodEnd,
            TotalPremiumAmount = dto.TotalPremiumAmount,
            EmployerContribution = dto.EmployerContribution,
            EmployeeContribution = dto.EmployeeContribution,
            CoveredLivesCount = dto.CoveredLivesCount,
            Status = MedicalInsurancePremiumPaymentStatus.Pending,
            DueDate = dto.DueDate,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void ApplyTo(this RecordMedicalInsurancePremiumPaymentDto dto, MedicalInsurancePremiumRecord entity, Guid userId)
    {
        entity.PaymentMethod = dto.PaymentMethod;
        entity.PaymentReference = dto.PaymentReference;
        entity.PaymentDate = dto.PaymentDate;
        entity.Status = MedicalInsurancePremiumPaymentStatus.Paid;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalInsurancePremiumRecordSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalInsurancePremiumRecord> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalInsurancePremiumRecordDto> ToDtoList(this IEnumerable<MedicalInsurancePremiumRecord> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL BENEFIT SCHEME
    // ========================================================================

    #region MedicalBenefitScheme

    private static T MapMedicalBenefitSchemeCore<T>(MedicalBenefitScheme entity) where T : MedicalBenefitSchemeDto, new()
    {
        var dto = new T();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.Name = entity.Name;
        dto.Code = entity.Code;
        dto.Description = entity.Description;
        dto.EffectiveDate = entity.EffectiveDate;
        dto.ExpiryDate = entity.ExpiryDate;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        dto.Tiers = entity.Tiers.Select(t => t.ToDto()).ToList();
        return dto;
    }

    public static MedicalBenefitSchemeDto ToDto(this MedicalBenefitScheme entity)
        => MapMedicalBenefitSchemeCore<MedicalBenefitSchemeDto>(entity);

    public static MedicalBenefitSchemeDetailDto ToDetailDto(this MedicalBenefitScheme entity)
        => MapMedicalBenefitSchemeCore<MedicalBenefitSchemeDetailDto>(entity);

    public static MedicalBenefitSchemeSummaryDto ToSummaryDto(this MedicalBenefitScheme entity)
    {
        return new MedicalBenefitSchemeSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            EffectiveDate = entity.EffectiveDate,
            IsActive = entity.IsActive,
            TierCount = entity.Tiers?.Count ?? 0,
        };
    }

    public static MedicalBenefitScheme ToEntity(this CreateMedicalBenefitSchemeDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalBenefitScheme
        {
            TenantId = tenantId,
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalBenefitScheme entity, UpdateMedicalBenefitSchemeDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.Description = dto.Description;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalBenefitSchemeSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalBenefitScheme> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalBenefitSchemeDto> ToDtoList(this IEnumerable<MedicalBenefitScheme> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL BENEFIT TIER
    // ========================================================================

    #region MedicalBenefitTier

    public static MedicalBenefitTierDto ToDto(this MedicalBenefitTier entity)
    {
        var dto = new MedicalBenefitTierDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.SchemeId = entity.SchemeId;
        dto.SchemeName = entity.Scheme?.Name ?? string.Empty;
        dto.TierName = entity.TierName;
        dto.StaffLevelId = entity.StaffLevelId;
        dto.StaffLevelName = entity.StaffLevel?.Name;
        dto.TierDescription = entity.TierDescription;
        dto.AnnualLimit = entity.AnnualLimit;
        dto.InpatientLimit = entity.InpatientLimit;
        dto.OutpatientLimit = entity.OutpatientLimit;
        dto.DentalLimit = entity.DentalLimit;
        dto.OpticalLimit = entity.OpticalLimit;
        dto.MaternityLimit = entity.MaternityLimit;
        dto.MentalHealthLimit = entity.MentalHealthLimit;
        dto.PrescriptionLimit = entity.PrescriptionLimit;
        dto.CoversDependents = entity.CoversDependents;
        dto.MaxDependents = entity.MaxDependents;
        dto.DependentAnnualLimit = entity.DependentAnnualLimit;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalBenefitTierSummaryDto ToSummaryDto(this MedicalBenefitTier entity)
    {
        return new MedicalBenefitTierSummaryDto
        {
            Id = entity.Id,
            TierName = entity.TierName,
            StaffLevelName = entity.StaffLevel?.Name,
            AnnualLimit = entity.AnnualLimit,
            IsActive = entity.IsActive,
        };
    }

    public static MedicalBenefitTier ToEntity(this CreateMedicalBenefitTierDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalBenefitTier
        {
            TenantId = tenantId,
            SchemeId = dto.SchemeId,
            TierName = dto.TierName,
            StaffLevelId = dto.StaffLevelId,
            TierDescription = dto.TierDescription,
            AnnualLimit = dto.AnnualLimit,
            InpatientLimit = dto.InpatientLimit,
            OutpatientLimit = dto.OutpatientLimit,
            DentalLimit = dto.DentalLimit,
            OpticalLimit = dto.OpticalLimit,
            MaternityLimit = dto.MaternityLimit,
            MentalHealthLimit = dto.MentalHealthLimit,
            PrescriptionLimit = dto.PrescriptionLimit,
            CoversDependents = dto.CoversDependents,
            MaxDependents = dto.MaxDependents,
            DependentAnnualLimit = dto.DependentAnnualLimit,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalBenefitTier entity, UpdateMedicalBenefitTierDto dto, Guid userId)
    {
        entity.TierName = dto.TierName;
        entity.StaffLevelId = dto.StaffLevelId;
        entity.TierDescription = dto.TierDescription;
        entity.AnnualLimit = dto.AnnualLimit;
        entity.InpatientLimit = dto.InpatientLimit;
        entity.OutpatientLimit = dto.OutpatientLimit;
        entity.DentalLimit = dto.DentalLimit;
        entity.OpticalLimit = dto.OpticalLimit;
        entity.MaternityLimit = dto.MaternityLimit;
        entity.MentalHealthLimit = dto.MentalHealthLimit;
        entity.PrescriptionLimit = dto.PrescriptionLimit;
        entity.CoversDependents = dto.CoversDependents;
        entity.MaxDependents = dto.MaxDependents;
        entity.DependentAnnualLimit = dto.DependentAnnualLimit;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalBenefitTierSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalBenefitTier> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalBenefitTierDto> ToDtoList(this IEnumerable<MedicalBenefitTier> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // EMPLOYEE HEALTH PROFILE
    // ========================================================================

    #region EmployeeHealthProfile

    private static T MapEmployeeHealthProfileCore<T>(EmployeeHealthProfile entity) where T : EmployeeHealthProfileDto, new()
    {
        var dto = new T();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.EmployeeId = entity.EmployeeId;
        dto.EmployeeName = entity.Employee?.FullName ?? string.Empty;
        dto.EmployeeNumber = entity.Employee?.EmployeeNumber;
        dto.BloodGroup = entity.BloodGroup;
        dto.HeightCm = entity.HeightCm;
        dto.WeightKg = entity.WeightKg;
        dto.DisabilityStatus = entity.DisabilityStatus;
        dto.DisabilityDescription = entity.DisabilityDescription;
        dto.EmergencyContactName = entity.EmergencyContactName;
        dto.EmergencyContactPhone = entity.EmergencyContactPhone;
        dto.EmergencyContactRelationship = entity.EmergencyContactRelationship;
        dto.PreferredFacilityId = entity.PreferredFacilityId;
        dto.PreferredFacilityName = entity.PreferredFacility?.FacilityName;
        dto.PreferredPhysicianId = entity.PreferredPhysicianId;
        dto.PreferredPhysicianName = FormatPhysicianName(entity.PreferredPhysician);
        dto.LastUpdated = entity.LastUpdated;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static EmployeeHealthProfileDto ToDto(this EmployeeHealthProfile entity)
        => MapEmployeeHealthProfileCore<EmployeeHealthProfileDto>(entity);

    public static EmployeeHealthProfileDetailDto ToDetailDto(this EmployeeHealthProfile entity)
    {
        var dto = MapEmployeeHealthProfileCore<EmployeeHealthProfileDetailDto>(entity);
        dto.Conditions = entity.Conditions.Select(c => c.ToDto()).ToList();
        dto.Allergies = entity.Allergies.Select(a => a.ToDto()).ToList();
        dto.MedicalExams = entity.MedicalExams.Select(e => e.ToSummaryDto()).ToList();
        return dto;
    }

    public static EmployeeHealthProfile ToEntity(this CreateEmployeeHealthProfileDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeHealthProfile
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            BloodGroup = dto.BloodGroup,
            HeightCm = dto.HeightCm,
            WeightKg = dto.WeightKg,
            DisabilityStatus = dto.DisabilityStatus,
            DisabilityDescription = dto.DisabilityDescription,
            EmergencyContactName = dto.EmergencyContactName,
            EmergencyContactPhone = dto.EmergencyContactPhone,
            EmergencyContactRelationship = dto.EmergencyContactRelationship,
            PreferredFacilityId = dto.PreferredFacilityId,
            PreferredPhysicianId = dto.PreferredPhysicianId,
            LastUpdated = DateTime.UtcNow,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeHealthProfile entity, UpdateEmployeeHealthProfileDto dto, Guid userId)
    {
        entity.BloodGroup = dto.BloodGroup;
        entity.HeightCm = dto.HeightCm;
        entity.WeightKg = dto.WeightKg;
        entity.DisabilityStatus = dto.DisabilityStatus;
        entity.DisabilityDescription = dto.DisabilityDescription;
        entity.EmergencyContactName = dto.EmergencyContactName;
        entity.EmergencyContactPhone = dto.EmergencyContactPhone;
        entity.EmergencyContactRelationship = dto.EmergencyContactRelationship;
        entity.PreferredFacilityId = dto.PreferredFacilityId;
        entity.PreferredPhysicianId = dto.PreferredPhysicianId;
        entity.LastUpdated = DateTime.UtcNow;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeHealthProfileDto> ToDtoList(this IEnumerable<EmployeeHealthProfile> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // EMPLOYEE HEALTH CONDITION
    // ========================================================================

    #region EmployeeHealthCondition

    public static EmployeeHealthConditionDto ToDto(this EmployeeHealthCondition entity)
    {
        var dto = new EmployeeHealthConditionDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.HealthProfileId = entity.HealthProfileId;
        dto.ConditionName = entity.ConditionName;
        dto.ICDCode = entity.ICDCode;
        dto.Severity = entity.Severity;
        dto.Status = entity.Status;
        dto.DiagnosedDate = entity.DiagnosedDate;
        dto.ResolvedDate = entity.ResolvedDate;
        dto.TreatmentSummary = entity.TreatmentSummary;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static EmployeeHealthCondition ToEntity(this CreateEmployeeHealthConditionDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeHealthCondition
        {
            TenantId = tenantId,
            HealthProfileId = dto.HealthProfileId,
            ConditionName = dto.ConditionName,
            ICDCode = dto.ICDCode,
            Severity = dto.Severity,
            Status = dto.Status,
            DiagnosedDate = dto.DiagnosedDate,
            ResolvedDate = dto.ResolvedDate,
            TreatmentSummary = dto.TreatmentSummary,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeHealthCondition entity, UpdateEmployeeHealthConditionDto dto, Guid userId)
    {
        entity.ConditionName = dto.ConditionName;
        entity.ICDCode = dto.ICDCode;
        entity.Severity = dto.Severity;
        entity.Status = dto.Status;
        entity.DiagnosedDate = dto.DiagnosedDate;
        entity.ResolvedDate = dto.ResolvedDate;
        entity.TreatmentSummary = dto.TreatmentSummary;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeHealthConditionDto> ToDtoList(this IEnumerable<EmployeeHealthCondition> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // EMPLOYEE ALLERGY
    // ========================================================================

    #region EmployeeAllergy

    public static EmployeeAllergyDto ToDto(this EmployeeAllergy entity)
    {
        var dto = new EmployeeAllergyDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.HealthProfileId = entity.HealthProfileId;
        dto.Allergen = entity.Allergen;
        dto.AllergyType = entity.AllergyType;
        dto.Severity = entity.Severity;
        dto.ReactionDescription = entity.ReactionDescription;
        dto.ManagementPlan = entity.ManagementPlan;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static EmployeeAllergy ToEntity(this CreateEmployeeAllergyDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeAllergy
        {
            TenantId = tenantId,
            HealthProfileId = dto.HealthProfileId,
            Allergen = dto.Allergen,
            AllergyType = dto.AllergyType,
            Severity = dto.Severity,
            ReactionDescription = dto.ReactionDescription,
            ManagementPlan = dto.ManagementPlan,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeAllergy entity, UpdateEmployeeAllergyDto dto, Guid userId)
    {
        entity.Allergen = dto.Allergen;
        entity.AllergyType = dto.AllergyType;
        entity.Severity = dto.Severity;
        entity.ReactionDescription = dto.ReactionDescription;
        entity.ManagementPlan = dto.ManagementPlan;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeAllergyDto> ToDtoList(this IEnumerable<EmployeeAllergy> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // EMPLOYEE MEDICAL EXAM
    // ========================================================================

    #region EmployeeMedicalExam

    private static T MapEmployeeMedicalExamCore<T>(EmployeeMedicalExam entity) where T : EmployeeMedicalExamDto, new()
    {
        var dto = new T();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.HealthProfileId = entity.HealthProfileId;
        dto.ExamDate = entity.ExamDate;
        dto.FacilityId = entity.FacilityId;
        dto.FacilityName = entity.Facility?.FacilityName;
        dto.PhysicianId = entity.PhysicianId;
        dto.PhysicianName = FormatPhysicianName(entity.Physician);
        dto.HeightCm = entity.HeightCm;
        dto.WeightKg = entity.WeightKg;
        dto.BloodPressure = entity.BloodPressure;
        dto.BMIRecorded = entity.BMIRecorded;
        dto.VisionResult = entity.VisionResult;
        dto.HearingResult = entity.HearingResult;
        dto.Result = entity.Result;
        dto.Findings = entity.Findings;
        dto.Recommendations = entity.Recommendations;
        dto.Restrictions = entity.Restrictions;
        dto.NextExamDueDate = entity.NextExamDueDate;
        dto.LinkedClaimId = entity.LinkedClaimId;
        dto.LinkedClaimNumber = entity.LinkedClaim?.ClaimNumber;
        dto.Notes = entity.Notes;
        dto.Documents = entity.Documents.Select(d => d.ToDto()).ToList();
        return dto;
    }

    public static EmployeeMedicalExamDto ToDto(this EmployeeMedicalExam entity)
        => MapEmployeeMedicalExamCore<EmployeeMedicalExamDto>(entity);

    public static EmployeeMedicalExamDetailDto ToDetailDto(this EmployeeMedicalExam entity)
        => MapEmployeeMedicalExamCore<EmployeeMedicalExamDetailDto>(entity);

    public static EmployeeMedicalExamSummaryDto ToSummaryDto(this EmployeeMedicalExam entity)
    {
        return new EmployeeMedicalExamSummaryDto
        {
            Id = entity.Id,
            ExamDate = entity.ExamDate,
            FacilityName = entity.Facility?.FacilityName,
            Result = entity.Result,
            NextExamDueDate = entity.NextExamDueDate,
        };
    }

    public static EmployeeMedicalExam ToEntity(this CreateEmployeeMedicalExamDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeMedicalExam
        {
            TenantId = tenantId,
            HealthProfileId = dto.HealthProfileId,
            ExamDate = dto.ExamDate,
            FacilityId = dto.FacilityId,
            PhysicianId = dto.PhysicianId,
            HeightCm = dto.HeightCm,
            WeightKg = dto.WeightKg,
            BloodPressure = dto.BloodPressure,
            BMIRecorded = dto.BMIRecorded,
            VisionResult = dto.VisionResult,
            HearingResult = dto.HearingResult,
            Result = dto.Result,
            Findings = dto.Findings,
            Recommendations = dto.Recommendations,
            Restrictions = dto.Restrictions,
            NextExamDueDate = dto.NextExamDueDate,
            LinkedClaimId = dto.LinkedClaimId,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeMedicalExam entity, UpdateEmployeeMedicalExamDto dto, Guid userId)
    {
        entity.ExamDate = dto.ExamDate;
        entity.FacilityId = dto.FacilityId;
        entity.PhysicianId = dto.PhysicianId;
        entity.HeightCm = dto.HeightCm;
        entity.WeightKg = dto.WeightKg;
        entity.BloodPressure = dto.BloodPressure;
        entity.BMIRecorded = dto.BMIRecorded;
        entity.VisionResult = dto.VisionResult;
        entity.HearingResult = dto.HearingResult;
        entity.Result = dto.Result;
        entity.Findings = dto.Findings;
        entity.Recommendations = dto.Recommendations;
        entity.Restrictions = dto.Restrictions;
        entity.NextExamDueDate = dto.NextExamDueDate;
        entity.LinkedClaimId = dto.LinkedClaimId;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeMedicalExamSummaryDto> ToSummaryDtoList(this IEnumerable<EmployeeMedicalExam> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<EmployeeMedicalExamDto> ToDtoList(this IEnumerable<EmployeeMedicalExam> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // EMPLOYEE MEDICAL EXAM DOCUMENT
    // ========================================================================

    #region EmployeeMedicalExamDocument

    public static EmployeeMedicalExamDocumentDto ToDto(this EmployeeMedicalExamDocument entity)
    {
        var dto = new EmployeeMedicalExamDocumentDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ExamId = entity.ExamId;
        dto.FileName = entity.FileName;
        dto.FilePath = entity.FilePath;
        dto.Description = entity.Description;
        dto.UploadDate = entity.UploadDate;
        return dto;
    }

    public static EmployeeMedicalExamDocument ToEntity(this CreateEmployeeMedicalExamDocumentDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeMedicalExamDocument
        {
            TenantId = tenantId,
            ExamId = dto.ExamId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            FileUploadRecordId = dto.FileUploadRecordId,
            DocumentRecordId = dto.DocumentRecordId,
            DocumentVersionId = dto.DocumentVersionId,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<EmployeeMedicalExamDocumentDto> ToDtoList(this IEnumerable<EmployeeMedicalExamDocument> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL CLAIM PRE-AUTHORIZATION
    // ========================================================================

    #region MedicalClaimPreAuthorization

    public static MedicalClaimPreAuthorizationDto ToDto(this MedicalClaimPreAuthorization entity)
    {
        var dto = new MedicalClaimPreAuthorizationDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.AuthorizationNumber = entity.AuthorizationNumber;
        dto.EmployeeId = entity.EmployeeId;
        dto.EmployeeName = entity.Employee?.FullName ?? string.Empty;
        dto.DependentId = entity.DependentId;
        dto.DependentName = entity.DependentId.HasValue ? FormatDependentName(entity.Dependent) : null;
        dto.PolicyId = entity.PolicyId;
        dto.PolicyNumber = entity.Policy?.PolicyNumber ?? string.Empty;
        dto.FacilityId = entity.FacilityId;
        dto.FacilityName = entity.Facility?.FacilityName;
        dto.PhysicianId = entity.PhysicianId;
        dto.PhysicianName = FormatPhysicianName(entity.Physician);
        dto.ServiceType = entity.ServiceType;
        dto.IsEmergency = entity.IsEmergency;
        dto.Diagnosis = entity.Diagnosis;
        dto.ProposedTreatment = entity.ProposedTreatment;
        dto.PlannedServiceDate = entity.PlannedServiceDate;
        dto.EstimatedCost = entity.EstimatedCost;
        dto.RequestDate = entity.RequestDate;
        dto.Status = entity.Status;
        dto.ApprovedBy = entity.ApprovedBy;
        dto.ApprovedByName = entity.Approver?.FullName;
        dto.AuthorizedAmount = entity.AuthorizedAmount;
        dto.AuthorizationDate = entity.AuthorizationDate;
        dto.ExpiryDate = entity.ExpiryDate;
        dto.RejectionReason = entity.RejectionReason;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalClaimPreAuthorizationSummaryDto ToSummaryDto(this MedicalClaimPreAuthorization entity)
    {
        return new MedicalClaimPreAuthorizationSummaryDto
        {
            Id = entity.Id,
            AuthorizationNumber = entity.AuthorizationNumber,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            ServiceType = entity.ServiceType,
            Status = entity.Status,
            PlannedServiceDate = entity.PlannedServiceDate,
            EstimatedCost = entity.EstimatedCost,
        };
    }

    public static MedicalClaimPreAuthorization ToEntity(this CreateMedicalClaimPreAuthorizationDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalClaimPreAuthorization
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            DependentId = dto.DependentId,
            PolicyId = dto.PolicyId,
            FacilityId = dto.FacilityId,
            PhysicianId = dto.PhysicianId,
            ServiceType = dto.ServiceType,
            IsEmergency = dto.IsEmergency,
            Diagnosis = dto.Diagnosis,
            ProposedTreatment = dto.ProposedTreatment,
            PlannedServiceDate = dto.PlannedServiceDate,
            EstimatedCost = dto.EstimatedCost,
            RequestDate = DateTime.UtcNow,
            Status = ClaimPreAuthorizationStatus.Draft,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalClaimPreAuthorization entity, UpdateMedicalClaimPreAuthorizationDto dto, Guid userId)
    {
        entity.FacilityId = dto.FacilityId;
        entity.PhysicianId = dto.PhysicianId;
        entity.ServiceType = dto.ServiceType;
        entity.IsEmergency = dto.IsEmergency;
        entity.Diagnosis = dto.Diagnosis;
        entity.ProposedTreatment = dto.ProposedTreatment;
        entity.PlannedServiceDate = dto.PlannedServiceDate;
        entity.EstimatedCost = dto.EstimatedCost;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this ApproveMedicalClaimPreAuthorizationDto dto, MedicalClaimPreAuthorization entity, Guid userId)
    {
        entity.ApprovedBy = dto.ApprovedBy;
        entity.AuthorizedAmount = dto.AuthorizedAmount;
        entity.AuthorizationDate = DateTime.UtcNow;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.Status = ClaimPreAuthorizationStatus.Approved;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        // D-16: a transition is a write. Without these two lines the row after it is
        // indistinguishable from the row before as to who moved it, and none of these three
        // entities carries a domain actor FK to fall back on.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    /// <remarks>
    /// ⚠ The rejector is recorded in <c>UpdatedBy</c> only. <c>ApprovedBy</c> is deliberately left
    /// alone: it is the approver's field, and writing it here would make a rejected authorization
    /// read as approved-by-that-person on every screen that binds it. A dedicated rejector column
    /// needs a migration and belongs with the pre-authorization edit build, not with this fix.
    /// </remarks>
    public static void ApplyTo(this RejectMedicalClaimPreAuthorizationDto dto, MedicalClaimPreAuthorization entity, Guid userId)
    {
        entity.RejectionReason = dto.RejectionReason;
        entity.Status = ClaimPreAuthorizationStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        // D-16: a transition is a write. Without these two lines the row after it is
        // indistinguishable from the row before as to who moved it, and none of these three
        // entities carries a domain actor FK to fall back on.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalClaimPreAuthorizationSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalClaimPreAuthorization> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalClaimPreAuthorizationDto> ToDtoList(this IEnumerable<MedicalClaimPreAuthorization> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL REFERRAL
    // ========================================================================

    #region MedicalReferral

    public static MedicalReferralDto ToDto(this MedicalReferral entity)
    {
        var dto = new MedicalReferralDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ReferralNumber = entity.ReferralNumber;
        dto.EmployeeId = entity.EmployeeId;
        dto.EmployeeName = entity.Employee?.FullName ?? string.Empty;
        dto.IsForDependent = entity.IsForDependent;
        dto.DependentId = entity.DependentId;
        dto.DependentName = entity.IsForDependent ? FormatDependentName(entity.Dependent) : null;
        dto.ReferringFacilityId = entity.ReferringFacilityId;
        dto.ReferringFacilityName = entity.ReferringFacility?.FacilityName;
        dto.ReferringPhysicianId = entity.ReferringPhysicianId;
        dto.ReferringPhysicianName = FormatPhysicianName(entity.ReferringPhysician);
        dto.ReferredToFacilityId = entity.ReferredToFacilityId;
        dto.ReferredToFacilityName = entity.ReferredToFacility?.FacilityName;
        dto.ReferredToPhysicianId = entity.ReferredToPhysicianId;
        dto.ReferredToPhysicianName = FormatPhysicianName(entity.ReferredToPhysician);
        dto.ReferralDate = entity.ReferralDate;
        dto.ExpiryDate = entity.ExpiryDate;
        dto.Priority = entity.Priority;
        dto.Diagnosis = entity.Diagnosis;
        dto.ReasonForReferral = entity.ReasonForReferral;
        dto.Status = entity.Status;
        dto.CompletedDate = entity.CompletedDate;
        dto.OutcomeSummary = entity.OutcomeSummary;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalReferralSummaryDto ToSummaryDto(this MedicalReferral entity)
    {
        return new MedicalReferralSummaryDto
        {
            Id = entity.Id,
            ReferralNumber = entity.ReferralNumber,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            Priority = entity.Priority,
            Status = entity.Status,
            ReferralDate = entity.ReferralDate,
        };
    }

    public static MedicalReferral ToEntity(this CreateMedicalReferralDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalReferral
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            IsForDependent = dto.IsForDependent,
            DependentId = dto.DependentId,
            ReferringFacilityId = dto.ReferringFacilityId,
            ReferringPhysicianId = dto.ReferringPhysicianId,
            ReferredToFacilityId = dto.ReferredToFacilityId,
            ReferredToPhysicianId = dto.ReferredToPhysicianId,
            ReferralDate = dto.ReferralDate,
            ExpiryDate = dto.ExpiryDate,
            Priority = dto.Priority,
            Diagnosis = dto.Diagnosis,
            ReasonForReferral = dto.ReasonForReferral,
            Status = MedicalReferralStatus.Pending,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalReferral entity, UpdateMedicalReferralDto dto, Guid userId)
    {
        entity.ReferredToFacilityId = dto.ReferredToFacilityId;
        entity.ReferredToPhysicianId = dto.ReferredToPhysicianId;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.Priority = dto.Priority;
        entity.Diagnosis = dto.Diagnosis;
        entity.ReasonForReferral = dto.ReasonForReferral;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this UpdateMedicalReferralStatusDto dto, MedicalReferral entity, Guid userId)
    {
        entity.Status = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.OutcomeSummary))
            entity.OutcomeSummary = dto.OutcomeSummary;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        // D-16: a transition is a write. Without these two lines the row after it is
        // indistinguishable from the row before as to who moved it, and none of these three
        // entities carries a domain actor FK to fall back on.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this CompleteMedicalReferralDto dto, MedicalReferral entity, Guid userId)
    {
        entity.Status = MedicalReferralStatus.Completed;
        entity.CompletedDate = dto.CompletedDate;
        entity.OutcomeSummary = dto.OutcomeSummary;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        // D-16: a transition is a write. Without these two lines the row after it is
        // indistinguishable from the row before as to who moved it, and none of these three
        // entities carries a domain actor FK to fall back on.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalReferralSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalReferral> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalReferralDto> ToDtoList(this IEnumerable<MedicalReferral> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL APPOINTMENT
    // ========================================================================

    #region MedicalAppointment

    public static MedicalAppointmentDto ToDto(this MedicalAppointment entity)
    {
        var dto = new MedicalAppointmentDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.AppointmentNumber = entity.AppointmentNumber;
        dto.EmployeeId = entity.EmployeeId;
        dto.EmployeeName = entity.Employee?.FullName ?? string.Empty;
        dto.IsForDependent = entity.IsForDependent;
        dto.DependentId = entity.DependentId;
        dto.DependentName = entity.IsForDependent ? FormatDependentName(entity.Dependent) : null;
        dto.FacilityId = entity.FacilityId;
        dto.FacilityName = entity.Facility?.FacilityName ?? string.Empty;
        dto.PhysicianId = entity.PhysicianId;
        dto.PhysicianName = FormatPhysicianName(entity.Physician);
        dto.AppointmentDateTime = entity.AppointmentDateTime;
        dto.DurationMinutes = entity.DurationMinutes;
        dto.ServiceType = entity.ServiceType;
        dto.Purpose = entity.Purpose;
        dto.Status = entity.Status;
        dto.CheckInTime = entity.CheckInTime;
        dto.CheckOutTime = entity.CheckOutTime;
        dto.OutcomeSummary = entity.OutcomeSummary;
        dto.CancellationReason = entity.CancellationReason;
        dto.LinkedReferralId = entity.LinkedReferralId;
        dto.LinkedReferralNumber = entity.LinkedReferral?.ReferralNumber;
        dto.LinkedClaimId = entity.LinkedClaimId;
        dto.LinkedClaimNumber = entity.LinkedClaim?.ClaimNumber;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static MedicalAppointmentSummaryDto ToSummaryDto(this MedicalAppointment entity)
    {
        return new MedicalAppointmentSummaryDto
        {
            Id = entity.Id,
            AppointmentNumber = entity.AppointmentNumber,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            FacilityName = entity.Facility?.FacilityName ?? string.Empty,
            AppointmentDateTime = entity.AppointmentDateTime,
            Status = entity.Status,
        };
    }

    public static MedicalAppointment ToEntity(this CreateMedicalAppointmentDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalAppointment
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            IsForDependent = dto.IsForDependent,
            DependentId = dto.DependentId,
            FacilityId = dto.FacilityId,
            PhysicianId = dto.PhysicianId,
            AppointmentDateTime = dto.AppointmentDateTime,
            DurationMinutes = dto.DurationMinutes,
            ServiceType = dto.ServiceType,
            Purpose = dto.Purpose,
            Status = MedicalAppointmentStatus.Draft,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalAppointment entity, UpdateMedicalAppointmentDto dto, Guid userId)
    {
        entity.PhysicianId = dto.PhysicianId;
        entity.AppointmentDateTime = dto.AppointmentDateTime;
        entity.DurationMinutes = dto.DurationMinutes;
        entity.ServiceType = dto.ServiceType;
        entity.Purpose = dto.Purpose;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this UpdateMedicalAppointmentStatusDto dto, MedicalAppointment entity, Guid userId)
    {
        entity.Status = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.OutcomeSummary))
            entity.OutcomeSummary = dto.OutcomeSummary;
        if (!string.IsNullOrWhiteSpace(dto.CancellationReason))
            entity.CancellationReason = dto.CancellationReason;
        // D-16: a transition is a write. Without these two lines the row after it is
        // indistinguishable from the row before as to who moved it, and none of these three
        // entities carries a domain actor FK to fall back on.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this CancelMedicalAppointmentDto dto, MedicalAppointment entity, Guid userId)
    {
        entity.Status = MedicalAppointmentStatus.Cancelled;
        entity.CancellationReason = dto.CancellationReason;
        // D-16: a transition is a write. Without these two lines the row after it is
        // indistinguishable from the row before as to who moved it, and none of these three
        // entities carries a domain actor FK to fall back on.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this CheckInMedicalAppointmentDto dto, MedicalAppointment entity, Guid userId)
    {
        entity.Status = MedicalAppointmentStatus.CheckedIn;
        entity.CheckInTime = dto.CheckInTime;
        // D-16: a transition is a write. Without these two lines the row after it is
        // indistinguishable from the row before as to who moved it, and none of these three
        // entities carries a domain actor FK to fall back on.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this CheckOutMedicalAppointmentDto dto, MedicalAppointment entity, Guid userId)
    {
        entity.Status = MedicalAppointmentStatus.Completed;
        entity.CheckOutTime = dto.CheckOutTime;
        if (!string.IsNullOrWhiteSpace(dto.OutcomeSummary))
            entity.OutcomeSummary = dto.OutcomeSummary;
        // D-16: a transition is a write. Without these two lines the row after it is
        // indistinguishable from the row before as to who moved it, and none of these three
        // entities carries a domain actor FK to fall back on.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalAppointmentSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalAppointment> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalAppointmentDto> ToDtoList(this IEnumerable<MedicalAppointment> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // NHIS CLAIM
    // ========================================================================

    #region NHISClaim

    private static T MapNHISClaimCore<T>(NHISClaim entity) where T : NHISClaimDto, new()
    {
        var dto = new T();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ClaimNumber = entity.ClaimNumber;
        dto.EmployeeId = entity.EmployeeId;
        dto.EmployeeName = entity.Employee?.FullName ?? string.Empty;
        dto.IsForDependent = entity.IsForDependent;
        dto.DependentId = entity.DependentId;
        dto.DependentName = entity.IsForDependent ? FormatDependentName(entity.Dependent) : null;
        dto.NHISMembershipNumber = entity.NHISMembershipNumber;
        dto.FacilityId = entity.FacilityId;
        dto.FacilityName = entity.Facility?.FacilityName ?? string.Empty;
        dto.PhysicianId = entity.PhysicianId;
        dto.PhysicianName = FormatPhysicianName(entity.Physician);
        dto.ServiceDate = entity.ServiceDate;
        dto.ServiceType = entity.ServiceType;
        dto.ServiceDescription = entity.ServiceDescription;
        dto.Diagnosis = entity.Diagnosis;
        dto.ICDCode = entity.ICDCode;
        dto.TotalCost = entity.TotalCost;
        dto.NHISCoveredAmount = entity.NHISCoveredAmount;
        dto.CoPayAmount = entity.CoPayAmount;
        dto.BatchNumber = entity.BatchNumber;
        dto.SubmissionDate = entity.SubmissionDate;
        dto.Status = entity.Status;
        dto.ApprovalDate = entity.ApprovalDate;
        dto.ApprovedAmount = entity.ApprovedAmount;
        dto.RejectionDate = entity.RejectionDate;
        dto.RejectionReason = entity.RejectionReason;
        dto.PaymentDate = entity.PaymentDate;
        dto.PaymentReference = entity.PaymentReference;
        dto.LinkedMedicalClaimId = entity.LinkedMedicalClaimId;
        dto.LinkedMedicalClaimNumber = entity.LinkedMedicalClaim?.ClaimNumber;
        dto.Notes = entity.Notes;
        return dto;
    }

    public static NHISClaimDto ToDto(this NHISClaim entity)
        => MapNHISClaimCore<NHISClaimDto>(entity);

    public static NHISClaimDetailDto ToDetailDto(this NHISClaim entity)
    {
        var dto = MapNHISClaimCore<NHISClaimDetailDto>(entity);
        dto.Documents = entity.Documents.Select(d => d.ToDto()).ToList();
        return dto;
    }

    public static NHISClaimSummaryDto ToSummaryDto(this NHISClaim entity)
    {
        return new NHISClaimSummaryDto
        {
            Id = entity.Id,
            ClaimNumber = entity.ClaimNumber,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            ServiceDate = entity.ServiceDate,
            TotalCost = entity.TotalCost,
            Status = entity.Status,
        };
    }

    public static NHISClaim ToEntity(this CreateNHISClaimDto dto, Guid tenantId, Guid userId)
    {
        return new NHISClaim
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            IsForDependent = dto.IsForDependent,
            DependentId = dto.DependentId,
            NHISMembershipNumber = dto.NHISMembershipNumber,
            FacilityId = dto.FacilityId,
            PhysicianId = dto.PhysicianId,
            ServiceDate = dto.ServiceDate,
            ServiceType = dto.ServiceType,
            ServiceDescription = dto.ServiceDescription,
            Diagnosis = dto.Diagnosis,
            ICDCode = dto.ICDCode,
            TotalCost = dto.TotalCost,
            NHISCoveredAmount = dto.NHISCoveredAmount,
            CoPayAmount = dto.CoPayAmount,
            LinkedMedicalClaimId = dto.LinkedMedicalClaimId,
            Status = NHISClaimStatus.Draft,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this NHISClaim entity, UpdateNHISClaimDto dto, Guid userId)
    {
        entity.NHISMembershipNumber = dto.NHISMembershipNumber;
        entity.PhysicianId = dto.PhysicianId;
        entity.ServiceDate = dto.ServiceDate;
        entity.ServiceType = dto.ServiceType;
        entity.ServiceDescription = dto.ServiceDescription;
        entity.Diagnosis = dto.Diagnosis;
        entity.ICDCode = dto.ICDCode;
        entity.TotalCost = dto.TotalCost;
        entity.NHISCoveredAmount = dto.NHISCoveredAmount;
        entity.CoPayAmount = dto.CoPayAmount;
        entity.BatchNumber = dto.BatchNumber;
        entity.LinkedMedicalClaimId = dto.LinkedMedicalClaimId;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this UpdateNHISClaimStatusDto dto, NHISClaim entity, Guid userId)
    {
        entity.Status = dto.Status;
        entity.ApprovedAmount = dto.ApprovedAmount;
        entity.RejectionReason = dto.RejectionReason;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;

        if (dto.Status == NHISClaimStatus.Approved || dto.Status == NHISClaimStatus.PartiallyApproved)
            entity.ApprovalDate = DateTime.UtcNow;

        if (dto.Status == NHISClaimStatus.Rejected)
            entity.RejectionDate = DateTime.UtcNow;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this SubmitNHISClaimDto dto, NHISClaim entity, Guid userId)
    {
        entity.BatchNumber = dto.BatchNumber;
        entity.SubmissionDate = dto.SubmissionDate;
        entity.Status = NHISClaimStatus.Submitted;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this RecordNHISClaimPaymentDto dto, NHISClaim entity, Guid userId)
    {
        entity.PaymentReference = dto.PaymentReference;
        entity.PaymentDate = dto.PaymentDate;
        entity.Status = NHISClaimStatus.Paid;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<NHISClaimSummaryDto> ToSummaryDtoList(this IEnumerable<NHISClaim> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<NHISClaimDto> ToDtoList(this IEnumerable<NHISClaim> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // NHIS CLAIM DOCUMENT
    // ========================================================================

    #region NHISClaimDocument

    public static NHISClaimDocumentDto ToDto(this NHISClaimDocument entity)
    {
        var dto = new NHISClaimDocumentDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.NHISClaimId = entity.NHISClaimId;
        dto.FileName = entity.FileName;
        dto.FilePath = entity.FilePath;
        dto.FileUploadRecordId = entity.FileUploadRecordId;
        dto.DocumentRecordId = entity.DocumentRecordId;
        dto.DocumentVersionId = entity.DocumentVersionId;
        dto.Description = entity.Description;
        dto.UploadDate = entity.UploadDate;
        return dto;
    }

    public static NHISClaimDocument ToEntity(this CreateNHISClaimDocumentDto dto, Guid tenantId, Guid userId)
    {
        return new NHISClaimDocument
        {
            TenantId = tenantId,
            NHISClaimId = dto.NHISClaimId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            FileUploadRecordId = dto.FileUploadRecordId,
            DocumentRecordId = dto.DocumentRecordId,
            DocumentVersionId = dto.DocumentVersionId,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<NHISClaimDocumentDto> ToDtoList(this IEnumerable<NHISClaimDocument> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL EXPENSE CLAIM
    // ========================================================================

    #region MedicalExpenseClaim

    private static T MapMedicalExpenseClaimCore<T>(MedicalExpenseClaim entity) where T : MedicalExpenseClaimDto, new()
    {
        var dto = new T();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ClaimNumber = entity.ClaimNumber;
        dto.EmployeeId = entity.EmployeeId;
        dto.EmployeeName = entity.Employee?.FullName ?? string.Empty;
        dto.EmployeeNumber = entity.Employee?.EmployeeNumber;
        dto.IsForDependent = entity.IsForDependent;
        dto.DependentId = entity.DependentId;
        dto.DependentName = entity.IsForDependent ? FormatDependentName(entity.Dependent) : null;
        dto.ClaimDate = entity.ClaimDate;
        dto.ServiceDate = entity.ServiceDate;
        dto.ServiceEndDate = entity.ServiceEndDate;
        dto.ExpenseType = entity.ExpenseType;
        dto.Description = entity.Description;
        dto.FacilityId = entity.FacilityId;
        dto.FacilityName = entity.Facility?.FacilityName ?? string.Empty;
        dto.PhysicianId = entity.PhysicianId;
        dto.PhysicianName = FormatPhysicianName(entity.Physician);
        dto.Diagnosis = entity.Diagnosis;
        dto.ICDCode = entity.ICDCode;
        dto.TreatmentReceived = entity.TreatmentReceived;
        dto.IsEmergency = entity.IsEmergency;
        dto.RequiredHospitalization = entity.RequiredHospitalization;
        dto.AdmissionStart = entity.AdmissionStart;
        dto.AdmissionEnd = entity.AdmissionEnd;
        dto.PreAuthorizationId = entity.PreAuthorizationId;
        dto.PreAuthorizationNumber = entity.PreAuthorization?.AuthorizationNumber;
        dto.ReferralId = entity.ReferralId;
        dto.ReferralNumber = entity.Referral?.ReferralNumber;
        dto.TotalAmount = entity.TotalAmount;
        dto.AmountRequested = entity.AmountRequested;
        dto.InsurancePolicyId = entity.InsurancePolicyId;
        dto.InsurancePolicyNumber = entity.InsurancePolicy?.PolicyNumber;
        dto.LeaveRequestId = entity.LeaveRequestId;
        dto.LeaveRequestNumber = entity.LeaveRequest?.RequestNumber;
        dto.Status = entity.Status;
        dto.AmountApproved = entity.AmountApproved;
        dto.PaymentProcessed = entity.PaymentProcessed;
        dto.PaymentDate = entity.PaymentDate;
        dto.PaymentReference = entity.PaymentReference;
        dto.PaymentMethod = entity.PaymentMethod;
        dto.IsFlaggedForReview = entity.IsFlaggedForReview;
        dto.FlagReason = entity.FlagReason;
        dto.AdditionalNotes = entity.AdditionalNotes;
        return dto;
    }

    public static MedicalExpenseClaimDto ToDto(this MedicalExpenseClaim entity)
        => MapMedicalExpenseClaimCore<MedicalExpenseClaimDto>(entity);

    public static MedicalExpenseClaimDetailDto ToDetailDto(this MedicalExpenseClaim entity)
    {
        var dto = MapMedicalExpenseClaimCore<MedicalExpenseClaimDetailDto>(entity);
        dto.Approvals = entity.Approvals.Select(a => a.ToDto()).ToList();
        dto.Documents = entity.Documents.Select(d => d.ToDto()).ToList();
        dto.Items = entity.Items.Select(i => i.ToDto()).ToList();
        dto.Notes = entity.Notes.Select(n => n.ToDto()).ToList();
        dto.InsuranceClaims = entity.InsuranceClaims.Select(c => c.ToSummaryDto()).ToList();
        dto.LinkedNHISClaims = entity.LinkedNHISClaims.Select(c => c.ToSummaryDto()).ToList();
        return dto;
    }

    public static MedicalExpenseClaimSummaryDto ToSummaryDto(this MedicalExpenseClaim entity)
    {
        return new MedicalExpenseClaimSummaryDto
        {
            Id = entity.Id,
            ClaimNumber = entity.ClaimNumber,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            IsForDependent = entity.IsForDependent,
            DependentName = entity.IsForDependent ? FormatDependentName(entity.Dependent) : null,
            ClaimDate = entity.ClaimDate,
            ServiceDate = entity.ServiceDate,
            ExpenseType = entity.ExpenseType,
            FacilityName = entity.Facility?.FacilityName ?? string.Empty,
            AmountRequested = entity.AmountRequested,
            AmountApproved = entity.AmountApproved,
            Status = entity.Status,
            IsFlaggedForReview = entity.IsFlaggedForReview,
        };
    }

    public static MedicalExpenseClaim ToEntity(this CreateMedicalExpenseClaimDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalExpenseClaim
        {
            TenantId = tenantId,
            IsForDependent = dto.IsForDependent,
            DependentId = dto.DependentId,
            ClaimDate = DateTime.UtcNow,
            ServiceDate = dto.ServiceDate,
            ServiceEndDate = dto.ServiceEndDate,
            ExpenseType = dto.ExpenseType,
            Description = dto.Description,
            FacilityId = dto.FacilityId,
            PhysicianId = dto.PhysicianId,
            Diagnosis = dto.Diagnosis,
            ICDCode = dto.ICDCode,
            TreatmentReceived = dto.TreatmentReceived,
            IsEmergency = dto.IsEmergency,
            RequiredHospitalization = dto.RequiredHospitalization,
            AdmissionStart = dto.AdmissionStart,
            AdmissionEnd = dto.AdmissionEnd,
            PreAuthorizationId = dto.PreAuthorizationId,
            ReferralId = dto.ReferralId,
            TotalAmount = dto.TotalAmount,
            AmountRequested = dto.AmountRequested,
            InsurancePolicyId = dto.InsurancePolicyId,
            LeaveRequestId = dto.LeaveRequestId,
            Status = ClaimStatus.Pending,
            AdditionalNotes = dto.AdditionalNotes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalExpenseClaim entity, UpdateMedicalExpenseClaimDto dto, Guid userId)
    {
        entity.ServiceDate = dto.ServiceDate;
        entity.ServiceEndDate = dto.ServiceEndDate;
        entity.ExpenseType = dto.ExpenseType;
        entity.Description = dto.Description;
        entity.FacilityId = dto.FacilityId;
        entity.PhysicianId = dto.PhysicianId;
        entity.Diagnosis = dto.Diagnosis;
        entity.ICDCode = dto.ICDCode;
        entity.TreatmentReceived = dto.TreatmentReceived;
        entity.IsEmergency = dto.IsEmergency;
        entity.RequiredHospitalization = dto.RequiredHospitalization;
        entity.AdmissionStart = dto.AdmissionStart;
        entity.AdmissionEnd = dto.AdmissionEnd;
        entity.PreAuthorizationId = dto.PreAuthorizationId;
        entity.ReferralId = dto.ReferralId;
        entity.TotalAmount = dto.TotalAmount;
        entity.AmountRequested = dto.AmountRequested;
        entity.InsurancePolicyId = dto.InsurancePolicyId;
        entity.LeaveRequestId = dto.LeaveRequestId;
        entity.AdditionalNotes = dto.AdditionalNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this FlagMedicalExpenseClaimDto dto, MedicalExpenseClaim entity, Guid userId)
    {
        entity.IsFlaggedForReview = true;
        entity.FlagReason = dto.FlagReason;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this UnflagMedicalExpenseClaimDto dto, MedicalExpenseClaim entity, Guid userId)
    {
        entity.IsFlaggedForReview = false;
        entity.FlagReason = null;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.AdditionalNotes = dto.Notes;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void ApplyTo(this ProcessMedicalExpensePaymentDto dto, MedicalExpenseClaim entity, Guid userId)
    {
        entity.PaymentProcessed = true;
        entity.PaymentMethod = dto.PaymentMethod;
        entity.PaymentReference = dto.PaymentReference;
        entity.PaymentDate = dto.PaymentDate;
        entity.Status = ClaimStatus.Paid;
        // D-16: a transition is a write. None of these entities carries a domain actor FK,
        // so UpdatedBy is the only record of who moved the row.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalExpenseClaimSummaryDto> ToSummaryDtoList(this IEnumerable<MedicalExpenseClaim> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<MedicalExpenseClaimDto> ToDtoList(this IEnumerable<MedicalExpenseClaim> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL EXPENSE APPROVAL
    // ========================================================================

    #region MedicalExpenseApproval

    public static MedicalExpenseApprovalDto ToDto(this MedicalExpenseApproval entity)
    {
        var dto = new MedicalExpenseApprovalDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ClaimId = entity.ClaimId;
        dto.ApproverId = entity.ApproverId;
        dto.ApproverName = entity.Approver?.FullName;
        dto.Status = entity.Status;
        dto.ActionDate = entity.ActionDate;
        dto.Comments = entity.Comments;
        return dto;
    }

    public static MedicalExpenseApproval ToEntity(this ProcessMedicalExpenseClaimDto dto, Guid tenantId, Guid userId, Guid approverId)
    {
        return new MedicalExpenseApproval
        {
            TenantId = tenantId,
            ClaimId = dto.ClaimId,
            ApproverId = approverId,
            Status = dto.Status,
            ActionDate = DateTime.UtcNow,
            Comments = dto.Comments,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<MedicalExpenseApprovalDto> ToDtoList(this IEnumerable<MedicalExpenseApproval> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL EXPENSE ITEM
    // ========================================================================

    #region MedicalExpenseItem

    public static MedicalExpenseItemDto ToDto(this MedicalExpenseItem entity)
    {
        var dto = new MedicalExpenseItemDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ClaimId = entity.ClaimId;
        dto.Description = entity.Description;
        dto.ItemType = entity.ItemType;
        dto.Quantity = entity.Quantity;
        dto.UnitCost = entity.UnitCost;
        dto.Remarks = entity.Remarks;
        return dto;
    }

    public static MedicalExpenseItem ToEntity(this CreateMedicalExpenseItemDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalExpenseItem
        {
            TenantId = tenantId,
            ClaimId = dto.ClaimId,
            Description = dto.Description,
            ItemType = dto.ItemType,
            Quantity = dto.Quantity,
            UnitCost = dto.UnitCost,
            Remarks = dto.Remarks,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MedicalExpenseItem entity, UpdateMedicalExpenseItemDto dto, Guid userId)
    {
        entity.ClaimId = dto.ClaimId;
        entity.Description = dto.Description;
        entity.ItemType = dto.ItemType;
        entity.Quantity = dto.Quantity;
        entity.UnitCost = dto.UnitCost;
        entity.Remarks = dto.Remarks;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MedicalExpenseItemDto> ToDtoList(this IEnumerable<MedicalExpenseItem> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL EXPENSE DOCUMENT
    // ========================================================================

    #region MedicalExpenseDocument

    public static MedicalExpenseDocumentDto ToDto(this MedicalExpenseDocument entity)
    {
        var dto = new MedicalExpenseDocumentDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ClaimId = entity.ClaimId;
        dto.FileName = entity.FileName;
        dto.FilePath = entity.FilePath;
        dto.Type = entity.Type;
        dto.Description = entity.Description;
        dto.UploadDate = entity.UploadDate;
        return dto;
    }

    public static MedicalExpenseDocument ToEntity(this CreateMedicalExpenseDocumentDto dto, Guid tenantId, Guid userId)
    {
        return new MedicalExpenseDocument
        {
            TenantId = tenantId,
            ClaimId = dto.ClaimId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            FileUploadRecordId = dto.FileUploadRecordId,
            DocumentRecordId = dto.DocumentRecordId,
            DocumentVersionId = dto.DocumentVersionId,
            Type = dto.Type,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<MedicalExpenseDocumentDto> ToDtoList(this IEnumerable<MedicalExpenseDocument> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // MEDICAL EXPENSE CLAIM NOTE
    // ========================================================================

    #region MedicalExpenseClaimNote

    public static MedicalExpenseClaimNoteDto ToDto(this MedicalExpenseClaimNote entity)
    {
        var dto = new MedicalExpenseClaimNoteDto();
        MapAuditFields(entity, dto);
        dto.TenantId = entity.TenantId;
        dto.ClaimId = entity.ClaimId;
        dto.AuthorId = entity.AuthorId;
        dto.AuthorName = entity.Author?.FullName ?? string.Empty;
        dto.NoteType = entity.NoteType;
        dto.Content = entity.Content;
        dto.IsInternal = entity.IsInternal;
        dto.NoteDate = entity.NoteDate;
        return dto;
    }

    public static MedicalExpenseClaimNote ToEntity(this AddMedicalExpenseClaimNoteDto dto, Guid tenantId, Guid userId, Guid authorId)
    {
        return new MedicalExpenseClaimNote
        {
            TenantId = tenantId,
            ClaimId = dto.ClaimId,
            AuthorId = authorId,
            NoteType = dto.NoteType,
            Content = dto.Content,
            IsInternal = dto.IsInternal,
            NoteDate = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<MedicalExpenseClaimNoteDto> ToDtoList(this IEnumerable<MedicalExpenseClaimNote> entities)
        => entities.Select(e => e.ToDto());

    #endregion
}
