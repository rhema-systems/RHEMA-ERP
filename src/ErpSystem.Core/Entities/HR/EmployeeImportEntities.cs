using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// One uploaded employee workbook: what was checked, what HR decided, what was written.
/// </summary>
/// <remarks>
/// <para>Persisted rather than held in memory because the fix-in-Excel loop and the commit both
/// outlive a request: HR downloads the checked file, corrects it, and comes back; the commit runs in
/// the background and is polled. It is also the audit record the import/export catalogue asks for
/// (§5.6): who loaded which file, and with what result.</para>
/// <para>⚠ The session never writes an employee itself. Every row goes through
/// <c>IEmployeeService.ImportEmployeeAsync</c> and its siblings, so the staff-number register, the
/// payroll-membership gate and tenant stamping apply exactly as they do to a hand-typed create.</para>
/// </remarks>
public class EmployeeImportSession : TenantEntity
{
    /// <summary>Human handle, e.g. <c>EMPIMP-20260903-7F3A</c>.</summary>
    [Required]
    [MaxLength(50)]
    public string Reference { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>SHA-256 of the uploaded bytes. A repeat upload of a committed file is warned about.</summary>
    [Required]
    [MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    /// <summary>Version stamped in the template's hidden <c>_meta</c> sheet.</summary>
    public int TemplateVersion { get; set; }

    /// <summary>The source workbook, retained through the controlled upload gate.</summary>
    public Guid? SourceFileUploadRecordId { get; set; }
    public Guid? SourceDocumentRecordId { get; set; }

    public Guid UploadedByUserId { get; set; }

    [MaxLength(200)]
    public string? UploadedByName { get; set; }

    public DateTime UploadedOn { get; set; }

    public EmployeeImportSessionStatus Status { get; set; } = EmployeeImportSessionStatus.Validated;

    public EmployeeImportCommitPolicy? CommitPolicy { get; set; }
    public Guid? CommitRequestedByUserId { get; set; }
    public DateTime? CommitRequestedOn { get; set; }
    public DateTime? CommitStartedOn { get; set; }
    public DateTime? CommitCompletedOn { get; set; }

    public int TotalRows { get; set; }
    public int ReadyCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public int SkippedCount { get; set; }
    public int CommittedCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>File-level warnings (JSON array of findings). Errors never reach a session.</summary>
    public string? FileFindingsJson { get; set; }

    /// <summary>Result of the counter reconcile that closes a commit (JSON).</summary>
    public string? CounterReconciliationJson { get; set; }

    [MaxLength(2000)]
    public string? FailureMessage { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public virtual ICollection<EmployeeImportRow> Rows { get; set; } = new List<EmployeeImportRow>();
}

/// <summary>One data row of the uploaded workbook, as read, as resolved, and as decided.</summary>
public class EmployeeImportRow : TenantEntity
{
    [Required]
    public Guid SessionId { get; set; }

    /// <summary>The Excel row number, so findings can say <c>Employees!L7</c>.</summary>
    public int RowNumber { get; set; }

    [MaxLength(50)]
    public string? StaffNumber { get; set; }

    [MaxLength(250)]
    public string? DisplayName { get; set; }

    public EmploymentType? EmploymentType { get; set; }

    /// <summary>The cells as read, keyed by column key (JSON object of strings).</summary>
    [Required]
    public string RawJson { get; set; } = "{}";

    /// <summary>The create payload and child intents the row became (JSON), when it resolved.</summary>
    public string? ResolvedJson { get; set; }

    /// <summary>JSON array of findings: severity, column, cell, message, suggestions.</summary>
    [Required]
    public string FindingsJson { get; set; } = "[]";

    public EmployeeImportRowOutcome Outcome { get; set; } = EmployeeImportRowOutcome.Ready;

    /// <summary>HR's toggle to leave a row out of the commit.</summary>
    public bool Skip { get; set; }

    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }

    /// <summary>Manager given as another row of the same file (resolved after that row commits).</summary>
    public int? ManagerRowNumber { get; set; }

    public Guid? CreatedEmployeeId { get; set; }

    [MaxLength(2000)]
    public string? CommitMessage { get; set; }

    public DateTime? CommittedOn { get; set; }

    [ForeignKey(nameof(SessionId))]
    public virtual EmployeeImportSession Session { get; set; } = null!;

    [ForeignKey(nameof(CreatedEmployeeId))]
    public virtual Employee? CreatedEmployee { get; set; }
}
