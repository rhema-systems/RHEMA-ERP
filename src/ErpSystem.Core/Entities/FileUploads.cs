using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities;

[Table("FileUploadPolicies")]
public class FileUploadPolicy : TenantEntity
{
    // Category name (e.g. "ehc-ticket"). Use "*" for the tenant default policy.
    [Required]
    [StringLength(100)]
    public string Category { get; set; } = "*";

    public bool IsEnabled { get; set; } = true;

    // If null, fallback to app config defaults.
    public long? MaxFileSizeBytes { get; set; }

    // Optional global tenant quota (applies when Category == "*"; may be null).
    public long? MaxTenantTotalBytes { get; set; }

    // Optional per-category quota (applies when Category != "*"; may be null).
    public long? MaxCategoryTotalBytes { get; set; }

    // Optional overrides (CSV, lower/upper is ignored).
    [StringLength(2000)]
    public string? AllowedExtensionsCsv { get; set; }

    [StringLength(4000)]
    public string? AllowedMimeTypesCsv { get; set; }

    public bool RequireVirusScan { get; set; } = false;
}

[Table("FileUploadRecords")]
public class FileUploadRecord : TenantEntity
{
    [Required]
    [StringLength(100)]
    public string Category { get; set; } = "general";

    [Required]
    [StringLength(512)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    [StringLength(50)]
    public string StorageProvider { get; set; } = string.Empty;

    public Guid UploadedByUserId { get; set; }

    public FileVirusScanStatus VirusScanStatus { get; set; } = FileVirusScanStatus.Skipped;

    public DateTime? ScannedAtUtc { get; set; }

    [StringLength(500)]
    public string? VirusScanMessage { get; set; }

    // Physical storage cleanup is intentionally asynchronous. IsDeleted and
    // these fields are committed with the owning domain transaction before a
    // background worker is allowed to remove the stored object.
    public DateTime? StorageDeletedAtUtc { get; set; }

    public int StorageDeleteAttemptCount { get; set; }

    public DateTime? StorageDeleteLastAttemptAtUtc { get; set; }

    public DateTime? StorageDeleteNextAttemptAtUtc { get; set; }

    [StringLength(2000)]
    public string? StorageDeleteLastError { get; set; }
}
