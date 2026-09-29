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
            IsOnPayroll = e.IsOnPayroll,
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
            IsOnPayroll = e.IsOnPayroll,
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
            // The edit form re-opens its address cascade from this id.
            GeoAreaId = e.GeoAreaId,
            // Detail fields
            DateOfBirth = e.DateOfBirth,
            MaritalStatus = e.MaritalStatus,
            Religion = e.Religion,
            GenderDescription = e.GenderDescription,
            Hometown = e.Hometown,
            HasDisability = e.HasDisability,
            HasPhoto = e.PhotoFileUploadRecordId != null || e.PhotoDocumentRecordId != null,
            PhotoFileName = e.PhotoFileName,
            PhotoMimeType = e.PhotoMimeType,
            PhotoFileSizeBytes = e.PhotoFileSizeBytes,
            DisabilityDescription = e.DisabilityDescription,
            DisabilityTypeId = e.DisabilityTypeId,
            DisabilityTypeName = e.DisabilityType?.Name,
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
            ProbationSource = e.ProbationSource,
            // Computed on read so it cannot go stale behind a change of hire date or term. Null
            // where there is nothing to compute from — an employee with no start date, or none.
            ExpectedConfirmationDate = e.DateEmployed is { } hired && e.ProbationPeriodDays > 0
                ? hired.AddDays(e.ProbationPeriodDays)
                : null,
            ConfirmationDate = e.ConfirmationDate,
            ConfirmationSource = e.ConfirmationSource,
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
            OffPayrollReason = e.OffPayrollReason,
            OffPayrollNote = e.OffPayrollNote,
            PayBasis = e.PayBasis,
            PayBasisNote = e.PayBasisNote,
            BadgeNumber = e.BadgeNumber,
            Notes = e.Notes,
            // Round 4, lane O.
            MaintenanceAssignment = e.MaintenanceAssignment,
            PositionIsTechnicianRole = e.Position != null && e.Position.IsTechnicianRole,
            Specialization = e.Specialization,
            CertificationLevel = e.CertificationLevel,
            ExperienceLevel = e.ExperienceLevel,
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
        to.IsOnPayroll = from.IsOnPayroll;
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
        to.GenderDescription = from.GenderDescription;
        to.Hometown = from.Hometown;
        to.HasDisability = from.HasDisability;
        to.HasPhoto = from.HasPhoto;
        to.PhotoFileName = from.PhotoFileName;
        to.PhotoMimeType = from.PhotoMimeType;
        to.PhotoFileSizeBytes = from.PhotoFileSizeBytes;
        to.DisabilityDescription = from.DisabilityDescription;
        to.DisabilityTypeId = from.DisabilityTypeId;
        to.DisabilityTypeName = from.DisabilityTypeName;
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
        to.ProbationSource = from.ProbationSource;
        to.ExpectedConfirmationDate = from.ExpectedConfirmationDate;
        to.ConfirmationDate = from.ConfirmationDate;
        to.ConfirmationSource = from.ConfirmationSource;
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
        to.OffPayrollReason = from.OffPayrollReason;
        to.OffPayrollNote = from.OffPayrollNote;
        to.PayBasis = from.PayBasis;
        to.PayBasisNote = from.PayBasisNote;
        to.BadgeNumber = from.BadgeNumber;
        to.Notes = from.Notes;
        // Round 4, lane O — a field this copy omits reaches the 360 screens as its default.
        to.MaintenanceAssignment = from.MaintenanceAssignment;
        to.PositionIsTechnicianRole = from.PositionIsTechnicianRole;
        to.Specialization = from.Specialization;
        to.CertificationLevel = from.CertificationLevel;
        to.ExperienceLevel = from.ExperienceLevel;
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

    /// <summary>
    /// Whether an employee record is live, given the employment status it carries.
    ///
    /// <para><c>Employee</c> holds the same fact twice — <c>StaffStatus</c> and <c>IsActive</c> —
    /// and the four lifecycle methods on <c>EmployeeService</c> (activate, deactivate, terminate,
    /// reinstate) each set both, in lockstep. The create and update paths did not: the create
    /// hardcoded <c>IsActive = true</c> whatever status it was handed, and the update let a caller
    /// move either flag without the other. Posting an employee with
    /// <c>staffStatus: "Terminated"</c> therefore returned <c>isActive: true</c>, and every one of
    /// the twenty-nine HR reads that filter on <c>IsActive</c> would have counted that person as
    /// still on the payroll.</para>
    ///
    /// <para>The rule is read off what the codebase already decides rather than invented:
    /// deactivate writes <c>Inactive</c>, terminate writes <c>Terminated</c>, and both write
    /// <c>IsActive = false</c>. <c>Retired</c> is added here because a retired employee is
    /// likewise gone — no path produces it today and no row carries it, so this is the one part
    /// of the rule that is a judgement rather than an observation, and it is called out for TDC
    /// rather than buried. <c>Suspended</c> and <c>OnLeave</c> are still employed and stay live.</para>
    /// </summary>
    public static bool IsLiveRecordFor(StaffStatus status) => status switch
    {
        StaffStatus.Inactive => false,
        StaffStatus.Terminated => false,
        StaffStatus.Retired => false,
        _ => true,
    };

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
            GenderDescription = dto.GenderDescription,
            Hometown = dto.Hometown,
            HasDisability = dto.HasDisability,
            // Round 3, lane P2: a type without the tick is contradictory — the service refuses it
            // before this runs, and a false tick clears both the type and the notes.
            DisabilityTypeId = dto.HasDisability ? dto.DisabilityTypeId : null,
            DisabilityDescription = dto.HasDisability ? dto.DisabilityDescription : null,
            IsFullTime = dto.IsFullTime,
            DateEmployed = dto.DateEmployed,

            Address = dto.Address,
            // ⚠ City/State are written here from whatever the caller sent, then OVERWRITTEN by the
            // service when GeoAreaId is set — see EmployeeService.ApplyGeoAreaSnapshotAsync. The
            // tree wins; these two are a snapshot of it.
            City = dto.City,
            State = dto.State,
            PostalCode = dto.PostalCode,
            DigitalAddress = dto.DigitalAddress,
            CountryId = dto.CountryId,
            GeoAreaId = dto.GeoAreaId,
            EmailAddress = dto.EmailAddress,
            TelephoneNumber = dto.TelephoneNumber,
            BusinessNumber = dto.BusinessNumber,
            MobileNumber = dto.MobileNumber,
            Extension = dto.Extension,

            EmploymentType = dto.EmploymentType,
            // ⚠ A placeholder, overwritten by the caller. The term is settled against the POSITION
            // (EmployeeService.ResolveProbationAsync), which this mapper cannot see; the entity's
            // own default stands in only so the property is never left unassigned.
            ProbationPeriodDays = dto.ProbationPeriodDays ?? 90,
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
            // Membership is validated in EmployeeService before this runs (an off-payroll create
            // carrying a salary is refused there); here the fields are simply carried.
            IsOnPayroll = dto.IsOnPayroll,
            OffPayrollReason = dto.IsOnPayroll ? null : dto.OffPayrollReason,
            OffPayrollNote = dto.IsOnPayroll ? null : NullIfBlank(dto.OffPayrollNote),
            BadgeNumber = dto.BadgeNumber,
            // ⚠ PicturePath deliberately NOT set from the DTO — see Apply below.
            Notes = dto.Notes,
            IsExpatriate = dto.IsExpatriate,
            IsActive = IsLiveRecordFor(dto.StaffStatus),

            // Round 4, lane O. Following the position (or saying nothing) leaves both false, and the
            // save-time rule derives the answer from the post; Include / Exclude record HR's say-so.
            MaintenanceAssignmentSetByHand = dto.MaintenanceAssignment is MaintenanceAssignmentMode.Include or MaintenanceAssignmentMode.Exclude,
            CanBeAssignedToMaintenance = dto.MaintenanceAssignment == MaintenanceAssignmentMode.Include,
            Specialization = NullIfBlank(dto.Specialization),
            CertificationLevel = NullIfBlank(dto.CertificationLevel),
            ExperienceLevel = NullIfBlank(dto.ExperienceLevel)
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
        if (dto.GenderDescription != null) e.GenderDescription = dto.GenderDescription;
        if (dto.Hometown != null) e.Hometown = dto.Hometown;
        // ⚠ Always written, unlike the strings around it. A bool cannot say "not supplied", and
        // treating false as absent would make the tick impossible to UNtick.
        e.HasDisability = dto.HasDisability;
        if (dto.DisabilityDescription != null) e.DisabilityDescription = dto.DisabilityDescription;
        // Round 3, lane P2. Like the description: written when supplied, so a partial update (the
        // import's) leaves it alone; untick and both go.
        if (dto.DisabilityTypeId.HasValue) e.DisabilityTypeId = dto.DisabilityTypeId;
        if (!dto.HasDisability) { e.DisabilityTypeId = null; e.DisabilityDescription = null; }
        if (dto.DateEmployed.HasValue) e.DateEmployed = dto.DateEmployed;

        e.IsFullTime = dto.IsFullTime;

        if (dto.Address != null) e.Address = dto.Address;
        if (dto.City != null) e.City = dto.City;
        if (dto.State != null) e.State = dto.State;
        if (dto.PostalCode != null) e.PostalCode = dto.PostalCode;
        if (dto.DigitalAddress != null) e.DigitalAddress = dto.DigitalAddress;
        if (dto.CountryId.HasValue) e.CountryId = dto.CountryId;

        // ⚠ ClearGeoArea is checked FIRST and wins. A nullable id cannot say both "leave it alone"
        // and "remove it", and every other optional field here reads null as "not supplied" — so
        // without the explicit flag, emptying the region picker would save successfully and change
        // nothing. The snapshot columns are left alone on a clear: the record still has to say
        // where the person lives, even once the structured link is gone.
        if (dto.ClearGeoArea) e.GeoAreaId = null;
        else if (dto.GeoAreaId.HasValue) e.GeoAreaId = dto.GeoAreaId;

        // null = not supplied; blank = clear (email is optional since 2026-09-03). The service
        // re-applies the normalised value after this; kept here so Apply stays self-consistent.
        if (dto.EmailAddress != null)
            e.EmailAddress = string.IsNullOrWhiteSpace(dto.EmailAddress) ? null : dto.EmailAddress.Trim();
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
        // (`IsActive` is reconciled against `StaffStatus` at the end of this method, after the
        //  caller's own `isActive` has had its say — see the note there.)
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

        // Round 4, lane O — who decides whether Maintenance may assign this person work. ⚠ Following
        // the position only hands the answer back to the post: the value itself is derived at save
        // time (ApplicationDbContext.HrTechnicianRole.cs), the one place that reads the post's flag.
        switch (dto.MaintenanceAssignment)
        {
            case MaintenanceAssignmentMode.FollowPosition:
                e.MaintenanceAssignmentSetByHand = false;
                break;
            case MaintenanceAssignmentMode.Include:
                e.MaintenanceAssignmentSetByHand = true;
                e.CanBeAssignedToMaintenance = true;
                break;
            case MaintenanceAssignmentMode.Exclude:
                e.MaintenanceAssignmentSetByHand = true;
                e.CanBeAssignedToMaintenance = false;
                break;
        }
        // Null = not supplied; blank = clear. The strings above cannot be emptied from a form that
        // sends blanks as null; these three can, because the form sends them as they stand.
        if (dto.Specialization != null) e.Specialization = NullIfBlank(dto.Specialization);
        if (dto.CertificationLevel != null) e.CertificationLevel = NullIfBlank(dto.CertificationLevel);
        if (dto.ExperienceLevel != null) e.ExperienceLevel = NullIfBlank(dto.ExperienceLevel);
        // ⚠ PicturePath is NOT written from the DTO. It is the legacy caller-supplied file
        // location, kept so ported images still resolve; the photo is set through the gated
        // upload endpoint on EmployeeDocumentsController. Accepting it here let a caller point
        // the photo at any file under the legacy roots the download will serve from.

        if (dto.LastPromotionDate.HasValue) e.LastPromotionDate = dto.LastPromotionDate;
        if (dto.LastReviewDate.HasValue) e.LastReviewDate = dto.LastReviewDate;
        if (dto.NextReviewDate.HasValue) e.NextReviewDate = dto.NextReviewDate;

        if (dto.TerminationDate.HasValue) e.TerminationDate = dto.TerminationDate;
        if (dto.TerminationReason != null) e.TerminationReason = Enum.TryParse<TerminationReason>(dto.TerminationReason, out var tr) ? tr : null;
        if (dto.TerminationNotes != null) e.TerminationNotes = dto.TerminationNotes;

        if (dto.IsExpatriate.HasValue) e.IsExpatriate = dto.IsExpatriate.Value;
        if (dto.IsActive.HasValue) e.IsActive = dto.IsActive.Value;

        // The status is the authority, and it gets the last word. A payload carrying both
        // `staffStatus: "Terminated"` and `isActive: true` is contradicting itself; before this
        // line it was stored exactly as sent, and the record then read as employed to the
        // twenty-nine HR queries that filter on `IsActive` while reading as gone to the four
        // headline counts that group `StaffStatus`. Whichever order the two assignments happened
        // to run in decided which of those was true, which is not a thing a caller should be able
        // to choose by accident.
        //
        // A status-only update therefore carries `IsActive` with it — moving someone to
        // `Terminated` through this path can no longer leave them counted as on the payroll — and
        // an `isActive` a status contradicts is overruled rather than half-applied. The dedicated
        // lifecycle endpoints (activate / deactivate / terminate / reinstate) already set the two
        // together and are unaffected.
        if (dto.StaffStatus.HasValue || dto.IsActive.HasValue)
            e.IsActive = IsLiveRecordFor(e.StaffStatus);
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
            Region = e.Region,
            CountryId = e.CountryId,
            GeoAreaId = e.GeoAreaId,
            RelationshipTypeId = e.RelationshipTypeId,
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
            Region = dto.Region,
            CountryId = dto.CountryId,
            GeoAreaId = dto.GeoAreaId,
            RelationshipTypeId = dto.RelationshipTypeId,
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
        e.Region = dto.Region;
        if (dto.CountryId.HasValue) e.CountryId = dto.CountryId;
        // ⚠ Full replace, unlike CountryId above: this DTO's own address fields are full-replace,
        // so emptying the cascade on the form has to clear the link. See the DTO's remark.
        e.GeoAreaId = dto.GeoAreaId;
        e.RelationshipTypeId = dto.RelationshipTypeId;
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
            GeoAreaId = e.GeoAreaId,
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
            GeoAreaId = dto.GeoAreaId,
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
        // ⚠ Full replace, unlike CountryId above — see the DTO's remark.
        e.GeoAreaId = dto.GeoAreaId;
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
            GenderDescription = d.GenderDescription,
            HasPhoto = d.PhotoFileUploadRecordId != null || d.PhotoDocumentRecordId != null,
            PhotoFileName = d.PhotoFileName,
            PhotoMimeType = d.PhotoMimeType,
            PhotoFileSizeBytes = d.PhotoFileSizeBytes,
            HasDisability = d.HasDisability,
            DisabilityDescription = d.DisabilityDescription,
            DisabilityTypeId = d.DisabilityTypeId,
            DisabilityTypeName = d.DisabilityType?.Name,
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
            GenderDescription = dto.GenderDescription,
            HasDisability = dto.HasDisability,
            // Round 3, lane P2: a type without the tick is contradictory — the service refuses it
            // before this runs, and a false tick clears both the type and the notes.
            DisabilityTypeId = dto.HasDisability ? dto.DisabilityTypeId : null,
            DisabilityDescription = dto.HasDisability ? dto.DisabilityDescription : null,
            GhanaCardNumber = dto.GhanaCardNumber,
            Phone = dto.Phone,
            DigitalAddress = dto.DigitalAddress,
            Occupation = dto.Occupation,
            IsEligibleForBenefits = dto.IsEligibleForBenefits,
            IsDeceased = dto.IsDeceased,
            // ⚠ As above: the dependant photo goes through the gated upload, not a path string.
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
        d.GenderDescription = dto.GenderDescription;
        if (dto.HasDisability.HasValue) d.HasDisability = dto.HasDisability.Value;
        d.DisabilityDescription = dto.DisabilityDescription;
        // Round 3, lane P2: the dependant update is a replace, like its description.
        d.DisabilityTypeId = dto.DisabilityTypeId;
        if (!d.HasDisability) { d.DisabilityTypeId = null; d.DisabilityDescription = null; }
        d.GhanaCardNumber = dto.GhanaCardNumber;
        d.Phone = dto.Phone;
        d.DigitalAddress = dto.DigitalAddress;
        d.Occupation = dto.Occupation;
        if (dto.IsEligibleForBenefits.HasValue) d.IsEligibleForBenefits = dto.IsEligibleForBenefits.Value;
        if (dto.IsDeceased.HasValue) d.IsDeceased = dto.IsDeceased.Value;
        // ⚠ PicturePath is legacy-read-only; the gated upload sets the dependant's photo.
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
            CertifyingBodyId = s.CertifyingBodyId,
            // ⚠ Resolved from the navigation, so the READ has to Include CertifyingBodyRef. A name
            // declared on a DTO and populated by nothing is this module's most repeated defect.
            CertifyingBodyName = s.CertifyingBodyRef?.Name,
            IsVerified = s.IsVerified,
            IsCertificationExpired = s.CertificationExpiryDate.HasValue && s.CertificationExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow),
            Notes = s.Notes,
            // Round 2, lane C2. Resolved from the navigation, so every read Includes
            // EmployeeCertification and its Certification.
            EmployeeCertificationId = s.EmployeeCertificationId,
            EmployeeCertificationName = s.EmployeeCertification?.Certification?.Name,
            RequiresCertification = s.Skill?.RequiresCertification ?? false,
            IsCompliant = SkillIsCompliant(s)
        };

    /// <summary>
    /// A skill that requires certification is compliant when a linked credential is not revoked
    /// and not expired, or — for rows recorded before the catalogue — the per-skill certification
    /// is still in date. Any other skill is compliant by definition.
    /// </summary>
    private static bool SkillIsCompliant(EmployeeSkill s)
    {
        if (s.Skill == null || !s.Skill.RequiresCertification) return true;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var c = s.EmployeeCertification;
        if (c != null && !c.IsDeleted && !c.IsRevoked && (!c.ExpiresOn.HasValue || c.ExpiresOn.Value >= today))
            return true;
        return s.IsCertified && (!s.CertificationExpiryDate.HasValue || s.CertificationExpiryDate.Value >= today);
    }

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
            CertifyingBodyId = dto.CertifyingBodyId,
            EmployeeCertificationId = dto.EmployeeCertificationId,
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
        // ⚠ Applied UNCONDITIONALLY, unlike its neighbours, and that is deliberate. Every other
        // field here treats null as "not supplied", which means none of them can ever be CLEARED.
        // For a free-text box that is merely annoying; for a picker it is a trap — choose the wrong
        // certifying body once and there would be no way back to "none". The sole caller is the
        // skill form, which posts the whole record, so "absent" and "cleared" are the same intent.
        s.CertifyingBodyId = dto.CertifyingBodyId;
        // Same reasoning, same unconditional application (round 2, lane C2).
        s.EmployeeCertificationId = dto.EmployeeCertificationId;
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
            ContractTypeId = c.ContractTypeId,
            // Null unless the caller Included it — the list and single reads both do.
            ContractTypeName = c.ContractType?.Name,
            StartDate = c.StartDate,
            EffectiveDate = c.EffectiveDate,
            EndDate = c.EndDate,
            ContractEndDate = c.ContractEndDate,
            IsCurrent = c.IsCurrent,
            Salary = c.Salary,
            PayFrequency = c.PayFrequency.ToString(),
            PayFrequencyType = c.PayFrequency,
            TaxTreatmentType = c.TaxTreatmentType,
            WithholdingTaxRate = c.WithholdingTaxRate,
            IsPensionApplicable = c.IsPensionApplicable,
            IsTaxExempt = c.IsTaxExempt,
            WorkingHoursPerWeek = c.WorkingHoursPerWeek,
            ProbationPeriodDays = c.ProbationPeriodDays,
            ConfirmationDate = c.ConfirmationDate,
            CurrencyCode = c.CurrencyCode,
            WorkSchedule = c.WorkSchedule,
            SpecialConditions = c.SpecialConditions,
            Notes = c.Notes,
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
            CountryId = w.CountryId,
            City = w.City,
            Region = w.Region,
            GeoAreaId = w.GeoAreaId,
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
            CountryId = dto.CountryId,
            City = dto.City,
            Region = dto.Region,
            GeoAreaId = dto.GeoAreaId,
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
        w.City = dto.City;
        w.Region = dto.Region;
        if (dto.CountryId.HasValue) w.CountryId = dto.CountryId;
        // ⚠ Full replace, unlike CountryId above: this DTO's address fields are full-replace, so
        // emptying the cascade on the form has to clear the link.
        w.GeoAreaId = dto.GeoAreaId;
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
            VisaIssueDate = a.VisaIssueDate,
            VisaExpiryDate = a.VisaExpiryDate,
            WorkPermitNumber = a.WorkPermitNumber,
            WorkPermitIssueDate = a.WorkPermitIssueDate,
            WorkPermitExpiryDate = a.WorkPermitExpiryDate,
            ResidentPermitNumber = a.ResidentPermitNumber,
            ResidentPermitIssueDate = a.ResidentPermitIssueDate,
            ResidentPermitExpiryDate = a.ResidentPermitExpiryDate,
            // ⚠ Only populated where the caller Included them. A detail read that silently returns
            // an empty family is the shape D-09 kept producing; the service Includes it.
            FamilyMembers = a.FamilyMembers == null
                ? new List<ExpatriateFamilyMemberDto>()
                : a.FamilyMembers.Where(m => !m.IsDeleted).Select(m => m.ToDto()).ToList()
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
            VisaIssueDate = dto.VisaIssueDate,
            VisaExpiryDate = dto.VisaExpiryDate,
            WorkPermitNumber = dto.WorkPermitNumber,
            WorkPermitIssueDate = dto.WorkPermitIssueDate,
            WorkPermitExpiryDate = dto.WorkPermitExpiryDate,
            ResidentPermitNumber = dto.ResidentPermitNumber,
            ResidentPermitIssueDate = dto.ResidentPermitIssueDate,
            ResidentPermitExpiryDate = dto.ResidentPermitExpiryDate
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
        if (dto.VisaIssueDate.HasValue) a.VisaIssueDate = dto.VisaIssueDate;
        if (dto.WorkPermitIssueDate.HasValue) a.WorkPermitIssueDate = dto.WorkPermitIssueDate;
        if (dto.ResidentPermitNumber != null) a.ResidentPermitNumber = dto.ResidentPermitNumber;
        if (dto.ResidentPermitIssueDate.HasValue) a.ResidentPermitIssueDate = dto.ResidentPermitIssueDate;
        if (dto.ResidentPermitExpiryDate.HasValue) a.ResidentPermitExpiryDate = dto.ResidentPermitExpiryDate;
    }

    // ── expatriate family members ────────────────────────────────────────────

    public static ExpatriateFamilyMemberDto ToDto(this ExpatriateFamilyMember m) => new()
    {
        Id = m.Id,
        ExpatriateAssignmentId = m.ExpatriateAssignmentId,
        FullName = m.FullName,
        Relationship = m.Relationship,
        RelationshipDescription = m.RelationshipDescription,
        GenderDescription = m.GenderDescription,
        Gender = m.Gender,
        DateOfBirth = m.DateOfBirth,
        PassportNumber = m.PassportNumber,
        PassportExpiryDate = m.PassportExpiryDate,
        ResidentPermitNumber = m.ResidentPermitNumber,
        ResidentPermitIssueDate = m.ResidentPermitIssueDate,
        ResidentPermitExpiryDate = m.ResidentPermitExpiryDate,
        ArrivalDate = m.ArrivalDate,
        DepartureDate = m.DepartureDate,
        Notes = m.Notes,
    };

    public static ExpatriateFamilyMember ToEntity(this CreateExpatriateFamilyMemberDto dto) => new()
    {
        ExpatriateAssignmentId = dto.ExpatriateAssignmentId,
        FullName = dto.FullName,
        Relationship = dto.Relationship,
        RelationshipDescription = dto.RelationshipDescription,
        GenderDescription = dto.GenderDescription,
        Gender = dto.Gender,
        DateOfBirth = dto.DateOfBirth,
        PassportNumber = dto.PassportNumber,
        PassportExpiryDate = dto.PassportExpiryDate,
        ResidentPermitNumber = dto.ResidentPermitNumber,
        ResidentPermitIssueDate = dto.ResidentPermitIssueDate,
        ResidentPermitExpiryDate = dto.ResidentPermitExpiryDate,
        ArrivalDate = dto.ArrivalDate,
        DepartureDate = dto.DepartureDate,
        Notes = dto.Notes,
    };

    /// <remarks>
    /// ⚠ A full overwrite, not the "only if provided" idiom used above. Every field here is one a
    /// user can legitimately CLEAR — a permit number entered against the wrong person, a departure
    /// date set by mistake — and a null-means-absent mapper makes clearing impossible.
    /// </remarks>
    public static void Apply(this UpdateExpatriateFamilyMemberDto dto, ExpatriateFamilyMember m)
    {
        m.FullName = dto.FullName;
        m.Relationship = dto.Relationship;
        m.RelationshipDescription = dto.RelationshipDescription;
        m.GenderDescription = dto.GenderDescription;
        m.Gender = dto.Gender;
        m.DateOfBirth = dto.DateOfBirth;
        m.PassportNumber = dto.PassportNumber;
        m.PassportExpiryDate = dto.PassportExpiryDate;
        m.ResidentPermitNumber = dto.ResidentPermitNumber;
        m.ResidentPermitIssueDate = dto.ResidentPermitIssueDate;
        m.ResidentPermitExpiryDate = dto.ResidentPermitExpiryDate;
        m.ArrivalDate = dto.ArrivalDate;
        m.DepartureDate = dto.DepartureDate;
        m.Notes = dto.Notes;
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

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Whether this placement is the one in force on <paramref name="asOf"/>: taken effect, not
    /// ended, and not withdrawn.
    /// </summary>
    /// <remarks>
    /// ⚠ The in-memory twin of the predicate every as-of QUERY uses (<c>ActiveAssignments</c>,
    /// <c>EmolumentService</c>, <c>GetCurrentAsync</c>). The four copies had drifted — two never
    /// asked whether the placement had STARTED — and a projection that disagrees with the query is
    /// how a screen says "Active" about a row no calculation will use. EF cannot translate a method
    /// into SQL, so the query sites still spell it out; this is for materialised rows.
    /// </remarks>
    public static bool IsInForceOn(this EmployeeSalaryAssignment s, DateTime asOf)
        => s.WithdrawnAt == null
        && s.EffectiveDate <= asOf
        && (s.EffectiveTo == null || s.EffectiveTo >= asOf);

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
            IsActive = s.IsInForceOn(DateTime.Today),
            IsScheduled = s.WithdrawnAt == null && s.EffectiveDate > DateTime.Today,
            WithdrawnAt = s.WithdrawnAt,
            WithdrawnReason = s.WithdrawnReason,
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
            IsActive = s.IsInForceOn(DateTime.Today),
            IsScheduled = s.WithdrawnAt == null && s.EffectiveDate > DateTime.Today,
            WithdrawnAt = s.WithdrawnAt,
            WithdrawnReason = s.WithdrawnReason,
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
            RelationshipTypeId = r.RelationshipTypeId,
            PhoneNumber = r.PhoneNumber,
            EmailAddress = r.EmailAddress,
            IsPrimary = r.IsPrimary,
            IsActive = r.IsActive,
            IsContacted = r.IsContacted,
            // One flag rather than making every screen reason about which of three ids means
            // "there is a file". The list carries it since round 2 so the tab can show a paperclip.
            HasLetter = r.LetterFileUploadRecordId != null || r.LetterDocumentRecordId != null,
            LetterFileName = r.LetterFileName,
            LetterMimeType = r.LetterMimeType,
            LetterFileSizeBytes = r.LetterFileSizeBytes
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
            RelationshipTypeId = r.RelationshipTypeId,
            PhoneNumber = r.PhoneNumber,
            EmailAddress = r.EmailAddress,
            IsPrimary = r.IsPrimary,
            IsActive = r.IsActive,
            IsContacted = r.IsContacted,
            ContactedDate = r.ContactedDate,
            ReferenceNotes = r.ReferenceNotes,
            // ⚠ One flag rather than making every screen reason about which of three ids means
            // "there is a file". The download affordance keys off this.
            HasLetter = r.LetterFileUploadRecordId != null || r.LetterDocumentRecordId != null,
            LetterFileName = r.LetterFileName,
            LetterMimeType = r.LetterMimeType,
            LetterFileSizeBytes = r.LetterFileSizeBytes
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
            RelationshipTypeId = dto.RelationshipTypeId,
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
        // ⚠ Clear-flag rather than a bare null, because every field on this DTO means "not
        // supplied" when null — the ClearNationalIdType shape. The service overwrites Relationship
        // from the catalogue row AFTER this runs, so the order here does not matter.
        if (dto.ClearRelationshipType) r.RelationshipTypeId = null;
        else if (dto.RelationshipTypeId.HasValue) r.RelationshipTypeId = dto.RelationshipTypeId;
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
            IsActive = g.IsActive,
            HasPhoto = g.PhotoFileUploadRecordId != null || g.PhotoDocumentRecordId != null,
            // ⚠ Both need the navigations loaded — the list read Includes them. A repository
            // FindAsync would leave them null and this would read "no type, no documents" for
            // every row, which is the stale-navigation shape met sixteen times in this module.
            NationalIdTypeId = g.NationalIdTypeId,
            NationalIdTypeName = g.NationalIdTypeRef?.Name,
            RelationshipTypeId = g.RelationshipTypeId,
            DocumentCount = g.Documents?.Count(d => !d.IsDeleted) ?? 0
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
            Region = g.Region,
            DigitalAddress = g.DigitalAddress,
            CountryId = g.CountryId,
            GeoAreaId = g.GeoAreaId,
            RelationshipTypeId = g.RelationshipTypeId,
            PhoneNumber = g.PhoneNumber,
            EmailAddress = g.EmailAddress,
            JobTitle = g.JobTitle,
            EmployerName = g.EmployerName,
            EmployerAddress = g.EmployerAddress,
            EmployerPhone = g.EmployerPhone,
            MonthlyIncome = g.MonthlyIncome,
            AmountGuaranteed = g.AmountGuaranteed,
            AmountGuaranteedCurrencyCode = g.AmountGuaranteedCurrencyCode,
            GenderDescription = g.GenderDescription,
            HasPhoto = g.PhotoFileUploadRecordId != null || g.PhotoDocumentRecordId != null,
            PhotoFileName = g.PhotoFileName,
            PhotoMimeType = g.PhotoMimeType,
            PhotoFileSizeBytes = g.PhotoFileSizeBytes,
            NationalIdType = g.NationalIdType,
            NationalIdTypeId = g.NationalIdTypeId,
            NationalIdTypeName = g.NationalIdTypeRef?.Name,
            DocumentCount = g.Documents?.Count(d => !d.IsDeleted) ?? 0,
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
            Region = dto.Region,
            DigitalAddress = dto.DigitalAddress,
            CountryId = dto.CountryId,
            GeoAreaId = dto.GeoAreaId,
            RelationshipTypeId = dto.RelationshipTypeId,
            PhoneNumber = dto.PhoneNumber,
            EmailAddress = dto.EmailAddress,
            JobTitle = dto.JobTitle,
            EmployerName = dto.EmployerName,
            EmployerAddress = dto.EmployerAddress,
            EmployerPhone = dto.EmployerPhone,
            MonthlyIncome = dto.MonthlyIncome,
            AmountGuaranteed = dto.AmountGuaranteed,
            AmountGuaranteedCurrencyCode = dto.AmountGuaranteedCurrencyCode,
            GenderDescription = dto.GenderDescription,
            NationalIdType = dto.NationalIdType,
            NationalIdTypeId = dto.NationalIdTypeId,
            NationalIdNumber = dto.NationalIdNumber,
            NationalIdExpiryDate = dto.NationalIdExpiryDate,
            HasSignedGuarantorForm = dto.HasSignedGuarantorForm,
            DateFormSigned = dto.DateFormSigned,
            // GuarantorFormPath is deliberately not mapped: it is no longer on the DTO.
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
        if (dto.Region != null) g.Region = dto.Region;
        if (dto.DigitalAddress != null) g.DigitalAddress = dto.DigitalAddress;
        if (dto.CountryId.HasValue) g.CountryId = dto.CountryId;
        // ⚠ Clear-flags, not bare nulls: every field on this DTO means "not supplied" when null.
        if (dto.ClearGeoArea) g.GeoAreaId = null;
        else if (dto.GeoAreaId.HasValue) g.GeoAreaId = dto.GeoAreaId;
        if (dto.ClearRelationshipType) g.RelationshipTypeId = null;
        else if (dto.RelationshipTypeId.HasValue) g.RelationshipTypeId = dto.RelationshipTypeId;
        if (dto.PhoneNumber != null) g.PhoneNumber = dto.PhoneNumber;
        if (dto.EmailAddress != null) g.EmailAddress = dto.EmailAddress;
        if (dto.JobTitle != null) g.JobTitle = dto.JobTitle;
        if (dto.EmployerName != null) g.EmployerName = dto.EmployerName;
        if (dto.EmployerAddress != null) g.EmployerAddress = dto.EmployerAddress;
        if (dto.EmployerPhone != null) g.EmployerPhone = dto.EmployerPhone;
        if (dto.MonthlyIncome.HasValue) g.MonthlyIncome = dto.MonthlyIncome;
        if (dto.AmountGuaranteed.HasValue) g.AmountGuaranteed = dto.AmountGuaranteed;
        if (dto.AmountGuaranteedCurrencyCode != null) g.AmountGuaranteedCurrencyCode = dto.AmountGuaranteedCurrencyCode;
        if (dto.GenderDescription != null) g.GenderDescription = dto.GenderDescription;
        if (dto.NationalIdType != null) g.NationalIdType = dto.NationalIdType;
        if (dto.ClearNationalIdType) g.NationalIdTypeId = null;
        else if (dto.NationalIdTypeId.HasValue) g.NationalIdTypeId = dto.NationalIdTypeId;
        if (dto.NationalIdNumber != null) g.NationalIdNumber = dto.NationalIdNumber;
        if (dto.NationalIdExpiryDate.HasValue) g.NationalIdExpiryDate = dto.NationalIdExpiryDate;
        if (dto.HasSignedGuarantorForm.HasValue) g.HasSignedGuarantorForm = dto.HasSignedGuarantorForm.Value;
        if (dto.DateFormSigned.HasValue) g.DateFormSigned = dto.DateFormSigned;
        // GuarantorFormPath is no longer writable — see the entity remark.
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
        // Nullable-means-not-supplied, so switching a row from a catalogue bank back to a typed
        // name has to say so explicitly — otherwise the old link would survive beside the new text.
        if (dto.ClearBankLink)                 { e.BankId = null; e.BranchId = null; }
        if (dto.BankId.HasValue)               e.BankId               = dto.BankId;
        // Changing the bank without naming a branch drops the old branch rather than pairing it
        // with a bank it does not belong to; the service refuses the mismatch anyway.
        if (dto.BankId.HasValue && !dto.BranchId.HasValue) e.BranchId = null;
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
