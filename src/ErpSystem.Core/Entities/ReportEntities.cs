using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Entities;

/// <summary>
/// Canonical values for the production report scheduler. The legacy reporting
/// entities pre-date FR-RP-010, so these values keep the new worker, API and UI
/// aligned while the same tables continue serving manual report execution.
/// </summary>
public static class ReportAutomationValues
{
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";
    public const string Quarterly = "Quarterly";
    public const string Yearly = "Yearly";

    public const string Active = "Active";
    public const string Paused = "Paused";
    public const string Error = "Error";

    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";

    public const string ScheduledTrigger = "Scheduled";
    public const string ManualTrigger = "Manual";

    public static readonly IReadOnlySet<string> Frequencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Daily, Weekly, Monthly, Quarterly, Yearly
    };

    public static readonly IReadOnlySet<string> ExportFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "PDF", "XLSX"
    };
}

public class Report : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "draft"; // draft, published, archived

    public string? Query { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Parameters { get; set; } // JSON string

    [Column(TypeName = "nvarchar(max)")]
    public string? Columns { get; set; } // JSON string

    [Column(TypeName = "nvarchar(max)")]
    public string? Visualization { get; set; } // JSON string

    [Column(TypeName = "nvarchar(max)")]
    public string? Tags { get; set; } // JSON array as string

    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
    public bool IsScheduled { get; set; } = false;

    // Module assignment
    public Guid? ModuleId { get; set; }

    // Navigation properties
    public virtual TenantModule? Module { get; set; }
    public virtual ICollection<ReportSchedule> Schedules { get; set; } = new List<ReportSchedule>();
    public virtual ICollection<ReportExecution> Executions { get; set; } = new List<ReportExecution>();
    public virtual ICollection<UserReportFavorite> Favorites { get; set; } = new List<UserReportFavorite>();
    public virtual ICollection<ReportRoleAssignment> RoleAssignments { get; set; } = new List<ReportRoleAssignment>();
}

public class ReportSchedule : TenantEntity
{
    [Required]
    public Guid ReportId { get; set; }

    /// <summary>
    /// Pins the schedule to the exact published template version approved by
    /// Finance. A newer template version is never substituted silently.
    /// </summary>
    public Guid? ReportTemplateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Frequency { get; set; } = string.Empty; // daily, weekly, monthly, quarterly

    public TimeOnly TimeOfDay { get; set; }
    public int? DayOfWeek { get; set; } // 0-6 for weekly schedules
    public int? DayOfMonth { get; set; } // 1-31 for monthly schedules

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextExecutionDate { get; set; }
    public DateTime? LastExecutionDate { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? EmailRecipients { get; set; } // JSON array as string

    [MaxLength(10)]
    public string? ExportFormat { get; set; } = "pdf";

    [Column(TypeName = "nvarchar(max)")]
    public string? Parameters { get; set; } // JSON string

    /// <summary>
    /// In-application recipient user IDs. External email delivery is intentionally
    /// retained as a downstream integration boundary rather than being simulated.
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? RecipientUserIds { get; set; }

    public Guid? RunAsUserId { get; set; }
    public int MaximumRetryAttempts { get; set; } = 3;
    public int ConsecutiveFailureCount { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }

    public DateTime? PausedAt { get; set; }
    public Guid? PausedById { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsActive { get; set; } = true;

    [MaxLength(20)]
    public string Status { get; set; } = "active"; // active, paused, error

    // Navigation properties
    public virtual Report Report { get; set; } = null!;
    public virtual ReportTemplate? ReportTemplate { get; set; }
    public virtual ApplicationUser? RunAsUser { get; set; }
    public virtual ApplicationUser? PausedBy { get; set; }
}

public class ReportTemplate : TenantEntity
{
    public Guid? ReportId { get; set; }

    [Required]
    [MaxLength(80)]
    public string TemplateKey { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Audience { get; set; } = "Finance";

    [Required]
    [MaxLength(20)]
    public string Cadence { get; set; } = "AdHoc";

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    [Required]
    [MaxLength(10)]
    public string DefaultOutputFormat { get; set; } = "Online";

    [Column(TypeName = "nvarchar(max)")]
    public string? OutputFormats { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? SavedFilters { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? GenerationMetadata { get; set; }

    [MaxLength(20)]
    public string? ChartType { get; set; }

    public bool IsCustom { get; set; } = false;
    public DateTime? LastUsed { get; set; }
    public int UsageCount { get; set; } = 0;
    public DateTime? LastGeneratedAt { get; set; }
    public Guid? LastGeneratedBy { get; set; }

    [MaxLength(10)]
    public string? LastGenerationFormat { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Tags { get; set; } // JSON array as string

    [MaxLength(500)]
    public string? PreviewImage { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Configuration { get; set; } // JSON string

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Report? Report { get; set; }
}

public class ReportExecution : TenantEntity
{
    [Required]
    public Guid ReportId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public Guid? ReportScheduleId { get; set; }
    public Guid? ReportTemplateId { get; set; }
    public Guid? ReportExportId { get; set; }
    public int? TemplateVersion { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AttemptNumber { get; set; } = 1;

    [MaxLength(20)]
    public string Trigger { get; set; } = ReportAutomationValues.ManualTrigger;

    /// <summary>
    /// Prevents two API nodes from completing or retrying the same scheduled
    /// occurrence concurrently after the unique key has selected its owner.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public TimeSpan ExecutionTime { get; set; }

    public int TotalRows { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "success"; // success, failed, cancelled

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Parameters { get; set; } // JSON string

    [Column(TypeName = "nvarchar(max)")]
    public string? ResultMetadata { get; set; } // JSON string

    // Navigation properties
    public virtual Report Report { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ReportSchedule? ReportSchedule { get; set; }
    public virtual ReportTemplate? ReportTemplate { get; set; }
    public virtual ReportExport? ReportExport { get; set; }
}

public class UserReportFavorite : TenantEntity
{
    [Required]
    public Guid ReportId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public DateTime FavoritedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Report Report { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}

public class ReportExport : TenantEntity
{
    [Required]
    public Guid ReportId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(10)]
    public string Format { get; set; } = string.Empty; // pdf, csv, xlsx, json

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? StoragePath { get; set; }

    [MaxLength(100)]
    public string? ContentType { get; set; }

    [MaxLength(64)]
    public string? Sha256Checksum { get; set; }

    public DateTime? RetainUntil { get; set; }

    public long FileSize { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    public string Status { get; set; } = "completed"; // completed, failed

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Parameters { get; set; } // JSON string

    // Navigation properties
    public virtual Report Report { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}

public class ReportRoleAssignment : TenantEntity
{
    [Required]
    public Guid ReportId { get; set; }

    [Required]
    public Guid RoleId { get; set; }

    /// <summary>
    /// Can view/access the report
    /// </summary>
    public bool CanRead { get; set; } = true;

    /// <summary>
    /// Can execute/run the report
    /// </summary>
    public bool CanExecute { get; set; } = true;

    /// <summary>
    /// Can export the report
    /// </summary>
    public bool CanExport { get; set; } = false;

    /// <summary>
    /// Can modify the report (only for reports they have access to)
    /// </summary>
    public bool CanEdit { get; set; } = false;

    /// <summary>
    /// Can schedule the report
    /// </summary>
    public bool CanSchedule { get; set; } = false;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? AssignedBy { get; set; }

    // Navigation properties
    public virtual Report Report { get; set; } = null!;
    public virtual ApplicationRole Role { get; set; } = null!;
}
