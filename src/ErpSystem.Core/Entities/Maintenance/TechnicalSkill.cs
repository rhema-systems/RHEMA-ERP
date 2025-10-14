using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Technical skills required for maintenance work
/// </summary>
public class TechnicalSkill : TenantEntity
{
    /// <summary>
    /// Skill name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique skill code
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the skill
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Skill category (Electrical, HVAC, Plumbing, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Required skill level (Basic, Intermediate, Advanced, Expert)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string SkillLevel { get; set; } = string.Empty;

    /// <summary>
    /// Complexity rating (Low, Medium, High, Very High)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Complexity { get; set; } = string.Empty;

    /// <summary>
    /// Risk level associated with this skill (Low, Medium, High)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Prerequisites for this skill (JSON array)
    /// </summary>
    public string Prerequisites { get; set; } = "[]";

    /// <summary>
    /// Required certifications (JSON array)
    /// </summary>
    public string Certifications { get; set; } = "[]";

    /// <summary>
    /// Estimated learning hours
    /// </summary>
    public int EstimatedLearningHours { get; set; } = 40;

    /// <summary>
    /// Required tools and equipment (JSON array)
    /// </summary>
    public string ToolsRequired { get; set; } = "[]";

    /// <summary>
    /// Safety requirements and protocols
    /// </summary>
    [MaxLength(1000)]
    public string SafetyRequirements { get; set; } = string.Empty;

    /// <summary>
    /// Competency areas covered by this skill (JSON array)
    /// </summary>
    public string CompetencyAreas { get; set; } = "[]";

    /// <summary>
    /// Related maintenance types (JSON array)
    /// </summary>
    public string RelatedMaintenanceTypes { get; set; } = "[]";

    /// <summary>
    /// Whether the skill is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this skill data comes from HR module
    /// </summary>
    public bool IsFromHRModule { get; set; } = false;

    /// <summary>
    /// Last synchronization date with HR system
    /// </summary>
    public DateTime? LastSyncDate { get; set; }

    // Aliases for service compatibility
    [NotMapped]
    public DateTime CreatedDate => CreatedAt;
    
    [NotMapped]
    public DateTime? LastModifiedDate => UpdatedAt;

    // Computed properties for reporting
    /// <summary>
    /// Number of technicians with this skill
    /// </summary>
    [NotMapped]
    public int TechniciansCount { get; set; }

    /// <summary>
    /// Average proficiency rating for this skill
    /// </summary>
    [NotMapped]
    public decimal AverageRating { get; set; }

    // Navigation properties
    /// <summary>
    /// Technician skill assignments
    /// </summary>
    public virtual ICollection<TechnicianSkillAssignment> TechnicianAssignments { get; set; } = new List<TechnicianSkillAssignment>();
}

/// <summary>
/// Assignment of skills to technicians with proficiency levels
/// </summary>
public class TechnicianSkillAssignment : TenantEntity
{
    /// <summary>
    /// Technician/user ID
    /// </summary>
    [Required]
    public Guid TechnicianId { get; set; }

    /// <summary>
    /// Technical skill being assigned
    /// </summary>
    [Required]
    public Guid SkillId { get; set; }

    /// <summary>
    /// Proficiency level (1-5 scale)
    /// </summary>
    public int ProficiencyLevel { get; set; } = 1;

    /// <summary>
    /// Text description of proficiency
    /// </summary>
    [MaxLength(100)]
    public string ProficiencyDescription { get; set; } = string.Empty;

    /// <summary>
    /// Date skill was acquired
    /// </summary>
    [Required]
    public DateTime AcquiredDate { get; set; }

    /// <summary>
    /// Skill expiration date (for certifications)
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Whether the skill has been verified
    /// </summary>
    public bool IsVerified { get; set; } = false;

    /// <summary>
    /// User who verified the skill
    /// </summary>
    [MaxLength(100)]
    public string VerifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date of last assessment/verification
    /// </summary>
    public DateTime? LastAssessmentDate { get; set; }

    /// <summary>
    /// Additional notes about the skill assignment
    /// </summary>
    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    // Computed properties
    /// <summary>
    /// Whether the skill certification is expired
    /// </summary>
    [NotMapped]
    public bool IsExpired => ExpirationDate.HasValue && ExpirationDate < DateTime.UtcNow;

    /// <summary>
    /// Days until skill certification expires
    /// </summary>
    [NotMapped]
    public int DaysUntilExpiration => ExpirationDate.HasValue ? 
        (int)(ExpirationDate.Value - DateTime.UtcNow).TotalDays : -1;

    // Navigation properties
    /// <summary>
    /// Related technical skill
    /// </summary>
    public virtual TechnicalSkill? Skill { get; set; }

    /// <summary>
    /// Related technician/employee
    /// </summary>
    public virtual Employee? Technician { get; set; }
}

/// <summary>
/// Skill assessment records and evaluations
/// </summary>
public class SkillAssessment : BaseEntity
{
    /// <summary>
    /// Technician being assessed
    /// </summary>
    [Required]
    public Guid TechnicianId { get; set; }

    /// <summary>
    /// Skill being assessed
    /// </summary>
    [Required]
    public Guid SkillId { get; set; }

    /// <summary>
    /// Assessment type (Initial, Periodic, Verification, Certification)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string AssessmentType { get; set; } = string.Empty;

    /// <summary>
    /// Assessment date
    /// </summary>
    [Required]
    public DateTime AssessmentDate { get; set; }

    /// <summary>
    /// Assessor/evaluator ID
    /// </summary>
    [Required]
    public Guid AssessorId { get; set; }

    /// <summary>
    /// Score achieved (0-100)
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Pass/fail result
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Proficiency level demonstrated (1-5)
    /// </summary>
    public int ProficiencyLevel { get; set; }

    /// <summary>
    /// Assessment method used
    /// </summary>
    [MaxLength(100)]
    public string AssessmentMethod { get; set; } = string.Empty;

    /// <summary>
    /// Detailed assessment notes
    /// </summary>
    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Areas of strength identified
    /// </summary>
    [MaxLength(500)]
    public string Strengths { get; set; } = string.Empty;

    /// <summary>
    /// Areas needing improvement
    /// </summary>
    [MaxLength(500)]
    public string ImprovementAreas { get; set; } = string.Empty;

    /// <summary>
    /// Recommended training or development
    /// </summary>
    [MaxLength(500)]
    public string Recommendations { get; set; } = string.Empty;

    /// <summary>
    /// Next assessment due date
    /// </summary>
    public DateTime? NextAssessmentDate { get; set; }

    // Navigation properties
    /// <summary>
    /// Related technical skill
    /// </summary>
    public virtual TechnicalSkill? Skill { get; set; }
}

/// <summary>
/// Skill gap analysis and workforce planning
/// </summary>
public class SkillGapAnalysis : BaseEntity
{
    /// <summary>
    /// Analysis name/title
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string AnalysisName { get; set; } = string.Empty;

    /// <summary>
    /// Skill being analyzed
    /// </summary>
    [Required]
    public Guid SkillId { get; set; }

    /// <summary>
    /// Required number of technicians with this skill
    /// </summary>
    public int RequiredTechnicians { get; set; }

    /// <summary>
    /// Current number of qualified technicians
    /// </summary>
    public int CurrentTechnicians { get; set; }

    /// <summary>
    /// Gap (positive = shortage, negative = surplus)
    /// </summary>
    public int Gap { get; set; }

    /// <summary>
    /// Business impact of the gap (Low, Medium, High, Critical)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ImpactLevel { get; set; } = string.Empty;

    /// <summary>
    /// Recommended actions to address the gap
    /// </summary>
    [MaxLength(1000)]
    public string RecommendedActions { get; set; } = string.Empty;

    /// <summary>
    /// Target completion date for addressing the gap
    /// </summary>
    public DateTime? TargetDate { get; set; }

    /// <summary>
    /// Analysis period start date
    /// </summary>
    [Required]
    public DateTime AnalysisPeriodStart { get; set; }

    /// <summary>
    /// Analysis period end date
    /// </summary>
    [Required]
    public DateTime AnalysisPeriodEnd { get; set; }

    /// <summary>
    /// Current status of gap mitigation efforts
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Open"; // Open, InProgress, Closed

    // Navigation properties
    /// <summary>
    /// Related technical skill
    /// </summary>
    public virtual TechnicalSkill? Skill { get; set; }
}