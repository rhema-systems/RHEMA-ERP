using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// Round 4, lane I — orientation triggers that fire, and the answer to "why"
// ============================================================================

/// <summary>An audience rule's target, asked "how many people is that?" before it is saved.</summary>
public class OrientationAudienceReachRequestDto
{
    [Required]
    public HrAudienceTargetType TargetType { get; set; }

    public Guid? TargetEntityId { get; set; }

    public OrientationAudiencePopulation Population { get; set; } = OrientationAudiencePopulation.Anyone;
}

public class OrientationAudienceReachDto
{
    /// <summary>Active employees the rule reaches today.</summary>
    public int Count { get; set; }

    /// <summary>The rule in words: "New hires in Operations (and the units beneath it)".</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>What one evaluation of the rules did — or, on a preview, would do.</summary>
public class OrientationTriggerRunResultDto
{
    /// <summary>What fired: Hire, Transfer, Promotion, Publish, Scheduled, Manual, or Sweep.</summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>True when nothing was written — the "this will enrol 12 people" read.</summary>
    public bool IsPreview { get; set; }

    public DateOnly AsOf { get; set; }

    public int ProgramsEvaluated { get; set; }
    public int RulesEvaluated { get; set; }

    /// <summary>Enrollments created (or, on a preview, that would be).</summary>
    public int Enrolled { get; set; }

    /// <summary>Reached, and already on the programme — every earlier enrollment counts, including
    /// one HR withdrew, so a rule can never re-enrol somebody a person took off.</summary>
    public int AlreadyEnrolled { get; set; }

    /// <summary>Reached, but an exclusion rule of the same programme keeps them out.</summary>
    public int Excluded { get; set; }

    /// <summary>Reached, but a mandatory prerequisite programme is not yet completed. The sweep
    /// picks them up on the first night after they complete it.</summary>
    public int WaitingOnPrerequisite { get; set; }

    /// <summary>Set when an event hook failed. The host's own work stood; the nightly sweep retries.</summary>
    public string? Error { get; set; }

    /// <summary>At most 500 rows; the counts above are always complete.</summary>
    public List<OrientationTriggerEnrolmentDto> Enrolments { get; set; } = new();
}

public class OrientationTriggerEnrolmentDto
{
    public Guid? EnrollmentId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }
    public Guid ProgramId { get; set; }
    public string ProgramTitle { get; set; } = string.Empty;
    public Guid RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public OrientationEnrollmentTrigger TriggerEvent { get; set; }
    public DateOnly? TriggerDate { get; set; }
}

/// <summary>
/// "Which rules would fire for this employee, and why" (lane I5) — the PDF's question answered
/// inside the product instead of by reading code.
/// </summary>
public class OrientationTriggerDiagnosisDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public bool IsActive { get; set; }
    public DateOnly AsOf { get; set; }

    /// <summary>
    /// The date the hire trigger counts from: the employment date. Null when none was entered —
    /// and then no hire rule fires for this person. The record's creation date is deliberately NOT
    /// used instead: a seeded or imported record is created long after (or before) anyone joins.
    /// </summary>
    public DateOnly? HireDate { get; set; }

    public string? OrganizationUnitName { get; set; }
    public string? OrganizationLevelName { get; set; }
    public string? PositionTitle { get; set; }
    public string? LocationName { get; set; }
    public string EmploymentType { get; set; } = string.Empty;

    /// <summary>The derived populations this person is in today: NewHires, Management, Contractors.</summary>
    public List<string> Populations { get; set; } = new();

    /// <summary>Implemented transfers and promotions the movement triggers could count from.</summary>
    public List<OrientationTriggerMovementDto> RecentMovements { get; set; } = new();

    public List<OrientationProgramDiagnosisDto> Programs { get; set; } = new();

    /// <summary>The onboarding side: which plan template would be chosen for this person.</summary>
    public OnboardingTemplateApplicabilityDto? Onboarding { get; set; }
    public Guid? OnboardingPlanId { get; set; }
    public string? OnboardingPlanTemplateName { get; set; }
    public string? OnboardingPlanSelectionReason { get; set; }
}

public class OrientationTriggerMovementDto
{
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public OrientationEnrollmentTrigger Trigger { get; set; }
    public DateOnly EffectiveDate { get; set; }
}

public class OrientationProgramDiagnosisDto
{
    public Guid ProgramId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string ProgramTitle { get; set; } = string.Empty;
    public OrientationProgramStatus ProgramStatus { get; set; }

    /// <summary>
    /// One word for the outcome: WouldEnrolNow, Enrolled, Excluded, WaitingForDate, WindowLapsed,
    /// WaitingOnPrerequisite, OnlyWhenHrEnrols, NoTriggeringEvent, NotInAudience, ProgramNotActive.
    /// </summary>
    public string Verdict { get; set; } = string.Empty;

    /// <summary>The verdict as a sentence a person can act on.</summary>
    public string Explanation { get; set; } = string.Empty;

    public Guid? EnrollmentId { get; set; }
    public string? EnrollmentStatus { get; set; }
    public string? EnrollmentSource { get; set; }
    public string? EnrolledByRuleName { get; set; }

    public List<string> MissingPrerequisites { get; set; } = new();
    public List<OrientationRuleDiagnosisDto> Rules { get; set; } = new();
}

public class OrientationRuleDiagnosisDto
{
    public Guid RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public bool IsInclusive { get; set; }
    public bool IsActive { get; set; }
    public OrientationEnrollmentTrigger Trigger { get; set; }
    public HrAudienceTargetType TargetType { get; set; }
    public string? TargetName { get; set; }
    public OrientationAudiencePopulation Population { get; set; }
    public int EnrollmentDelayDays { get; set; }

    public bool MatchesTarget { get; set; }
    public bool MatchesPopulation { get; set; }

    /// <summary>The date a dated trigger counts from, when this person has one.</summary>
    public DateOnly? TriggerDate { get; set; }
    public DateOnly? FiresFrom { get; set; }
    public DateOnly? FiresUntil { get; set; }

    /// <summary>Due, NotYet, Lapsed, NoEvent, Undated or OnlyByHr.</summary>
    public string Timing { get; set; } = string.Empty;

    public string Explanation { get; set; } = string.Empty;
}

// ============================================================================
// Round 4, lane I4 — onboarding template applicability
// ============================================================================

public class OnboardingPlanTemplateAudienceDto
{
    public Guid Id { get; set; }
    public Guid PlanTemplateId { get; set; }
    public HrAudienceTargetType TargetType { get; set; }
    public Guid? TargetEntityId { get; set; }
    public string? TargetEntityName { get; set; }
    public bool IsInclusive { get; set; }
}

public class CreateOnboardingPlanTemplateAudienceDto
{
    [Required]
    public HrAudienceTargetType TargetType { get; set; }

    public Guid? TargetEntityId { get; set; }

    public bool IsInclusive { get; set; } = true;
}

/// <summary>Which onboarding plan template a hire would get, and why that one.</summary>
public class OnboardingTemplateApplicabilityDto
{
    public Guid? TemplateId { get; set; }
    public string? TemplateName { get; set; }

    /// <summary>The grounds, in words: "Position: Estates Officer".</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Position, OrganizationUnit, OrganizationLevel, Location, AllEmployees, Default or None.</summary>
    public string MatchedOn { get; set; } = "None";

    /// <summary>
    /// True when two templates matched equally specifically. The one chosen is the first by name —
    /// deterministic, but a choice HR should make on purpose, so the screens say so.
    /// </summary>
    public bool IsAmbiguous { get; set; }

    public List<OnboardingTemplateCandidateDto> Candidates { get; set; } = new();
}

public class OnboardingTemplateCandidateDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string MatchedOn { get; set; } = string.Empty;
    public string? MatchedTargetName { get; set; }

    /// <summary>Higher wins: position 100, own unit 90 falling by one per level up (floor 51),
    /// level 40, location 30, everyone 10, the default template 0.</summary>
    public int Specificity { get; set; }

    public bool IsExcluded { get; set; }
    public string? ExcludedBy { get; set; }
}
