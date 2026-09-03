namespace ErpSystem.Core.Enums;

/// <summary>Where an employee import session stands. Forward-only except for cancellation.</summary>
public enum EmployeeImportSessionStatus
{
    /// <summary>Uploaded, parsed and checked; nothing written to the register yet.</summary>
    Validated = 1,

    /// <summary>HR confirmed the commit; the background committer has not picked it up yet.</summary>
    CommitRequested = 2,

    /// <summary>Rows are being written. Progress is on the session counters.</summary>
    Committing = 3,

    /// <summary>Every row that was due to commit did.</summary>
    Committed = 4,

    /// <summary>The commit ran to the end but at least one row failed at write time.</summary>
    CommittedWithErrors = 5,

    /// <summary>Abandoned before commit. Rows are kept for the record.</summary>
    Cancelled = 6,

    /// <summary>The committer itself broke (not a row failure). See FailureMessage.</summary>
    Failed = 7,
}

/// <summary>What the checker decided about a row, then what the committer did with it.</summary>
public enum EmployeeImportRowOutcome
{
    /// <summary>No findings.</summary>
    Ready = 1,

    /// <summary>Will import; the person lands on the follow-up list.</summary>
    Warning = 2,

    /// <summary>Will not import until the cell is fixed.</summary>
    Error = 3,

    /// <summary>Written in full.</summary>
    Committed = 4,

    /// <summary>The employee was created but a child record (grade, contract, qualification, ID) was refused.</summary>
    CommittedWithIssues = 5,

    /// <summary>The service refused the row at write time; nothing was created for it.</summary>
    Failed = 6,

    /// <summary>HR took the row out of the commit.</summary>
    Skipped = 7,
}

/// <summary>How a commit treats rows with errors.</summary>
public enum EmployeeImportCommitPolicy
{
    /// <summary>Write every Ready and Warning row; leave Error rows behind.</summary>
    ValidRowsOnly = 1,

    /// <summary>Refuse to start while any un-skipped row has an error.</summary>
    AllOrNothing = 2,
}

public enum EmployeeImportFindingSeverity
{
    Error = 1,
    Warning = 2,
}

/// <summary>What a session is allowed to do to the register (phase 4, 2026-09-03).</summary>
public enum EmployeeImportMode
{
    /// <summary>Every row is a new employee; a staff number already in the register is an error.</summary>
    CreateOnly = 1,

    /// <summary>Every row updates an existing employee; a staff number not in the register is an error.</summary>
    UpdateOnly = 2,

    /// <summary>A known staff number updates, an unknown one creates.</summary>
    CreateOrUpdate = 3,
}

/// <summary>What the checker decided a row will do.</summary>
public enum EmployeeImportRowAction
{
    Create = 1,

    /// <summary>Only the filled cells change; a blank cell leaves the record as it is.</summary>
    Update = 2,
}
