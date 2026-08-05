using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// Binds an anonymous CV upload on the public careers portal to the tenant and vacancy
/// it was minted for, so a later application can claim it exactly once.
/// </summary>
/// <remarks>
/// <para>
/// The public portal uploads a CV before the candidate record exists, so the upload has
/// to be parked somewhere claimable. Handing the caller the raw
/// <c>FileUploadRecordId</c> would let anyone who guesses a GUID attach a stranger's CV
/// to their own application, so the browser instead receives an opaque random token and
/// only the SHA-256 hash of it is stored here — the row itself is then useless to anyone
/// who can read the table.
/// </para>
/// <para>
/// Tickets are single-use (<see cref="ClaimedAtUtc"/>) and short-lived
/// (<see cref="ExpiresAtUtc"/>); a background sweeper deletes the underlying upload for
/// any ticket that is never claimed, which is the common case for abandoned forms.
/// </para>
/// </remarks>
[Table("PublicCvUploadTickets")]
public class PublicCvUploadTicket : TenantEntity
{
    /// <summary>The controlled upload this ticket releases.</summary>
    public Guid FileUploadRecordId { get; set; }

    /// <summary>
    /// Vacancy the upload was minted against. Claiming re-checks this, so a ticket
    /// cannot be replayed against a different vacancy.
    /// </summary>
    public Guid JobVacancyId { get; set; }

    /// <summary>Lowercase hex SHA-256 of the token handed to the browser. Never the token itself.</summary>
    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Set the moment an application consumes this ticket. Null means unclaimed.</summary>
    public DateTime? ClaimedAtUtc { get; set; }

    public Guid? ClaimedByCandidateId { get; set; }
}

/// <summary>
/// One row per legacy HR file processed by the migration utility that moves
/// pre-controlled-boundary uploads out of the public web root.
/// </summary>
/// <remarks>
/// A dedicated ledger rather than bookkeeping columns on each of the eight owning tables:
/// it keeps the domain schema clean, makes the job idempotent and re-runnable, and gives
/// operators one place to review what was migrated, what was missing, and what was
/// quarantined.
/// </remarks>
[Table("HrLegacyFileMigrationEntries")]
public class HrLegacyFileMigrationEntry : TenantEntity
{
    /// <summary>Owning entity type, e.g. <c>JobCandidateDocument</c>.</summary>
    [Required, MaxLength(150)]
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>Path as stored on the owning row before migration.</summary>
    [Required, MaxLength(1000)]
    public string LegacyPath { get; set; } = string.Empty;

    public HrLegacyFileMigrationStatus Status { get; set; }
        = HrLegacyFileMigrationStatus.Pending;

    /// <summary>
    /// The <c>ControlledFileUploadException.Code</c> that rejected the file, when the
    /// status is <see cref="HrLegacyFileMigrationStatus.Quarantined"/>.
    /// </summary>
    [MaxLength(100)]
    public string? ErrorCode { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>Controlled upload created for this file, once it passed the gate.</summary>
    public Guid? FileUploadRecordId { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }
}

public enum HrLegacyFileMigrationStatus
{
    Pending = 0,

    /// <summary>Adopted into controlled storage and, where applicable, the central DMS.</summary>
    Migrated = 1,

    /// <summary>The owning row referenced a path that no longer exists on disk.</summary>
    Missing = 2,

    /// <summary>
    /// Rejected by the upload gate — infected, or otherwise not permitted. The original is
    /// moved to a quarantine folder rather than deleted: destroying evidence attached to a
    /// live disciplinary case or medical exam is not this utility's decision to make.
    /// </summary>
    Quarantined = 3,

    /// <summary>Failed for a transient reason and can be retried by re-running the job.</summary>
    Failed = 4
}
