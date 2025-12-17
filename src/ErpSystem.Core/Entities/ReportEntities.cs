using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Entities;

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

    public bool IsActive { get; set; } = true;

    [MaxLength(20)]
    public string Status { get; set; } = "active"; // active, paused, error

    // Navigation properties
    public virtual Report Report { get; set; } = null!;
}

public class ReportTemplate : TenantEntity
{
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

    [MaxLength(20)]
    public string? ChartType { get; set; }

    public bool IsCustom { get; set; } = false;
    public DateTime? LastUsed { get; set; }
    public int UsageCount { get; set; } = 0;

    [Column(TypeName = "nvarchar(max)")]
    public string? Tags { get; set; } // JSON array as string

    [MaxLength(500)]
    public string? PreviewImage { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Configuration { get; set; } // JSON string
}

public class ReportExecution : TenantEntity
{
    [Required]
    public Guid ReportId { get; set; }

    [Required]
    public Guid UserId { get; set; }

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
