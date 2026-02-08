using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities;

/// <summary>
/// Database-backed exception log entry (admin-visible).
/// Intended for operational troubleshooting and auditing (Smartstore-style log viewer).
/// </summary>
public class SystemExceptionLog : TenantEntity
{
    /// <summary>
    /// Deduplication fingerprint (stable key used to group repeated exceptions).
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Number of times this exception fingerprint has occurred.
    /// </summary>
    public int OccurrenceCount { get; set; } = 1;

    /// <summary>
    /// First time this fingerprint occurred (UTC).
    /// </summary>
    public DateTime FirstOccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time this fingerprint occurred (UTC).
    /// </summary>
    public DateTime LastOccurredAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(20)]
    public string Level { get; set; } = "Error"; // Warning, Error, Critical

    [MaxLength(200)]
    public string? Logger { get; set; }

    [Required]
    [MaxLength(1000)]
    public string ShortMessage { get; set; } = string.Empty;

    public string? FullMessage { get; set; }

    [MaxLength(200)]
    public string? ExceptionType { get; set; }

    public string? StackTrace { get; set; }

    [MaxLength(100)]
    public string? TraceId { get; set; }

    [MaxLength(20)]
    public string? RequestMethod { get; set; }

    [MaxLength(500)]
    public string? RequestPath { get; set; }

    [MaxLength(2000)]
    public string? QueryString { get; set; }

    [MaxLength(500)]
    public string? ReferrerUrl { get; set; }

    [MaxLength(45)]
    public string? RemoteIpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public Guid? UserId { get; set; }

    [MaxLength(200)]
    public string? Username { get; set; }

    // Basic triage fields
    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedById { get; set; }

    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }
}
