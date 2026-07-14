using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Procedures;

[Table("ProcedureCases")]
public class ProcedureCase : TenantEntity
{
    [Required]
    [StringLength(50)]
    public string Module { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [StringLength(80)]
    public string? ReferenceNumber { get; set; }

    [StringLength(150)]
    public string? ApplicantName { get; set; }

    [StringLength(100)]
    public string? SourceDepartment { get; set; }

    public DateTime? ReceivedDate { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(40)]
    public string Status { get; set; } = "Open";

    public int CurrentStageIndex { get; set; }

    [StringLength(120)]
    public string CurrentStageName { get; set; } = string.Empty;

    [StringLength(150)]
    public string? CurrentStageOwner { get; set; }

    [StringLength(150)]
    public string? CurrentAssignedRole { get; set; }

    public Guid? WorkflowDefinitionId { get; set; }

    public Guid? WorkflowInstanceId { get; set; }

    public Guid? WorkflowStepId { get; set; }

    public Guid OpenedById { get; set; }

    public Guid? LastActionById { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<ProcedureCaseField> Fields { get; set; } = new List<ProcedureCaseField>();
    public virtual ICollection<ProcedureCaseChecklistItem> ChecklistItems { get; set; } = new List<ProcedureCaseChecklistItem>();
    public virtual ICollection<ProcedureCaseDocument> Documents { get; set; } = new List<ProcedureCaseDocument>();
    public virtual ICollection<ProcedureCaseActivity> Activities { get; set; } = new List<ProcedureCaseActivity>();
}

[Table("ProcedureCaseFields")]
public class ProcedureCaseField : TenantEntity
{
    public Guid ProcedureCaseId { get; set; }

    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Label { get; set; } = string.Empty;

    [StringLength(40)]
    public string FieldType { get; set; } = "text";

    [Column(TypeName = "nvarchar(max)")]
    public string? Value { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? OptionsJson { get; set; }

    public virtual ProcedureCase ProcedureCase { get; set; } = null!;
}

[Table("ProcedureCaseChecklistItems")]
public class ProcedureCaseChecklistItem : TenantEntity
{
    public Guid ProcedureCaseId { get; set; }

    public int StageIndex { get; set; }

    [Required]
    [StringLength(120)]
    public string StageName { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Text { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public Guid? CompletedById { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ProcedureCase ProcedureCase { get; set; } = null!;
}

[Table("ProcedureCaseDocuments")]
public class ProcedureCaseDocument : TenantEntity
{
    public Guid ProcedureCaseId { get; set; }

    [Required]
    [StringLength(180)]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? RequiredFrom { get; set; }

    public bool IsMandatory { get; set; }

    [StringLength(250)]
    public string? FileName { get; set; }

    [StringLength(500)]
    public string? FileUrl { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public Guid? UploadedById { get; set; }

    public DateTime? UploadedAt { get; set; }

    public virtual ProcedureCase ProcedureCase { get; set; } = null!;
}

[Table("ProcedureCaseActivities")]
public class ProcedureCaseActivity : TenantEntity
{
    public Guid ProcedureCaseId { get; set; }

    [Required]
    [StringLength(80)]
    public string Action { get; set; } = string.Empty;

    [StringLength(120)]
    public string? StageName { get; set; }

    [StringLength(1000)]
    public string? Details { get; set; }

    public Guid PerformedById { get; set; }

    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    public virtual ProcedureCase ProcedureCase { get; set; } = null!;
}
