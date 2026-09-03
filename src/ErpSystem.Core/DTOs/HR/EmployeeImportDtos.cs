using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>One thing the checker has to say about a cell or a file.</summary>
public class EmployeeImportFindingDto
{
    public EmployeeImportFindingSeverity Severity { get; set; }

    /// <summary>Column key (e.g. <c>Department</c>) or null for a file-level finding.</summary>
    public string? Column { get; set; }

    /// <summary>Excel address, e.g. <c>Employees!L7</c>, so the person can go straight there.</summary>
    public string? Cell { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Nearest matches for a lookup that failed, best first.</summary>
    public List<string> Suggestions { get; set; } = new();
}

public class EmployeeImportSessionSummaryDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int TemplateVersion { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedOn { get; set; }
    public EmployeeImportSessionStatus Status { get; set; }
    public EmployeeImportCommitPolicy? CommitPolicy { get; set; }
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
    public string? FailureMessage { get; set; }
    public List<EmployeeImportFindingDto> FileFindings { get; set; } = new();
    public List<EmployeeImportCounterReconcileDto> CounterReconciliation { get; set; } = new();
    public Guid? SourceDocumentRecordId { get; set; }
}

/// <summary>What the staff-number counter of one register looked like after the commit.</summary>
public class EmployeeImportCounterReconcileDto
{
    public Guid FormatId { get; set; }
    public string Register { get; set; } = string.Empty;
    public long CounterBefore { get; set; }
    public long CounterAfter { get; set; }
    public string? Message { get; set; }
}

public class EmployeeImportRowDto
{
    public Guid Id { get; set; }
    public int RowNumber { get; set; }
    public string? StaffNumber { get; set; }
    public string? DisplayName { get; set; }
    public EmploymentType? EmploymentType { get; set; }
    public EmployeeImportRowOutcome Outcome { get; set; }
    public bool Skip { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<EmployeeImportFindingDto> Findings { get; set; } = new();

    /// <summary>The cells as read, keyed by column key.</summary>
    public Dictionary<string, string?> Values { get; set; } = new();

    public Guid? CreatedEmployeeId { get; set; }
    public string? CommitMessage { get; set; }
    public DateTime? CommittedOn { get; set; }
}

public class EmployeeImportRowPageDto
{
    public List<EmployeeImportRowDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class EmployeeImportCommitRequestDto
{
    public EmployeeImportCommitPolicy Policy { get; set; } = EmployeeImportCommitPolicy.ValidRowsOnly;
}

public class EmployeeImportSkipRowDto
{
    public bool Skip { get; set; }
}

public class EmployeeImportProgressDto
{
    public EmployeeImportSessionStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int ToCommit { get; set; }
    public int CommittedCount { get; set; }
    public int FailedCount { get; set; }
    public int Remaining { get; set; }
    public DateTime? CommitStartedOn { get; set; }
    public DateTime? CommitCompletedOn { get; set; }
    public string? FailureMessage { get; set; }
}

/// <summary>A created employee and what still needs completing on their profile.</summary>
public class EmployeeImportFollowUpDto
{
    public Guid EmployeeId { get; set; }
    public string StaffNumber { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public List<string> Items { get; set; } = new();
}

/// <summary>A session the background committer should pick up, with the actor to run it as.</summary>
public class EmployeeImportPendingCommitDto
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
}

/// <summary>The column catalogue the template carries, for the wizard's guide page.</summary>
public class EmployeeImportColumnGuideDto
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Help { get; set; } = string.Empty;
}

// ── The resolved shape a row becomes. Serialised into EmployeeImportRow.ResolvedJson and
//    replayed by the committer, so the checker and the committer cannot disagree about a row.

public class EmployeeImportResolvedRow
{
    public CreateEmployeeDto Employee { get; set; } = new();

    public EmployeeImportResolvedSalary? Salary { get; set; }
    public EmployeeImportResolvedContract? Contract { get; set; }
    public List<EmployeeImportResolvedQualification> Qualifications { get; set; } = new();
    public List<EmployeeImportResolvedIdentification> Identifications { get; set; } = new();

    /// <summary>Set when the manager is another row of the same file.</summary>
    public int? ManagerRowNumber { get; set; }
}

public class EmployeeImportResolvedSalary
{
    public Guid GradeId { get; set; }
    public Guid LevelId { get; set; }
    public Guid? NotchId { get; set; }
    public string LevelCode { get; set; } = string.Empty;
    public int? NotchNumber { get; set; }
}

public class EmployeeImportResolvedContract
{
    public string ContractNumber { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlySalary { get; set; }
}

public class EmployeeImportResolvedQualification
{
    public Guid? QualificationId { get; set; }
    public string? CustomName { get; set; }
    public string Institution { get; set; } = string.Empty;
    public int? YearCompleted { get; set; }

    /// <summary>"Highest" or "Professional" — for the follow-up list's wording.</summary>
    public string Kind { get; set; } = string.Empty;
}

public class EmployeeImportResolvedIdentification
{
    public Guid IdentificationTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
}
