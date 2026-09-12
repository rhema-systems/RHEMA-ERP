using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums.Safety;

// ============================================================
//  DOMAIN GROUPS
//  A. Reference / Lookup Catalog
//  B. Incident Management & Investigation
//  C. Hazard Register & Formal Risk Assessment
//  D. Safety Inspections & Audits
//  E. Permit-to-Work System
//  F. PPE Management
//  G. Safety Equipment
//  H. Contractor SHE Management
//  I. SHE Training & Awareness
//  J. Waste Management
//  K. Environmental Management
//  L. Occupational Health Management
//  M. Emergency Preparedness & Response
//  N. Regulatory Compliance Register
//  O. Safety Signage Register
//  P. SHE Performance Metrics / KPIs
//  Q. Safety Committee & Meetings
//  R. Return-to-Work Plans
//  S. SHE Reminder Engine
//  T. SHE Audit Management
//  U. Stop-Work Authority
//  V. Statutory Incident Submissions
//  W. SHE Controlled Document Register
//  X. Environmental Permit & Licence Register
//  Y. Environmental Monitoring Schedules
//  Z. Regulatory Updates Register
//  AA. Sustainability Initiatives
//  AB. Environmental Compliance Reviews & Clearance
//  AC. Monthly Environmental Reports
// ============================================================

namespace ErpSystem.Core.Entities.HR.Safety;

// ──────────────────────────────────────────────────────────
//  A. REFERENCE / LOOKUP CATALOG
// ──────────────────────────────────────────────────────────

/// <summary>
/// Lookup table for categories of safety incidents (Accident, Near Miss,
/// Dangerous Occurrence, Environmental, etc.). Drives IsReportable logic.
/// </summary>
public class SheIncidentType : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public SheIncidentCategory Category { get; set; }

    /// <summary>True when this incident type must be reported to an external authority.</summary>
    public bool IsReportable { get; set; }

    public Guid? RegulatoryBodyId { get; set; }

    [ForeignKey(nameof(RegulatoryBodyId))]
    public virtual SheRegulatoryBody? RegulatoryBody { get; set; }

    /// <summary>Statutory reporting window in hours (e.g. 24h for fatalities).</summary>
    public int? ReportingWindowHours { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SheIncidentTypeCorrectiveAction> DefaultCorrectiveActions { get; set; } = new List<SheIncidentTypeCorrectiveAction>();
}

/// <summary>
/// Predefined corrective actions that auto-populate on an incident of a given type.
/// </summary>
public class SheIncidentTypeCorrectiveAction : TenantEntity
{
    public Guid IncidentTypeId { get; set; }

    [ForeignKey(nameof(IncidentTypeId))]
    public virtual SheIncidentType IncidentType { get; set; } = null!;

    public Guid CorrectiveActionTemplateId { get; set; }

    [ForeignKey(nameof(CorrectiveActionTemplateId))]
    public virtual SheCorrectiveActionTemplate CorrectiveActionTemplate { get; set; } = null!;

    public int DisplayOrder { get; set; }

    /// <summary>Calendar days after incident date by which this action must be completed.</summary>
    public int? DeadlineDays { get; set; }

    public bool IsMandatory { get; set; } = true;
}

/// <summary>
/// Master classification of injury types (e.g. Laceration, Fracture, Burn, Strain).
/// </summary>
public class SheInjuryType : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Anatomical body parts. Linked from SafetyIncidentInvolvedPerson for structured injury reporting.
/// </summary>
public class SheBodyPart : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Region { get; set; } // e.g. Upper Limb, Lower Limb, Head

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Catalogue of standard corrective / preventive actions referenced across incidents,
/// hazards, inspections, and equipment.
/// </summary>
public class SheCorrectiveActionTemplate : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public SheCorrectiveActionCategory Category { get; set; }

    /// <summary>Default calendar days to complete from assignment date.</summary>
    public int? DefaultDeadlineDays { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SheIncidentTypeCorrectiveAction> IncidentTypeCorrectiveActions { get; set; } = new List<SheIncidentTypeCorrectiveAction>();
    public virtual ICollection<SheHazardCorrectiveAction> HazardCorrectiveActions { get; set; } = new List<SheHazardCorrectiveAction>();
    public virtual ICollection<SafetyEquipmentInspectionAction> InspectionActions { get; set; } = new List<SafetyEquipmentInspectionAction>();
}

/// <summary>
/// External regulatory / statutory bodies to whom incidents or compliance obligations
/// must be reported (e.g. Ghana Labour Commission, EPA, GNFS).
/// </summary>
public class SheRegulatoryBody : TenantEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ShortName { get; set; }

    [MaxLength(300)]
    public string? ContactAddress { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    public SheRegulatoryDomain Domain { get; set; }

    public bool IsActive { get; set; } = true;
}

// ──────────────────────────────────────────────────────────
//  B. INCIDENT MANAGEMENT & INVESTIGATION
// ──────────────────────────────────────────────────────────

public class SafetyIncident : TenantEntity
{
    [Required, MaxLength(30)]
    public string IncidentNumber { get; set; } = string.Empty;

    // ── Classification ──
    public SheIncidentCategory Category { get; set; }
    public SheIncidentSeverity Severity { get; set; }
    public SheIncidentStatus Status { get; set; }

    public Guid? IncidentTypeId { get; set; }

    [ForeignKey(nameof(IncidentTypeId))]
    public virtual SheIncidentType? IncidentType { get; set; }

    // ── When / Where ──
    public DateTime IncidentDate { get; set; }
    public TimeSpan? IncidentTime { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public Guid? SupervisorId { get; set; }

    [ForeignKey(nameof(SupervisorId))]
    public virtual Employee? Supervisor { get; set; }

    // ── Description ──
    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ImmediateCause { get; set; }

    [MaxLength(2000)]
    public string? UnderlyingCause { get; set; }

    [MaxLength(2000)]
    public string? ContributingFactors { get; set; }

    // Near Miss specific
    public bool CouldHaveCausedInjury { get; set; }

    [MaxLength(500)]
    public string? PotentialConsequence { get; set; }

    // ── Reporting ──
    public Guid ReportedById { get; set; }

    [ForeignKey(nameof(ReportedById))]
    public virtual Employee ReportedBy { get; set; } = null!;

    public DateTime ReportedDate { get; set; }

    [MaxLength(500)]
    public string? ImmediateActionTaken { get; set; }

    // ── Risk Rating ──
    public int? LikelihoodBefore { get; set; }   // 1-5
    public int? SeverityBefore { get; set; }      // 1-5
    public int? RiskScoreBefore { get; set; }     // computed in service: Likelihood * Severity

    public int? LikelihoodAfter { get; set; }
    public int? SeverityAfter { get; set; }
    public int? RiskScoreAfter { get; set; }

    // ── Investigation ──
    public bool RequiresInvestigation { get; set; }

    public Guid? LeadInvestigatorId { get; set; }

    [ForeignKey(nameof(LeadInvestigatorId))]
    public virtual Employee? LeadInvestigator { get; set; }

    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationTargetDate { get; set; }
    public DateTime? InvestigationCompleteDate { get; set; }

    [MaxLength(4000)]
    public string? RootCauseAnalysis { get; set; }

    [MaxLength(4000)]
    public string? InvestigationFindings { get; set; }

    public SheRootCauseMethod? RootCauseMethod { get; set; }

    // ── Regulatory Notification ──
    public bool ReportableToAuthority { get; set; }

    public Guid? ReportedToBodyId { get; set; }

    [ForeignKey(nameof(ReportedToBodyId))]
    public virtual SheRegulatoryBody? ReportedToBody { get; set; }

    public DateTime? AuthorityNotificationDate { get; set; }

    [MaxLength(100)]
    public string? AuthorityReferenceNumber { get; set; }

    public Guid? AuthorityNotifiedById { get; set; }

    [ForeignKey(nameof(AuthorityNotifiedById))]
    public virtual Employee? AuthorityNotifiedBy { get; set; }

    // ── Insurance ──
    public bool InsuranceClaimFiled { get; set; }
    public DateTime? ClaimFiledDate { get; set; }

    [MaxLength(100)]
    public string? ClaimReferenceNumber { get; set; }

    public Guid? InsuranceProviderId { get; set; }

    [ForeignKey(nameof(InsuranceProviderId))]
    public virtual MedicalInsuranceProvider? InsuranceProvider { get; set; }

    public decimal? ClaimAmount { get; set; }
    public bool ClaimApproved { get; set; }
    public decimal? AmountPaid { get; set; }

    // ── Lost Time Metrics (for LTIFR) ──
    /// <summary>Total lost calendar days across all involved persons. Denormalised for KPI queries.</summary>
    public int TotalLostDays { get; set; }

    public bool IsLostTimeInjury { get; set; }

    // ── Governance Review ──
    public Guid? ReviewedById { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }

    public DateTime? ReviewedDate { get; set; }

    [MaxLength(1000)]
    public string? ReviewComments { get; set; }

    // ── Closure ──
    public DateTime? ClosedDate { get; set; }

    public Guid? ClosedById { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }

    /// <summary>What the organisation takes away from this incident (FR-ENV-027 / FR-SHE-104; slice 15).</summary>
    [MaxLength(2000)]
    public string? LessonsLearned { get; set; }

    // ── Navigation ──
    public virtual ICollection<SafetyIncidentInvolvedPerson> InvolvedPersons { get; set; } = new List<SafetyIncidentInvolvedPerson>();
    public virtual ICollection<SafetyIncidentWitness> Witnesses { get; set; } = new List<SafetyIncidentWitness>();
    public virtual ICollection<SafetyIncidentCorrectiveAction> CorrectiveActions { get; set; } = new List<SafetyIncidentCorrectiveAction>();
    public virtual ICollection<SafetyIncidentFollowUp> FollowUps { get; set; } = new List<SafetyIncidentFollowUp>();
    public virtual ICollection<SafetyIncidentDocument> Documents { get; set; } = new List<SafetyIncidentDocument>();
    public virtual ICollection<SafetyIncidentInvestigationTeamMember> InvestigationTeam { get; set; } = new List<SafetyIncidentInvestigationTeamMember>();
    public virtual ICollection<SheStatutoryIncidentSubmission> StatutorySubmissions { get; set; } = new List<SheStatutoryIncidentSubmission>();
}

public class SafetyIncidentInvolvedPerson : TenantEntity
{
    public Guid IncidentId { get; set; }

    [ForeignKey(nameof(IncidentId))]
    public virtual SafetyIncident Incident { get; set; } = null!;

    // Person may be employee or external (contractor / visitor)
    public bool IsEmployee { get; set; }

    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? OrganizationOrCompany { get; set; } // for non-employees

    public SheInvolvedPersonRole RoleInIncident { get; set; }

    public bool WasOnDuty { get; set; }

    [MaxLength(500)]
    public string? ActivityBeingPerformed { get; set; }

    // PPE
    public bool WasUsingPpe { get; set; }

    [MaxLength(500)]
    public string? PpeUsed { get; set; }

    public bool PpeWasAdequate { get; set; }

    // ── Injury Details ──
    public bool WasInjured { get; set; }

    [MaxLength(1000)]
    public string? InjuryDescription { get; set; }

    public SheInjuryClassification? InjuryClassification { get; set; }

    public Guid? InjuryTypeId { get; set; }

    [ForeignKey(nameof(InjuryTypeId))]
    public virtual SheInjuryType? InjuryType { get; set; }

    public bool IsFatal { get; set; }

    public virtual ICollection<SafetyIncidentInjuredBodyPart> InjuredBodyParts { get; set; } = new List<SafetyIncidentInjuredBodyPart>();

    // ── First Aid ──
    public bool FirstAidGiven { get; set; }

    [MaxLength(500)]
    public string? FirstAidDetails { get; set; }

    public Guid? FirstAidProviderId { get; set; }

    [ForeignKey(nameof(FirstAidProviderId))]
    public virtual Employee? FirstAidProvider { get; set; }

    // ── Medical Treatment ──
    public bool MedicalTreatmentRequired { get; set; }

    public Guid? HealthcareFacilityId { get; set; }

    [ForeignKey(nameof(HealthcareFacilityId))]
    public virtual HealthcareFacility? HealthcareFacility { get; set; }

    public DateTime? TreatmentDate { get; set; }

    [MaxLength(500)]
    public string? DiagnosisGiven { get; set; }

    /// <summary>Optional link to a medical expense claim raised for this person.</summary>
    public Guid? MedicalExpenseClaimId { get; set; }

    [ForeignKey(nameof(MedicalExpenseClaimId))]
    public virtual MedicalExpenseClaim? MedicalExpenseClaim { get; set; }

    // ── Lost Time ──
    public bool ResultedInTimeOff { get; set; }
    public DateTime? TimeOffStartDate { get; set; }
    public DateTime? TimeOffEndDate { get; set; }
    public int? LostDays { get; set; }

    public bool OnLightDuty { get; set; }

    [MaxLength(500)]
    public string? LightDutyRestrictions { get; set; }

    /// <summary>Optional link to a return-to-work plan created for this person.</summary>
    public Guid? ReturnToWorkPlanId { get; set; }

    [ForeignKey(nameof(ReturnToWorkPlanId))]
    public virtual SheReturnToWorkPlan? ReturnToWorkPlan { get; set; }
}

/// <summary>
/// Maps body parts affected per involved person in a structured way.
/// </summary>
public class SafetyIncidentInjuredBodyPart : TenantEntity
{
    public Guid InvolvedPersonId { get; set; }

    [ForeignKey(nameof(InvolvedPersonId))]
    public virtual SafetyIncidentInvolvedPerson InvolvedPerson { get; set; } = null!;

    public Guid BodyPartId { get; set; }

    [ForeignKey(nameof(BodyPartId))]
    public virtual SheBodyPart BodyPart { get; set; } = null!;

    public SheBodySide? Side { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class SafetyIncidentWitness : TenantEntity
{
    public Guid IncidentId { get; set; }

    [ForeignKey(nameof(IncidentId))]
    public virtual SafetyIncident Incident { get; set; } = null!;

    public bool IsEmployee { get; set; }

    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? EmailAddress { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [MaxLength(3000)]
    public string? Statement { get; set; }

    public DateTime? StatementDate { get; set; }
    public bool StatementSigned { get; set; }

    [MaxLength(500)]
    public string? StatementDocumentPath { get; set; }

    public Guid? InterviewedById { get; set; }

    [ForeignKey(nameof(InterviewedById))]
    public virtual Employee? InterviewedBy { get; set; }

    public DateTime? InterviewDate { get; set; }
}

/// <summary>
/// Additional members on the investigation team beyond the lead investigator.
/// </summary>
public class SafetyIncidentInvestigationTeamMember : TenantEntity
{
    public Guid IncidentId { get; set; }

    [ForeignKey(nameof(IncidentId))]
    public virtual SafetyIncident Incident { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [MaxLength(100)]
    public string Role { get; set; } = string.Empty; // Lead, Technical Expert, HR Rep, Union Rep

    public DateTime JoinedDate { get; set; }
}

public class SafetyIncidentCorrectiveAction : TenantEntity
{
    public Guid IncidentId { get; set; }

    [ForeignKey(nameof(IncidentId))]
    public virtual SafetyIncident Incident { get; set; } = null!;

    /// <summary>Nullable — action may come from the type's default list or be added ad-hoc.</summary>
    public Guid? IncidentTypeCorrectiveActionId { get; set; }

    [ForeignKey(nameof(IncidentTypeCorrectiveActionId))]
    public virtual SheIncidentTypeCorrectiveAction? IncidentTypeCorrectiveAction { get; set; }

    [Required, MaxLength(1000)]
    public string ActionDescription { get; set; } = string.Empty;

    public SheCorrectiveActionPriority Priority { get; set; }
    public SheCorrectiveActionStatus Status { get; set; }

    public Guid ResponsiblePersonId { get; set; }

    [ForeignKey(nameof(ResponsiblePersonId))]
    public virtual Employee ResponsiblePerson { get; set; } = null!;

    public DateTime DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }

    // Effectiveness verification
    public bool EffectivenessVerified { get; set; }
    public DateTime? VerificationDate { get; set; }

    [MaxLength(500)]
    public string? EffectivenessReviewNotes { get; set; }

    public Guid? VerifiedById { get; set; }

    [ForeignKey(nameof(VerifiedById))]
    public virtual Employee? VerifiedBy { get; set; }
}

public class SafetyIncidentDocument : TenantEntity
{
    public Guid IncidentId { get; set; }

    [ForeignKey(nameof(IncidentId))]
    public virtual SafetyIncident Incident { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public SheIncidentDocumentType Type { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; }

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

public class SafetyIncidentFollowUp : TenantEntity
{
    public Guid IncidentId { get; set; }

    [ForeignKey(nameof(IncidentId))]
    public virtual SafetyIncident Incident { get; set; } = null!;

    public DateTime FollowUpDate { get; set; }

    [MaxLength(1000)]
    public string? ActionsTaken { get; set; }

    [MaxLength(500)]
    public string? PersonCondition { get; set; }

    [Required, MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    public bool FurtherFollowUpRequired { get; set; }
    public DateTime? NextFollowUpDate { get; set; }

    public Guid ConductedById { get; set; }

    [ForeignKey(nameof(ConductedById))]
    public virtual Employee ConductedBy { get; set; } = null!;
}

// ──────────────────────────────────────────────────────────
//  C. HAZARD REGISTER & FORMAL RISK ASSESSMENT
// ──────────────────────────────────────────────────────────

/// <summary>
/// Living hazard register entry. Hazards may be discovered during inspections,
/// pre-task assessments, or entered directly.
/// </summary>
public class SheHazard : TenantEntity
{
    [MaxLength(30)]
    public string? Code { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public SheHazardCategory Category { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    // Inherent (uncontrolled) risk
    public int InherentLikelihood { get; set; }   // 1-5
    public int InherentSeverity { get; set; }      // 1-5
    public int InherentRiskScore { get; set; }     // computed in service

    // Residual (post-control) risk
    public int ResidualLikelihood { get; set; }
    public int ResidualSeverity { get; set; }
    public int ResidualRiskScore { get; set; }

    public SheHazardRiskLevel ResidualRiskLevel { get; set; }

    public SheHazardStatus Status { get; set; }

    public Guid? OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public virtual Employee? Owner { get; set; }

    public DateTime? ReviewDueDate { get; set; }
    public DateTime? LastReviewedDate { get; set; }

    public Guid? LastReviewedById { get; set; }

    [ForeignKey(nameof(LastReviewedById))]
    public virtual Employee? LastReviewedBy { get; set; }

    /// <summary>
    /// Who reported the hazard (2026-09-04). Stamped from the token for a self-service report;
    /// the SHE desk may name the reporter when recording a hazard that reached it in person.
    /// Nullable because every hazard created before this column has no reporter on record.
    /// </summary>
    public Guid? ReportedById { get; set; }

    [ForeignKey(nameof(ReportedById))]
    public virtual Employee? ReportedBy { get; set; }

    public DateTime? ReportedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SheHazardControl> Controls { get; set; } = new List<SheHazardControl>();
    public virtual ICollection<SheHazardCorrectiveAction> CorrectiveActions { get; set; } = new List<SheHazardCorrectiveAction>();
}

/// <summary>
/// Hierarchy-of-controls record per hazard (Eliminate → Substitute → Engineering →
/// Administrative → PPE).
/// </summary>
public class SheHazardControl : TenantEntity
{
    public Guid HazardId { get; set; }

    [ForeignKey(nameof(HazardId))]
    public virtual SheHazard Hazard { get; set; } = null!;

    public SheHierarchyOfControl ControlLevel { get; set; }

    [Required, MaxLength(500)]
    public string ControlDescription { get; set; } = string.Empty;

    public SheControlStatus Status { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    [ForeignKey(nameof(ResponsiblePersonId))]
    public virtual Employee? ResponsiblePerson { get; set; }

    public DateTime? ImplementationDate { get; set; }
    public DateTime? ReviewDate { get; set; }
}

public class SheHazardCorrectiveAction : TenantEntity
{
    public Guid HazardId { get; set; }

    [ForeignKey(nameof(HazardId))]
    public virtual SheHazard Hazard { get; set; } = null!;

    public Guid CorrectiveActionTemplateId { get; set; }

    [ForeignKey(nameof(CorrectiveActionTemplateId))]
    public virtual SheCorrectiveActionTemplate CorrectiveActionTemplate { get; set; } = null!;

    public int? DeadlineDays { get; set; }
    public bool IsMandatory { get; set; } = true;
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Formal Risk Assessment (HIRA / JHA / Pre-Task RA). A time-stamped, approved
/// assessment document, separate from the living hazard register. Covers §4.4.
/// </summary>
public class SheRiskAssessment : TenantEntity
{
    [Required, MaxLength(30)]
    public string AssessmentNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public SheRiskAssessmentType Type { get; set; }

    [MaxLength(1000)]
    public string? Scope { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? SpecificActivity { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public SheRiskAssessmentStatus Status { get; set; }

    // Authorship
    public Guid PreparedById { get; set; }

    [ForeignKey(nameof(PreparedById))]
    public virtual Employee PreparedBy { get; set; } = null!;

    public DateTime PreparedDate { get; set; }

    // Review / Approval
    public Guid? ReviewedById { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }

    public DateTime? ReviewedDate { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    // Validity
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? NextReviewDate { get; set; }

    public int Version { get; set; } = 1;

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public virtual ICollection<SheRiskAssessmentHazard> AssessedHazards { get; set; } = new List<SheRiskAssessmentHazard>();
    public virtual ICollection<SheRiskAssessmentAcknowledgement> Acknowledgements { get; set; } = new List<SheRiskAssessmentAcknowledgement>();
}

/// <summary>
/// A hazard line item within a risk assessment, with inherent/residual scoring.
/// </summary>
public class SheRiskAssessmentHazard : TenantEntity
{
    public Guid RiskAssessmentId { get; set; }

    [ForeignKey(nameof(RiskAssessmentId))]
    public virtual SheRiskAssessment RiskAssessment { get; set; } = null!;

    /// <summary>Optional link to the master hazard register.</summary>
    public Guid? HazardId { get; set; }

    [ForeignKey(nameof(HazardId))]
    public virtual SheHazard? Hazard { get; set; }

    public int ItemNumber { get; set; }

    [Required, MaxLength(300)]
    public string HazardDescription { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? PotentialConsequences { get; set; }

    [MaxLength(300)]
    public string? AffectedPersons { get; set; }

    // Inherent risk
    public int InherentLikelihood { get; set; }
    public int InherentSeverity { get; set; }
    public int InherentRiskScore { get; set; }
    public SheRiskLevel InherentRiskLevel { get; set; }

    // Controls
    [MaxLength(2000)]
    public string? ControlMeasures { get; set; }

    // Residual risk
    public int ResidualLikelihood { get; set; }
    public int ResidualSeverity { get; set; }
    public int ResidualRiskScore { get; set; }
    public SheRiskLevel ResidualRiskLevel { get; set; }

    [MaxLength(200)]
    public string? ResponsiblePerson { get; set; }

    public DateTime? TargetDate { get; set; }
}

/// <summary>
/// Sign-off proving workers read and understood the risk assessment before commencing.
/// </summary>
public class SheRiskAssessmentAcknowledgement : TenantEntity
{
    public Guid RiskAssessmentId { get; set; }

    [ForeignKey(nameof(RiskAssessmentId))]
    public virtual SheRiskAssessment RiskAssessment { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public DateTime AcknowledgedDate { get; set; }

    [MaxLength(500)]
    public string? SignaturePath { get; set; }

    [MaxLength(500)]
    public string? Comments { get; set; }
}

// ──────────────────────────────────────────────────────────
//  D. SAFETY INSPECTIONS & AUDITS
// ──────────────────────────────────────────────────────────

public class SheInspectionChecklist : TenantEntity
{
    [Required, MaxLength(30)]
    public string ChecklistNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public SheInspectionType Type { get; set; }

    public int Version { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SheInspectionChecklistItem> Items { get; set; } = new List<SheInspectionChecklistItem>();
}

public class SheInspectionChecklistItem : TenantEntity
{
    public Guid ChecklistId { get; set; }

    [ForeignKey(nameof(ChecklistId))]
    public virtual SheInspectionChecklist Checklist { get; set; } = null!;

    public int ItemOrder { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;

    public bool IsMandatory { get; set; }

    [MaxLength(200)]
    public string? RegulatoryReference { get; set; }

    public SheRiskLevel? AssociatedRiskLevel { get; set; }
}

public class SafetyInspection : TenantEntity
{
    [Required, MaxLength(30)]
    public string InspectionNumber { get; set; } = string.Empty;

    public DateTime InspectionDate { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public SheInspectionType Type { get; set; }
    public SheInspectionCategory Category { get; set; }

    public Guid? ChecklistId { get; set; }

    [ForeignKey(nameof(ChecklistId))]
    public virtual SheInspectionChecklist? Checklist { get; set; }

    public Guid InspectorId { get; set; }

    [ForeignKey(nameof(InspectorId))]
    public virtual Employee Inspector { get; set; } = null!;

    [MaxLength(200)]
    public string? ExternalInspectorName { get; set; }

    [MaxLength(200)]
    public string? ExternalInspectorOrganization { get; set; }

    [MaxLength(2000)]
    public string? FindingsAndObservations { get; set; }

    [MaxLength(2000)]
    public string? RecommendedActions { get; set; }

    [MaxLength(1000)]
    public string? PositiveObservations { get; set; }

    public SheInspectionStatus Status { get; set; }
    public SheRiskLevel? OverallRiskRating { get; set; }

    /// <summary>Compliance score as a percentage 0–100.</summary>
    public int? ComplianceScore { get; set; }

    public DateTime? ComplianceDeadline { get; set; }
    public DateTime? NextInspectionDueDate { get; set; }

    public DateTime? ClosedDate { get; set; }

    public Guid? ClosedById { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }

    public virtual ICollection<SafetyInspectionItem> Items { get; set; } = new List<SafetyInspectionItem>();
    public virtual ICollection<SafetyInspectionHazard> Hazards { get; set; } = new List<SafetyInspectionHazard>();
    public virtual ICollection<SafetyInspectionDocument> Documents { get; set; } = new List<SafetyInspectionDocument>();
}

public class SafetyInspectionItem : TenantEntity
{
    public Guid InspectionId { get; set; }

    [ForeignKey(nameof(InspectionId))]
    public virtual SafetyInspection Inspection { get; set; } = null!;

    public Guid? ChecklistItemId { get; set; }

    [ForeignKey(nameof(ChecklistItemId))]
    public virtual SheInspectionChecklistItem? ChecklistItem { get; set; }

    [Required, MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;

    public SheComplianceStatus Status { get; set; }

    [MaxLength(1000)]
    public string? DeficiencyNoted { get; set; }

    [MaxLength(500)]
    public string? ActionRequired { get; set; }

    public SheRiskLevel? RiskLevel { get; set; }
    public DateTime? TargetDate { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    [ForeignKey(nameof(ResponsiblePersonId))]
    public virtual Employee? ResponsiblePerson { get; set; }

    public bool IsResolved { get; set; }
    public DateTime? ResolvedDate { get; set; }

    [MaxLength(500)]
    public string? ResolutionNotes { get; set; }

    public Guid? ResolvedById { get; set; }

    [ForeignKey(nameof(ResolvedById))]
    public virtual Employee? ResolvedBy { get; set; }
}

/// <summary>
/// Hazard discovered during an inspection. Links back to the master hazard register
/// once assessed (discovery → register promotion).
/// </summary>
public class SafetyInspectionHazard : TenantEntity
{
    public Guid InspectionId { get; set; }

    [ForeignKey(nameof(InspectionId))]
    public virtual SafetyInspection Inspection { get; set; } = null!;

    public Guid? HazardId { get; set; }

    [ForeignKey(nameof(HazardId))]
    public virtual SheHazard? Hazard { get; set; }

    [Required, MaxLength(500)]
    public string HazardDescription { get; set; } = string.Empty;

    public SheHazardStatus Status { get; set; }
    public SheHazardRiskLevel InitialRiskLevel { get; set; }
    public SheHazardRiskLevel ResidualRiskLevel { get; set; }

    public DateTime? ReviewDueDate { get; set; }

    public Guid? OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public virtual Employee? Owner { get; set; }

    public virtual ICollection<SafetyInspectionHazardAction> Actions { get; set; } = new List<SafetyInspectionHazardAction>();
}

public class SafetyInspectionHazardAction : TenantEntity
{
    public Guid InspectionHazardId { get; set; }

    [ForeignKey(nameof(InspectionHazardId))]
    public virtual SafetyInspectionHazard InspectionHazard { get; set; } = null!;

    public Guid CorrectiveActionTemplateId { get; set; }

    [ForeignKey(nameof(CorrectiveActionTemplateId))]
    public virtual SheCorrectiveActionTemplate CorrectiveActionTemplate { get; set; } = null!;

    public SheCorrectiveActionStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }

    public Guid? AssignedToId { get; set; }

    [ForeignKey(nameof(AssignedToId))]
    public virtual Employee? AssignedTo { get; set; }
}

public class SafetyInspectionDocument : TenantEntity
{
    public Guid InspectionId { get; set; }

    [ForeignKey(nameof(InspectionId))]
    public virtual SafetyInspection Inspection { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; }

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

// ──────────────────────────────────────────────────────────
//  E. PERMIT-TO-WORK SYSTEM
// ──────────────────────────────────────────────────────────

/// <summary>
/// A Permit-to-Work (PTW) issued before any high-risk activity begins.
/// Covers §3.0 and §4.3.9 (hot work, confined space, working at height,
/// excavation, electrical isolation, etc.).
/// </summary>
public class ShePermitToWork : TenantEntity
{
    [Required, MaxLength(30)]
    public string PermitNumber { get; set; } = string.Empty;

    public ShePermitType PermitType { get; set; }

    [Required, MaxLength(300)]
    public string WorkDescription { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    // ── Requestor ──
    public Guid RequestedById { get; set; }

    [ForeignKey(nameof(RequestedById))]
    public virtual Employee RequestedBy { get; set; } = null!;

    public Guid? ContractorId { get; set; }

    [ForeignKey(nameof(ContractorId))]
    public virtual SheContractor? Contractor { get; set; }

    public DateTime RequestedDate { get; set; }

    // ── Permit Window ──
    public DateTime PlannedStartDate { get; set; }
    public TimeSpan PlannedStartTime { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public TimeSpan PlannedEndTime { get; set; }

    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    // ── Status & Approvals ──
    public ShePermitStatus Status { get; set; }

    public Guid? IssuedById { get; set; }

    [ForeignKey(nameof(IssuedById))]
    public virtual Employee? IssuedBy { get; set; }

    public DateTime? IssuedDate { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    // ── Risk & Controls ──
    [MaxLength(2000)]
    public string? HazardsIdentified { get; set; }

    [MaxLength(2000)]
    public string? ControlMeasures { get; set; }

    [MaxLength(1000)]
    public string? PpeRequired { get; set; }

    [MaxLength(500)]
    public string? GasTestResults { get; set; }

    [MaxLength(500)]
    public string? IsolationDetails { get; set; } // LOTO info

    // ── Linked Risk Assessment ──
    public Guid? RiskAssessmentId { get; set; }

    [ForeignKey(nameof(RiskAssessmentId))]
    public virtual SheRiskAssessment? RiskAssessment { get; set; }

    // ── Suspension / Cancellation ──
    public bool IsSuspended { get; set; }
    public DateTime? SuspendedDate { get; set; }

    [MaxLength(500)]
    public string? SuspensionReason { get; set; }

    public Guid? SuspendedById { get; set; }

    [ForeignKey(nameof(SuspendedById))]
    public virtual Employee? SuspendedBy { get; set; }

    // ── Closure ──
    public DateTime? ClosedDate { get; set; }

    public Guid? ClosedById { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }

    public bool WorkCompletedSatisfactorily { get; set; }
    public bool AreaLeftSafe { get; set; }

    [MaxLength(500)]
    public string? ReinstatementNotes { get; set; }

    // ── Navigation ──
    public virtual ICollection<ShePermitToWorkWorker> AuthorisedWorkers { get; set; } = new List<ShePermitToWorkWorker>();
    public virtual ICollection<ShePermitToWorkExtension> Extensions { get; set; } = new List<ShePermitToWorkExtension>();
    public virtual ICollection<ShePermitToWorkDocument> Documents { get; set; } = new List<ShePermitToWorkDocument>();
}

public class ShePermitToWorkWorker : TenantEntity
{
    public Guid PermitToWorkId { get; set; }

    [ForeignKey(nameof(PermitToWorkId))]
    public virtual ShePermitToWork PermitToWork { get; set; } = null!;

    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    [MaxLength(200)]
    public string WorkerName { get; set; } = string.Empty; // for external workers

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? TradeOrRole { get; set; }

    public bool Briefed { get; set; }
    public DateTime? BriefedDate { get; set; }
    public bool SignedOff { get; set; }
    public DateTime? SignedDate { get; set; }
}

public class ShePermitToWorkExtension : TenantEntity
{
    public Guid PermitToWorkId { get; set; }

    [ForeignKey(nameof(PermitToWorkId))]
    public virtual ShePermitToWork PermitToWork { get; set; } = null!;

    public int ExtensionNumber { get; set; }

    public DateTime NewEndDate { get; set; }
    public TimeSpan NewEndTime { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public Guid ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee ApprovedBy { get; set; } = null!;

    public DateTime ApprovedDate { get; set; }
}

public class ShePermitToWorkDocument : TenantEntity
{
    public Guid PermitToWorkId { get; set; }

    [ForeignKey(nameof(PermitToWorkId))]
    public virtual ShePermitToWork PermitToWork { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; }

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

// ──────────────────────────────────────────────────────────
//  F. PPE MANAGEMENT
// ──────────────────────────────────────────────────────────

public class PpeType : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public ShePpeCategory Category { get; set; }

    [MaxLength(100)]
    public string? Standard { get; set; } // ANSI, EN, etc.

    public int? LifespanMonths { get; set; }
    public bool RequiresSerialNumber { get; set; }
    public bool HasExpiryDate { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<PpeInventory> InventoryItems { get; set; } = new List<PpeInventory>();
    public virtual ICollection<PpeIssuance> Issuances { get; set; } = new List<PpeIssuance>();
    public virtual ICollection<JobRolePpeRequirement> JobRoleRequirements { get; set; } = new List<JobRolePpeRequirement>();
}

public class PpeInventory : TenantEntity
{
    public Guid PpeTypeId { get; set; }

    [ForeignKey(nameof(PpeTypeId))]
    public virtual PpeType PpeType { get; set; } = null!;

    [Required, MaxLength(50)]
    public string ItemCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Brand { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Model { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Size { get; set; }

    public int QuantityInStock { get; set; }
    public int MinimumStockLevel { get; set; }
    public int ReorderLevel { get; set; }

    [MaxLength(200)]
    public string? StorageLocation { get; set; }

    public decimal? UnitCost { get; set; }

    [MaxLength(200)]
    public string? Supplier { get; set; }

    public DateTime? LastRestockDate { get; set; }

    public Guid? LastRestockedById { get; set; }

    [ForeignKey(nameof(LastRestockedById))]
    public virtual Employee? LastRestockedBy { get; set; }
}

public class PpeIssuance : TenantEntity
{
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid PpeTypeId { get; set; }

    [ForeignKey(nameof(PpeTypeId))]
    public virtual PpeType PpeType { get; set; } = null!;

    public DateTime IssueDate { get; set; }
    public int Quantity { get; set; }

    [MaxLength(20)]
    public string? Size { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }

    public ShePpeCondition? ConditionWhenIssued { get; set; }
    public ShePpeCondition? ConditionWhenReturned { get; set; }

    public Guid IssuedById { get; set; }

    [ForeignKey(nameof(IssuedById))]
    public virtual Employee IssuedBy { get; set; } = null!;

    public bool IsReturned { get; set; }

    public Guid? ReturnedToId { get; set; }

    [ForeignKey(nameof(ReturnedToId))]
    public virtual Employee? ReturnedTo { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Role-based PPE requirement matrix — which PPE a given job role must be issued.
/// </summary>
public class JobRolePpeRequirement : TenantEntity
{
    [Required, MaxLength(50)]
    public string JobRoleCode { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string JobRoleName { get; set; } = string.Empty;

    public Guid PpeTypeId { get; set; }

    [ForeignKey(nameof(PpeTypeId))]
    public virtual PpeType PpeType { get; set; } = null!;

    public int Quantity { get; set; } = 1;
    public int ReplacementFrequencyMonths { get; set; }
    public bool IsMandatory { get; set; } = true;
}

// ──────────────────────────────────────────────────────────
//  G. SAFETY EQUIPMENT
// ──────────────────────────────────────────────────────────

public class SafetyEquipment : TenantEntity
{
    [Required, MaxLength(30)]
    public string EquipmentNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public SheSafetyEquipmentType Type { get; set; }

    public Guid LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location Location { get; set; } = null!;

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public DateTime? InstallationDate { get; set; }

    public bool RequiresRegularInspection { get; set; }
    public int InspectionFrequencyDays { get; set; }
    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDueDate { get; set; }

    public bool RequiresCertification { get; set; }
    public DateTime? CertificationExpiryDate { get; set; }

    [MaxLength(500)]
    public string? CertificationDocumentPath { get; set; }

    public SheSafetyEquipmentStatus Status { get; set; }

    public DateTime? OutOfServiceDate { get; set; }

    [MaxLength(500)]
    public string? OutOfServiceReason { get; set; }

    public DateTime? ExpiryDate { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDueDate { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    [ForeignKey(nameof(ResponsiblePersonId))]
    public virtual Employee? ResponsiblePerson { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public virtual ICollection<SafetyEquipmentInspection> Inspections { get; set; } = new List<SafetyEquipmentInspection>();
    public virtual ICollection<SafetyEquipmentMaintenance> MaintenanceRecords { get; set; } = new List<SafetyEquipmentMaintenance>();
}

public class SafetyEquipmentInspection : TenantEntity
{
    public Guid EquipmentId { get; set; }

    [ForeignKey(nameof(EquipmentId))]
    public virtual SafetyEquipment Equipment { get; set; } = null!;

    public DateTime InspectionDate { get; set; }
    public SheInspectionType InspectionType { get; set; }

    public Guid InspectedById { get; set; }

    [ForeignKey(nameof(InspectedById))]
    public virtual Employee InspectedBy { get; set; } = null!;

    public SheInspectionResult Result { get; set; }

    [MaxLength(2000)]
    public string? Findings { get; set; }

    [MaxLength(2000)]
    public string? DeficienciesNoted { get; set; }

    public DateTime? NextInspectionDate { get; set; }

    public virtual ICollection<SafetyEquipmentInspectionAction> InspectionActions { get; set; } = new List<SafetyEquipmentInspectionAction>();
}

public class SafetyEquipmentInspectionAction : TenantEntity
{
    public Guid SafetyEquipmentInspectionId { get; set; }

    [ForeignKey(nameof(SafetyEquipmentInspectionId))]
    public virtual SafetyEquipmentInspection Inspection { get; set; } = null!;

    public Guid CorrectiveActionTemplateId { get; set; }

    [ForeignKey(nameof(CorrectiveActionTemplateId))]
    public virtual SheCorrectiveActionTemplate CorrectiveActionTemplate { get; set; } = null!;

    public SheCorrectiveActionStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }

    public Guid? AssignedToId { get; set; }

    [ForeignKey(nameof(AssignedToId))]
    public virtual Employee? AssignedTo { get; set; }
}

public class SafetyEquipmentMaintenance : TenantEntity
{
    public Guid EquipmentId { get; set; }

    [ForeignKey(nameof(EquipmentId))]
    public virtual SafetyEquipment Equipment { get; set; } = null!;

    /// <summary>
    /// Loose link to an equipment-maintenance module record (no FK to avoid cross-module coupling).
    /// </summary>
    public Guid? MaintenanceRecordId { get; set; }

    public DateTime MaintenanceDate { get; set; }

    [MaxLength(100)]
    public string? MaintenanceType { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool EquipmentTakenOutOfService { get; set; }
    public DateTime? OutOfServiceStart { get; set; }
    public DateTime? OutOfServiceEnd { get; set; }

    public decimal? Cost { get; set; }

    [MaxLength(200)]
    public string? PerformedBy { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

// ──────────────────────────────────────────────────────────
//  H. CONTRACTOR SHE MANAGEMENT  (§9.0)
// ──────────────────────────────────────────────────────────

public class SheContractor : TenantEntity
{
    [Required, MaxLength(30)]
    public string ContractorCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TradingName { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(100)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(50)]
    public string? PrimaryContactPhone { get; set; }

    [MaxLength(100)]
    public string? PrimaryContactEmail { get; set; }

    public SheContractorStatus SheStatus { get; set; }

    /// <summary>Overall SHE pre-qualification score (0–100).</summary>
    public int? PreQualificationScore { get; set; }

    public DateTime? PreQualificationDate { get; set; }
    public DateTime? PreQualificationExpiryDate { get; set; }

    public Guid? PreQualifiedById { get; set; }

    [ForeignKey(nameof(PreQualifiedById))]
    public virtual Employee? PreQualifiedBy { get; set; }

    [MaxLength(1000)]
    public string? SheConditions { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SheContractorInduction> Inductions { get; set; } = new List<SheContractorInduction>();
    public virtual ICollection<SheContractorInspection> SheInspections { get; set; } = new List<SheContractorInspection>();
    public virtual ICollection<SheContractorNonCompliance> NonCompliances { get; set; } = new List<SheContractorNonCompliance>();
    public virtual ICollection<SheContractorDocument> Documents { get; set; } = new List<SheContractorDocument>();
}

public class SheContractorInduction : TenantEntity
{
    public Guid ContractorId { get; set; }

    [ForeignKey(nameof(ContractorId))]
    public virtual SheContractor Contractor { get; set; } = null!;

    [Required, MaxLength(200)]
    public string WorkerName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? WorkerIdOrPassport { get; set; }

    [MaxLength(100)]
    public string? Trade { get; set; }

    public DateTime InductionDate { get; set; }

    public Guid ConductedById { get; set; }

    [ForeignKey(nameof(ConductedById))]
    public virtual Employee ConductedBy { get; set; } = null!;

    public bool InductionPassed { get; set; }

    public DateTime? InductionExpiryDate { get; set; }

    [MaxLength(500)]
    public string? SignaturePath { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class SheContractorInspection : TenantEntity
{
    public Guid ContractorId { get; set; }

    [ForeignKey(nameof(ContractorId))]
    public virtual SheContractor Contractor { get; set; } = null!;

    [Required, MaxLength(30)]
    public string InspectionNumber { get; set; } = string.Empty;

    public DateTime InspectionDate { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid InspectorId { get; set; }

    [ForeignKey(nameof(InspectorId))]
    public virtual Employee Inspector { get; set; } = null!;

    public int? ComplianceScore { get; set; }
    public SheInspectionResult Result { get; set; }

    [MaxLength(3000)]
    public string? Findings { get; set; }

    [MaxLength(2000)]
    public string? RecommendedActions { get; set; }

    public DateTime? NextInspectionDate { get; set; }

    public SheInspectionStatus Status { get; set; }
}

public class SheContractorNonCompliance : TenantEntity
{
    public Guid ContractorId { get; set; }

    [ForeignKey(nameof(ContractorId))]
    public virtual SheContractor Contractor { get; set; } = null!;

    [Required, MaxLength(30)]
    public string NoticeNumber { get; set; } = string.Empty;

    public DateTime IssuedDate { get; set; }

    public Guid IssuedById { get; set; }

    [ForeignKey(nameof(IssuedById))]
    public virtual Employee IssuedBy { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string ViolationDescription { get; set; } = string.Empty;

    public SheNonComplianceSeverity Severity { get; set; }

    public DateTime RectificationDeadline { get; set; }

    public bool IsRepeatViolation { get; set; }
    public int RepeatCount { get; set; }

    public SheNonComplianceStatus Status { get; set; }

    public DateTime? RectificationDate { get; set; }

    [MaxLength(1000)]
    public string? ContractorResponse { get; set; }

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }

    public DateTime? ClosedDate { get; set; }

    public Guid? ClosedById { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }

    public SheContractorSanction? SanctionApplied { get; set; }
}

public class SheContractorDocument : TenantEntity
{
    public Guid ContractorId { get; set; }

    [ForeignKey(nameof(ContractorId))]
    public virtual SheContractor Contractor { get; set; } = null!;

    public SheContractorDocumentType DocumentType { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Title { get; set; }

    public DateTime? DocumentDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public bool IsVerified { get; set; }

    public Guid? VerifiedById { get; set; }

    [ForeignKey(nameof(VerifiedById))]
    public virtual Employee? VerifiedBy { get; set; }

    public DateTime? VerifiedDate { get; set; }

    [MaxLength(300)]
    public string? VerificationNotes { get; set; }

    public DateTime UploadedDate { get; set; }

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

// ──────────────────────────────────────────────────────────
//  I. SHE TRAINING & AWARENESS  (§10.0)
// ──────────────────────────────────────────────────────────

public class SheTrainingPlan : TenantEntity
{
    [Required, MaxLength(30)]
    public string PlanNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int Year { get; set; }
    public int? Quarter { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public SheTrainingPlanStatus Status { get; set; }

    public Guid PreparedById { get; set; }

    [ForeignKey(nameof(PreparedById))]
    public virtual Employee PreparedBy { get; set; } = null!;

    public DateTime PreparedDate { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public virtual ICollection<SheTrainingProgram> Programs { get; set; } = new List<SheTrainingProgram>();
}

public class SheTrainingProgram : TenantEntity
{
    [Required, MaxLength(30)]
    public string ProgramCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public SheTrainingCategory Category { get; set; }

    public Guid? PlanId { get; set; }

    [ForeignKey(nameof(PlanId))]
    public virtual SheTrainingPlan? Plan { get; set; }

    public SheTrainingDeliveryMethod DeliveryMethod { get; set; }

    public int DurationMinutes { get; set; }

    public DateTime? ScheduledDate { get; set; }
    public DateTime? ActualDate { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    public Guid? TrainerId { get; set; }

    [ForeignKey(nameof(TrainerId))]
    public virtual Employee? Trainer { get; set; }

    [MaxLength(200)]
    public string? ExternalTrainerName { get; set; }

    [MaxLength(200)]
    public string? ExternalTrainerOrganization { get; set; }

    public SheTrainingStatus Status { get; set; }

    public int? MaxParticipants { get; set; }
    public int? ActualAttendees { get; set; }

    [MaxLength(500)]
    public string? MaterialPath { get; set; }

    public bool WasEvaluated { get; set; }

    [MaxLength(1000)]
    public string? EvaluationSummary { get; set; }

    public Guid? EvaluatedById { get; set; }

    [ForeignKey(nameof(EvaluatedById))]
    public virtual Employee? EvaluatedBy { get; set; }

    public DateTime? EvaluationDate { get; set; }

    public virtual ICollection<SheTrainingAttendance> Attendances { get; set; } = new List<SheTrainingAttendance>();
}

public class SheTrainingAttendance : TenantEntity
{
    public Guid ProgramId { get; set; }

    [ForeignKey(nameof(ProgramId))]
    public virtual SheTrainingProgram Program { get; set; } = null!;

    public bool IsEmployee { get; set; }

    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    [MaxLength(200)]
    public string AttendanceName { get; set; } = string.Empty; // used when not an employee

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    public bool Attended { get; set; }
    public DateTime? SignedDate { get; set; }

    [MaxLength(500)]
    public string? SignaturePath { get; set; }

    public bool? AssessmentPassed { get; set; }
    public int? AssessmentScore { get; set; }

    public DateTime? CertificateExpiryDate { get; set; }

    [MaxLength(200)]
    public string? CertificateDocumentPath { get; set; }
}

// ──────────────────────────────────────────────────────────
//  J. WASTE MANAGEMENT  (§5.2)
// ──────────────────────────────────────────────────────────

public class SheWasteType : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public SheWasteClassification Classification { get; set; }

    [MaxLength(500)]
    public string? DisposalRequirements { get; set; }

    [MaxLength(200)]
    public string? RegulatoryReference { get; set; }

    public bool RequiresManifest { get; set; }

    public bool IsActive { get; set; } = true;
}

public class SheWasteDisposalRecord : TenantEntity
{
    [Required, MaxLength(30)]
    public string RecordNumber { get; set; } = string.Empty;

    public Guid WasteTypeId { get; set; }

    [ForeignKey(nameof(WasteTypeId))]
    public virtual SheWasteType WasteType { get; set; } = null!;

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? GenerationArea { get; set; }

    /// <summary>On-site interim storage before disposal (FR-ENV-020, slice 17).</summary>
    [MaxLength(200)]
    public string? StorageLocation { get; set; }

    public DateTime DisposalDate { get; set; }

    public decimal Quantity { get; set; }
    public SheWasteMeasurementUnit Unit { get; set; }

    public SheWasteDisposalMethod DisposalMethod { get; set; }

    public Guid? WasteContractorId { get; set; }

    [ForeignKey(nameof(WasteContractorId))]
    public virtual SheContractor? WasteContractor { get; set; }

    [MaxLength(100)]
    public string? ManifestNumber { get; set; }

    [MaxLength(500)]
    public string? DisposalSite { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid RecordedById { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee RecordedBy { get; set; } = null!;

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

// ──────────────────────────────────────────────────────────
//  K. ENVIRONMENTAL MANAGEMENT  (§5.0)
// ──────────────────────────────────────────────────────────

public class SheEnvironmentalIncident : TenantEntity
{
    [Required, MaxLength(30)]
    public string IncidentNumber { get; set; } = string.Empty;

    /// <summary>Optional link when the environmental incident is also a general safety incident.</summary>
    public Guid? SafetyIncidentId { get; set; }

    [ForeignKey(nameof(SafetyIncidentId))]
    public virtual SafetyIncident? SafetyIncident { get; set; }

    public SheEnvironmentalIncidentType Type { get; set; }

    public SheEnvironmentalMedia AffectedMedia { get; set; }

    public SheIncidentSeverity Severity { get; set; }

    public DateTime IncidentDate { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Required, MaxLength(3000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? SpillVolume { get; set; }

    [MaxLength(500)]
    public string? SubstanceInvolved { get; set; }

    [MaxLength(1000)]
    public string? ImmediateResponseAction { get; set; }

    public bool ReportedToEpa { get; set; }
    public DateTime? EpaNotificationDate { get; set; }

    [MaxLength(100)]
    public string? EpaReferenceNumber { get; set; }

    public SheEnvironmentalIncidentStatus Status { get; set; }

    public Guid ReportedById { get; set; }

    [ForeignKey(nameof(ReportedById))]
    public virtual Employee ReportedBy { get; set; } = null!;

    public DateTime ReportedDate { get; set; }

    [MaxLength(2000)]
    public string? InvestigationFindings { get; set; }

    [MaxLength(2000)]
    public string? CorrectiveActions { get; set; }

    /// <summary>FR-ENV-027 (slice 17) — preventive actions distinct from the corrective ones.</summary>
    [MaxLength(2000)]
    public string? PreventiveActions { get; set; }

    /// <summary>FR-ENV-027 (slice 17) — captured at close-out, mirrors SafetyIncident.LessonsLearned.</summary>
    [MaxLength(2000)]
    public string? LessonsLearned { get; set; }

    public DateTime? ClosedDate { get; set; }

    public Guid? ClosedById { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }
}

public class SheEnvironmentalMonitoringRecord : TenantEntity
{
    [Required, MaxLength(30)]
    public string RecordNumber { get; set; } = string.Empty;

    public SheEnvironmentalMonitoringType MonitoringType { get; set; }

    /// <summary>Set when the record completes a scheduled activity (FR-ENV-023/024, slice 17).</summary>
    public Guid? ScheduleId { get; set; }

    [ForeignKey(nameof(ScheduleId))]
    public virtual SheEnvironmentalMonitoringSchedule? Schedule { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? MonitoringPoint { get; set; }

    public DateTime MeasurementDate { get; set; }

    public decimal MeasuredValue { get; set; }

    [Required, MaxLength(30)]
    public string Unit { get; set; } = string.Empty;

    public decimal? RegulatoryLimit { get; set; }
    public decimal? ActionLevel { get; set; }

    public bool ExceedsLimit { get; set; }
    public bool ExceedsActionLevel { get; set; }

    [MaxLength(500)]
    public string? InstrumentUsed { get; set; }

    [MaxLength(200)]
    public string? WeatherConditions { get; set; }

    public Guid MeasuredById { get; set; }

    [ForeignKey(nameof(MeasuredById))]
    public virtual Employee MeasuredBy { get; set; } = null!;

    [MaxLength(1000)]
    public string? Comments { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

// ──────────────────────────────────────────────────────────
//  L. OCCUPATIONAL HEALTH MANAGEMENT  (§13.0)
// ──────────────────────────────────────────────────────────

public class SheOccupationalHealthSurveillance : TenantEntity
{
    [Required, MaxLength(30)]
    public string SurveillanceNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public SheHealthSurveillanceType Type { get; set; }

    [MaxLength(300)]
    public string? ExposureHazard { get; set; }

    public DateTime ExaminationDate { get; set; }
    public DateTime? NextExaminationDate { get; set; }

    public Guid? HealthcareFacilityId { get; set; }

    [ForeignKey(nameof(HealthcareFacilityId))]
    public virtual HealthcareFacility? HealthcareFacility { get; set; }

    [MaxLength(200)]
    public string? ExaminingPhysician { get; set; }

    public SheHealthSurveillanceResult Result { get; set; }

    [MaxLength(2000)]
    public string? Findings { get; set; }

    [MaxLength(1000)]
    public string? Recommendations { get; set; }

    public bool WorkRestrictionIssued { get; set; }

    [MaxLength(500)]
    public string? WorkRestrictionDetails { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public Guid RecordedById { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee RecordedBy { get; set; } = null!;
}

public class SheFirstAidStation : TenantEntity
{
    [Required, MaxLength(30)]
    public string StationCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location Location { get; set; } = null!;

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public SheFirstAidStationType Type { get; set; }

    public Guid? ResponsibleAiderId { get; set; }

    [ForeignKey(nameof(ResponsibleAiderId))]
    public virtual Employee? ResponsibleAider { get; set; }

    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDate { get; set; }

    public bool IsFullyStocked { get; set; }

    [MaxLength(500)]
    public string? StockingDeficiencies { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(300)]
    public string? Notes { get; set; }
}

public class SheWellnessProgram : TenantEntity
{
    [Required, MaxLength(30)]
    public string ProgramCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public SheWellnessProgramType Type { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public Guid? CoordinatorId { get; set; }

    [ForeignKey(nameof(CoordinatorId))]
    public virtual Employee? Coordinator { get; set; }

    public SheWellnessProgramStatus Status { get; set; }

    public int? ParticipantsCount { get; set; }

    [MaxLength(1000)]
    public string? Outcomes { get; set; }

    public bool IsActive { get; set; } = true;
}

// ──────────────────────────────────────────────────────────
//  M. EMERGENCY PREPAREDNESS & RESPONSE  (§7.0)
// ──────────────────────────────────────────────────────────

public class EmergencyPlan : TenantEntity
{
    [Required, MaxLength(100)]
    public string PlanNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string PlanName { get; set; } = string.Empty;

    public SheEmergencyType Type { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Procedures { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? EvacuationRouteDocumentPath { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    public DateTime LastReviewed { get; set; }
    public DateTime NextReviewDate { get; set; }

    public Guid PlanOwnerId { get; set; }

    [ForeignKey(nameof(PlanOwnerId))]
    public virtual Employee PlanOwner { get; set; } = null!;

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SheAssemblyPoint> AssemblyPoints { get; set; } = new List<SheAssemblyPoint>();
    public virtual ICollection<EmergencyContact> EmergencyContacts { get; set; } = new List<EmergencyContact>();
    public virtual ICollection<EmergencyDrill> Drills { get; set; } = new List<EmergencyDrill>();
    public virtual ICollection<EmergencyResponseTeam> TeamMembers { get; set; } = new List<EmergencyResponseTeam>();
}

public class SheAssemblyPoint : TenantEntity
{
    public Guid EmergencyPlanId { get; set; }

    [ForeignKey(nameof(EmergencyPlanId))]
    public virtual EmergencyPlan EmergencyPlan { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public decimal? GpsLatitude { get; set; }
    public decimal? GpsLongitude { get; set; }

    public int? Capacity { get; set; }

    public bool IsActive { get; set; } = true;
}

public class EmergencyContact : TenantEntity
{
    public Guid EmergencyPlanId { get; set; }

    [ForeignKey(nameof(EmergencyPlanId))]
    public virtual EmergencyPlan EmergencyPlan { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(50)]
    public string PrimaryPhone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? AlternatePhone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    public bool IsExternal { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class EmergencyDrill : TenantEntity
{
    public Guid EmergencyPlanId { get; set; }

    [ForeignKey(nameof(EmergencyPlanId))]
    public virtual EmergencyPlan EmergencyPlan { get; set; } = null!;

    [Required, MaxLength(30)]
    public string DrillNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string DrillName { get; set; } = string.Empty;

    public DateTime DrillDate { get; set; }
    public TimeSpan? DrillTime { get; set; }

    [MaxLength(1000)]
    public string? Scenario { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    public Guid? DepartmentId { get; set; }

    [ForeignKey(nameof(DepartmentId))]
    public virtual Department? Department { get; set; }

    public bool WasAnnounced { get; set; }
    public int? ParticipantsCount { get; set; }
    public TimeSpan? EvacuationTime { get; set; }

    [MaxLength(2000)]
    public string? Observations { get; set; }

    [MaxLength(1000)]
    public string? StrengthsIdentified { get; set; }

    [MaxLength(1000)]
    public string? AreasForImprovement { get; set; }

    [MaxLength(1000)]
    public string? CorrectiveActions { get; set; }

    public bool ObjectivesMet { get; set; }

    public Guid CoordinatorId { get; set; }

    [ForeignKey(nameof(CoordinatorId))]
    public virtual Employee Coordinator { get; set; } = null!;

    public DateTime? NextDrillScheduledDate { get; set; }

    [MaxLength(500)]
    public string? DrillReportDocumentPath { get; set; }
}

public class EmergencyResponseTeam : TenantEntity
{
    public Guid EmergencyPlanId { get; set; }

    [ForeignKey(nameof(EmergencyPlanId))]
    public virtual EmergencyPlan EmergencyPlan { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Responsibilities { get; set; }

    public DateTime? CertificateExpiryDate { get; set; }

    [MaxLength(200)]
    public string? CertificateDocumentPath { get; set; }

    public bool IsActive { get; set; } = true;
}

// ──────────────────────────────────────────────────────────
//  N. REGULATORY COMPLIANCE REGISTER  (§17.0)
// ──────────────────────────────────────────────────────────

public class SheRegulatoryObligation : TenantEntity
{
    [Required, MaxLength(30)]
    public string ObligationCode { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public SheRegulatoryDomain Domain { get; set; }

    [MaxLength(200)]
    public string? LegislationName { get; set; }

    [MaxLength(100)]
    public string? SectionOrClause { get; set; }

    public Guid? RegulatoryBodyId { get; set; }

    [ForeignKey(nameof(RegulatoryBodyId))]
    public virtual SheRegulatoryBody? RegulatoryBody { get; set; }

    public SheComplianceStatus ComplianceStatus { get; set; }

    [MaxLength(1000)]
    public string? ComplianceNotes { get; set; }

    public Guid? ObligationOwnerId { get; set; }

    [ForeignKey(nameof(ObligationOwnerId))]
    public virtual Employee? ObligationOwner { get; set; }

    public DateTime? LastReviewedDate { get; set; }
    public DateTime? NextReviewDate { get; set; }

    [MaxLength(500)]
    public string? LastAmendmentNotes { get; set; }

    public DateTime? LastAmendmentDate { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SheRegulatoryComplianceEvidence> EvidenceRecords { get; set; } = new List<SheRegulatoryComplianceEvidence>();
}

public class SheRegulatoryComplianceEvidence : TenantEntity
{
    public Guid ObligationId { get; set; }

    [ForeignKey(nameof(ObligationId))]
    public virtual SheRegulatoryObligation Obligation { get; set; } = null!;

    [Required, MaxLength(200)]
    public string EvidenceTitle { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime EvidenceDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public Guid RecordedById { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee RecordedBy { get; set; } = null!;
}

// ──────────────────────────────────────────────────────────
//  O. SAFETY SIGNAGE REGISTER  (§15.0)
// ──────────────────────────────────────────────────────────

public class SafetySign : TenantEntity
{
    [Required, MaxLength(30)]
    public string SignCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public SheSafetySignType SignType { get; set; }

    public Guid LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location Location { get; set; } = null!;

    [MaxLength(300)]
    public string? SpecificPosition { get; set; }

    public DateTime InstallationDate { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Material { get; set; }

    public bool IsPhotoluminescent { get; set; }

    public SheSafetySignStatus Status { get; set; }

    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDate { get; set; }

    [MaxLength(500)]
    public string? InspectionNotes { get; set; }

    public bool IsActive { get; set; } = true;
}

// ──────────────────────────────────────────────────────────
//  P. SHE PERFORMANCE METRICS / KPIs  (§19.0, §22.0)
// ──────────────────────────────────────────────────────────

public class ShePerformanceSnapshot : TenantEntity
{
    [Required, MaxLength(30)]
    public string SnapshotNumber { get; set; } = string.Empty;

    public SheSnapshotPeriodType PeriodType { get; set; }

    public int Year { get; set; }

    /// <summary>Month (1–12) for monthly; quarter (1–4) for quarterly. Null for annual.</summary>
    public int? PeriodNumber { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    // KPI 1: Accidents / Incidents
    public int TotalAccidents { get; set; }
    public int TotalIncidents { get; set; }
    public int TotalNearMisses { get; set; }
    public int TotalDangerousOccurrences { get; set; }
    public int TotalFatalities { get; set; }
    public int TotalLostTimeInjuries { get; set; }

    // KPI 2: LTIFR
    public decimal? LostTimeInjuryFrequencyRate { get; set; }
    public long TotalManHoursWorked { get; set; }
    public int TotalLostDays { get; set; }

    // KPI 3: Inspections
    public int InspectionsPlanned { get; set; }
    public int InspectionsConducted { get; set; }
    public int InspectionsOverdue { get; set; }

    // KPI 4: Corrective Actions
    public int CorrectiveActionsIssued { get; set; }
    public int CorrectiveActionsCompleted { get; set; }
    public int CorrectiveActionsOverdue { get; set; }
    public decimal? CorrectiveActionClosureRate { get; set; }

    // KPI 5: Training
    public int TrainingProgramsPlanned { get; set; }
    public int TrainingProgramsConducted { get; set; }
    public int TotalTrainingHours { get; set; }

    // KPI 6: Contractor Compliance
    public int ContractorsOnSite { get; set; }
    public int ContractorInspectionsConducted { get; set; }
    public int ContractorNonComplianceNoticesIssued { get; set; }
    public decimal? ContractorComplianceRate { get; set; }

    // KPI 7: Environmental Incidents
    public int EnvironmentalIncidents { get; set; }
    public int EnvironmentalIncidentsReportedToEpa { get; set; }

    // KPI 8: Emergency Drills
    public int EmergencyDrillsPlanned { get; set; }
    public int EmergencyDrillsConducted { get; set; }

    // KPI 9: PPE Compliance
    public decimal? PpeComplianceRate { get; set; }

    // KPI 10: Housekeeping
    public decimal? HousekeepingComplianceRating { get; set; }

    // KPI 11: Regulatory Compliance
    public int RegulatoryObligationsTotal { get; set; }
    public int RegulatoryObligationsCompliant { get; set; }
    public int RegulatoryObligationsNonCompliant { get; set; }
    public int RegulatoryObligationsExpiringSoon { get; set; }

    // Snapshot metadata
    public Guid PreparedById { get; set; }

    [ForeignKey(nameof(PreparedById))]
    public virtual Employee PreparedBy { get; set; } = null!;

    public DateTime PreparedDate { get; set; }

    public Guid? ReviewedById { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }

    public DateTime? ReviewedDate { get; set; }

    [MaxLength(1000)]
    public string? ManagementComments { get; set; }

    [MaxLength(500)]
    public string? ReportDocumentPath { get; set; }

    // ── Computed KPIs (slice 14) ──
    // Written only by SheKpiComputationService — never hand-entered. Null until the
    // first computation runs (or when the inputs make the figure undefined, e.g.
    // zero man-hours for the frequency rates).

    /// <summary>TRIR — recordable incidents (accidents + occupational illness) × 200,000 / man-hours (OSHA base).</summary>
    public decimal? TotalRecordableIncidentRate { get; set; }

    /// <summary>Near misses × 1,000,000 / man-hours (same base as LTIFR).</summary>
    public decimal? NearMissFrequencyRate { get; set; }

    /// <summary>Attended employee sign-ins / registered employee sign-ins on conducted SHE programs, %.</summary>
    public decimal? TrainingCompletionRate { get; set; }

    /// <summary>Drills conducted in the period that met their objectives, %.</summary>
    public decimal? FireDrillObjectivesMetRate { get; set; }

    /// <summary>Waste mass diverted to recycling/composting over total disposed mass (kg/tonne records), %.</summary>
    public decimal? WasteRecyclingRate { get; set; }

    /// <summary>Average compliance score across scored workplace inspections in the period, %.</summary>
    public decimal? AverageInspectionComplianceScore { get; set; }

    /// <summary>When the computed figures were last refreshed from live data. Null = figures are hand-reported only.</summary>
    public DateTime? KpisComputedAt { get; set; }

    public Guid? KpisComputedById { get; set; }

    [ForeignKey(nameof(KpisComputedById))]
    public virtual Employee? KpisComputedBy { get; set; }
}

// ──────────────────────────────────────────────────────────
//  Q. SAFETY COMMITTEE & MEETINGS  (§16.0)
// ──────────────────────────────────────────────────────────

public class SafetyCommittee : TenantEntity
{
    [Required, MaxLength(200)]
    public string CommitteeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime EstablishedDate { get; set; }

    public Guid? ChairPersonId { get; set; }

    [ForeignKey(nameof(ChairPersonId))]
    public virtual Employee? ChairPerson { get; set; }

    public int? MeetingFrequencyDays { get; set; }

    [MaxLength(200)]
    public string? MeetingSchedule { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SafetyCommitteeMember> Members { get; set; } = new List<SafetyCommitteeMember>();
    public virtual ICollection<SafetyMeeting> Meetings { get; set; } = new List<SafetyMeeting>();
}

public class SafetyCommitteeMember : TenantEntity
{
    public Guid CommitteeId { get; set; }

    [ForeignKey(nameof(CommitteeId))]
    public virtual SafetyCommittee Committee { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    public DateTime JoinDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SafetyMeeting : TenantEntity
{
    public Guid? CommitteeId { get; set; }

    [ForeignKey(nameof(CommitteeId))]
    public virtual SafetyCommittee? Committee { get; set; }

    [Required, MaxLength(30)]
    public string MeetingNumber { get; set; } = string.Empty;

    public DateTime MeetingDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    [Required, MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    public SheSafetyMeetingType Type { get; set; }

    [MaxLength(3000)]
    public string? Agenda { get; set; }

    [MaxLength(5000)]
    public string? Minutes { get; set; }

    [MaxLength(2000)]
    public string? TopicsDiscussed { get; set; }

    [MaxLength(1000)]
    public string? DecisionsMade { get; set; }

    public Guid? FacilitatorId { get; set; }

    [ForeignKey(nameof(FacilitatorId))]
    public virtual Employee? Facilitator { get; set; }

    public int? AttendeesCount { get; set; }

    public virtual ICollection<SafetyMeetingAttendee> Attendees { get; set; } = new List<SafetyMeetingAttendee>();
    public virtual ICollection<SafetyMeetingActionItem> ActionItems { get; set; } = new List<SafetyMeetingActionItem>();
    public virtual ICollection<SafetyMeetingDocument> Documents { get; set; } = new List<SafetyMeetingDocument>();
}

public class SafetyMeetingAttendee : TenantEntity
{
    public Guid MeetingId { get; set; }

    [ForeignKey(nameof(MeetingId))]
    public virtual SafetyMeeting Meeting { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public bool Attended { get; set; }
    public DateTime? SignedDate { get; set; }
}

public class SafetyMeetingActionItem : TenantEntity
{
    public Guid MeetingId { get; set; }

    [ForeignKey(nameof(MeetingId))]
    public virtual SafetyMeeting Meeting { get; set; } = null!;

    [Required, MaxLength(1000)]
    public string ActionDescription { get; set; } = string.Empty;

    public SheActionItemPriority Priority { get; set; }
    public SheActionItemStatus Status { get; set; }

    public Guid? AssignedToId { get; set; }

    [ForeignKey(nameof(AssignedToId))]
    public virtual Employee? AssignedTo { get; set; }

    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }
}

public class SafetyMeetingDocument : TenantEntity
{
    public Guid MeetingId { get; set; }

    [ForeignKey(nameof(MeetingId))]
    public virtual SafetyMeeting Meeting { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; }
}

// ──────────────────────────────────────────────────────────
//  R. RETURN-TO-WORK PLANS  (§13.0)
// ──────────────────────────────────────────────────────────

public class SheReturnToWorkPlan : TenantEntity
{
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid? SafetyIncidentId { get; set; }

    [ForeignKey(nameof(SafetyIncidentId))]
    public virtual SafetyIncident? SafetyIncident { get; set; }

    [Required, MaxLength(30)]
    public string PlanNumber { get; set; } = string.Empty;

    public DateTime PlanDate { get; set; }
    public DateTime? PlannedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }

    [MaxLength(1000)]
    public string? MedicalRestrictions { get; set; }

    public DateTime? MedicalClearanceDate { get; set; }

    [MaxLength(500)]
    public string? MedicalClearanceNotes { get; set; }

    public bool RequiresWorkplaceModifications { get; set; }

    [MaxLength(1000)]
    public string? WorkplaceModificationsDescription { get; set; }

    public SheReturnToWorkStatus Status { get; set; }

    public Guid? CoordinatorId { get; set; }

    [ForeignKey(nameof(CoordinatorId))]
    public virtual Employee? Coordinator { get; set; }

    public Guid? SupervisorId { get; set; }

    [ForeignKey(nameof(SupervisorId))]
    public virtual Employee? Supervisor { get; set; }

    public DateTime? CompletionDate { get; set; }
    public bool SuccessfullyCompleted { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }

    public virtual ICollection<SheReturnToWorkPhase> Phases { get; set; } = new List<SheReturnToWorkPhase>();
    public virtual ICollection<SheReturnToWorkReview> Reviews { get; set; } = new List<SheReturnToWorkReview>();
}

public class SheReturnToWorkPhase : TenantEntity
{
    public Guid ReturnToWorkPlanId { get; set; }

    [ForeignKey(nameof(ReturnToWorkPlanId))]
    public virtual SheReturnToWorkPlan ReturnToWorkPlan { get; set; } = null!;

    [Required, MaxLength(100)]
    public string PhaseName { get; set; } = string.Empty;

    public int PhaseNumber { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool RequiresReducedHours { get; set; }
    public int? HoursPerDay { get; set; }
    public int? DaysPerWeek { get; set; }

    [Required, MaxLength(1000)]
    public string Duties { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Restrictions { get; set; } = string.Empty;

    public DateTime AssessmentDate { get; set; }

    public Guid AssessedById { get; set; }

    [ForeignKey(nameof(AssessedById))]
    public virtual Employee AssessedBy { get; set; } = null!;

    [MaxLength(1000)]
    public string? EmployeeProgress { get; set; }

    [MaxLength(500)]
    public string? ChallengesFaced { get; set; }

    [MaxLength(500)]
    public string? AccommodationsEffectiveness { get; set; }

    [MaxLength(500)]
    public string? RecommendedAdjustments { get; set; }

    [MaxLength(500)]
    public string? EmployeeFeedback { get; set; }

    public bool PhaseCompleted { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public bool CanContinuePlan { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }
}

public class SheReturnToWorkReview : TenantEntity
{
    public Guid ReturnToWorkPlanId { get; set; }

    [ForeignKey(nameof(ReturnToWorkPlanId))]
    public virtual SheReturnToWorkPlan ReturnToWorkPlan { get; set; } = null!;

    public DateTime ReviewDate { get; set; }
    public int ReviewNumber { get; set; }

    [MaxLength(500)]
    public string? EmployeeCondition { get; set; }

    [MaxLength(500)]
    public string? WorkProgress { get; set; }

    [MaxLength(500)]
    public string? IssuesIdentified { get; set; }

    [MaxLength(500)]
    public string? RecommendedActions { get; set; }

    public Guid ReviewedById { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee ReviewedBy { get; set; } = null!;

    public DateTime? NextReviewDate { get; set; }
}

// ──────────────────────────────────────────────────────────
//  S. SHE REMINDER ENGINE  (slice 13; FRD §17)
// ──────────────────────────────────────────────────────────

/// <summary>
/// One execution of the SHE reminder sweep for one tenant — scheduled (background
/// service) or manual (the run-now endpoint). Carries the outcome counts the admin
/// screen shows; the per-item detail hangs off <see cref="SheReminderDispatchLog"/>.
/// </summary>
public class SheReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [Required, MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }
    public int PermitsExpired { get; set; }
    public int RiskAssessmentsExpired { get; set; }

    /// <summary>Environmental permits the sweep flipped to Expired (slice 17, FR-ENV-019).</summary>
    public int EnvironmentalPermitsExpired { get; set; }

    public virtual ICollection<SheReminderDispatchLog> DispatchLogs { get; set; } = new List<SheReminderDispatchLog>();
}

/// <summary>
/// One reminder actually dispatched by a sweep. The unique (TenantId, DedupeKey)
/// index is the engine's send-once guarantee: a key encodes the item, the reminder
/// kind, its due date and the ladder threshold (or escalation tier) hit, so each
/// rung fires exactly once — and a rescheduled due date re-arms the ladder because
/// it produces new keys.
/// </summary>
public class SheReminderDispatchLog : TenantEntity
{
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual SheReminderRun Run { get; set; } = null!;

    /// <summary>Machine kind, e.g. "PermitExpiringSoon", "RegulatoryReviewDue".</summary>
    [Required, MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item type, e.g. "Permit to work".</summary>
    [Required, MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept SHE record (no FK — the target table varies by kind).</summary>
    public Guid EntityId { get; set; }

    /// <summary>What the notification shows: number/code plus a short name.</summary>
    [Required, MaxLength(250)]
    public string Reference { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    /// <summary>Days remaining at dispatch time (negative when overdue).</summary>
    public int DaysRemaining { get; set; }

    /// <summary>0 = due-soon ladder rung; 1–3 = overdue escalation tier.</summary>
    public int EscalationTier { get; set; }

    [Required, MaxLength(300)]
    public string DedupeKey { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────
//  T. SHE AUDIT MANAGEMENT  (slice 15, FRD §12 / FR-SHE-229)
// ──────────────────────────────────────────────────────────

/// <summary>
/// A management-system SHE audit: planning → execution → findings → CAPA →
/// verification → closure. Distinct from workplace inspections (section D) and
/// from Maintenance's unrelated SafetyAudit scaffolding.
/// </summary>
public class SheAudit : TenantEntity
{
    [Required, MaxLength(30)]
    public string AuditNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public SheAuditType Type { get; set; }

    /// <summary>The standard or criteria audited against, e.g. "ISO 45001:2018", "Factories, Offices and Shops Act".</summary>
    [MaxLength(200)]
    public string? Standard { get; set; }

    [MaxLength(1000)]
    public string? Scope { get; set; }

    [MaxLength(1000)]
    public string? Objectives { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    /// <summary>Internal coordinator even for external audits — the accountable employee.</summary>
    public Guid LeadAuditorId { get; set; }

    [ForeignKey(nameof(LeadAuditorId))]
    public virtual Employee LeadAuditor { get; set; } = null!;

    [MaxLength(200)]
    public string? ExternalAuditorName { get; set; }

    [MaxLength(200)]
    public string? ExternalAuditorOrganization { get; set; }

    public DateTime PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public SheAuditStatus Status { get; set; }

    // ── Report ──
    [MaxLength(4000)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? ReportDocumentPath { get; set; }

    public DateTime? ReportIssuedDate { get; set; }

    // ── Closure ──
    public DateTime? ClosedDate { get; set; }

    public Guid? ClosedById { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }

    public virtual ICollection<SheAuditTeamMember> TeamMembers { get; set; } = new List<SheAuditTeamMember>();
    public virtual ICollection<SheAuditFinding> Findings { get; set; } = new List<SheAuditFinding>();
}

public class SheAuditTeamMember : TenantEntity
{
    public Guid AuditId { get; set; }

    [ForeignKey(nameof(AuditId))]
    public virtual SheAudit Audit { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// One audit finding. Corrective actions hang off the finding as template-linked
/// rows (the slice-14 unified tracker's fifth source); the finding itself closes
/// only after its actions complete and the resolution is verified.
/// </summary>
public class SheAuditFinding : TenantEntity
{
    public Guid AuditId { get; set; }

    [ForeignKey(nameof(AuditId))]
    public virtual SheAudit Audit { get; set; } = null!;

    /// <summary>Server-assigned sequence within the audit; soft-deleted findings keep their number.</summary>
    public int FindingNumber { get; set; }

    public SheAuditFindingClassification Classification { get; set; }

    /// <summary>Clause / section of the audited standard, e.g. "45001 §8.1.2".</summary>
    [MaxLength(100)]
    public string? ClauseReference { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Evidence { get; set; }

    public SheAuditFindingStatus Status { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    [ForeignKey(nameof(ResponsiblePersonId))]
    public virtual Employee? ResponsiblePerson { get; set; }

    public DateTime? DueDate { get; set; }

    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }

    public DateTime? ResolvedDate { get; set; }

    // ── Effectiveness verification ──
    public Guid? VerifiedById { get; set; }

    [ForeignKey(nameof(VerifiedById))]
    public virtual Employee? VerifiedBy { get; set; }

    public DateTime? VerifiedDate { get; set; }

    [MaxLength(1000)]
    public string? VerificationNotes { get; set; }

    public virtual ICollection<SheAuditFindingAction> Actions { get; set; } = new List<SheAuditFindingAction>();
}

public class SheAuditFindingAction : TenantEntity
{
    public Guid FindingId { get; set; }

    [ForeignKey(nameof(FindingId))]
    public virtual SheAuditFinding Finding { get; set; } = null!;

    public Guid CorrectiveActionTemplateId { get; set; }

    [ForeignKey(nameof(CorrectiveActionTemplateId))]
    public virtual SheCorrectiveActionTemplate CorrectiveActionTemplate { get; set; } = null!;

    public SheCorrectiveActionStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }

    public Guid? AssignedToId { get; set; }

    [ForeignKey(nameof(AssignedToId))]
    public virtual Employee? AssignedTo { get; set; }
}

// ──────────────────────────────────────────────────────────
//  U. STOP-WORK AUTHORITY  (slice 15, FR-SHE-200)
// ──────────────────────────────────────────────────────────

/// <summary>
/// A stop-work order: any employee may halt work they believe is imminently
/// dangerous (raise stays open like incident/hazard reporting); HR/SHE routes,
/// resolves and finally clears the resumption. Raised → UnderReview → Resolved
/// → Cleared, with Cancelled for false alarms.
/// </summary>
public class SheStopWorkOrder : TenantEntity
{
    [Required, MaxLength(30)]
    public string OrderNumber { get; set; } = string.Empty;

    public Guid RaisedById { get; set; }

    [ForeignKey(nameof(RaisedById))]
    public virtual Employee RaisedBy { get; set; } = null!;

    public DateTime RaisedDate { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    /// <summary>What work was stopped.</summary>
    [Required, MaxLength(1000)]
    public string WorkDescription { get; set; } = string.Empty;

    /// <summary>Why — the imminent danger observed.</summary>
    [Required, MaxLength(2000)]
    public string ReasonDescription { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ImmediateActionsTaken { get; set; }

    // Optional bridges to the records the stop-work concerns.
    public Guid? PermitToWorkId { get; set; }

    [ForeignKey(nameof(PermitToWorkId))]
    public virtual ShePermitToWork? PermitToWork { get; set; }

    public Guid? HazardId { get; set; }

    [ForeignKey(nameof(HazardId))]
    public virtual SheHazard? Hazard { get; set; }

    public Guid? IncidentId { get; set; }

    [ForeignKey(nameof(IncidentId))]
    public virtual SafetyIncident? Incident { get; set; }

    public SheStopWorkStatus Status { get; set; }

    /// <summary>The manager the order is routed to for resolution.</summary>
    public Guid? RoutedToId { get; set; }

    [ForeignKey(nameof(RoutedToId))]
    public virtual Employee? RoutedTo { get; set; }

    public DateTime? RoutedDate { get; set; }

    // ── Resolution ──
    [MaxLength(2000)]
    public string? ResolutionDescription { get; set; }

    public Guid? ResolvedById { get; set; }

    [ForeignKey(nameof(ResolvedById))]
    public virtual Employee? ResolvedBy { get; set; }

    public DateTime? ResolvedDate { get; set; }

    // ── Clearance (work resumes) ──
    public Guid? ClearedById { get; set; }

    [ForeignKey(nameof(ClearedById))]
    public virtual Employee? ClearedBy { get; set; }

    public DateTime? ClearedDate { get; set; }

    [MaxLength(1000)]
    public string? ClearanceNotes { get; set; }

    // ── Cancellation (false alarm) ──
    public Guid? CancelledById { get; set; }

    [ForeignKey(nameof(CancelledById))]
    public virtual Employee? CancelledBy { get; set; }

    public DateTime? CancelledDate { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }
}

// ──────────────────────────────────────────────────────────
//  V. STATUTORY INCIDENT SUBMISSIONS  (slice 15, FR-SHE-103)
// ──────────────────────────────────────────────────────────

/// <summary>
/// One submission of a reportable incident to a regulatory body (Labour
/// Department, EPA, GNFS, …) — initial notification through final report. The
/// first submission stamps the incident's denormalised authority-notification
/// fields when they are still blank.
/// </summary>
public class SheStatutoryIncidentSubmission : TenantEntity
{
    public Guid IncidentId { get; set; }

    [ForeignKey(nameof(IncidentId))]
    public virtual SafetyIncident Incident { get; set; } = null!;

    public Guid RegulatoryBodyId { get; set; }

    [ForeignKey(nameof(RegulatoryBodyId))]
    public virtual SheRegulatoryBody RegulatoryBody { get; set; } = null!;

    public SheStatutorySubmissionType Type { get; set; }
    public SheStatutorySubmissionMethod Method { get; set; }

    public DateTime SubmissionDate { get; set; }

    /// <summary>The authority's reference for this submission, once assigned.</summary>
    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public Guid SubmittedById { get; set; }

    [ForeignKey(nameof(SubmittedById))]
    public virtual Employee SubmittedBy { get; set; } = null!;

    /// <summary>The submitted artefact (report / form) on the document store.</summary>
    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public bool AcknowledgementReceived { get; set; }
    public DateTime? AcknowledgementDate { get; set; }

    [MaxLength(100)]
    public string? AcknowledgementReference { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

// ──────────────────────────────────────────────────────────
//  W. SHE CONTROLLED DOCUMENT REGISTER  (slice 16, FR-SHE-246/170)
// ──────────────────────────────────────────────────────────

/// <summary>
/// One controlled SHE document (policy, procedure, emergency plan, …) in the
/// area's register — FR-SHE-170 filing/retrieval and FR-SHE-246 version
/// control. The register row carries the SHE classification and lifecycle;
/// the file versions themselves live in the central DMS
/// (<see cref="CentralDocumentRecord"/> / CentralDocumentVersion), each bound
/// to a scanned controlled upload — SHE deliberately mints no parallel
/// version store.
/// </summary>
public class SheControlledDocument : TenantEntity
{
    [Required, MaxLength(30)]
    public string DocumentNumber { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public SheControlledDocumentCategory Category { get; set; }

    public SheControlledDocumentStatus Status { get; set; } = SheControlledDocumentStatus.Draft;

    /// <summary>Free-text search terms for FR-SHE-170 retrieval.</summary>
    [MaxLength(500)]
    public string? Keywords { get; set; }

    /// <summary>The document's custodian — owns the content and its review cycle.</summary>
    public Guid OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public virtual Employee Owner { get; set; } = null!;

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    // ── Version control (central DMS binding) ──
    /// <summary>Null until the first version is uploaded and DMS-registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    [ForeignKey(nameof(DocumentRecordId))]
    public virtual CentralDocumentRecord? DocumentRecord { get; set; }

    /// <summary>Mirror of the DMS record's CurrentVersion for list reads.</summary>
    [MaxLength(20)]
    public string? CurrentVersionLabel { get; set; }

    // ── Approval / effectivity (FR-SHE-246) ──
    public DateTime? EffectiveDate { get; set; }

    /// <summary>Drives NextReviewDate on activation when no explicit date is given.</summary>
    public int? ReviewFrequencyMonths { get; set; }

    public DateTime? NextReviewDate { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    // ── Archival (obsolete documents stay on the register) ──
    public Guid? ArchivedById { get; set; }

    [ForeignKey(nameof(ArchivedById))]
    public virtual Employee? ArchivedBy { get; set; }

    public DateTime? ArchivedDate { get; set; }

    [MaxLength(500)]
    public string? ArchiveReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

// ──────────────────────────────────────────────────────────
//  X. ENVIRONMENTAL PERMIT & LICENCE REGISTER  (slice 17, FR-ENV-017–019)
// ──────────────────────────────────────────────────────────

/// <summary>
/// One environmental permit, licence, EPA registration or certificate on the
/// Part D Permit Register (FR-ENV-017). The reminder engine rides the statutory
/// 180/90/60/30/14/7 ladder toward ExpiryDate (FR-ENV-018) and flips a live
/// permit to Expired past it — expired rows escalate to the admin audience and
/// show red on the dashboard (FR-ENV-019). The permit document itself rides
/// the controlled-upload gate onto the central DMS, exactly like the slice-16
/// register — no bare string paths.
/// </summary>
public class SheEnvironmentalPermit : TenantEntity
{
    /// <summary>The register's own sequence (EPR-YYYY-NNNN), distinct from the authority's number.</summary>
    [Required, MaxLength(30)]
    public string RegisterNumber { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string PermitName { get; set; } = string.Empty;

    public SheEnvironmentalPermitType PermitType { get; set; }

    /// <summary>The issuing authority's own permit/licence number.</summary>
    [MaxLength(100)]
    public string? AuthorityReferenceNumber { get; set; }

    public Guid? IssuingBodyId { get; set; }

    [ForeignKey(nameof(IssuingBodyId))]
    public virtual SheRegulatoryBody? IssuingBody { get; set; }

    /// <summary>FR-ENV-017's responsible officer — owns the renewal.</summary>
    public Guid ResponsibleOfficerId { get; set; }

    [ForeignKey(nameof(ResponsibleOfficerId))]
    public virtual Employee ResponsibleOfficer { get; set; } = null!;

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Conditions attached to the permit that operations must honour.</summary>
    [MaxLength(2000)]
    public string? Conditions { get; set; }

    public DateTime IssueDate { get; set; }

    /// <summary>The renewal ladder's hook (FR-ENV-018).</summary>
    public DateTime ExpiryDate { get; set; }

    public int? RenewalPeriodMonths { get; set; }

    public SheEnvironmentalPermitStatus Status { get; set; } = SheEnvironmentalPermitStatus.Active;

    // ── Permit document (central DMS binding, slice-16 stance) ──
    /// <summary>Null until the permit document is uploaded and DMS-registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    [ForeignKey(nameof(DocumentRecordId))]
    public virtual CentralDocumentRecord? DocumentRecord { get; set; }

    [MaxLength(20)]
    public string? CurrentVersionLabel { get; set; }

    // ── Renewal trail ──
    public DateTime? LastRenewedDate { get; set; }

    public Guid? LastRenewedById { get; set; }

    [ForeignKey(nameof(LastRenewedById))]
    public virtual Employee? LastRenewedBy { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

// ──────────────────────────────────────────────────────────
//  Y. ENVIRONMENTAL MONITORING SCHEDULES  (slice 17, FR-ENV-023–024)
// ──────────────────────────────────────────────────────────

/// <summary>
/// A recurring monitoring obligation (dust, waste storage, noise, air quality,
/// annual performance review — FR-ENV-023). The reminder engine ladders toward
/// NextDueDate (FR-ENV-024); recording completion links the monitoring record
/// that satisfied the cycle and advances the schedule by its interval.
/// </summary>
public class SheEnvironmentalMonitoringSchedule : TenantEntity
{
    [Required, MaxLength(30)]
    public string ScheduleNumber { get; set; } = string.Empty;

    public SheEnvironmentalMonitoringType MonitoringType { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [MaxLength(200)]
    public string? MonitoringPoint { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Days between cycles — completion advances NextDueDate by this much.</summary>
    public int FrequencyDays { get; set; }

    /// <summary>The reminder sweep's hook.</summary>
    public DateTime NextDueDate { get; set; }

    public DateTime? LastPerformedDate { get; set; }

    public Guid? ResponsibleOfficerId { get; set; }

    [ForeignKey(nameof(ResponsibleOfficerId))]
    public virtual Employee? ResponsibleOfficer { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SheEnvironmentalMonitoringRecord> Records { get; set; } = new List<SheEnvironmentalMonitoringRecord>();
}

// ──────────────────────────────────────────────────────────
//  Z. REGULATORY UPDATES REGISTER  (slice 17, FR-ENV-030–032 / FR-SHE-182)
// ──────────────────────────────────────────────────────────

/// <summary>
/// One recorded change in environmental/SHE legislation or standards — EPA,
/// Ghana Standards Authority, ministries, international standards, new LIs
/// (FR-ENV-030, FR-SHE-182). Carries the FR-ENV-031 assessment (summary,
/// affected departments, deadline, risk, required actions, management
/// notification) and the FR-ENV-032 register columns, and is tracked to
/// compliance closure. May link to the obligations register when the update
/// amends an obligation already under management.
/// </summary>
public class SheRegulatoryUpdate : TenantEntity
{
    /// <summary>The register's own sequence (REG-YYYY-NNNN).</summary>
    [Required, MaxLength(30)]
    public string UpdateNumber { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>The authority's own regulation/LI number (FR-ENV-032's "regulation number").</summary>
    [MaxLength(100)]
    public string? RegulationReference { get; set; }

    public Guid? RegulatoryBodyId { get; set; }

    [ForeignKey(nameof(RegulatoryBodyId))]
    public virtual SheRegulatoryBody? RegulatoryBody { get; set; }

    /// <summary>Free-text authority when the body is not on the reference register.</summary>
    [MaxLength(200)]
    public string? AuthorityName { get; set; }

    public SheRegulatoryDomain Domain { get; set; }

    [Required, MaxLength(2000)]
    public string Summary { get; set; } = string.Empty;

    public DateTime IssueDate { get; set; }
    public DateTime? EffectiveDate { get; set; }

    [MaxLength(500)]
    public string? AffectedDepartments { get; set; }

    /// <summary>The reminder sweep ladders toward this while the update is not closed.</summary>
    public DateTime? ComplianceDeadline { get; set; }

    public SheRegulatoryUpdateRiskLevel RiskLevel { get; set; }

    [MaxLength(2000)]
    public string? RequiredActions { get; set; }

    public SheRegulatoryUpdateStatus Status { get; set; } = SheRegulatoryUpdateStatus.Recorded;

    public SheComplianceStatus ComplianceStatus { get; set; } = SheComplianceStatus.NotAssessed;

    public DateTime? ReviewDate { get; set; }

    [MaxLength(1000)]
    public string? OfficerComments { get; set; }

    public Guid? LinkedObligationId { get; set; }

    [ForeignKey(nameof(LinkedObligationId))]
    public virtual SheRegulatoryObligation? LinkedObligation { get; set; }

    // ── FR-ENV-031 management notification ──
    public DateTime? ManagementNotifiedAt { get; set; }

    public Guid? ManagementNotifiedById { get; set; }

    [ForeignKey(nameof(ManagementNotifiedById))]
    public virtual Employee? ManagementNotifiedBy { get; set; }

    public Guid RecordedById { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee RecordedBy { get; set; } = null!;

    // ── Compliance closure ──
    public DateTime? ClosedAt { get; set; }

    public Guid? ClosedById { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }
}

// ──────────────────────────────────────────────────────────
//  AA. SUSTAINABILITY INITIATIVES  (slice 17, FR-ENV-028–029)
// ──────────────────────────────────────────────────────────

/// <summary>
/// One tracked sustainability initiative (energy/water/paper savings, tree
/// planting, recycling, carbon reduction, cost savings — FR-ENV-028). The
/// aggregates feed the dashboard KPI strip and the monthly environmental
/// report (FR-ENV-029). Spec priority D.
/// </summary>
public class SheSustainabilityInitiative : TenantEntity
{
    [Required, MaxLength(30)]
    public string InitiativeNumber { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public SheSustainabilityCategory Category { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    public Guid? OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public virtual Employee? Owner { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public SheSustainabilityStatus Status { get; set; } = SheSustainabilityStatus.Planned;

    /// <summary>Target and achieved values in MeasurementUnit (kWh, m³, kg, trees, …).</summary>
    public decimal? TargetValue { get; set; }
    public decimal? ActualValue { get; set; }

    [MaxLength(50)]
    public string? MeasurementUnit { get; set; }

    /// <summary>Estimated cost saving in GHS — summed for the monthly report.</summary>
    public decimal? EstimatedCostSavings { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

// ──────────────────────────────────────────────────────────
//  AB. ENVIRONMENTAL COMPLIANCE REVIEWS & CLEARANCE  (slice 17, FR-ENV-001–016)
// ──────────────────────────────────────────────────────────

/// <summary>
/// One activity/project submitted for environmental compliance review
/// (FR-ENV-001): screening determination (FR-ENV-009), officer decision
/// (FR-ENV-004), management approval routing (FR-ENV-005), EPA submission
/// trail (FR-ENV-011) and clearance issue (FR-ENV-016). ProjectReference is
/// the deliberate seam for the Project module: automatic triggering at
/// project creation (FR-ENV-008/012/013) and the procurement/construction
/// hard block (FR-ENV-010, decision DR-09 pending) wire in when a Project
/// trigger source exists — the register and its gates stand alone until then.
/// </summary>
public class SheEnvironmentalReview : TenantEntity
{
    [Required, MaxLength(30)]
    public string ReviewNumber { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string ProjectName { get; set; } = string.Empty;

    public SheEnvironmentalWorkClassification WorkClassification { get; set; }

    /// <summary>Free-text pointer to the initiating record — the stubbed Project-module seam.</summary>
    [MaxLength(200)]
    public string? ProjectReference { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public Guid? ResponsibleManagerId { get; set; }

    [ForeignKey(nameof(ResponsibleManagerId))]
    public virtual Employee? ResponsibleManager { get; set; }

    public Guid SubmittedById { get; set; }

    [ForeignKey(nameof(SubmittedById))]
    public virtual Employee SubmittedBy { get; set; } = null!;

    public DateTime SubmittedDate { get; set; }

    /// <summary>FR-ENV-014's 90/60/30-day project-notification reminders ladder toward this.</summary>
    public DateTime? PlannedStartDate { get; set; }

    [Required, MaxLength(3000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ApplicableLaws { get; set; }

    public bool PermitRequired { get; set; }

    [MaxLength(2000)]
    public string? ComplianceChecklist { get; set; }

    // ── Screening determination (FR-ENV-009) ──
    public bool RequiresRegistration { get; set; }
    public bool RequiresEnvironmentalPermit { get; set; }
    public bool RequiresFullEia { get; set; }
    public bool RequiresRiskAssessment { get; set; }
    public bool RequiresEpaSubmission { get; set; }
    public bool RequiresManagementApproval { get; set; }

    [MaxLength(2000)]
    public string? ScreeningNotes { get; set; }

    public DateTime? ScreeningCompletedDate { get; set; }

    public Guid? ScreenedById { get; set; }

    [ForeignKey(nameof(ScreenedById))]
    public virtual Employee? ScreenedBy { get; set; }

    // ── Officer decision (FR-ENV-004) ──
    public SheEnvironmentalReviewStatus Status { get; set; } = SheEnvironmentalReviewStatus.Submitted;

    [MaxLength(2000)]
    public string? OfficerComments { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    // ── Management approval (FR-ENV-005) ──
    public DateTime? ManagementApprovedDate { get; set; }

    public Guid? ManagementApprovedById { get; set; }

    [ForeignKey(nameof(ManagementApprovedById))]
    public virtual Employee? ManagementApprovedBy { get; set; }

    // ── EPA submission (FR-ENV-011) ──
    public DateTime? EpaSubmissionDate { get; set; }

    [MaxLength(100)]
    public string? EpaSubmissionReference { get; set; }

    // ── Clearance (FR-ENV-016) ──
    public DateTime? ClearanceIssuedDate { get; set; }

    public Guid? ClearanceIssuedById { get; set; }

    [ForeignKey(nameof(ClearanceIssuedById))]
    public virtual Employee? ClearanceIssuedBy { get; set; }

    // ── Project commencement approval (FR-ENV-011's trail end) ──
    public DateTime? CommencementApprovedDate { get; set; }

    public Guid? CommencementApprovedById { get; set; }

    [ForeignKey(nameof(CommencementApprovedById))]
    public virtual Employee? CommencementApprovedBy { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public virtual ICollection<SheEnvironmentalReviewAction> Actions { get; set; } = new List<SheEnvironmentalReviewAction>();
}

/// <summary>
/// The review's append-only audit trail (FR-ENV-006/011): every lifecycle
/// action — submission, screening, corrections, approval, rejection,
/// management approval, EPA submission, clearance, commencement — lands one
/// immutable row.
/// </summary>
public class SheEnvironmentalReviewAction : TenantEntity
{
    public Guid ReviewId { get; set; }

    [ForeignKey(nameof(ReviewId))]
    public virtual SheEnvironmentalReview Review { get; set; } = null!;

    [Required, MaxLength(60)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public Guid ActorId { get; set; }

    [ForeignKey(nameof(ActorId))]
    public virtual Employee Actor { get; set; } = null!;

    public DateTime ActionDate { get; set; }
}

// ──────────────────────────────────────────────────────────
//  AC. MONTHLY ENVIRONMENTAL REPORTS  (slice 17, FR-ENV-033–034)
// ──────────────────────────────────────────────────────────

/// <summary>
/// One generated monthly environmental report (FR-ENV-033). The sweep
/// auto-generates the prior month's report when missing; an unsubmitted
/// report can be regenerated (figures recomputed), a submitted one is
/// permanent history (FR-ENV-034). All figures are computed at generation
/// time from the live registers — never hand-entered.
/// </summary>
public class SheMonthlyEnvironmentalReport : TenantEntity
{
    /// <summary>Natural key: ENV-RPT-YYYY-MM.</summary>
    [Required, MaxLength(30)]
    public string ReportNumber { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Month { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public DateTime GeneratedAt { get; set; }

    /// <summary>Null when the reminder engine generated the report.</summary>
    public Guid? GeneratedById { get; set; }

    [ForeignKey(nameof(GeneratedById))]
    public virtual Employee? GeneratedBy { get; set; }

    // ── Compliance (obligations register) ──
    public int ObligationsTotal { get; set; }
    public int ObligationsCompliant { get; set; }
    public decimal? CompliancePercentage { get; set; }

    // ── Permit status + upcoming renewals ──
    public int PermitsActive { get; set; }
    public int PermitsExpiringIn90Days { get; set; }
    public int PermitsExpired { get; set; }

    // ── Reviews ──
    public int ProjectsReviewed { get; set; }
    public int ClearancesIssued { get; set; }

    // ── Waste (mass-based, kilogram/tonne records only — mixed units are incommensurable) ──
    public decimal WasteGeneratedKg { get; set; }
    public decimal WasteRecycledKg { get; set; }
    public decimal? WasteRecyclingRate { get; set; }

    // ── Incidents & monitoring ──
    public int EnvironmentalIncidents { get; set; }
    public int EnvironmentalIncidentsClosed { get; set; }
    public int MonitoringExceedances { get; set; }

    // ── Audits & corrective actions ──
    public int AuditFindingsRaised { get; set; }
    public int CorrectiveActionsOpen { get; set; }

    // ── Regulations & sustainability ──
    public int NewRegulatoryUpdates { get; set; }
    public int SustainabilityInitiativesActive { get; set; }
    public int SustainabilityInitiativesCompleted { get; set; }
    public decimal SustainabilityCostSavings { get; set; }

    /// <summary>The officer's narrative — editable until submission.</summary>
    [MaxLength(3000)]
    public string? OfficerSummary { get; set; }

    // ── Electronic submission to management (FR-ENV-034) ──
    public DateTime? SubmittedToManagementAt { get; set; }

    public Guid? SubmittedById { get; set; }

    [ForeignKey(nameof(SubmittedById))]
    public virtual Employee? SubmittedBy { get; set; }
}
