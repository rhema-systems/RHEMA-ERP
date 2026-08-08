using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Extensions;

/// <summary>
/// Explicit mapping for the Employee aggregate and its sub-resources.
/// This keeps EmployeeService orchestration-focused and avoids AutoMapper usage.
/// </summary>
public static class EmployeeMappingExtensions
{
    #region Employee

    public static EmployeeDto ToSummaryDto(this Employee e)
        => new()
        {
            Id = e.Id,
            EmployeeNumber = e.EmployeeNumber,
            FirstName = e.FirstName,
            MiddleName = e.MiddleName,
            LastName = e.LastName,
            FullName = e.FullName,
            DisplayName = e.DisplayName,
            Title = e.Title,
            Gender = e.Gender,
            EmailAddress = e.EmailAddress,
            MobileNumber = e.MobileNumber,

            // Legacy display (back-compat)
            DepartmentName = e.Department?.Name ?? string.Empty,
            SectionName = e.Section?.Name,

            PositionTitle = e.Position?.Title ?? string.Empty,
            StaffLevelName = e.Position?.StaffLevel?.Name,

            // Preferred display
            OrganizationLevelName = e.OrganizationLevel?.Name,
            OrganizationUnitName = e.OrganizationUnit?.Name,
            LocationLevelName = e.LocationLevel?.Name,
            LocationId = e.LocationId,
            LocationName = e.Location?.Name,

            StaffStatus = e.StaffStatus,
            EmploymentType = e.EmploymentType,
            IsActive = e.IsActive,
            IsFullTime = e.IsFullTime,
            IsExpatriate = e.IsExpatriate,
            DateEmployed = e.DateEmployed,
            YearsOfService = e.YearsOfService,
            CanBeAssignedToMaintenance = e.CanBeAssignedToMaintenance,
            PicturePath = e.PicturePath
        };

    public static EmployeeDetailDto ToDetailDto(this Employee e)
        => new()
        {
            // Base summary fields
            Id = e.Id,
            EmployeeNumber = e.EmployeeNumber,
            FirstName = e.FirstName,
            MiddleName = e.MiddleName,
            LastName = e.LastName,
            FullName = e.FullName,
            DisplayName = e.DisplayName,
            Title = e.Title,
            Gender = e.Gender,
            EmailAddress = e.EmailAddress,
            MobileNumber = e.MobileNumber,

            DepartmentName = e.Department?.Name ?? string.Empty,
            SectionName = e.Section?.Name,
            PositionTitle = e.Position?.Title ?? string.Empty,
            StaffLevelName = e.Position?.StaffLevel?.Name,

            OrganizationLevelName = e.OrganizationLevel?.Name,
            OrganizationUnitName = e.OrganizationUnit?.Name,
            LocationLevelName = e.LocationLevel?.Name,
            LocationName = e.Location?.Name,

            StaffStatus = e.StaffStatus,
            EmploymentType = e.EmploymentType,
            IsActive = e.IsActive,
            IsFullTime = e.IsFullTime,
            IsExpatriate = e.IsExpatriate,
            DateEmployed = e.DateEmployed,
            YearsOfService = e.YearsOfService,
            CanBeAssignedToMaintenance = e.CanBeAssignedToMaintenance,
            PicturePath = e.PicturePath,

            // IDs for edit forms
            DepartmentId = e.DepartmentId,
            SectionId = e.SectionId,
            OrganizationLevelId = e.OrganizationLevelId,
            OrganizationUnitId = e.OrganizationUnitId,
            PositionId = e.PositionId,
            ManagerId = e.ManagerId,
            LocationLevelId = e.LocationLevelId,
            LocationId = e.LocationId,
            CountryId = e.CountryId,
            // Detail fields
            DateOfBirth = e.DateOfBirth,
            MaritalStatus = e.MaritalStatus,
            Religion = e.Religion,
            Address = e.Address,
            City = e.City,
            State = e.State,
            PostalCode = e.PostalCode,
            DigitalAddress = e.DigitalAddress,
            CountryName = e.Country?.Name,
            TelephoneNumber = e.TelephoneNumber,
            BusinessNumber = e.BusinessNumber,
            Extension = e.Extension,
            ProbationPeriodDays = e.ProbationPeriodDays,
            ConfirmationDate = e.ConfirmationDate,
            RetirementDate = e.RetirementDate,
            TaxNumber = e.TaxNumber,
            SocialSecurityNumber = e.SocialSecurityNumber,
            TINNumber = e.TINNumber,
            BloodType = e.BloodType,
            ShiftName = null, // Shift assignment now managed via ShiftAssignment entity
            Salary = e.Salary,
            PayTax = e.PayTax,
            SSFund = e.SSFund,
            GrossUp = e.GrossUp,
            Tier2Only = e.Tier2Only,
            Overtime = e.Overtime,
            BadgeNumber = e.BadgeNumber,
            Notes = e.Notes,
            LastPromotionDate = e.LastPromotionDate,
            LastReviewDate = e.LastReviewDate,
            NextReviewDate = e.NextReviewDate,
            StationName = e.Location?.Name,
            TerminationDate = e.TerminationDate,
            TerminationReason = e.TerminationReason?.ToString(),
            TerminationNotes = e.TerminationNotes,
            IsOnProbation = e.IsOnProbation,

            EmergencyContacts = e.EmergencyContacts.Select(ToDto).ToList(),
            Dependents = e.Dependents.Select(ToLegacyDto).ToList(),
            Qualifications = e.Qualifications.Select(ToDto).ToList(),
            Skills = e.Skills.Select(ToDto).ToList(),
            ContractDetails = e.ContractDetails.Select(ToDto).ToList()
        };

    public static EmployeeFullProfileDto ToFullProfileDto(this Employee e)
    {
        var dto = new EmployeeFullProfileDto();

        // Start from detail mapping
        var detail = e.ToDetailDto();
        Copy(detail, dto);

        dto.IdentificationCards = e.IdentificationCards.Select(ToListDto).ToList();
        dto.WorkHistories = e.WorkHistories.Select(ToListDto).ToList();
        dto.ExpatriateAssignments = e.ExpatriateAssignments.Select(ToListDto).ToList();
        dto.PositionHistories = e.PositionHistories.Select(ToListDto).ToList();
        dto.SalaryAssignments = e.SalaryAssignments.Select(ToListDto).ToList();
        dto.Referees = e.Referees.Select(ToListDto).ToList();
        dto.Guarantors = e.Guarantors.Select(ToListDto).ToList();

        dto.DependentBenefits = e.Dependents
            .SelectMany(d => d.EmployeeDependentBenefits)
            .Select(ToDto)
            .ToList();

        return dto;
    }

    private static void Copy(EmployeeDetailDto from, EmployeeFullProfileDto to)
    {
        // EmployeeDto fields
        to.Id = from.Id;
        to.EmployeeNumber = from.EmployeeNumber;
        to.FirstName = from.FirstName;
        to.MiddleName = from.MiddleName;
        to.LastName = from.LastName;
        to.FullName = from.FullName;
        to.DisplayName = from.DisplayName;
        to.Title = from.Title;
        to.Gender = from.Gender;
        to.EmailAddress = from.EmailAddress;
        to.MobileNumber = from.MobileNumber;
        to.DepartmentName = from.DepartmentName;
        to.SectionName = from.SectionName;
        to.PositionTitle = from.PositionTitle;
        to.OrganizationLevelName = from.OrganizationLevelName;
        to.OrganizationUnitName = from.OrganizationUnitName;
        to.LocationLevelName = from.LocationLevelName;
        to.LocationName = from.LocationName;
        to.StaffStatus = from.StaffStatus;
        to.EmploymentType = from.EmploymentType;
        to.IsActive = from.IsActive;
        to.IsFullTime = from.IsFullTime;
        to.IsExpatriate = from.IsExpatriate;
        to.DateEmployed = from.DateEmployed;
        to.YearsOfService = from.YearsOfService;
        to.CanBeAssignedToMaintenance = from.CanBeAssignedToMaintenance;
        to.PicturePath = from.PicturePath;

        // Detail fields
        to.DepartmentId = from.DepartmentId;
        to.SectionId = from.SectionId;
        to.OrganizationLevelId = from.OrganizationLevelId;
        to.OrganizationUnitId = from.OrganizationUnitId;
        to.PositionId = from.PositionId;
        to.ManagerId = from.ManagerId;
        to.LocationLevelId = from.LocationLevelId;
        to.LocationId = from.LocationId;
        to.CountryId = from.CountryId;
        to.ShiftId = from.ShiftId;

        to.DateOfBirth = from.DateOfBirth;
        to.MaritalStatus = from.MaritalStatus;
        to.Religion = from.Religion;
        to.Address = from.Address;
        to.City = from.City;
        to.State = from.State;
        to.PostalCode = from.PostalCode;
        to.DigitalAddress = from.DigitalAddress;
        to.CountryName = from.CountryName;
        to.TelephoneNumber = from.TelephoneNumber;
        to.BusinessNumber = from.BusinessNumber;
        to.Extension = from.Extension;
        to.ProbationPeriodDays = from.ProbationPeriodDays;
        to.ConfirmationDate = from.ConfirmationDate;
        to.RetirementDate = from.RetirementDate;
        to.TaxNumber = from.TaxNumber;
        to.TINNumber = from.TINNumber;
        to.BloodType = from.BloodType;
        to.ShiftName = from.ShiftName;
        to.Salary = from.Salary;
        to.PayTax = from.PayTax;
        to.SSFund = from.SSFund;
        to.GrossUp = from.GrossUp;
        to.Tier2Only = from.Tier2Only;
        to.Overtime = from.Overtime;
        to.BadgeNumber = from.BadgeNumber;
        to.Notes = from.Notes;
        to.LastPromotionDate = from.LastPromotionDate;
        to.LastReviewDate = from.LastReviewDate;
        to.NextReviewDate = from.NextReviewDate;
        to.StationName = from.StationName;
        to.TerminationDate = from.TerminationDate;
        to.TerminationReason = from.TerminationReason;
        to.TerminationNotes = from.TerminationNotes;
        to.IsOnProbation = from.IsOnProbation;

        to.EmergencyContacts = from.EmergencyContacts;
        to.Dependents = from.Dependents;
        to.Qualifications = from.Qualifications;
        to.Skills = from.Skills;
        to.ContractDetails = from.ContractDetails;
    }

    public static Employee ToEntity(this CreateEmployeeDto dto, string employeeNumber, Guid organizationLevelId, Guid locationLevelId)
        => new()
        {
            EmployeeNumber = employeeNumber,
            FirstName = dto.FirstName,
            MiddleName = dto.MiddleName,
            LastName = dto.LastName,
            Title = dto.Title,
            Gender = dto.Gender,
            DateOfBirth = dto.DateOfBirth,
            MaritalStatus = dto.MaritalStatus,
            Religion = dto.Religion,
            IsFullTime = dto.IsFullTime,
            DateEmployed = dto.DateEmployed,

            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            PostalCode = dto.PostalCode,
            DigitalAddress = dto.DigitalAddress,
            CountryId = dto.CountryId,
            EmailAddress = dto.EmailAddress,
            TelephoneNumber = dto.TelephoneNumber,
            BusinessNumber = dto.BusinessNumber,
            MobileNumber = dto.MobileNumber,
            Extension = dto.Extension,

            EmploymentType = dto.EmploymentType,
            ProbationPeriodDays = dto.ProbationPeriodDays,
            ConfirmationDate = dto.ConfirmationDate,
            RetirementDate = dto.RetirementDate,

            // Deprecated fields - convert Guid.Empty to null for backward compatibility
            DepartmentId = dto.DepartmentId == Guid.Empty ? null : dto.DepartmentId,
            SectionId = dto.SectionId == Guid.Empty ? null : dto.SectionId,
            OrganizationLevelId = organizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            StaffStatus = dto.StaffStatus,
            LocationLevelId = locationLevelId,
            LocationId = dto.LocationId,
            ManagerId = dto.ManagerId,

            TaxNumber = dto.TaxNumber,
            SocialSecurityNumber = dto.SocialSecurityNumber,
            TINNumber = dto.TINNumber,
            BloodType = dto.BloodType,
            Salary = dto.Salary,
            PayTax = dto.PayTax,
            SSFund = dto.SSFund,
            GrossUp = dto.GrossUp,
            Tier2Only = dto.Tier2Only,
            Overtime = dto.Overtime,
            BadgeNumber = dto.BadgeNumber,
            PicturePath = dto.PicturePath,
            Notes = dto.Notes,
            IsExpatriate = dto.IsExpatriate,
            IsActive = true
        };

    public static void Apply(this UpdateEmployeeDto dto, Employee e, Guid? organizationLevelId = null, Guid? locationLevelId = null)
    {
        if (!string.IsNullOrWhiteSpace(dto.EmployeeNumber))
            e.EmployeeNumber = dto.EmployeeNumber.Trim();

        if (!string.IsNullOrWhiteSpace(dto.FirstName))
            e.FirstName = dto.FirstName.Trim();

        if (dto.MiddleName != null)
            e.MiddleName = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.LastName))
            e.LastName = dto.LastName.Trim();

        if (dto.Title != null) e.Title = dto.Title;
        if (dto.Gender.HasValue) e.Gender = dto.Gender;
        if (dto.DateOfBirth.HasValue) e.DateOfBirth = dto.DateOfBirth;
        if (dto.MaritalStatus.HasValue) e.MaritalStatus = dto.MaritalStatus;
        if (dto.Religion != null) e.Religion = dto.Religion;
        if (dto.DateEmployed.HasValue) e.DateEmployed = dto.DateEmployed;

        e.IsFullTime = dto.IsFullTime;

        if (dto.Address != null) e.Address = dto.Address;
        if (dto.City != null) e.City = dto.City;
        if (dto.State != null) e.State = dto.State;
        if (dto.PostalCode != null) e.PostalCode = dto.PostalCode;
        if (dto.DigitalAddress != null) e.DigitalAddress = dto.DigitalAddress;
        if (dto.CountryId.HasValue) e.CountryId = dto.CountryId;

        if (!string.IsNullOrWhiteSpace(dto.EmailAddress)) e.EmailAddress = dto.EmailAddress.Trim();
        if (dto.TelephoneNumber != null) e.TelephoneNumber = dto.TelephoneNumber;
        if (dto.BusinessNumber != null) e.BusinessNumber = dto.BusinessNumber;
        if (dto.MobileNumber != null) e.MobileNumber = dto.MobileNumber;
        if (dto.Extension != null) e.Extension = dto.Extension;

        if (dto.EmploymentType.HasValue) e.EmploymentType = dto.EmploymentType.Value;
        if (dto.ProbationPeriodDays.HasValue) e.ProbationPeriodDays = dto.ProbationPeriodDays.Value;
        if (dto.ConfirmationDate.HasValue) e.ConfirmationDate = dto.ConfirmationDate;
        if (dto.RetirementDate.HasValue) e.RetirementDate = dto.RetirementDate;
        if (dto.DepartmentId.HasValue) e.DepartmentId = dto.DepartmentId.Value;
        if (dto.SectionId.HasValue) e.SectionId = dto.SectionId;
        if (dto.PositionId.HasValue) e.PositionId = dto.PositionId.Value;

        if (organizationLevelId.HasValue) e.OrganizationLevelId = organizationLevelId.Value;
        if (dto.OrganizationUnitId.HasValue) e.OrganizationUnitId = dto.OrganizationUnitId.Value;

        if (dto.StaffStatus.HasValue) e.StaffStatus = dto.StaffStatus.Value;

        if (locationLevelId.HasValue) e.LocationLevelId = locationLevelId.Value;
        if (dto.LocationId.HasValue) e.LocationId = dto.LocationId;
        if (dto.ManagerId.HasValue) e.ManagerId = dto.ManagerId;

        if (dto.TaxNumber != null) e.TaxNumber = dto.TaxNumber;
        if (dto.SocialSecurityNumber != null) e.SocialSecurityNumber = dto.SocialSecurityNumber;
        if (dto.TINNumber != null) e.TINNumber = dto.TINNumber;
        if (dto.BloodType.HasValue) e.BloodType = dto.BloodType;
        if (dto.Salary.HasValue) e.Salary = dto.Salary;

        if (dto.PayTax.HasValue) e.PayTax = dto.PayTax.Value;
        if (dto.SSFund.HasValue) e.SSFund = dto.SSFund.Value;
        if (dto.GrossUp.HasValue) e.GrossUp = dto.GrossUp.Value;
        if (dto.Tier2Only.HasValue) e.Tier2Only = dto.Tier2Only.Value;
        if (dto.Overtime.HasValue) e.Overtime = dto.Overtime.Value;

        if (dto.BadgeNumber != null) e.BadgeNumber = dto.BadgeNumber;
        if (dto.Notes != null) e.Notes = dto.Notes;
        if (dto.PicturePath != null) e.PicturePath = dto.PicturePath;

        if (dto.LastPromotionDate.HasValue) e.LastPromotionDate = dto.LastPromotionDate;
        if (dto.LastReviewDate.HasValue) e.LastReviewDate = dto.LastReviewDate;
        if (dto.NextReviewDate.HasValue) e.NextReviewDate = dto.NextReviewDate;

        if (dto.TerminationDate.HasValue) e.TerminationDate = dto.TerminationDate;
        if (dto.TerminationReason != null) e.TerminationReason = Enum.TryParse<TerminationReason>(dto.TerminationReason, out var tr) ? tr : null;
        if (dto.TerminationNotes != null) e.TerminationNotes = dto.TerminationNotes;

        if (dto.IsExpatriate.HasValue) e.IsExpatriate = dto.IsExpatriate.Value;
        if (dto.IsActive.HasValue) e.IsActive = dto.IsActive.Value;
    }

    #endregion

    #region Emergency Contacts

    public static EmployeeEmergencyContactDto ToDto(this EmployeeEmergencyContact e)
        => new()
        {
            Id = e.Id,
            EmployeeId = e.EmployeeId,
            FirstName = e.FirstName,
            MiddleName = e.MiddleName,
            LastName = e.LastName,
            Relationship = e.Relationship,
            ContactType = e.ContactType,
            PhoneNumber = e.PhoneNumber,
            AlternatePhoneNumber = e.AlternatePhoneNumber,
            EmailAddress = e.EmailAddress,
            Address = e.Address,
            City = e.City,
            CountryId = e.CountryId,
            DigitalAddress = e.DigitalAddress,
            IsPrimary = e.IsPrimary,
            IsActive = e.IsActive,
            Notes = e.Notes
        };

    public static EmployeeEmergencyContact ToEntity(this CreateEmployeeEmergencyContactDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            FirstName = dto.FirstName,
            MiddleName = dto.MiddleName,
            LastName = dto.LastName,
            Relationship = dto.Relationship,
            ContactType = dto.ContactType,
            PhoneNumber = dto.PhoneNumber,
            AlternatePhoneNumber = dto.AlternatePhoneNumber,
            EmailAddress = dto.EmailAddress,
            Address = dto.Address,
            City = dto.City,
            CountryId = dto.CountryId,
            DigitalAddress = dto.DigitalAddress,
            IsPrimary = dto.IsPrimary,
            IsActive = dto.IsActive,
            Notes = dto.Notes
        };

    public static void Apply(this UpdateEmployeeEmergencyContactDto dto, EmployeeEmergencyContact e)
    {
        if (!string.IsNullOrWhiteSpace(dto.FirstName)) e.FirstName = dto.FirstName.Trim();
        e.MiddleName = dto.MiddleName;
        if (!string.IsNullOrWhiteSpace(dto.LastName)) e.LastName = dto.LastName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Relationship)) e.Relationship = dto.Relationship.Trim();
        if (dto.ContactType.HasValue) e.ContactType = dto.ContactType.Value;
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) e.PhoneNumber = dto.PhoneNumber.Trim();
        e.AlternatePhoneNumber = dto.AlternatePhoneNumber;
        e.EmailAddress = dto.EmailAddress;
        e.Address = dto.Address;
        e.City = dto.City;
        if (dto.CountryId.HasValue) e.CountryId = dto.CountryId;
        e.DigitalAddress = dto.DigitalAddress;
        if (dto.IsPrimary.HasValue) e.IsPrimary = dto.IsPrimary.Value;
        if (dto.IsActive.HasValue) e.IsActive = dto.IsActive.Value;
        e.Notes = dto.Notes;
    }

    #endregion

    #region Address Contacts

    public static EmployeeContactDto ToDto(this EmployeeContact e)
        => new()
        {
            Id = e.Id,
            EmployeeId = e.EmployeeId,
            ContactType = e.ContactType,
            AddressLine1 = e.AddressLine1,
            AddressLine2 = e.AddressLine2,
            City = e.City,
            Region = e.Region,
            DigitalAddress = e.DigitalAddress,
            CountryId = e.CountryId,
            IsPrimary = e.IsPrimary
        };

    public static EmployeeContact ToEntity(this CreateEmployeeContactDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            ContactType = dto.ContactType,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            Region = dto.Region,
            DigitalAddress = dto.DigitalAddress,
            CountryId = dto.CountryId,
            IsPrimary = dto.IsPrimary
        };

    public static void Apply(this UpdateEmployeeContactDto dto, EmployeeContact e)
    {
        if (dto.ContactType.HasValue) e.ContactType = dto.ContactType.Value;
        e.AddressLine1 = dto.AddressLine1;
        e.AddressLine2 = dto.AddressLine2;
        e.City = dto.City;
        e.Region = dto.Region;
        e.DigitalAddress = dto.DigitalAddress;
        if (dto.CountryId.HasValue) e.CountryId = dto.CountryId;
        if (dto.IsPrimary.HasValue) e.IsPrimary = dto.IsPrimary.Value;
    }

    #endregion

    #region Dependents & Benefits

    public static EmployeeDependentDto ToLegacyDto(this EmployeeDependent d)
        => new()
        {
            Id = d.Id,
            EmployeeId = d.EmployeeId,
            FirstName = d.FirstName,
            MiddleName = d.MiddleName,
            LastName = d.LastName,
            Relationship = d.Relationship.ToString(),
            DateOfBirth = d.DateOfBirth,
            Gender = d.Gender,
            Occupation = d.Occupation,
            IsEligibleForBenefits = d.IsEligibleForBenefits,
            Age = d.DateOfBirth.HasValue ? (DateTime.Today.Year - d.DateOfBirth.Value.Year) : null,
            IsDeceased = d.IsDeceased
        };

    public static EmployeeDependentReadDto ToReadDto(this EmployeeDependent d)
        => new()
        {
            Id = d.Id,
            EmployeeId = d.EmployeeId,
            FirstName = d.FirstName,
            MiddleName = d.MiddleName,
            LastName = d.LastName,
            Relationship = d.Relationship,
            RelationshipDescription = d.RelationshipDescription,
            DateOfBirth = d.DateOfBirth,
            Gender = d.Gender,
            HasDisability = d.HasDisability,
            DisabilityDescription = d.DisabilityDescription,
            GhanaCardNumber = d.GhanaCardNumber,
            Phone = d.Phone,
            DigitalAddress = d.DigitalAddress,
            Occupation = d.Occupation,
            IsEligibleForBenefits = d.IsEligibleForBenefits,
            IsDeceased = d.IsDeceased,
            PicturePath = d.PicturePath,
            Notes = d.Notes
        };

    public static EmployeeDependent ToEntity(this EmployeeDependentCreateDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            FirstName = dto.FirstName,
            MiddleName = dto.MiddleName,
            LastName = dto.LastName,
            Relationship = dto.Relationship,
            RelationshipDescription = dto.RelationshipDescription,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            HasDisability = dto.HasDisability,
            DisabilityDescription = dto.DisabilityDescription,
            GhanaCardNumber = dto.GhanaCardNumber,
            Phone = dto.Phone,
            DigitalAddress = dto.DigitalAddress,
            Occupation = dto.Occupation,
            IsEligibleForBenefits = dto.IsEligibleForBenefits,
            IsDeceased = dto.IsDeceased,
            PicturePath = dto.PicturePath,
            Notes = dto.Notes
        };

    public static void Apply(this EmployeeDependentUpdateDto dto, EmployeeDependent d)
    {
        if (!string.IsNullOrWhiteSpace(dto.FirstName)) d.FirstName = dto.FirstName.Trim();
        d.MiddleName = dto.MiddleName;
        if (!string.IsNullOrWhiteSpace(dto.LastName)) d.LastName = dto.LastName.Trim();
        if (dto.Relationship.HasValue) d.Relationship = dto.Relationship.Value;
        d.RelationshipDescription = dto.RelationshipDescription;
        d.DateOfBirth = dto.DateOfBirth;
        d.Gender = dto.Gender;
        if (dto.HasDisability.HasValue) d.HasDisability = dto.HasDisability.Value;
        d.DisabilityDescription = dto.DisabilityDescription;
        d.GhanaCardNumber = dto.GhanaCardNumber;
        d.Phone = dto.Phone;
        d.DigitalAddress = dto.DigitalAddress;
        d.Occupation = dto.Occupation;
        if (dto.IsEligibleForBenefits.HasValue) d.IsEligibleForBenefits = dto.IsEligibleForBenefits.Value;
        if (dto.IsDeceased.HasValue) d.IsDeceased = dto.IsDeceased.Value;
        d.PicturePath = dto.PicturePath;
        d.Notes = dto.Notes;
    }

    public static EmployeeDependentBenefitDto ToDto(this EmployeeDependentBenefit b)
        => new()
        {
            Id = b.Id,
            EmployeeDependentId = b.EmployeeDependentId,
            PolicyId = b.PolicyId,
            PolicyName = b.BenefitPolicy?.PolicyName,
            EnrolledDate = b.EnrolledDate,
            CoverageStartDate = b.CoverageStartDate,
            CoverageEndDate = b.CoverageEndDate,
            BenefitAmountUsed = b.BenefitAmountUsed,
            IsActive = b.IsActive
        };

    public static EmployeeDependentBenefit ToEntity(this CreateEmployeeDependentBenefitDto dto)
        => new()
        {
            EmployeeDependentId = dto.EmployeeDependentId,
            PolicyId = dto.PolicyId,
            EnrolledDate = dto.EnrolledDate,
            CoverageStartDate = dto.CoverageStartDate,
            CoverageEndDate = dto.CoverageEndDate,
            BenefitAmountUsed = dto.BenefitAmountUsed,
            IsActive = dto.IsActive
        };

    public static void Apply(this UpdateEmployeeDependentBenefitDto dto, EmployeeDependentBenefit b)
    {
        if (dto.CoverageStartDate.HasValue) b.CoverageStartDate = dto.CoverageStartDate;
        if (dto.CoverageEndDate.HasValue) b.CoverageEndDate = dto.CoverageEndDate;
        if (dto.BenefitAmountUsed.HasValue) b.BenefitAmountUsed = dto.BenefitAmountUsed.Value;
        if (dto.IsActive.HasValue) b.IsActive = dto.IsActive.Value;
    }

    #endregion

    #region Qualifications

    public static EmployeeQualificationDto ToDto(this EmployeeQualification q)
        => new()
        {
            Id = q.Id,
            EmployeeId = q.EmployeeId,
            QualificationId = q.QualificationId,
            QualificationName = q.Qualification?.Name ?? q.CustomQualificationName ?? string.Empty,
            CustomQualificationName = q.CustomQualificationName,
            Institution = q.Institution,
            FieldOfStudy = q.FieldOfStudy,
            StartDate = q.StartDate,
            CompletionDate = q.CompletionDate,
            Grade = q.Grade,
            CountryId = q.CountryId,
            CountryName = q.Country?.Name,
            Description = q.Description,
            IsVerified = q.IsVerified,
            Notes = q.Notes
        };

    public static EmployeeQualification ToEntity(this CreateEmployeeQualificationDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            QualificationId = dto.QualificationId,
            CustomQualificationName = dto.CustomQualificationName,
            Institution = dto.Institution,
            FieldOfStudy = dto.FieldOfStudy,
            StartDate = dto.StartDate,
            CompletionDate = dto.CompletionDate,
            Grade = dto.Grade,
            CountryId = dto.CountryId,
            Description = dto.Description,
            Notes = dto.Notes
        };

    public static void Apply(this UpdateEmployeeQualificationDto dto, EmployeeQualification q)
    {
        if (dto.QualificationId.HasValue) q.QualificationId = dto.QualificationId.Value;
        if (dto.CustomQualificationName != null) q.CustomQualificationName = dto.CustomQualificationName;
        if (!string.IsNullOrWhiteSpace(dto.Institution)) q.Institution = dto.Institution.Trim();
        q.FieldOfStudy = dto.FieldOfStudy;
        q.StartDate = dto.StartDate;
        q.CompletionDate = dto.CompletionDate;
        q.Grade = dto.Grade;
        if (dto.CountryId.HasValue) q.CountryId = dto.CountryId;
        q.Description = dto.Description;
        if (dto.IsVerified.HasValue) q.IsVerified = dto.IsVerified.Value;
        q.Notes = dto.Notes;
    }

    #endregion

    #region Skills

    public static EmployeeSkillDto ToDto(this EmployeeSkill s)
        => new()
        {
            Id = s.Id,
            EmployeeId = s.EmployeeId,
            SkillId = s.SkillId,
            SkillName = s.Skill?.Name ?? string.Empty,
            SkillCategory = s.Skill?.Category,
            SkillLevel = s.SkillLevel,
            AcquiredDate = s.AcquiredDate,
            CertificationDate = s.CertificationDate,
            CertificationExpiryDate = s.CertificationExpiryDate,
            CertificationNumber = s.CertificationNumber,
            CertifyingBody = s.CertifyingBody,
            IsVerified = s.IsVerified,
            IsCertificationExpired = s.CertificationExpiryDate.HasValue && s.CertificationExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow),
            Notes = s.Notes
        };

    public static EmployeeSkill ToEntity(this CreateEmployeeSkillDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            SkillId = dto.SkillId,
            SkillLevel = dto.SkillLevel,
            AcquiredDate = dto.AcquiredDate,
            CertificationDate = dto.CertificationDate,
            CertificationExpiryDate = dto.CertificationExpiryDate,
            CertificationNumber = dto.CertificationNumber,
            CertifyingBody = dto.CertifyingBody,
            Notes = dto.Notes,
            IsCertified = dto.CertificationDate.HasValue
        };

    public static void Apply(this UpdateEmployeeSkillDto dto, EmployeeSkill s)
    {
        if (dto.SkillLevel.HasValue) s.SkillLevel = dto.SkillLevel.Value;
        if (dto.AcquiredDate.HasValue) s.AcquiredDate = dto.AcquiredDate;
        if (dto.IsCertified.HasValue) s.IsCertified = dto.IsCertified.Value;
        if (dto.CertificationDate.HasValue) s.CertificationDate = dto.CertificationDate;
        if (dto.CertificationExpiryDate.HasValue) s.CertificationExpiryDate = dto.CertificationExpiryDate;
        if (dto.CertificationNumber != null) s.CertificationNumber = dto.CertificationNumber;
        if (dto.CertifyingBody != null) s.CertifyingBody = dto.CertifyingBody;
        if (dto.Notes != null) s.Notes = dto.Notes;
        if (dto.IsVerified.HasValue) s.IsVerified = dto.IsVerified.Value;
    }

    #endregion

    #region Contract Details (basic projection)

    public static EmployeeContractDetailDto ToDto(this EmployeeContractDetail c)
        => new()
        {
            Id = c.Id,
            EmployeeId = c.EmployeeId,
            ContractNumber = c.ContractNumber,
            EmploymentType = c.EmploymentType,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Salary = c.Salary,
            PayFrequency = c.PayFrequency.ToString(),
            PayFrequencyType = c.PayFrequency,
            TaxTreatmentType = c.TaxTreatmentType,
            WithholdingTaxRate = c.WithholdingTaxRate,
            IsPensionApplicable = c.IsPensionApplicable,
            IsTaxExempt = c.IsTaxExempt,
            WorkingHoursPerWeek = c.WorkingHoursPerWeek,
            VacationDaysPerYear = c.VacationDaysPerYear,
            SickDaysPerYear = c.SickDaysPerYear,
            Terms = c.Terms,
            IsActive = c.IsActive,
            ContractPath = c.ContractPath,
            ContractStatus = c.ContractStatus,
            TerminationDate = c.TerminationDate,
            TerminationReason = c.TerminationReason
        };

    #endregion

    #region Extended subresources (list projections)

    public static EmployeeIdentificationCardListDto ToListDto(this EmployeeIdentificationCard c)
        => new()
        {
            Id = c.Id,
            EmployeeId = c.EmployeeId,
            IdentificationTypeId = c.IdentificationTypeId,
            CardTypeName = c.IdentificationType?.Name ?? string.Empty,
            CardNumber = c.DocumentNumber,
            IssueDate = c.IssueDate,
            ExpiryDate = c.ExpiryDate,
            IssuingAuthority = c.IdentificationType?.IssuingAuthorityName,
            CountryId = c.IdentificationType?.IssuingCountryId,
            CountryName = c.IdentificationType?.IssuingCountry?.Name,
            IsVerified = c.IsVerified,
            DocumentPath = c.DocumentPath,
            Notes = c.Notes
        };

    public static EmployeeIdentificationCardDetailDto ToDetailDto(this EmployeeIdentificationCard c)
        => new()
        {
            Id = c.Id,
            EmployeeId = c.EmployeeId,
            IdentificationTypeId = c.IdentificationTypeId,
            CardTypeName = c.IdentificationType?.Name ?? string.Empty,
            CardNumber = c.DocumentNumber,
            IssueDate = c.IssueDate,
            ExpiryDate = c.ExpiryDate,
            IssuingAuthority = c.IdentificationType?.IssuingAuthorityName,
            CountryId = c.IdentificationType?.IssuingCountryId,
            CountryName = c.IdentificationType?.IssuingCountry?.Name,
            DocumentPath = c.DocumentPath,
            IsVerified = c.IsVerified,
            VerifiedDate = c.VerifiedDate,
            Notes = c.Notes
        };

    public static EmployeeIdentificationCard ToEntity(this CreateEmployeeIdentificationCardDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            IdentificationTypeId = dto.IdentificationTypeId,
            DocumentNumber = dto.DocumentNumber,
            IssueDate = dto.IssueDate,
            ExpiryDate = dto.ExpiryDate,
            DocumentPath = dto.DocumentPath,
            IsVerified = dto.IsVerified,
            VerifiedDate = dto.VerifiedDate,
            Notes = dto.Notes
        };

    public static void Apply(this UpdateEmployeeIdentificationCardDto dto, EmployeeIdentificationCard c)
    {
        if (dto.IdentificationTypeId.HasValue) c.IdentificationTypeId = dto.IdentificationTypeId.Value;
        if (!string.IsNullOrWhiteSpace(dto.DocumentNumber)) c.DocumentNumber = dto.DocumentNumber.Trim();
        if (dto.IssueDate.HasValue) c.IssueDate = dto.IssueDate;
        if (dto.ExpiryDate.HasValue) c.ExpiryDate = dto.ExpiryDate;
        if (dto.DocumentPath != null) c.DocumentPath = dto.DocumentPath;
        if (dto.IsVerified.HasValue) c.IsVerified = dto.IsVerified.Value;
        if (dto.VerifiedDate.HasValue) c.VerifiedDate = dto.VerifiedDate;
        if (dto.Notes != null) c.Notes = dto.Notes;
    }

    public static EmployeeWorkHistoryListDto ToListDto(this EmployeeWorkHistory w)
        => new()
        {
            Id = w.Id,
            EmployeeId = w.EmployeeId,
            CompanyName = w.CompanyName,
            JobTitle = w.JobTitle,
            StartDate = w.StartDate,
            EndDate = w.EndDate
        };

    public static EmployeeWorkHistoryDetailDto ToDetailDto(this EmployeeWorkHistory w)
        => new()
        {
            Id = w.Id,
            EmployeeId = w.EmployeeId,
            CompanyName = w.CompanyName,
            CompanyAddress = w.CompanyAddress,
            JobTitle = w.JobTitle,
            JobDescription = w.JobDescription,
            StartDate = w.StartDate,
            EndDate = w.EndDate,
            Salary = w.Salary,
            ReasonForLeaving = w.ReasonForLeaving,
            SupervisorName = w.SupervisorName,
            SupervisorPhone = w.SupervisorPhone,
            CanContact = w.CanContact
        };

    public static EmployeeWorkHistory ToEntity(this CreateEmployeeWorkHistoryDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            CompanyName = dto.CompanyName,
            CompanyAddress = dto.CompanyAddress,
            JobTitle = dto.JobTitle,
            JobDescription = dto.JobDescription,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Salary = dto.Salary,
            ReasonForLeaving = dto.ReasonForLeaving,
            SupervisorName = dto.SupervisorName,
            SupervisorPhone = dto.SupervisorPhone,
            CanContact = dto.CanContact
        };

    public static void Apply(this UpdateEmployeeWorkHistoryDto dto, EmployeeWorkHistory w)
    {
        if (!string.IsNullOrWhiteSpace(dto.CompanyName)) w.CompanyName = dto.CompanyName.Trim();
        w.CompanyAddress = dto.CompanyAddress;
        if (!string.IsNullOrWhiteSpace(dto.JobTitle)) w.JobTitle = dto.JobTitle.Trim();
        w.JobDescription = dto.JobDescription;
        if (dto.StartDate.HasValue) w.StartDate = dto.StartDate.Value;
        if (dto.EndDate.HasValue) w.EndDate = dto.EndDate;
        if (dto.Salary.HasValue) w.Salary = dto.Salary;
        w.ReasonForLeaving = dto.ReasonForLeaving;
        w.SupervisorName = dto.SupervisorName;
        w.SupervisorPhone = dto.SupervisorPhone;
        if (dto.CanContact.HasValue) w.CanContact = dto.CanContact.Value;
    }

    public static ExpatriateAssignmentListDto ToListDto(this ExpatriateAssignment a)
        => new()
        {
            Id = a.Id,
            EmployeeId = a.EmployeeId,
            HomeCountryId = a.HomeCountryId,
            HomeCountryName = a.Country?.Name,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            FamilyAccompanying = a.FamilyAccompanying
        };

    public static ExpatriateAssignmentDetailDto ToDetailDto(this ExpatriateAssignment a)
        => new()
        {
            Id = a.Id,
            EmployeeId = a.EmployeeId,
            HomeCountryId = a.HomeCountryId,
            HomeCountryName = a.Country?.Name,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            FamilyAccompanying = a.FamilyAccompanying,
            RelocationAllowance = a.RelocationAllowance,
            RelocationDate = a.RelocationDate,
            AssignmentObjective = a.AssignmentObjective,
            VisaType = a.VisaType,
            VisaExpiryDate = a.VisaExpiryDate,
            WorkPermitNumber = a.WorkPermitNumber,
            WorkPermitExpiryDate = a.WorkPermitExpiryDate
        };

    public static ExpatriateAssignment ToEntity(this CreateExpatriateAssignmentDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            HomeCountryId = dto.HomeCountryId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            RelocationAllowance = dto.RelocationAllowance,
            RelocationDate = dto.RelocationDate,
            FamilyAccompanying = dto.FamilyAccompanying,
            AssignmentObjective = dto.AssignmentObjective,
            VisaType = dto.VisaType,
            VisaExpiryDate = dto.VisaExpiryDate,
            WorkPermitNumber = dto.WorkPermitNumber,
            WorkPermitExpiryDate = dto.WorkPermitExpiryDate
        };

    public static void Apply(this UpdateExpatriateAssignmentDto dto, ExpatriateAssignment a)
    {
        if (dto.HomeCountryId.HasValue) a.HomeCountryId = dto.HomeCountryId.Value;
        if (dto.StartDate.HasValue) a.StartDate = dto.StartDate.Value;
        if (dto.EndDate.HasValue) a.EndDate = dto.EndDate;
        if (dto.RelocationAllowance.HasValue) a.RelocationAllowance = dto.RelocationAllowance;
        if (dto.RelocationDate.HasValue) a.RelocationDate = dto.RelocationDate;
        if (dto.FamilyAccompanying.HasValue) a.FamilyAccompanying = dto.FamilyAccompanying.Value;
        if (dto.AssignmentObjective != null) a.AssignmentObjective = dto.AssignmentObjective;
        if (dto.VisaType != null) a.VisaType = dto.VisaType;
        if (dto.VisaExpiryDate.HasValue) a.VisaExpiryDate = dto.VisaExpiryDate;
        if (dto.WorkPermitNumber != null) a.WorkPermitNumber = dto.WorkPermitNumber;
        if (dto.WorkPermitExpiryDate.HasValue) a.WorkPermitExpiryDate = dto.WorkPermitExpiryDate;
    }

    public static EmployeePositionHistoryListDto ToListDto(this EmployeePositionHistory h)
        => new()
        {
            Id = h.Id,
            EmployeeId = h.EmployeeId,
            PositionId = h.PositionId,
            PositionTitle = h.Position?.Title,
            StartDate = h.StartDate,
            EndDate = h.EndDate,
            ChangeReason = h.ChangeReason,
            IsCurrent = h.IsCurrent
        };

    public static EmployeePositionHistoryDetailDto ToDetailDto(this EmployeePositionHistory h)
        => new()
        {
            Id = h.Id,
            EmployeeId = h.EmployeeId,
            PositionId = h.PositionId,
            PositionTitle = h.Position?.Title,
            StartDate = h.StartDate,
            EndDate = h.EndDate,
            ChangeReason = h.ChangeReason,
            IsCurrent = h.IsCurrent,
            LocationLevelId = h.LocationLevelId,
            LocationId = h.LocationId,
            OrganizationLevelId = h.OrganizationLevelId,
            OrganizationUnitId = h.OrganizationUnitId,
            Notes = h.Notes
        };

    public static EmployeePositionHistory ToEntity(this CreateEmployeePositionHistoryDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            LocationLevelId = dto.LocationLevelId,
            LocationId = dto.LocationId,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ChangeReason = dto.ChangeReason,
            Notes = dto.Notes
        };

    public static void Apply(this UpdateEmployeePositionHistoryDto dto, EmployeePositionHistory h)
    {
        if (dto.LocationLevelId.HasValue) h.LocationLevelId = dto.LocationLevelId.Value;
        if (dto.LocationId.HasValue) h.LocationId = dto.LocationId;
        if (dto.OrganizationLevelId.HasValue) h.OrganizationLevelId = dto.OrganizationLevelId.Value;
        if (dto.OrganizationUnitId.HasValue) h.OrganizationUnitId = dto.OrganizationUnitId;
        if (dto.PositionId.HasValue) h.PositionId = dto.PositionId.Value;
        if (dto.StartDate.HasValue) h.StartDate = dto.StartDate.Value;
        if (dto.EndDate.HasValue) h.EndDate = dto.EndDate;
        if (dto.ChangeReason.HasValue) h.ChangeReason = dto.ChangeReason.Value;
        if (dto.Notes != null) h.Notes = dto.Notes;
    }

    public static EmployeeSalaryAssignmentListDto ToListDto(this EmployeeSalaryAssignment s)
        => new()
        {
            Id = s.Id,
            EmployeeId = s.EmployeeId,
            GradeId = s.GradeId,
            GradeCode = s.Grade?.Code,
            GradeName = s.Grade?.Name,
            LevelId = s.LevelId,
            LevelCode = s.Level?.Code,
            NotchId = s.NotchId,
            NotchNumber = s.Notch?.NotchNumber.ToString(),
            EffectiveDate = s.EffectiveDate,
            EffectiveTo = s.EffectiveTo,
            Reason = s.AssignmentReason,
            IsActive = s.EffectiveTo == null || s.EffectiveTo >= DateTime.Today,
            Amount = s.Notch?.SalaryAmount ?? s.Level?.MidSalary
        };

    public static EmployeeSalaryAssignmentDetailDto ToDetailDto(this EmployeeSalaryAssignment s)
        => new()
        {
            Id = s.Id,
            EmployeeId = s.EmployeeId,
            GradeId = s.GradeId,
            GradeCode = s.Grade?.Code,
            GradeName = s.Grade?.Name,
            LevelId = s.LevelId,
            LevelCode = s.Level?.Code,
            NotchId = s.NotchId,
            NotchNumber = s.Notch?.NotchNumber.ToString(),
            EffectiveDate = s.EffectiveDate,
            EffectiveTo = s.EffectiveTo,
            AssignmentReason = s.AssignmentReason,
            Reason = s.AssignmentReason,
            IsActive = s.EffectiveTo == null || s.EffectiveTo >= DateTime.Today,
            Amount = s.Notch?.SalaryAmount ?? s.Level?.MidSalary
        };

    public static EmployeeSalaryAssignment ToEntity(this CreateEmployeeSalaryAssignmentDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            GradeId = dto.GradeId,
            LevelId = dto.LevelId,
            NotchId = dto.NotchId,
            EffectiveDate = dto.EffectiveDate,
            EffectiveTo = dto.EffectiveTo,
            AssignmentReason = dto.AssignmentReason
        };

    public static void Apply(this UpdateEmployeeSalaryAssignmentDto dto, EmployeeSalaryAssignment s)
    {
        if (dto.GradeId.HasValue) s.GradeId = dto.GradeId.Value;
        if (dto.LevelId.HasValue) s.LevelId = dto.LevelId;
        if (dto.NotchId.HasValue) s.NotchId = dto.NotchId;
        if (dto.EffectiveDate.HasValue) s.EffectiveDate = dto.EffectiveDate.Value;
        if (dto.EffectiveTo.HasValue) s.EffectiveTo = dto.EffectiveTo;
        if (dto.AssignmentReason != null) s.AssignmentReason = dto.AssignmentReason;
    }

    public static EmployeeRefereeListDto ToListDto(this EmployeeReferee r)
        => new()
        {
            Id = r.Id,
            EmployeeId = r.EmployeeId,
            RefereeType = r.RefereeType,
            FullName = r.FullName,
            Organization = r.Organization,
            PositionOrTitle = r.PositionOrTitle,
            Relationship = r.Relationship,
            PhoneNumber = r.PhoneNumber,
            EmailAddress = r.EmailAddress,
            IsPrimary = r.IsPrimary,
            IsActive = r.IsActive
        };

    public static EmployeeRefereeDetailDto ToDetailDto(this EmployeeReferee r)
        => new()
        {
            Id = r.Id,
            EmployeeId = r.EmployeeId,
            RefereeType = r.RefereeType,
            FullName = r.FullName,
            Organization = r.Organization,
            PositionOrTitle = r.PositionOrTitle,
            Relationship = r.Relationship,
            PhoneNumber = r.PhoneNumber,
            EmailAddress = r.EmailAddress,
            IsPrimary = r.IsPrimary,
            IsActive = r.IsActive,
            IsContacted = r.IsContacted,
            ContactedDate = r.ContactedDate,
            ReferenceNotes = r.ReferenceNotes
        };

    public static EmployeeReferee ToEntity(this CreateEmployeeRefereeDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            RefereeType = dto.RefereeType,
            FullName = dto.FullName,
            Organization = dto.Organization,
            PositionOrTitle = dto.PositionOrTitle,
            Relationship = dto.Relationship,
            PhoneNumber = dto.PhoneNumber,
            EmailAddress = dto.EmailAddress,
            IsPrimary = dto.IsPrimary,
            IsActive = dto.IsActive
        };

    public static void Apply(this UpdateEmployeeRefereeDto dto, EmployeeReferee r)
    {
        if (dto.RefereeType.HasValue) r.RefereeType = dto.RefereeType.Value;
        if (!string.IsNullOrWhiteSpace(dto.FullName)) r.FullName = dto.FullName.Trim();
        if (dto.Organization != null) r.Organization = dto.Organization;
        if (dto.PositionOrTitle != null) r.PositionOrTitle = dto.PositionOrTitle;
        if (dto.Relationship != null) r.Relationship = dto.Relationship;
        if (dto.PhoneNumber != null) r.PhoneNumber = dto.PhoneNumber;
        if (dto.EmailAddress != null) r.EmailAddress = dto.EmailAddress;
        if (dto.IsContacted.HasValue) r.IsContacted = dto.IsContacted.Value;
        if (dto.ContactedDate.HasValue) r.ContactedDate = dto.ContactedDate;
        if (dto.ReferenceNotes != null) r.ReferenceNotes = dto.ReferenceNotes;
        if (dto.IsPrimary.HasValue) r.IsPrimary = dto.IsPrimary.Value;
        if (dto.IsActive.HasValue) r.IsActive = dto.IsActive.Value;
    }

    public static EmployeeGuarantorListDto ToListDto(this EmployeeGuarantor g)
        => new()
        {
            Id = g.Id,
            EmployeeId = g.EmployeeId,
            IsPrimary = g.IsPrimary,
            Relationship = g.Relationship,
            FirstName = g.FirstName,
            LastName = g.LastName,
            PhoneNumber = g.PhoneNumber,
            EmailAddress = g.EmailAddress,
            IsVerified = g.IsVerified,
            IsActive = g.IsActive
        };

    public static EmployeeGuarantorDetailDto ToDetailDto(this EmployeeGuarantor g)
        => new()
        {
            Id = g.Id,
            EmployeeId = g.EmployeeId,
            IsPrimary = g.IsPrimary,
            Relationship = g.Relationship,
            FirstName = g.FirstName,
            MiddleName = g.MiddleName,
            LastName = g.LastName,
            Title = g.Title,
            Gender = g.Gender,
            DateOfBirth = g.DateOfBirth,
            Address = g.Address,
            City = g.City,
            DigitalAddress = g.DigitalAddress,
            CountryId = g.CountryId,
            PhoneNumber = g.PhoneNumber,
            EmailAddress = g.EmailAddress,
            JobTitle = g.JobTitle,
            EmployerName = g.EmployerName,
            EmployerAddress = g.EmployerAddress,
            EmployerPhone = g.EmployerPhone,
            MonthlyIncome = g.MonthlyIncome,
            NationalIdType = g.NationalIdType,
            NationalIdNumberMasked = string.IsNullOrWhiteSpace(g.NationalIdNumber) ? null : Mask(g.NationalIdNumber),
            NationalIdExpiryDate = g.NationalIdExpiryDate,
            HasSignedGuarantorForm = g.HasSignedGuarantorForm,
            DateFormSigned = g.DateFormSigned,
            GuarantorFormPath = g.GuarantorFormPath,
            IsVerified = g.IsVerified,
            VerificationDate = g.VerificationDate,
            VerifiedByEmployeeId = g.VerifiedByEmployeeId,
            Notes = g.Notes,
            LastContactDate = g.LastContactDate,
            IsActive = g.IsActive
        };

    public static EmployeeGuarantor ToEntity(this CreateEmployeeGuarantorDto dto)
        => new()
        {
            EmployeeId = dto.EmployeeId,
            IsPrimary = dto.IsPrimary,
            Relationship = dto.Relationship,
            FirstName = dto.FirstName,
            MiddleName = dto.MiddleName,
            LastName = dto.LastName,
            Title = dto.Title,
            Gender = dto.Gender,
            DateOfBirth = dto.DateOfBirth,
            Address = dto.Address,
            City = dto.City,
            DigitalAddress = dto.DigitalAddress,
            CountryId = dto.CountryId,
            PhoneNumber = dto.PhoneNumber,
            EmailAddress = dto.EmailAddress,
            JobTitle = dto.JobTitle,
            EmployerName = dto.EmployerName,
            EmployerAddress = dto.EmployerAddress,
            EmployerPhone = dto.EmployerPhone,
            MonthlyIncome = dto.MonthlyIncome,
            NationalIdType = dto.NationalIdType,
            NationalIdNumber = dto.NationalIdNumber,
            NationalIdExpiryDate = dto.NationalIdExpiryDate,
            HasSignedGuarantorForm = dto.HasSignedGuarantorForm,
            DateFormSigned = dto.DateFormSigned,
            GuarantorFormPath = dto.GuarantorFormPath,
            Notes = dto.Notes,
            IsActive = dto.IsActive
        };

    public static void Apply(this UpdateEmployeeGuarantorDto dto, EmployeeGuarantor g)
    {
        if (dto.IsPrimary.HasValue) g.IsPrimary = dto.IsPrimary.Value;
        if (dto.Relationship != null) g.Relationship = dto.Relationship;
        if (dto.FirstName != null) g.FirstName = dto.FirstName;
        if (dto.MiddleName != null) g.MiddleName = dto.MiddleName;
        if (dto.LastName != null) g.LastName = dto.LastName;
        if (dto.Title != null) g.Title = dto.Title;
        if (dto.Gender.HasValue) g.Gender = dto.Gender;
        if (dto.DateOfBirth.HasValue) g.DateOfBirth = dto.DateOfBirth;
        if (dto.Address != null) g.Address = dto.Address;
        if (dto.City != null) g.City = dto.City;
        if (dto.DigitalAddress != null) g.DigitalAddress = dto.DigitalAddress;
        if (dto.CountryId.HasValue) g.CountryId = dto.CountryId;
        if (dto.PhoneNumber != null) g.PhoneNumber = dto.PhoneNumber;
        if (dto.EmailAddress != null) g.EmailAddress = dto.EmailAddress;
        if (dto.JobTitle != null) g.JobTitle = dto.JobTitle;
        if (dto.EmployerName != null) g.EmployerName = dto.EmployerName;
        if (dto.EmployerAddress != null) g.EmployerAddress = dto.EmployerAddress;
        if (dto.EmployerPhone != null) g.EmployerPhone = dto.EmployerPhone;
        if (dto.MonthlyIncome.HasValue) g.MonthlyIncome = dto.MonthlyIncome;
        if (dto.NationalIdType != null) g.NationalIdType = dto.NationalIdType;
        if (dto.NationalIdNumber != null) g.NationalIdNumber = dto.NationalIdNumber;
        if (dto.NationalIdExpiryDate.HasValue) g.NationalIdExpiryDate = dto.NationalIdExpiryDate;
        if (dto.HasSignedGuarantorForm.HasValue) g.HasSignedGuarantorForm = dto.HasSignedGuarantorForm.Value;
        if (dto.DateFormSigned.HasValue) g.DateFormSigned = dto.DateFormSigned;
        if (dto.GuarantorFormPath != null) g.GuarantorFormPath = dto.GuarantorFormPath;
        if (dto.IsVerified.HasValue) g.IsVerified = dto.IsVerified.Value;
        if (dto.VerificationDate.HasValue) g.VerificationDate = dto.VerificationDate;
        if (dto.VerifiedByEmployeeId.HasValue) g.VerifiedByEmployeeId = dto.VerifiedByEmployeeId;
        if (dto.Notes != null) g.Notes = dto.Notes;
        if (dto.LastContactDate.HasValue) g.LastContactDate = dto.LastContactDate;
        if (dto.IsActive.HasValue) g.IsActive = dto.IsActive.Value;
    }

    private static string Mask(string value)
    {
        if (value.Length <= 4) return new string('*', value.Length);
        return new string('*', value.Length - 4) + value[^4..];
    }

    #endregion

    #region EmployeeBank Reference Entities

    public static EmployeeBankDto ToDto(this EmployeeBank b, int branchCount = 0) => new()
    {
        Id          = b.Id,
        Name        = b.Name,
        Code        = b.Code,
        SwiftCode   = b.SwiftCode,
        CountryId   = b.CountryId,
        CountryName = b.Country?.Name,
        IsActive    = b.IsActive,
        BranchCount = branchCount,
    };

    public static EmployeeBank ToEntity(this CreateEmployeeBankDto dto) => new()
    {
        Name      = dto.Name.Trim(),
        Code      = dto.Code.Trim().ToUpperInvariant(),
        SwiftCode = dto.SwiftCode?.Trim().ToUpperInvariant(),
        CountryId = dto.CountryId,
        IsActive  = dto.IsActive,
    };

    public static void Apply(this UpdateEmployeeBankDto dto, EmployeeBank b)
    {
        if (dto.Name != null)      b.Name      = dto.Name.Trim();
        if (dto.Code != null)      b.Code      = dto.Code.Trim().ToUpperInvariant();
        if (dto.SwiftCode != null) b.SwiftCode = dto.SwiftCode.Trim().ToUpperInvariant();
        if (dto.CountryId.HasValue) b.CountryId = dto.CountryId;
        if (dto.IsActive.HasValue)  b.IsActive  = dto.IsActive.Value;
    }

    public static EmployeeBankBranchDto ToDto(this EmployeeBankBranch br) => new()
    {
        Id          = br.Id,
        BankId      = br.BankId,
        BankName    = br.Bank?.Name ?? string.Empty,
        BankCode    = br.Bank?.Code ?? string.Empty,
        Name        = br.Name,
        Code        = br.Code,
        Address     = br.Address,
        City        = br.City,
        CountryId   = br.CountryId,
        CountryName = br.Country?.Name,
        PhoneNumber = br.PhoneNumber,
        Email       = br.Email,
        IsActive    = br.IsActive,
    };

    public static EmployeeBankBranch ToEntity(this CreateEmployeeBankBranchDto dto) => new()
    {
        BankId      = dto.BankId,
        Name        = dto.Name.Trim(),
        Code        = dto.Code?.Trim().ToUpperInvariant(),
        Address     = dto.Address?.Trim(),
        City        = dto.City?.Trim(),
        CountryId   = dto.CountryId,
        PhoneNumber = dto.PhoneNumber?.Trim(),
        Email       = dto.Email?.Trim(),
        IsActive    = dto.IsActive,
    };

    public static void Apply(this UpdateEmployeeBankBranchDto dto, EmployeeBankBranch br)
    {
        if (dto.Name != null)         br.Name        = dto.Name.Trim();
        if (dto.Code != null)         br.Code        = dto.Code.Trim().ToUpperInvariant();
        if (dto.Address != null)      br.Address     = dto.Address.Trim();
        if (dto.City != null)         br.City        = dto.City.Trim();
        if (dto.CountryId.HasValue)   br.CountryId   = dto.CountryId;
        if (dto.PhoneNumber != null)  br.PhoneNumber = dto.PhoneNumber.Trim();
        if (dto.Email != null)        br.Email       = dto.Email.Trim();
        if (dto.IsActive.HasValue)    br.IsActive    = dto.IsActive.Value;
    }

    #endregion

    #region EmployeeBank Details

    public static EmployeeBankDetailDto ToDto(this EmployeeBankDetail e) => new()
    {
        Id                   = e.Id,
        EmployeeId           = e.EmployeeId,
        BankId               = e.BankId,
        BankCode             = e.Bank?.Code,
        BranchId             = e.BranchId,
        BranchCode           = e.Branch?.Code,
        BankName             = e.Bank?.Name ?? e.BankName,
        BranchName           = e.Branch?.Name ?? e.BranchName,
        AccountNumber        = Mask(e.AccountNumber),
        AccountName          = e.AccountName,
        MobileMoneyNumber    = e.MobileMoneyNumber,
        AccountType          = e.AccountType,
        AllocationPercentage = e.AllocationPercentage,
        IsPrimary            = e.IsPrimary,
        IsActive             = e.IsActive,
        IsVerified           = e.IsVerified,
        VerifiedDate         = e.VerifiedDate,
        VerifiedById         = e.VerifiedById,
    };

    public static EmployeeBankDetail ToEntity(this CreateEmployeeBankDetailDto dto) => new()
    {
        EmployeeId           = dto.EmployeeId,
        BankId               = dto.BankId,
        BranchId             = dto.BranchId,
        BankName             = dto.BankName,
        BranchName           = dto.BranchName,
        AccountNumber        = dto.AccountNumber,
        AccountName          = dto.AccountName,
        MobileMoneyNumber    = dto.MobileMoneyNumber,
        AccountType          = dto.AccountType,
        AllocationPercentage = dto.AllocationPercentage,
        IsPrimary            = dto.IsPrimary,
        IsActive             = true,
    };

    public static void Apply(this UpdateEmployeeBankDetailDto dto, EmployeeBankDetail e)
    {
        if (dto.BankId.HasValue)               e.BankId               = dto.BankId;
        if (dto.BranchId.HasValue)             e.BranchId             = dto.BranchId;
        if (dto.BankName != null)              e.BankName             = dto.BankName;
        if (dto.BranchName != null)            e.BranchName           = dto.BranchName;
        if (dto.AccountNumber != null)         e.AccountNumber        = dto.AccountNumber;
        if (dto.AccountName != null)           e.AccountName          = dto.AccountName;
        if (dto.MobileMoneyNumber != null)     e.MobileMoneyNumber    = dto.MobileMoneyNumber;
        if (dto.AccountType.HasValue)          e.AccountType          = dto.AccountType.Value;
        if (dto.AllocationPercentage.HasValue) e.AllocationPercentage = dto.AllocationPercentage.Value;
        if (dto.IsPrimary.HasValue)            e.IsPrimary            = dto.IsPrimary.Value;
        if (dto.IsActive.HasValue)             e.IsActive             = dto.IsActive.Value;
    }

    #endregion
}
