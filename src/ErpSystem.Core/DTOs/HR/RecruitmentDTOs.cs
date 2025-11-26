using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class VacancyListDto
{
    public Guid Id { get; set; }
    public string VacancyNumber { get; set; }
    public string JobTitle { get; set; }
    public string Department { get; set; }
    public string Station { get; set; }
    public VacancyType Type { get; set; }
    public string TypeName { get; set; }
    public VacancyStatus Status { get; set; }
    public string StatusName { get; set; }
    public int NumberOfPositions { get; set; }
    public DateTime? PublishDate { get; set; }
    public DateTime? ClosingDate { get; set; }
    public int ApplicationCount { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsExpired { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Detail DTO
public class VacancyDetailDto
{
    public Guid Id { get; set; }
    public string VacancyNumber { get; set; }

    // Basic Info
    public Guid PositionId { get; set; }
    public string PositionName { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; }
    public Guid? StationId { get; set; }
    public string StationName { get; set; }
    public Guid? RequisitionId { get; set; }
    public string RequisitionNumber { get; set; }

    // Vacancy Details
    public VacancyType Type { get; set; }
    public string TypeName { get; set; }
    public VacancyStatus Status { get; set; }
    public string StatusName { get; set; }
    public string JobTitle { get; set; }
    public string JobSummary { get; set; }
    public string KeyResponsibilities { get; set; }
    public string RequiredQualifications { get; set; }
    public string RequiredExperience { get; set; }
    public string DesiredSkills { get; set; }

    // Position Details
    public int NumberOfPositions { get; set; }
    // public EmploymentType EmploymentType { get; set; }
    public string EmploymentTypeName { get; set; }
    public string ContractDuration { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public string Benefits { get; set; }

    // Publishing
    public DateTime? PublishDate { get; set; }
    public DateTime? ClosingDate { get; set; }
    public bool IsInternalOnly { get; set; }
    public bool IsPublishedExternally { get; set; }
    public string AdvertisementChannels { get; set; }
    public string ExternalJobBoardUrls { get; set; }

    // Workflow
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; }
    public DateTime RequestDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }

    // Closure
    public DateTime? ClosedDate { get; set; }
    public VacancyClosureReason? ClosureReason { get; set; }
    public string ClosureReasonName { get; set; }
    public string ClosureNotes { get; set; }

    // Statistics
    public int TotalApplications { get; set; }
    public int ShortlistedApplications { get; set; }
    public int InterviewedApplications { get; set; }
    public int OfferedApplications { get; set; }

    // Collections
    public List<VacancyAttachmentDto> Attachments { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateVacancyDto
{
    public Guid PositionId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid? StationId { get; set; }
    public Guid? RequisitionId { get; set; }
    public VacancyType Type { get; set; }
    public string JobTitle { get; set; }
    public string JobSummary { get; set; }
    public string KeyResponsibilities { get; set; }
    public string RequiredQualifications { get; set; }
    public string RequiredExperience { get; set; }
    public string DesiredSkills { get; set; }
    public int NumberOfPositions { get; set; }
    // public EmploymentType EmploymentType { get; set; }
    public string ContractDuration { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public string Benefits { get; set; }
    public DateTime? ClosingDate { get; set; }
    public bool IsInternalOnly { get; set; }
}

// Update DTO
public class UpdateVacancyDto
{
    public Guid Id { get; set; }
    public string JobTitle { get; set; }
    public string JobSummary { get; set; }
    public string KeyResponsibilities { get; set; }
    public string RequiredQualifications { get; set; }
    public string RequiredExperience { get; set; }
    public string DesiredSkills { get; set; }
    public int NumberOfPositions { get; set; }
    public string ContractDuration { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public string Benefits { get; set; }
    public DateTime? ClosingDate { get; set; }
    public bool IsInternalOnly { get; set; }
}

// Publish DTO
public class PublishVacancyDto
{
    public Guid Id { get; set; }
    public DateTime PublishDate { get; set; }
    public bool IsPublishedExternally { get; set; }
    public string AdvertisementChannels { get; set; }
    public string ExternalJobBoardUrls { get; set; }
}

// Close DTO
public class CloseVacancyDto
{
    public Guid Id { get; set; }
    public DateTime ClosedDate { get; set; }
    public VacancyClosureReason ClosureReason { get; set; }
    public string ClosureNotes { get; set; }
}

// Attachment DTO
public class VacancyAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

// Dashboard DTO
public class VacancyDashboardDto
{
    public int TotalActiveVacancies { get; set; }
    public int TotalApplications { get; set; }
    public int PositionsToFill { get; set; }
    public int PositionsFilled { get; set; }
    public Dictionary<VacancyStatus, int> VacanciesByStatus { get; set; }
    public List<VacancyListDto> UrgentVacancies { get; set; }
    public List<VacancyListDto> ClosingSoon { get; set; }
}

// List DTO
public class JobApplicationListDto
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; }
    public Guid VacancyId { get; set; }
    public string VacancyTitle { get; set; }
    public string ApplicantName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public ApplicationStatus Status { get; set; }
    public string StatusName { get; set; }
    public ApplicationSource Source { get; set; }
    public string SourceName { get; set; }
    public DateTime ApplicationDate { get; set; }
    public decimal? AutoScore { get; set; }
    public bool IsShortlisted { get; set; }
    public int InterviewCount { get; set; }
}

// Detail DTO
public class JobApplicationDetailDto
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; }

    // Vacancy Info
    public Guid VacancyId { get; set; }
    public string VacancyNumber { get; set; }
    public string VacancyTitle { get; set; }

    // Personal Information
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string GenderName { get; set; }
    public string Nationality { get; set; }

    // Contact Information
    public string Email { get; set; }
    public string Phone { get; set; }
    public string AlternatePhone { get; set; }
    public string CurrentAddress { get; set; }
    public string DigitalAddress { get; set; }

    // Identification
    public string GhanaCardNumber { get; set; }
    public string PassportNumber { get; set; }
    public string SocialSecurityNumber { get; set; }

    // Qualifications
    public string HighestQualification { get; set; }
    public string Institution { get; set; }
    public int? YearOfCompletion { get; set; }

    // Employment Info
    public string CurrentEmployer { get; set; }
    public string CurrentPosition { get; set; }
    public int? YearsOfExperience { get; set; }
    public decimal? CurrentSalary { get; set; }
    public decimal? ExpectedSalary { get; set; }
    public int? NoticePeriodDays { get; set; }
    public bool IsCurrentlyEmployed { get; set; }

    // Application Details
    public ApplicationStatus Status { get; set; }
    public string StatusName { get; set; }
    public ApplicationSource Source { get; set; }
    public string SourceName { get; set; }
    public DateTime ApplicationDate { get; set; }
    public string CoverLetter { get; set; }

    // Screening
    public decimal? AutoScore { get; set; }
    public string AutoScoreBreakdown { get; set; }
    public bool IsShortlisted { get; set; }
    public DateTime? ShortlistedDate { get; set; }
    public Guid? ShortlistedById { get; set; }
    public string ShortlistedByName { get; set; }
    public string ShortlistingNotes { get; set; }

    // Rejection
    public bool IsRejected { get; set; }
    public DateTime? RejectedDate { get; set; }
    public Guid? RejectedById { get; set; }
    public string RejectedByName { get; set; }
    public string RejectionReason { get; set; }

    // Collections
    public List<ApplicationDocumentDto> Documents { get; set; }
    public List<ApplicationEducationDto> Education { get; set; }
    public List<ApplicationExperienceDto> Experience { get; set; }
    public List<ApplicationReferenceDto> References { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateJobApplicationDto
{
    public Guid VacancyId { get; set; }

    // Personal Information
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string Nationality { get; set; }

    // Contact Information
    public string Email { get; set; }
    public string Phone { get; set; }
    public string AlternatePhone { get; set; }
    public string CurrentAddress { get; set; }
    public string DigitalAddress { get; set; }

    // Identification
    public string GhanaCardNumber { get; set; }
    public string PassportNumber { get; set; }
    public string SocialSecurityNumber { get; set; }

    // Qualifications
    public string HighestQualification { get; set; }
    public string Institution { get; set; }
    public int? YearOfCompletion { get; set; }

    // Employment Info
    public string CurrentEmployer { get; set; }
    public string CurrentPosition { get; set; }
    public int? YearsOfExperience { get; set; }
    public decimal? CurrentSalary { get; set; }
    public decimal? ExpectedSalary { get; set; }
    public int? NoticePeriodDays { get; set; }
    public bool IsCurrentlyEmployed { get; set; }

    // Application Details
    public ApplicationSource Source { get; set; }
    public string CoverLetter { get; set; }

    // Collections
    public List<CreateApplicationEducationDto> Education { get; set; }
    public List<CreateApplicationExperienceDto> Experience { get; set; }
    public List<CreateApplicationReferenceDto> References { get; set; }
}

// Shortlist DTO
public class ShortlistApplicationDto
{
    public Guid Id { get; set; }
    public string ShortlistingNotes { get; set; }
}

// Reject DTO
public class RejectApplicationDto
{
    public Guid Id { get; set; }
    public string RejectionReason { get; set; }
}

// Supporting DTOs
public class ApplicationDocumentDto
{
    public Guid Id { get; set; }
    public DocumentType DocumentType { get; set; }
    public string DocumentTypeName { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class ApplicationEducationDto
{
    public Guid Id { get; set; }
    public string Institution { get; set; }
    public string Qualification { get; set; }
    public string FieldOfStudy { get; set; }
    public string Grade { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrentlyStudying { get; set; }
}

public class CreateApplicationEducationDto
{
    public string Institution { get; set; }
    public string Qualification { get; set; }
    public string FieldOfStudy { get; set; }
    public string Grade { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrentlyStudying { get; set; }
}

public class ApplicationExperienceDto
{
    public Guid Id { get; set; }
    public string Company { get; set; }
    public string Position { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrentPosition { get; set; }
    public string Responsibilities { get; set; }
    public string ReasonForLeaving { get; set; }
}

public class CreateApplicationExperienceDto
{
    public string Company { get; set; }
    public string Position { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrentPosition { get; set; }
    public string Responsibilities { get; set; }
    public string ReasonForLeaving { get; set; }
}

public class ApplicationReferenceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Position { get; set; }
    public string Organization { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Relationship { get; set; }
}

public class CreateApplicationReferenceDto
{
    public string Name { get; set; }
    public string Position { get; set; }
    public string Organization { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Relationship { get; set; }
}