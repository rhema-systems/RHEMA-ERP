using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Recruitment;

/// <summary>
/// Job vacancy/opening
/// </summary>
public class Vacancy : TenantEntity
{
    public string VacancyNumber { get; set; } = string.Empty;

    // Position Details
    public Guid PositionId { get; set; }
    public EmployeePosition Position { get; set; } = null!;

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public Guid? StationId { get; set; }
    public WorkStation? Station { get; set; }

    // Vacancy Information
    public string JobTitle { get; set; } = string.Empty;
    public int NumberOfPositions { get; set; }
    public VacancyType Type { get; set; } // New, Replacement, Temporary
    public VacancyStatus Status { get; set; }

    // Job Description
    public string JobSummary { get; set; } = string.Empty;
    public string KeyResponsibilities { get; set; } = string.Empty;
    public string RequiredQualifications { get; set; } = string.Empty;
    public string RequiredExperience { get; set; } = string.Empty;
    public string DesiredSkills { get; set; } = string.Empty;

    // Employment Terms
    // public EmploymentType EmploymentType { get; set; }
    public string? ContractDuration { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public string? Benefits { get; set; }

    // Recruitment Process
    public Guid? RequisitionId { get; set; }
    public StaffRequisition? Requisition { get; set; }

    public Guid RequestedById { get; set; }
    public Employee RequestedBy { get; set; } = null!;

    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }

    // Advertisement
    public bool IsPublished { get; set; }
    public DateTime? PublishDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public DateTime? ClosedDate { get; set; }

    public string? AdvertisementChannels { get; set; } // Website, Newspapers, etc.
    public string? ExternalJobBoardUrls { get; set; }

    // Selection Process
    public bool RequiresWrittenTest { get; set; }
    public bool RequiresPracticalTest { get; set; }
    public int NumberOfInterviewRounds { get; set; }

    // Closure
    public DateTime? FilledDate { get; set; }
    public VacancyClosureReason? ClosureReason { get; set; }
    public string? ClosureNotes { get; set; }

    // Relations
    public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    public ICollection<VacancyAttachment> Attachments { get; set; } = new List<VacancyAttachment>();
}

public class VacancyAttachment : TenantEntity
{
    public Guid VacancyId { get; set; }
    public Vacancy Vacancy { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// Job application from candidate
/// </summary>
public class JobApplication : TenantEntity
{
    public string ApplicationNumber { get; set; } = string.Empty;

    public Guid VacancyId { get; set; }
    public Vacancy Vacancy { get; set; } = null!;

    // Applicant Information
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {MiddleName} {LastName}".Trim();

    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }

    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? Nationality { get; set; }

    public string? CurrentAddress { get; set; }
    public string? DigitalAddress { get; set; }

    // Identification
    public string? GhanaCardNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? SocialSecurityNumber { get; set; }

    // Application Details
    public DateTime ApplicationDate { get; set; }
    public ApplicationStatus Status { get; set; }
    public ApplicationSource Source { get; set; } // Website, Referral, Walk-in

    // Qualifications
    public string HighestQualification { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public int? YearsOfExperience { get; set; }

    // Current Employment
    public string? CurrentEmployer { get; set; }
    public string? CurrentPosition { get; set; }
    public decimal? CurrentSalary { get; set; }
    public decimal? ExpectedSalary { get; set; }
    public int? NoticePeriodDays { get; set; }

    // Availability
    public DateTime? AvailableFrom { get; set; }

    // Cover Letter
    public string? CoverLetter { get; set; }

    // Screening
    public bool IsShortlisted { get; set; }
    public DateTime? ShortlistedDate { get; set; }
    public Guid? ShortlistedById { get; set; }
    public Employee? ShortlistedBy { get; set; }
    public string? ShortlistingNotes { get; set; }

    // Automatic Scoring (for dynamic shortlisting)
    public decimal? AutoScore { get; set; }
    public string? AutoScoreBreakdown { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    public Guid? RejectedById { get; set; }
    public Employee? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }

    // Offer
    public DateTime? OfferDate { get; set; }
    public bool OfferAccepted { get; set; }
    public DateTime? OfferAcceptanceDate { get; set; }

    // Relations
    public ICollection<ApplicationDocument> Documents { get; set; } = new List<ApplicationDocument>();
    public ICollection<ApplicationEducation> Education { get; set; } = new List<ApplicationEducation>();
    public ICollection<ApplicationExperience> Experience { get; set; } = new List<ApplicationExperience>();
    public ICollection<ApplicationReference> References { get; set; } = new List<ApplicationReference>();
    public ICollection<Interview> Interviews { get; set; } = new List<Interview>();
}

public class ApplicationDocument : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public JobApplication Application { get; set; } = null!;

    public DocumentType DocumentType { get; set; } // CV, Certificate, Cover Letter
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
}

public class ApplicationEducation : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public JobApplication Application { get; set; } = null!;

    public string Institution { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public string? Grade { get; set; }
    public int StartYear { get; set; }
    public int? EndYear { get; set; }
    public bool IsCurrent { get; set; }
}

public class ApplicationExperience : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public JobApplication Application { get; set; } = null!;

    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Responsibilities { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? ReasonForLeaving { get; set; }
}

public class ApplicationReference : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public JobApplication Application { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
}

/// <summary>
/// Interview session
/// </summary>
public class Interview : TenantEntity
{
    public string InterviewNumber { get; set; } = string.Empty;

    public Guid ApplicationId { get; set; }
    public JobApplication Application { get; set; } = null!;

    public int Round { get; set; } // 1st, 2nd, Final
    public InterviewType Type { get; set; } // Phone, Video, In-Person, Panel
    public InterviewStatus Status { get; set; }

    // Schedule
    public DateTime ScheduledDate { get; set; }
    public TimeSpan ScheduledStartTime { get; set; }
    public TimeSpan ScheduledEndTime { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }

    // Actual
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }

    // Panel
    public Guid? LeadInterviewerId { get; set; }
    public Employee? LeadInterviewer { get; set; }

    public ICollection<InterviewPanelist> Panelists { get; set; } = new List<InterviewPanelist>();
    public ICollection<InterviewEvaluation> Evaluations { get; set; } = new List<InterviewEvaluation>();

    // Outcome
    public InterviewOutcome? Outcome { get; set; }
    public string? OverallComments { get; set; }
    public decimal? OverallScore { get; set; }

    // Candidate Feedback
    public bool CandidateAttended { get; set; }
    public string? CandidateNoShowReason { get; set; }
}

public class InterviewPanelist : TenantEntity
{
    public Guid InterviewId { get; set; }
    public Interview Interview { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public PanelistRole Role { get; set; } // Chair, Member, Observer
    public bool IsRequired { get; set; }
    public bool Attended { get; set; }
}

public class InterviewEvaluation : TenantEntity
{
    public Guid InterviewId { get; set; }
    public Interview Interview { get; set; } = null!;

    public Guid EvaluatorId { get; set; }
    public Employee Evaluator { get; set; } = null!;

    // Evaluation Criteria
    public int? TechnicalSkillsScore { get; set; }
    public int? CommunicationScore { get; set; }
    public int? ProblemSolvingScore { get; set; }
    public int? CulturalFitScore { get; set; }
    public int? LeadershipScore { get; set; }

    public decimal? TotalScore { get; set; }
    public string? Strengths { get; set; }
    public string? Weaknesses { get; set; }
    public string? Comments { get; set; }

    public InterviewRecommendation Recommendation { get; set; }
    public DateTime EvaluationDate { get; set; }
}

/// <summary>
/// Shortlisting criteria for dynamic scoring
/// </summary>
public class ShortlistingCriteria : TenantEntity
{
    public Guid VacancyId { get; set; }
    public Vacancy Vacancy { get; set; } = null!;

    public string CriteriaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Weight { get; set; } // 0-100

    public ShortlistingCriteriaType Type { get; set; } // Qualification, Experience, Skills
    public string? RequiredValue { get; set; }
    public int? MinValue { get; set; }
    public int? MaxValue { get; set; }

    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }
}