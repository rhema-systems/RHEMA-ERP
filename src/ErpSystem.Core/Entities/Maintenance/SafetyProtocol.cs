using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Safety protocols and procedures for maintenance activities
/// </summary>
public class SafetyProtocol : TenantEntity
{
    /// <summary>
    /// Protocol name/title
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Protocol title (alias for Name for service compatibility)
    /// </summary>
    [NotMapped]
    public string Title
    {
        get => Name;
        set => Name = value;
    }

    /// <summary>
    /// Unique protocol code
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Protocol description
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Protocol category (PPE, Lockout/Tagout, Chemical Safety, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Severity level (Low, Medium, High, Critical)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Risk level (alias for Severity for service compatibility)
    /// </summary>
    [NotMapped]
    public string RiskLevel
    {
        get => Severity;
        set => Severity = value;
    }

    /// <summary>
    /// Regulatory standard reference (OSHA, EPA, DOT, etc.)
    /// </summary>
    [MaxLength(100)]
    public string RegulatoryStandard { get; set; } = string.Empty;

    /// <summary>
    /// Detailed safety procedures
    /// </summary>
    [Required]
    [MaxLength(5000)]
    public string Procedures { get; set; } = string.Empty;

    /// <summary>
    /// Required safety equipment (JSON array)
    /// </summary>
    public string RequiredEquipment { get; set; } = "[]";

    /// <summary>
    /// Required PPE (alias for RequiredEquipment for service compatibility)
    /// </summary>
    [NotMapped]
    public string RequiredPPE
    {
        get => RequiredEquipment;
        set => RequiredEquipment = value;
    }

    /// <summary>
    /// Required training courses (JSON array)
    /// </summary>
    public string RequiredTraining { get; set; } = "[]";

    /// <summary>
    /// Required certifications (JSON array)
    /// </summary>
    public string RequiredCertifications { get; set; } = "[]";

    /// <summary>
    /// Emergency response procedures
    /// </summary>
    [MaxLength(2000)]
    public string EmergencyProcedures { get; set; } = string.Empty;

    /// <summary>
    /// Preventive safety measures
    /// </summary>
    [MaxLength(2000)]
    public string PreventiveMeasures { get; set; } = string.Empty;

    /// <summary>
    /// Applicable maintenance types (JSON array)
    /// </summary>
    public string ApplicableMaintenanceTypes { get; set; } = "[]";

    /// <summary>
    /// Maintenance types (alias for service compatibility)
    /// </summary>
    [NotMapped]
    public string MaintenanceTypes
    {
        get => ApplicableMaintenanceTypes;
        set => ApplicableMaintenanceTypes = value;
    }

    /// <summary>
    /// Equipment types (alias for ApplicableAssetTypes for service compatibility)
    /// </summary>
    [NotMapped]
    public string EquipmentTypes
    {
        get => ApplicableAssetTypes;
        set => ApplicableAssetTypes = value;
    }

    /// <summary>
    /// Applicable asset types (JSON array)
    /// </summary>
    public string ApplicableAssetTypes { get; set; } = "[]";

    /// <summary>
    /// Whether this protocol is mandatory
    /// </summary>
    public bool IsMandatory { get; set; } = true;

    /// <summary>
    /// Whether the protocol is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last review date
    /// </summary>
    public DateTime? ReviewDate { get; set; }

    /// <summary>
    /// Last review date (alias for service compatibility)
    /// </summary>
    [NotMapped]
    public DateTime? LastReviewDate
    {
        get => ReviewDate;
        set => ReviewDate = value;
    }

    /// <summary>
    /// Next scheduled review date
    /// </summary>
    public DateTime? NextReviewDate { get; set; }

    /// <summary>
    /// Review frequency in months
    /// </summary>
    public int ReviewFrequencyMonths { get; set; } = 12;

    /// <summary>
    /// Minimum training level required
    /// </summary>
    [MaxLength(50)]
    public string MinimumTrainingLevel { get; set; } = string.Empty;

    /// <summary>
    /// Document version
    /// </summary>
    [NotMapped]
    public string DocumentVersion
    {
        get => Version;
        set => Version = value;
    }

    /// <summary>
    /// Effective date of the protocol
    /// </summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Expiration date of the protocol
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// User who reviewed the protocol
    /// </summary>
    [MaxLength(100)]
    public string ReviewedBy { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a regulatory protocol
    /// </summary>
    public bool IsRegulatory { get; set; } = false;

    /// <summary>
    /// Compliance checkpoints (JSON array)
    /// </summary>
    public string ComplianceCheckpoints { get; set; } = "[]";

    /// <summary>
    /// Regulatory sources (JSON array)
    /// </summary>
    public string RegulatorySources { get; set; } = "[]";

    /// <summary>
    /// Applicable environments (JSON array)
    /// </summary>
    public string ApplicableEnvironments { get; set; } = "[]";

    /// <summary>
    /// Status of the protocol
    /// </summary>
    [NotMapped]
    public string Status
    {
        get => ApprovalStatus;
        set => ApprovalStatus = value;
    }

    /// <summary>
    /// Approval status (Draft, PendingApproval, Approved, Rejected)
    /// </summary>
    [MaxLength(20)]
    public string ApprovalStatus { get; set; } = "Draft";

    /// <summary>
    /// User who approved the protocol
    /// </summary>
    [MaxLength(100)]
    public string ApprovedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date the protocol was approved
    /// </summary>
    public DateTime? ApprovalDate { get; set; }

    /// <summary>
    /// Protocol version
    /// </summary>
    [MaxLength(10)]
    public string Version { get; set; } = "1.0";

    // Computed properties for reporting
    /// <summary>
    /// Number of incidents related to this protocol
    /// </summary>
    [NotMapped]
    public int IncidentCount { get; set; }

    /// <summary>
    /// Number of compliance violations
    /// </summary>
    [NotMapped]
    public int ComplianceViolations { get; set; }

    /// <summary>
    /// Date of last incident
    /// </summary>
    [NotMapped]
    public DateTime? LastIncidentDate { get; set; }

    /// <summary>
    /// Compliance percentage based on audits
    /// </summary>
    [NotMapped]
    public decimal CompliancePercentage { get; set; }

    /// <summary>
    /// Compliance score (alias for service compatibility)
    /// </summary>
    [NotMapped]
    public decimal ComplianceScore
    {
        get => CompliancePercentage;
        set => CompliancePercentage = value;
    }

    /// <summary>
    /// Total violations count
    /// </summary>
    [NotMapped]
    public int TotalViolations
    {
        get => ComplianceViolations;
        set => ComplianceViolations = value;
    }

    /// <summary>
    /// Whether this protocol is due for review
    /// </summary>
    [NotMapped]
    public bool IsReviewDue => NextReviewDate.HasValue && NextReviewDate <= DateTime.UtcNow;

    /// <summary>
    /// Creation date (alias for service compatibility)
    /// </summary>
    [NotMapped]
    public DateTime CreatedDate
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    /// <summary>
    /// Last modification date (alias for service compatibility)
    /// </summary>
    [NotMapped]
    public DateTime LastModifiedDate
    {
        get => UpdatedAt ?? CreatedAt;
        set => UpdatedAt = value;
    }

    // Navigation properties
    /// <summary>
    /// Protocol adherence records
    /// </summary>
    public virtual ICollection<ProtocolAdherence> AdherenceRecords { get; set; } = new List<ProtocolAdherence>();

    /// <summary>
    /// Protocol violations
    /// </summary>
    public virtual ICollection<ProtocolViolation> Violations { get; set; } = new List<ProtocolViolation>();
}

/// <summary>
/// Records of safety protocol adherence during work orders
/// </summary>
public class ProtocolAdherence : TenantEntity
{
    /// <summary>
    /// Safety protocol being followed
    /// </summary>
    [Required]
    public Guid ProtocolId { get; set; }

    /// <summary>
    /// Work order where protocol was applied
    /// </summary>
    [Required]
    public Guid WorkOrderId { get; set; }

    /// <summary>
    /// Technician who followed the protocol
    /// </summary>
    [Required]
    public Guid TechnicianId { get; set; }

    /// <summary>
    /// Date/time when protocol was applied
    /// </summary>
    [Required]
    public DateTime AdherenceDate { get; set; }

    /// <summary>
    /// Whether the protocol was properly followed
    /// </summary>
    public bool WasFollowed { get; set; } = true;

    /// <summary>
    /// Compliance score (0-100)
    /// </summary>
    public int ComplianceScore { get; set; } = 100;

    /// <summary>
    /// Adherence level (Full, Partial, None)
    /// </summary>
    [MaxLength(20)]
    public string AdherenceLevel { get; set; } = "Full";

    /// <summary>
    /// Notes about protocol adherence
    /// </summary>
    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// User who verified adherence
    /// </summary>
    [MaxLength(100)]
    public string VerifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date of verification
    /// </summary>
    public DateTime? VerificationDate { get; set; }

    // Navigation properties
    /// <summary>
    /// Related safety protocol
    /// </summary>
    public virtual SafetyProtocol? Protocol { get; set; }
}

/// <summary>
/// Safety protocol violations and incidents
/// </summary>
public class ProtocolViolation : TenantEntity
{
    /// <summary>
    /// Safety protocol that was violated
    /// </summary>
    [Required]
    public Guid ProtocolId { get; set; }

    /// <summary>
    /// Work order during which violation occurred
    /// </summary>
    public Guid? WorkOrderId { get; set; }

    /// <summary>
    /// Technician who violated the protocol
    /// </summary>
    [Required]
    public Guid TechnicianId { get; set; }

    /// <summary>
    /// Date/time of violation
    /// </summary>
    [Required]
    public DateTime ViolationDate { get; set; }

    /// <summary>
    /// Type of violation (Procedure, PPE, Training, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ViolationType { get; set; } = string.Empty;

    /// <summary>
    /// Severity of the violation (Low, Medium, High, Critical)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Severity of violation (Minor, Moderate, Major, Critical)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ViolationSeverity { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the violation
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Root cause of the violation
    /// </summary>
    [MaxLength(500)]
    public string RootCause { get; set; } = string.Empty;

    /// <summary>
    /// Immediate corrective actions taken
    /// </summary>
    [MaxLength(1000)]
    public string ImmediateActions { get; set; } = string.Empty;

    /// <summary>
    /// Long-term preventive actions
    /// </summary>
    [MaxLength(1000)]
    public string PreventiveActions { get; set; } = string.Empty;

    /// <summary>
    /// Corrective action taken (alias for ImmediateActions for service compatibility)
    /// </summary>
    [NotMapped]
    public string CorrectiveAction
    {
        get => ImmediateActions;
        set => ImmediateActions = value;
    }

    /// <summary>
    /// Whether injury occurred as a result
    /// </summary>
    public bool InjuryOccurred { get; set; } = false;

    /// <summary>
    /// Whether property damage occurred
    /// </summary>
    public bool PropertyDamageOccurred { get; set; } = false;

    /// <summary>
    /// Estimated cost of violation (injury, damage, fines)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCost { get; set; }

    /// <summary>
    /// Status of violation investigation
    /// </summary>
    [MaxLength(20)]
    public string InvestigationStatus { get; set; } = "Open"; // Open, InProgress, Closed

    /// <summary>
    /// User assigned to investigate
    /// </summary>
    [MaxLength(100)]
    public string InvestigatorAssigned { get; set; } = string.Empty;

    /// <summary>
    /// Target date for corrective actions completion
    /// </summary>
    public DateTime? TargetCompletionDate { get; set; }

    /// <summary>
    /// Actual completion date of corrective actions
    /// </summary>
    public DateTime? ActualCompletionDate { get; set; }

    // Navigation properties
    /// <summary>
    /// Related safety protocol
    /// </summary>
    public virtual SafetyProtocol? Protocol { get; set; }
}

/// <summary>
/// Safety protocol training records
/// </summary>
public class ProtocolTraining : TenantEntity
{
    /// <summary>
    /// Safety protocol being trained on
    /// </summary>
    [Required]
    public Guid ProtocolId { get; set; }

    /// <summary>
    /// Technician receiving training
    /// </summary>
    [Required]
    public Guid TechnicianId { get; set; }

    /// <summary>
    /// Training date
    /// </summary>
    [Required]
    public DateTime TrainingDate { get; set; }

    /// <summary>
    /// Training method (Classroom, Online, On-the-job, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TrainingMethod { get; set; } = string.Empty;

    /// <summary>
    /// Training type (alias for TrainingMethod for service compatibility)
    /// </summary>
    [NotMapped]
    public string TrainingType
    {
        get => TrainingMethod;
        set => TrainingMethod = value;
    }

    /// <summary>
    /// Trainer/instructor name
    /// </summary>
    [MaxLength(100)]
    public string TrainerName { get; set; } = string.Empty;

    /// <summary>
    /// Training duration in hours
    /// </summary>
    public decimal TrainingHours { get; set; }

    /// <summary>
    /// Test/assessment score (if applicable)
    /// </summary>
    public int? TestScore { get; set; }

    /// <summary>
    /// Whether training was successfully completed
    /// </summary>
    public bool Completed { get; set; } = false;

    /// <summary>
    /// Completion status (NotStarted, InProgress, Completed, Failed)
    /// </summary>
    [MaxLength(20)]
    public string CompletionStatus { get; set; } = "NotStarted";

    /// <summary>
    /// Training score (alias for TestScore for service compatibility)
    /// </summary>
    [NotMapped]
    public int? Score
    {
        get => TestScore;
        set => TestScore = value;
    }

    /// <summary>
    /// Whether certification was issued for this training
    /// </summary>
    public bool CertificationIssued { get; set; } = false;

    /// <summary>
    /// Expiration date of training (if applicable)
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Training notes and comments
    /// </summary>
    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    // Navigation properties
    /// <summary>
    /// Related safety protocol
    /// </summary>
    public virtual SafetyProtocol? Protocol { get; set; }
}

/// <summary>
/// Safety audit results and findings
/// </summary>
public class SafetyAudit : BaseEntity
{
    /// <summary>
    /// Audit name/title
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string AuditName { get; set; } = string.Empty;

    /// <summary>
    /// Audit date
    /// </summary>
    [Required]
    public DateTime AuditDate { get; set; }

    /// <summary>
    /// Lead auditor
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string LeadAuditor { get; set; } = string.Empty;

    /// <summary>
    /// Audit team members (JSON array)
    /// </summary>
    public string AuditTeam { get; set; } = "[]";

    /// <summary>
    /// Audit scope and description
    /// </summary>
    [MaxLength(1000)]
    public string AuditScope { get; set; } = string.Empty;

    /// <summary>
    /// Overall audit score (0-100)
    /// </summary>
    public int OverallScore { get; set; }

    /// <summary>
    /// Audit findings and observations
    /// </summary>
    [MaxLength(2000)]
    public string Findings { get; set; } = string.Empty;

    /// <summary>
    /// Recommendations for improvement
    /// </summary>
    [MaxLength(2000)]
    public string Recommendations { get; set; } = string.Empty;

    /// <summary>
    /// Audit status (InProgress, Completed, FollowUpRequired)
    /// </summary>
    [MaxLength(20)]
    public string AuditStatus { get; set; } = "InProgress";

    /// <summary>
    /// Follow-up audit date (if required)
    /// </summary>
    public DateTime? FollowUpDate { get; set; }

    // Navigation properties
    /// <summary>
    /// Protocol audit details
    /// </summary>
    public virtual ICollection<ProtocolAuditDetail> AuditDetails { get; set; } = new List<ProtocolAuditDetail>();
}

/// <summary>
/// Detailed audit results for specific protocols
/// </summary>
public class ProtocolAuditDetail : BaseEntity
{
    /// <summary>
    /// Safety audit this detail belongs to
    /// </summary>
    [Required]
    public Guid AuditId { get; set; }

    /// <summary>
    /// Safety protocol being audited
    /// </summary>
    [Required]
    public Guid ProtocolId { get; set; }

    /// <summary>
    /// Compliance score for this protocol (0-100)
    /// </summary>
    public int ComplianceScore { get; set; }

    /// <summary>
    /// Whether protocol passed audit
    /// </summary>
    public bool Passed { get; set; } = true;

    /// <summary>
    /// Specific findings for this protocol
    /// </summary>
    [MaxLength(1000)]
    public string ProtocolFindings { get; set; } = string.Empty;

    /// <summary>
    /// Recommendations for this protocol
    /// </summary>
    [MaxLength(1000)]
    public string ProtocolRecommendations { get; set; } = string.Empty;

    /// <summary>
    /// Priority level for addressing issues (Low, Medium, High, Critical)
    /// </summary>
    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    // Navigation properties
    /// <summary>
    /// Related safety audit
    /// </summary>
    public virtual SafetyAudit? Audit { get; set; }

    /// <summary>
    /// Related safety protocol
    /// </summary>
    public virtual SafetyProtocol? Protocol { get; set; }
}